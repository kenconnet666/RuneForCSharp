using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 样式与默认值的作用域（对应 vue-tsx-pro 的 Provider）：Styles 里的样式按资源键叠在外层同键样式之上，
/// 内层只写要改的部分，其余沿用外层。WPF 原生的嵌套隐式样式是内层整个替换外层，这里把每一层串成 BasedOn 链。
/// <code>
/// &lt;rune:Provider&gt;
///   &lt;rune:Provider.Styles&gt;
///     &lt;ResourceDictionary&gt;
///       &lt;Style TargetType="rune:Button"&gt;&lt;Setter Property="Variant" Value="Outline" /&gt;&lt;/Style&gt;   &lt;!-- 默认值 --&gt;
///       &lt;Style x:Key="{x:Static rune:ButtonParts.Label}" TargetType="TextBlock"&gt;…&lt;/Style&gt;          &lt;!-- 部件样式 --&gt;
///     &lt;/ResourceDictionary&gt;
///   &lt;/rune:Provider.Styles&gt;
///   …
/// &lt;/rune:Provider&gt;
/// </code>
/// 合并顺序：主题样式 &lt; 应用资源 &lt; 外层 Provider &lt; 内层 Provider &lt; 元素自己的 Style 与本地值。
/// 类型键（隐式样式）不叠主题样式：主题样式由 DefaultStyleKey 另外生效，叠进来会把库的触发器抬到“样式”优先级；
/// 部件键叠到主题里的部件样式之上：部件没有“默认样式”这一层，主题里的就是基础。
/// </summary>
public class Provider : Decorator
{
    /// <summary>合成结果按（外层, 片段）缓存：同样的组合（列表里的每一项各包一个 Provider）共用同一个 Style。</summary>
    static ConditionalWeakTable<Style, ConditionalWeakTable<Style, Style>> cache = new();
    static readonly Style noOuter = new();
    static readonly List<WeakReference<Provider>> live = [];

    ResourceDictionary styles = new();

    /// <summary>算好的合成结果：写进 Resources 之前内层就能看到。</summary>
    Dictionary<object, Style>? composed;

    static Provider() => HotReload.Applied += RecomposeAll;

    /// <summary>
    /// 这一层的样式：类型键（隐式样式）用于组件的默认值与外观，<c>x:Key</c> 为部件键的用于部件。
    /// 只写要改的 setter 与触发器，不写 BasedOn。
    /// </summary>
    public ResourceDictionary Styles
    {
        get => styles;
        set
        {
            styles = value;
            // XAML 解析中赋值时片段里的 Setter 还没做完类型转换（Value="3" 还不是 Thickness），等 Initialized 再合成
            if (IsInitialized) Compose();
        }
    }

    /// <summary>合成时实际生成了几个 Style（缓存命中不算）。测试与性能观察用。</summary>
    internal static int Compositions { get; private set; }

    /// <inheritdoc />
    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        Compose();
    }

    /// <inheritdoc />
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        lock (live) live.Add(new WeakReference<Provider>(this));
        Compose();
    }

    /// <summary>
    /// 重新合成这一层与里面的各层。时机上的三个坑（docs/DESIGN.md）：
    /// FrameworkElement 在 XAML 里自上而下构造，挂到父级时 Styles 还没赋值；赋值时片段里的 Setter 还没做完类型转换；
    /// 子元素先于父元素 Initialized，内层合成时外层可能还没合成。所以自己与外层都初始化完才合成，外层合成后通知内层；
    /// 内层先写、外层后写（写 Resources 会让子树立刻重查资源，先写外层的话内层的子元素会先拿到外层结果、再换一次 Style）。
    /// </summary>
    void Compose()
    {
        if (!IsInitialized) return;
        for (var node = ParentOf(this); node != null; node = ParentOf(node))
            if (node is Provider { IsInitialized: false }) return;

        // 这一层写了类型键的类型：同类型的部件键也要合成（决定 19），即使这一层没写那个部件键
        var typeFragments = styles.Keys.OfType<Type>().Where(type => styles[type] is Style).ToDictionary(type => type, type => (Style)styles[type]);
        var keys = styles.Keys.Cast<object>().Where(key => styles[key] is Style)
            .Concat(typeFragments.Keys.SelectMany(PartKey.For))
            .Distinct();
        var resolved = new Dictionary<object, Style?>();
        var result = new Dictionary<object, Style>();
        foreach (var key in keys)
            if (Resolve(key) is { } style) result[key] = style;
        composed = result;

        // 这一层看到的某个键：外层的结果，部件键先叠这一层同类型的类型键样式，再叠这一层自己的片段
        Style? Resolve(object key)
        {
            if (resolved.TryGetValue(key, out var done)) return done;
            var style = FindOuter(key);
            if (key is PartKey part && typeFragments.TryGetValue(part.TargetType, out var typeFragment)) style = Layer(typeFragment, style);
            if (styles.Contains(key) && styles[key] is Style own) style = Layer(own, style);
            return resolved[key] = style;
        }

        // 片段叠到外层上；片段里的嵌套部件样式（Style.Resources 里按部件键写的）也叠到同键的结果上，这种片段的结果依赖位置，不进缓存
        Style Layer(Style fragment, Style? outer) => StyleCloner.HasNestedStyles(fragment)
            ? Rebase(fragment, outer, Resolve)
            : cache.GetValue(outer ?? noOuter, _ => new()).GetValue(fragment, f => Rebase(f, outer, Resolve));

        foreach (var inner in InnerProviders(this)) inner.Compose();
        foreach (var (key, style) in result)
            if (!ReferenceEquals(Resources[key], style))
                Resources[key] = style;
    }

    /// <summary>外层的同键样式：最近的祖先 Provider，其次应用资源；部件键最后取主题里的。</summary>
    Style? FindOuter(object key)
    {
        for (var node = ParentOf(this); node != null; node = ParentOf(node))
            if (node is Provider { composed: { } c } && c.TryGetValue(key, out var found))
                return found;
        var app = Application.Current;
        if (app?.Resources.Contains(key) == true) return app.Resources[key] as Style;
        return key is ComponentResourceKey ? app?.TryFindResource(key) as Style : null;
    }

    static Style Rebase(Style fragment, Style? outer, Func<object, Style?> resolve)
    {
        Compositions++;
        return StyleCloner.Rebase(fragment, outer, resolve);
    }

    static IEnumerable<Provider> InnerProviders(DependencyObject node)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
        {
            if (child is Provider p) yield return p;
            else foreach (var nested in InnerProviders(child)) yield return nested;
        }
    }

    /// <summary>逻辑父级优先（挂进 ContentControl 后、模板展开前没有视觉父级），没有时取视觉父级。</summary>
    static DependencyObject? ParentOf(DependencyObject node) =>
        LogicalTreeHelper.GetParent(node) ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null);

    /// <summary>代码热重载后：清掉缓存，从最外层的 Provider 开始重新合成。</summary>
    static void RecomposeAll()
    {
        cache = new();
        List<Provider> providers;
        lock (live)
        {
            live.RemoveAll(r => !r.TryGetTarget(out _));
            providers = live.Select(r => r.TryGetTarget(out var p) ? p : null).OfType<Provider>().ToList();
        }
        foreach (var p in providers)
            if (!AncestorProviders(p).Any()) p.Compose();
    }

    static IEnumerable<Provider> AncestorProviders(Provider p)
    {
        for (var node = ParentOf(p); node != null; node = ParentOf(node))
            if (node is Provider outer) yield return outer;
    }
}
