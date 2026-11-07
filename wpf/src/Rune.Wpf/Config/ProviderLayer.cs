using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 一层样式合成，挂在宿主元素上：<see cref="Provider"/>（Decorator）自己是宿主，也可以是任何 FrameworkElement
/// （<c>Provider.Styles</c> 的附加属性形式，对应 TS 的单个实例 <c>propsXxx</c>）。两种写法共用这一份实现。
/// 样式写在宿主的 Resources 里，所以宿主自己与它里面的元素都收得到；宿主自己的类型键样式也会生效到它自己身上。
/// 合并规则（DESIGN §4）：外层结果 &lt; 这一层的类型键片段 &lt; 这一层的部件键片段。
/// </summary>
sealed class ProviderLayer
{
    static readonly DependencyProperty LayerProperty = DependencyProperty.RegisterAttached("Layer", typeof(ProviderLayer), typeof(ProviderLayer));

    /// <summary>合成结果按（外层, 片段）缓存：同样的组合（列表里的每一项各包一个 Provider）共用同一个 Style。</summary>
    static ConditionalWeakTable<Style, ConditionalWeakTable<Style, Style>> cache = new();
    static readonly Style noOuter = new();
    static readonly List<WeakReference<ProviderLayer>> live = [];

    readonly FrameworkElement host;

    /// <summary>算好的合成结果：写进 Resources 之前内层就能看到。null 表示还没合成过。</summary>
    Dictionary<object, Style>? composed;

    /// <summary>上次合成时最近的外层。元素之后换了父级（没有通知可订阅）时，Loaded 里靠它发现外层变了。</summary>
    ProviderLayer? outer;

    /// <summary>上次合成时这一层有几条样式。XAML 里附加属性元素在宿主 Initialized 之后才赋值，而且先赋空字典、再往里加，没有通知。</summary>
    int composedCount;

    static ProviderLayer() => HotReload.Applied += RecomposeAll;

    ProviderLayer(FrameworkElement host)
    {
        this.host = host;
        lock (live) live.Add(new WeakReference<ProviderLayer>(this));
        if (!host.IsInitialized) host.Initialized += (_, _) => Compose();
        host.Loaded += (_, _) =>
        {
            if (composed is not null && (NearestOuter() != outer || (Provider.GetStyles(host)?.Count ?? 0) != composedCount)) Compose();
        };
    }

    /// <summary>这一层的样式（<see cref="Provider.StylesProperty"/>）变了。</summary>
    public static void OnStylesChanged(DependencyObject host, DependencyPropertyChangedEventArgs _)
    {
        var element = (FrameworkElement)host;
        var layer = Of(element);
        if (layer is null) element.SetValue(LayerProperty, layer = new ProviderLayer(element));
        // XAML 解析中赋值时片段里的 Setter 还没做完类型转换（Value="3" 还不是 Thickness），等宿主 Initialized 再合成
        if (element.IsInitialized) layer.Compose();
    }

    public static ProviderLayer? Of(DependencyObject node) => node.GetValue(LayerProperty) as ProviderLayer;

    /// <summary>
    /// 重新合成这一层与里面的各层。时机上的三个坑（docs/DESIGN.md）：
    /// FrameworkElement 在 XAML 里自上而下构造，挂到父级时 Styles 还没赋值；赋值时片段里的 Setter 还没做完类型转换；
    /// 子元素先于父元素 Initialized，内层合成时外层可能还没合成。所以自己与外层都初始化完才合成，外层合成后通知内层；
    /// 内层先写、外层后写（写 Resources 会让子树立刻重查资源，先写外层的话内层的子元素会先拿到外层结果、再换一次 Style）。
    /// </summary>
    public void Compose()
    {
        if (!host.IsInitialized) return;
        ProviderLayer? nearest = null;
        for (var node = ParentOf(host); node != null; node = ParentOf(node))
            if (Of(node) is { } ancestor)
            {
                if (!ancestor.host.IsInitialized) return;
                nearest ??= ancestor;
            }
        outer = nearest;

        var styles = Provider.GetStyles(host) ?? new ResourceDictionary();
        composedCount = styles.Count;
        // 这一层写了类型键的类型：同类型的部件键也要合成（决定 19），即使这一层没写那个部件键
        var typeFragments = styles.Keys.OfType<Type>().Where(type => styles[type] is Style).ToDictionary(type => type, type => (Style)styles[type]);
        var keys = styles.Keys.Cast<object>().Where(key => styles[key] is Style)
            .Concat(typeFragments.Keys.SelectMany(PartKey.For))
            .Distinct();
        var resolved = new Dictionary<object, Style?>();
        var result = new Dictionary<object, Style>();
        foreach (var key in keys)
            if (Resolve(key) is { } style) result[key] = style;
        var previous = composed;
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
        Style Layer(Style fragment, Style? below) => StyleCloner.HasNestedStyles(fragment)
            ? Rebase(fragment, below, Resolve)
            : cache.GetValue(below ?? noOuter, _ => new()).GetValue(fragment, f => Rebase(f, below, Resolve));

        foreach (var inner in InnerLayers(host)) inner.Compose();
        foreach (var (key, style) in result)
            if (!ReferenceEquals(host.Resources[key], style))
                host.Resources[key] = style;
        // Styles 换成了更少的键时，旧结果不能留在 Resources 里
        if (previous is not null)
            foreach (var key in previous.Keys.Where(key => !result.ContainsKey(key)))
                host.Resources.Remove(key);
    }

    /// <summary>外层的同键样式：最近的祖先层，其次应用资源；部件键最后取主题里的。</summary>
    Style? FindOuter(object key)
    {
        for (var node = ParentOf(host); node != null; node = ParentOf(node))
            if (Of(node) is { composed: { } c } && c.TryGetValue(key, out var found))
                return found;
        var app = Application.Current;
        if (app?.Resources.Contains(key) == true) return app.Resources[key] as Style;
        return key is ComponentResourceKey ? app?.TryFindResource(key) as Style : null;
    }

    ProviderLayer? NearestOuter()
    {
        for (var node = ParentOf(host); node != null; node = ParentOf(node))
            if (Of(node) is { } layer) return layer;
        return null;
    }

    static Style Rebase(Style fragment, Style? below, Func<object, Style?> resolve)
    {
        Provider.Compositions++;
        return StyleCloner.Rebase(fragment, below, resolve);
    }

    static IEnumerable<ProviderLayer> InnerLayers(DependencyObject node)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
        {
            if (Of(child) is { } layer) yield return layer;
            else foreach (var nested in InnerLayers(child)) yield return nested;
        }
    }

    /// <summary>逻辑父级优先（挂进 ContentControl 后、模板展开前没有视觉父级），没有时取视觉父级。</summary>
    static DependencyObject? ParentOf(DependencyObject node) =>
        LogicalTreeHelper.GetParent(node) ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null);

    /// <summary>代码热重载后：清掉缓存，从最外层的一层开始重新合成。</summary>
    static void RecomposeAll()
    {
        cache = new();
        List<ProviderLayer> layers;
        lock (live)
        {
            live.RemoveAll(r => !r.TryGetTarget(out _));
            layers = live.Select(r => r.TryGetTarget(out var l) ? l : null).OfType<ProviderLayer>().ToList();
        }
        foreach (var layer in layers)
            if (layer.NearestOuter() is null) layer.Compose();
    }
}
