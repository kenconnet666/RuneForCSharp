using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;

namespace Rune.Avalonia;

/// <summary>
/// 按钮组（vue-tsx-pro 的 ButtonGroup）：几个相关的按钮连成一条，共用外观。拆分按钮、翻页、缩放、对齐方式这类工具栏。
/// <code>
/// &lt;rune:ButtonGroup Variant="outline" Tone="neutral" AutomationProperties.Name="文字对齐"&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.AlignLeft}" AutomationProperties.Name="左对齐" /&gt;
///   &lt;rune:IconButton Icon="{x:Static rune:Lucide.AlignCenter}" AutomationProperties.Name="居中" /&gt;
/// &lt;/rune:ButtonGroup&gt;
/// </code>
/// 组里的按钮去掉相邻一侧的圆角、边框叠在一起，交叉轴拉齐（一样高，竖排时一样宽）；悬停、聚焦的那个浮到上面，边框与焦点环不被邻居盖住。
/// 组上写的 Variant、Tone、Size、Radius 是组里按钮的默认值，按钮自己写的优先；没写 Tone 时组里的按钮都是 accent（图标按钮也是，拆分按钮的两半同色）。实底（solid）的相邻按钮之间没有分隔，连成一条时用 outline、surface 更清楚。
/// 只是排一排、不相连的按钮用 Flex；选中其中一项用 ToggleGroup。读屏里是一个组，名称写 <c>AutomationProperties.Name</c>。
/// </summary>
public class ButtonGroup : AttachedGroup
{
    /// <summary>组里按钮的样式；不写时按钮用自己的默认。</summary>
    public static readonly StyledProperty<ButtonVariant?> VariantProperty = AvaloniaProperty.Register<ButtonGroup, ButtonVariant?>(nameof(Variant));

    /// <summary>组里按钮的色调；不写时是 accent。</summary>
    public static readonly StyledProperty<StatusTone?> ToneProperty = AvaloniaProperty.Register<ButtonGroup, StatusTone?>(nameof(Tone));

    /// <summary>组里按钮的字号档位。</summary>
    public static readonly StyledProperty<TextSize?> SizeProperty = AvaloniaProperty.Register<ButtonGroup, TextSize?>(nameof(Size));

    /// <summary>组里按钮的圆角（两端的按钮外侧用它）。</summary>
    public static readonly StyledProperty<Radius?> RadiusProperty = AvaloniaProperty.Register<ButtonGroup, Radius?>(nameof(Radius));

    static ButtonGroup()
    {
        // 组像 CSS 的 inline-flex：不被父级拉宽
        HorizontalAlignmentProperty.OverrideDefaultValue<ButtonGroup>(HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<ButtonGroup>(VerticalAlignment.Top);
    }

    /// <summary>组里按钮的样式。</summary>
    public ButtonVariant? Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>组里按钮的色调。</summary>
    public StatusTone? Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>组里按钮的字号档位。</summary>
    public TextSize? Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>组里按钮的圆角。</summary>
    public Radius? Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariantProperty || change.Property == ToneProperty || change.Property == SizeProperty || change.Property == RadiusProperty) Reapply();
    }

    /// <summary>组的外观是按钮的默认值：模板优先级（主题之上、按钮自己写的本地值之下）。</summary>
    protected override IEnumerable<IDisposable?> Defaults(Control child)
    {
        foreach (var value in base.Defaults(child)) yield return value;
        if (child is not ButtonCore button) yield break;
        if (Variant is { } variant) yield return button.SetValue(ButtonCore.VariantProperty, variant, BindingPriority.Template);
        // 组里的按钮外观要一致：没写色调时都按 Button 的默认（accent），图标按钮也是（拆分按钮的两半同色）
        yield return button.SetValue(ButtonCore.ToneProperty, Tone ?? StatusTone.Accent, BindingPriority.Template);
        if (Size is { } size) yield return button.SetValue(ButtonCore.SizeProperty, size, BindingPriority.Template);
        if (Radius is { } radius) yield return button.SetValue(ButtonCore.RadiusProperty, radius, BindingPriority.Template);
    }
}
