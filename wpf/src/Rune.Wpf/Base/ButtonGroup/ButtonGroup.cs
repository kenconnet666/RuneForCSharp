using System.Windows;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 按钮组（vue-tsx-pro 的 ButtonGroup）：几个相关的按钮连成一条，共用外观。拆分按钮、翻页、缩放、对齐方式这类工具栏。
/// <code>
/// &lt;rune:ButtonGroup Variant="outline" Tone="neutral" AutomationProperties.Name="文字对齐"&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.TextAlignStart}" AutomationProperties.Name="左对齐" /&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.TextAlignCenter}" AutomationProperties.Name="居中" /&gt;
/// &lt;/rune:ButtonGroup&gt;
/// </code>
/// 组里的按钮去掉相邻一侧的圆角、边框叠在一起，交叉轴拉齐（一样高，竖排时一样宽）；悬停、聚焦的那个浮到上面，边框与焦点环不被邻居盖住。
/// 组上写的 Variant、Tone、Size、Radius 是组里按钮的默认值：盖过主题，按钮自己写的（本地值、使用方的样式）优先；没写 Tone 时组里的按钮都是 accent
/// （图标按钮也是，拆分按钮的两半同色）。实底（solid）的相邻按钮之间没有分隔，连成一条时用 outline、surface 更清楚。
/// 只是排一排、不相连的按钮用 Flex；选中其中一项用 ToggleGroup。读屏里是一个组，名称写 <c>AutomationProperties.Name</c>。
/// </summary>
public class ButtonGroup : AttachedGroup
{
    /// <summary>组里按钮的样式；不写时按钮用自己的默认。</summary>
    public static readonly DependencyProperty VariantProperty = Register(nameof(Variant), typeof(ButtonVariant?));

    /// <summary>组里按钮的色调；不写时是 accent。</summary>
    public static readonly DependencyProperty ToneProperty = Register(nameof(Tone), typeof(StatusTone?));

    /// <summary>组里按钮的字号档位。</summary>
    public static readonly DependencyProperty SizeProperty = Register(nameof(Size), typeof(TextSize?));

    /// <summary>组里按钮的圆角（两端的按钮外侧用它）。</summary>
    public static readonly DependencyProperty RadiusProperty = Register(nameof(Radius), typeof(Radius?));

    static DependencyProperty Register(string name, Type type) =>
        DependencyProperty.Register(name, type, typeof(ButtonGroup), new PropertyMetadata(null, (d, _) => ((ButtonGroup)d).Refresh()));

    static ButtonGroup()
    {
        // 组像 CSS 的 inline-flex：不被父级拉宽
        HorizontalAlignmentProperty.OverrideMetadata(typeof(ButtonGroup), new FrameworkPropertyMetadata(HorizontalAlignment.Left));
        VerticalAlignmentProperty.OverrideMetadata(typeof(ButtonGroup), new FrameworkPropertyMetadata(VerticalAlignment.Top));
    }

    /// <summary>组里按钮的样式。</summary>
    public ButtonVariant? Variant { get => (ButtonVariant?)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>组里按钮的色调。</summary>
    public StatusTone? Tone { get => (StatusTone?)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>组里按钮的字号档位。</summary>
    public TextSize? Size { get => (TextSize?)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>组里按钮的圆角。</summary>
    public Radius? Radius { get => (Radius?)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    internal static object CoerceVariant(DependencyObject d, object baseValue) => Coerce(d, baseValue, ButtonCore.VariantProperty, group => group.Variant);

    internal static object CoerceTone(DependencyObject d, object baseValue) => Coerce(d, baseValue, ButtonCore.ToneProperty, group => group.Tone ?? StatusTone.Accent);

    internal static object CoerceSize(DependencyObject d, object baseValue) => Coerce(d, baseValue, ButtonCore.SizeProperty, group => group.Size);

    internal static object CoerceRadius(DependencyObject d, object baseValue) => Coerce(d, baseValue, ButtonCore.RadiusProperty, group => group.Radius);

    /// <summary>
    /// 组的外观盖过主题（默认值、默认样式）里的值；按钮自己的本地值、使用方的样式照用（同 TS：显式写的 prop &gt; 组 &gt; 默认）。
    /// </summary>
    static object Coerce(DependencyObject d, object baseValue, DependencyProperty property, Func<ButtonGroup, object?> value)
    {
        if (d is not FrameworkElement { Parent: ButtonGroup group } || value(group) is not { } shared) return baseValue;
        if (d.ReadLocalValue(property) != DependencyProperty.UnsetValue) return baseValue;
        if (DependencyPropertyHelper.GetValueSource(d, property).BaseValueSource
            is not (BaseValueSource.Default or BaseValueSource.DefaultStyle or BaseValueSource.DefaultStyleTrigger)) return baseValue;
        // 基础值变了时 WPF 先调强制、后记新来源：刚写的本地值、样式值这里还看不到。等这一轮记完再强制一次（那一次不再排）
        if (!Equals(baseValue, shared) && Rechecking.Add((d, property)))
            d.Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
            {
                d.CoerceValue(property);
                Rechecking.Remove((d, property));
            });
        return shared;
    }

    static readonly HashSet<(DependencyObject, DependencyProperty)> Rechecking = [];

    /// <summary>放进、移出组时重算按钮的外观（组的默认值在强制回调里）。</summary>
    protected override void OnJoined(UIElement child, bool joined)
    {
        if (child is not ButtonCore button) return;
        button.CoerceValue(ButtonCore.VariantProperty);
        button.CoerceValue(ButtonCore.ToneProperty);
        button.CoerceValue(ButtonCore.SizeProperty);
        button.CoerceValue(ButtonCore.RadiusProperty);
    }
}
