using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// em 尺寸：值 = 系数 × 字号，字号变了跟着变（对应 vue-tsx-pro 里组件内部一律用 em）。
/// 主题样式只设系数，控件用属性强制（coerce）算出实际值（Padding、行高、登记的圆角属性，以及 <see cref="ControlHeight"/> 的最小高度、宽度）：
/// <code>
/// // 控件的静态构造里接上：Padding、MinHeight、Width、FontSize 的元数据，以及自己的 CornerRadius
/// static Button() => EmSize.Register(typeof(Button), CornerRadiusProperty);
/// public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
///     nameof(CornerRadius), typeof(CornerRadius), typeof(Button), new FrameworkPropertyMetadata(default(CornerRadius), null, EmSize.CoerceCornerRadius));
/// </code>
/// <code>
/// &lt;Setter Property="rune:EmSize.Padding" Value="1,0.25" /&gt;   &lt;!-- Generic.xaml：水平 1em、垂直 0.25em --&gt;
/// &lt;Setter Property="rune:EmSize.CornerRadius" Value="0.4" /&gt;
/// </code>
/// 只有基础值来自默认值、继承或主题样式时才按字号算；使用方的样式与本地值照常生效。
/// 不用绑定：3000 个按钮实测创建 430 ms，绑定到字号的写法 878 ms，写死数值 273 ms。
/// </summary>
public static class EmSize
{
    /// <summary>内边距的 em 系数（Thickness 的四个值都是系数）。</summary>
    public static readonly DependencyProperty PaddingProperty = DependencyProperty.RegisterAttached(
        "Padding", typeof(Thickness?), typeof(EmSize), new PropertyMetadata(null, (d, _) => d.CoerceValue(Control.PaddingProperty)));

    /// <summary>圆角的 em 系数，作用在 <see cref="Register"/> 登记的圆角属性上。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(double?), typeof(EmSize), new PropertyMetadata(null, (d, _) => Refresh(d)));

    /// <summary>行高的 em 系数（按钮这类控件里文字的行盒：TS 的 lineHeight: tight），作用在继承的 TextBlock.LineHeight 上。</summary>
    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.RegisterAttached(
        "LineHeight", typeof(double?), typeof(EmSize), new PropertyMetadata(null, (d, _) => d.CoerceValue(TextBlock.LineHeightProperty)));

    static readonly Dictionary<Type, DependencyProperty> cornerRadiusByType = [];

    /// <summary>读行高系数。</summary>
    public static double? GetLineHeight(DependencyObject d) => (double?)d.GetValue(LineHeightProperty);

    /// <summary>设行高系数。</summary>
    public static void SetLineHeight(DependencyObject d, double? value) => d.SetValue(LineHeightProperty, value);

    /// <summary>读内边距系数。</summary>
    public static Thickness? GetPadding(DependencyObject d) => (Thickness?)d.GetValue(PaddingProperty);

    /// <summary>设内边距系数。</summary>
    public static void SetPadding(DependencyObject d, Thickness? value) => d.SetValue(PaddingProperty, value);

    /// <summary>读圆角系数。</summary>
    public static double? GetCornerRadius(DependencyObject d) => (double?)d.GetValue(CornerRadiusProperty);

    /// <summary>设圆角系数。</summary>
    public static void SetCornerRadius(DependencyObject d, double? value) => d.SetValue(CornerRadiusProperty, value);

