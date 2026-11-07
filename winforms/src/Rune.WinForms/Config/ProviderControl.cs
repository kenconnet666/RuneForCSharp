using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 主题、语言与组件定制的作用域（vue-tsx-pro 的 Provider，DESIGN §4"WinForms：分层解析"）：一个容器控件，里面的组件沿 Parent 链取最近的一层。
/// <code>
/// var scope = new ProviderControl { Theme = BuiltinThemes.Dark, Dock = DockStyle.Fill };
/// scope.Defaults.Set(new ButtonDefaults { Variant = ButtonVariant.Outline, Size = TextSize.Sm });   // 这片区域里按钮的默认值
/// scope.Styles.Set(new ButtonSx { Radius = Radius.Full });                                           // 这片区域里按钮的外观
/// scope.Controls.Add(toolbar);
/// </code>
/// 合并规则同上游：实例上写的 &gt; 内层作用域 &gt; 外层作用域 &gt; 组件默认，按记录的字段逐个取第一个写了的（<see cref="IScopeRecord{T}"/>）。
/// 主题、语言没写时沿用外层，最外层是应用级的 <see cref="RuneTheme.Current"/>、<see cref="RuneLang.Current"/>。
/// 作用域里的任何一项变了，里面的组件重新排版、重画（<see cref="IScopeAware"/>）；组件换了父级时按新位置重新查找。
/// </summary>
public class ProviderControl : Panel
{
    /// <summary>新建：默认值、外观记录都是空的，主题、语言沿用外层。</summary>
    public ProviderControl()
    {
        Defaults = new ScopeRecords(Changed);
        Styles = new ScopeRecords(Changed);
    }

    /// <summary>这片区域的主题；null 时沿用外层。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ThemeTokens? Theme { get; set { field = value; Changed(); } }

    /// <summary>这片区域的语言包；null 时沿用外层。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Lang? Lang { get; set { field = value; Changed(); } }

    /// <summary>组件的默认值（上游的 defaults.Xxx）：每种组件一个记录，如 <see cref="ButtonDefaults"/>。</summary>
    [Browsable(false)]
    public ScopeRecords Defaults { get; }

    /// <summary>组件的外观（上游的 components.Xxx.styles）：每种组件一个记录，如 <see cref="ButtonSx"/>；实例的 Sx 盖在上面。</summary>
    [Browsable(false)]
    public ScopeRecords Styles { get; }

    /// <summary>外层的作用域。</summary>
    ProviderControl? Outer => Of(Parent);

    /// <summary>这一层看到的主题：这一层的，其次外层的，最后应用级的（读应用级信号，绘制 effect 追踪得到）。</summary>
    public ThemeTokens EffectiveTheme => Theme ?? Outer?.EffectiveTheme ?? RuneTheme.Current.Value;

    /// <summary>这一层看到的语言包。</summary>
    public Lang EffectiveLang => Lang ?? Outer?.EffectiveLang ?? RuneLang.Current.Value;

    /// <summary>控件所在的作用域：控件自己是作用域时就是它，否则沿 Parent 链往外找（浮层窗口里的接着从弹出它的控件找）；没有时为 null。</summary>
    /// <param name="control">控件。</param>
    public static ProviderControl? Of(Control? control)
    {
        for (var node = control; node is not null; node = node is FloatingForm floating ? floating.Origin : node.Parent)
            if (node is ProviderControl scope) return scope;
        return null;
    }

    /// <summary>控件看到的主题（作用域的，没有时应用级的）。</summary>
    /// <param name="control">控件。</param>
    public static ThemeTokens ThemeOf(Control control) => Of(control)?.EffectiveTheme ?? RuneTheme.Current.Value;

    /// <summary>控件看到的语言包。</summary>
    /// <param name="control">控件。</param>
    public static Lang LangOf(Control control) => Of(control)?.EffectiveLang ?? RuneLang.Current.Value;

    /// <summary>某种组件的默认值合并到这一层：外层的在下、这一层的在上，逐字段；哪层都没写时为 null。</summary>
    /// <typeparam name="T">默认值记录。</typeparam>
    public T? ResolveDefaults<T>() where T : class, IScopeRecord<T> => Merge(Outer?.ResolveDefaults<T>(), Defaults.Get<T>());

    /// <summary>某种组件的外观合并到这一层。</summary>
    /// <typeparam name="T">外观记录。</typeparam>
    public T? ResolveStyles<T>() where T : class, IScopeRecord<T> => Merge(Outer?.ResolveStyles<T>(), Styles.Get<T>());

    /// <summary>实例的外观覆盖盖在这一层合并的外观上（组件的 <c>Sx</c>）；都没写时为 null。</summary>
    /// <param name="own">实例上写的。</param>
    /// <typeparam name="T">外观记录。</typeparam>
    public T? ResolveStyles<T>(T? own) where T : class, IScopeRecord<T> => Merge(ResolveStyles<T>(), own);

    static T? Merge<T>(T? below, T? over) where T : class, IScopeRecord<T> => below is null ? over : below.Merge(over);

    /// <summary>这一层变了：里面的组件重新排版、重画（嵌套的作用域也通知，它们看到的合并结果跟着变）。</summary>
    void Changed()
    {
        NotifyDescendants(this);
        Invalidate(true);
    }

    /// <summary>通知 <paramref name="parent"/> 里面的组件（不含它自己）作用域变了。</summary>
    /// <param name="parent">容器。</param>
    internal static void NotifyDescendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is IScopeAware aware) aware.ScopeChanged();
            NotifyDescendants(child);
        }
    }
}

/// <summary>作用域里每种组件一个的记录（默认值或外观），按记录的类型存取。</summary>
/// <param name="changed">存了新的记录时调用。</param>
public sealed class ScopeRecords(Action changed)
{
    readonly Dictionary<Type, object> records = [];

    /// <summary>设这种组件的记录（替换这一层原来的）；null 时清掉，沿用外层。</summary>
    /// <typeparam name="T">记录的类型。</typeparam>
    /// <param name="record">记录。</param>
    public void Set<T>(T? record) where T : class, IScopeRecord<T>
    {
        if (record is null) records.Remove(typeof(T));
        else records[typeof(T)] = record;
        changed();
    }

    /// <summary>这一层的记录；没设时为 null。</summary>
    /// <typeparam name="T">记录的类型。</typeparam>
    public T? Get<T>() where T : class, IScopeRecord<T> => records.TryGetValue(typeof(T), out var record) ? (T)record : null;
}

/// <summary>作用域里能逐字段合并的记录：字段可空，null 是"这一层不改"。</summary>
/// <typeparam name="T">记录自己的类型。</typeparam>
public interface IScopeRecord<T> where T : class
{
    /// <summary>上面一层盖住这一层：上层写了的字段用上层的，没写的沿用这一层。</summary>
    /// <param name="over">上面一层；null 时原样返回。</param>
    T Merge(T? over);
}

/// <summary>所在作用域变了时要重新排版、重画的宿主（元素宿主、自绘的组件控件）。</summary>
interface IScopeAware
{
    /// <summary>作用域里的主题、语言、默认值或外观变了。</summary>
    void ScopeChanged();
}
