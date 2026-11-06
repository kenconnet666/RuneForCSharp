using System.ComponentModel;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Data;
using global::Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;

namespace Rune.Avalonia;

/// <summary>
/// 档位解析出的值（DESIGN §4"档位的优先级"）。组件在代码里把档位（Tone、Radius……）解析成主题资源，存进这里的附加属性；
/// 组件主题里一条 setter 把真正的属性绑定过来：
/// <code>
/// &lt;Setter Property="Palette" Value="{rune:Resolved Palette}" /&gt;
/// &lt;Setter Property="CornerRadius" Value="{rune:Em Resolved=Radius}" /&gt;   &lt;!-- 字号 × 圆角档位的系数 --&gt;
/// </code>
/// 档位的值于是落在主题这一层（vue-tsx-pro 的"组件段"），使用方、Provider 的任何样式都盖得过它，不必带伪类。
/// 只用于不继承的属性：绑定在 Avalonia 里回不到继承的值（没有值时给属性的默认值），字号、文字颜色这类继承属性用 <see cref="TemplateResources"/>。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class Resolved
{
    /// <summary>状态色调解析出的调色板。</summary>
    public static readonly AttachedProperty<TonePalette?> PaletteProperty = AvaloniaProperty.RegisterAttached<StyledElement, TonePalette?>("Palette", typeof(Resolved));

    /// <summary>圆角档位解析出的 em 系数。</summary>
    public static readonly AttachedProperty<double?> RadiusProperty = AvaloniaProperty.RegisterAttached<StyledElement, double?>("Radius", typeof(Resolved));

    /// <summary>读调色板。</summary>
    public static TonePalette? GetPalette(StyledElement element) => element.GetValue(PaletteProperty);

    /// <summary>写调色板。</summary>
    public static void SetPalette(StyledElement element, TonePalette? value) => element.SetValue(PaletteProperty, value);

    /// <summary>读圆角系数。</summary>
    public static double? GetRadius(StyledElement element) => element.GetValue(RadiusProperty);

    /// <summary>写圆角系数。</summary>
    public static void SetRadius(StyledElement element, double? value) => element.SetValue(RadiusProperty, value);

    /// <summary>把档位解析到主题资源：key 为 null 时清掉。本地值的绑定会替换上一次的，不用另记。</summary>
    internal static void Bind(StyledElement element, AvaloniaProperty property, string? key)
    {
        if (key is null) element.ClearValue(property);
        else element.Bind(property, element.GetResourceObservable(key));
    }

    /// <summary>名字 → 附加属性（<c>{rune:Resolved Palette}</c>、<c>{rune:Em Resolved=Radius}</c>）。</summary>
    internal static AvaloniaProperty Find(string name) => name switch
    {
        "Palette" => PaletteProperty,
        "Radius" => RadiusProperty,
        _ => throw new ArgumentException($"Resolved 上没有 {name}：可用的是 Palette、Radius。", nameof(name)),
    };

    /// <summary>绑定到元素自己的这个附加属性。</summary>
    internal static BindingBase SelfBinding(string name) =>
        new CompiledBinding(new CompiledBindingPathBuilder().Self().Property(Find(name), PropertyInfoAccessorFactory.CreateAvaloniaPropertyAccessor).Build());
}

/// <summary>
/// XAML：<c>{rune:Resolved Palette}</c>，把属性绑定到 <see cref="Resolved"/> 上同名的附加属性（档位解析出的值），写在组件主题的 setter 里。
/// </summary>
/// <param name="name">附加属性的名字（Palette、Radius）。</param>
public sealed class ResolvedExtension(string name)
{
    /// <summary>XAML 调用：返回绑定。</summary>
    /// <param name="serviceProvider">XAML 服务（不使用）。</param>
    public object ProvideValue(IServiceProvider serviceProvider) => Resolved.SelfBinding(name);
}

/// <summary>
/// 档位解析出的继承属性（字号、字重、字体、文字颜色）：按模板优先级绑定主题资源。没写档位时解绑，回到继承的值；
/// 使用方带类或伪类的样式（<c>rune|Text.title</c>）盖得过它，只按类型选的样式盖不过（DESIGN §4"档位的优先级"）。
/// </summary>
/// <param name="owner">组件。</param>
sealed class TemplateResources(StyledElement owner)
{
    readonly Dictionary<AvaloniaProperty, IDisposable> bindings = [];

    /// <summary>绑定到主题资源；key 为 null 时解绑。</summary>
    public void Bind(AvaloniaProperty property, string? key) =>
        Replace(property, key is null ? null : owner.Bind(property, owner.GetResourceObservable(key), BindingPriority.Template));

    /// <summary>直接给值（字号档位写的像素数）。</summary>
    public void Set<T>(StyledProperty<T> property, T value) => Replace(property, owner.SetValue(property, value, BindingPriority.Template));

    void Replace(AvaloniaProperty property, IDisposable? binding)
    {
        if (bindings.Remove(property, out var old)) old.Dispose();
        if (binding is not null) bindings[property] = binding;
    }
}