    /// <summary>
    /// 在控件的静态构造里调用：Padding、圆角按系数计算，MinHeight、Width 按 <see cref="ControlHeight"/> 计算，字号变化时重新计算。
    /// </summary>
    /// <param name="controlType">控件类型。</param>
    /// <param name="cornerRadiusProperty">控件自己的圆角属性（注册时 coerce 回调写 <see cref="CoerceCornerRadius"/>）；没有就不传。</param>
    public static void Register(Type controlType, DependencyProperty? cornerRadiusProperty = null)
    {
        Control.PaddingProperty.OverrideMetadata(controlType, new FrameworkPropertyMetadata(default(Thickness), null, CoercePadding));
        FrameworkElement.MinHeightProperty.OverrideMetadata(controlType, new FrameworkPropertyMetadata(0d, null, CoerceMinHeight));
        FrameworkElement.WidthProperty.OverrideMetadata(controlType, new FrameworkPropertyMetadata(double.NaN, null, CoerceWidth));
        TextBlock.LineHeightProperty.OverrideMetadata(controlType, new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.Inherits, null, CoerceLineHeight));
        Control.FontSizeProperty.OverrideMetadata(controlType, new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => Refresh(d)));
        if (cornerRadiusProperty is not null) cornerRadiusByType[controlType] = cornerRadiusProperty;
    }

    /// <summary>圆角属性的 coerce 回调。</summary>
    public static object CoerceCornerRadius(DependencyObject d, object baseValue)
    {
        if (GetCornerRadius(d) is not { } f || CornerRadiusFor(d.GetType()) is not { } property) return baseValue;
        return FromLibrary(d, property, baseValue) ? new CornerRadius(f * FontSize(d)) : baseValue;
    }

    static object CoercePadding(DependencyObject d, object baseValue)
    {
        if (GetPadding(d) is not { } f || !FromLibrary(d, Control.PaddingProperty, baseValue)) return baseValue;
        var size = FontSize(d);
        return new Thickness(f.Left * size, f.Top * size, f.Right * size, f.Bottom * size);
    }

    static object CoerceMinHeight(DependencyObject d, object baseValue)
    {
        if (!ControlHeight.GetMinHeight(d) || !FromLibrary(d, FrameworkElement.MinHeightProperty, baseValue)) return baseValue;
        return ControlHeight.Of(FontSize(d), ControlHeight.GetFactor(d));
    }

    static object CoerceWidth(DependencyObject d, object baseValue)
    {
        if (!ControlHeight.GetWidth(d) || !FromLibrary(d, FrameworkElement.WidthProperty, baseValue)) return baseValue;
        return ControlHeight.Of(FontSize(d), ControlHeight.GetFactor(d));
    }

    static object CoerceLineHeight(DependencyObject d, object baseValue)
    {
        if (GetLineHeight(d) is not { } f || !FromLibrary(d, TextBlock.LineHeightProperty, baseValue)) return baseValue;
        return f * FontSize(d);
    }

    internal static void Refresh(DependencyObject d)
    {
        d.CoerceValue(TextBlock.LineHeightProperty);
        d.CoerceValue(Control.PaddingProperty);
        d.CoerceValue(FrameworkElement.MinHeightProperty);
        d.CoerceValue(FrameworkElement.WidthProperty);
        if (CornerRadiusFor(d.GetType()) is { } property) d.CoerceValue(property);
    }

    static DependencyProperty? CornerRadiusFor(Type? type)
    {
        for (; type is not null; type = type.BaseType)
            if (cornerRadiusByType.TryGetValue(type, out var property)) return property;
        return null;
    }

    static double FontSize(DependencyObject d) => (double)d.GetValue(Control.FontSizeProperty);

    /// <summary>
    /// 基础值是不是库给的（可以按 em 算）。库自己从不直接设这些属性、只设系数，所以基础值不是默认值就一定来自使用方。
    /// 不能只看值来源：基础值变化时 WPF 先调 coerce、后记录新来源，回调里读到的还是旧来源（实测：使用方样式的
    /// Padding=3 生效时这里读到 Default）。基础值等于默认值时再看来源与本地值，区分“没人设”与“使用方显式设成默认值”。
    /// </summary>
    static bool FromLibrary(DependencyObject d, DependencyProperty p, object baseValue)
    {
        if (!Equals(baseValue, p.GetMetadata(d).DefaultValue)) return false;
        if (d.ReadLocalValue(p) != DependencyProperty.UnsetValue) return false;
        return DependencyPropertyHelper.GetValueSource(d, p).BaseValueSource
            is BaseValueSource.Default or BaseValueSource.Inherited or BaseValueSource.DefaultStyle or BaseValueSource.DefaultStyleTrigger;
    }
}
