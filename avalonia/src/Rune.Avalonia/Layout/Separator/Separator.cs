using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 分隔线（vue-tsx-pro 的 Separator）：默认是装饰（读屏看不到）；分隔两组不同的内容、要读屏知道时 <c>Decorative="False"</c>。
/// <code>
/// &lt;rune:Separator /&gt;
/// &lt;rune:Flex Align="center" Gap="sm"&gt;A&lt;rune:Separator Orientation="vertical" /&gt;B&lt;/rune:Flex&gt;   &lt;!-- 竖线随行高拉伸 --&gt;
/// &lt;rune:Separator&gt;或者&lt;/rune:Separator&gt;                       &lt;!-- 横线中间带文字 --&gt;
/// &lt;rune:Separator LabelAlign="start"&gt;更改后&lt;/rune:Separator&gt;   &lt;!-- 文字靠起始边，线在后面 --&gt;
/// </code>
/// 线是 borderWidths.xs 宽、borderMuted 色，自己画（DESIGN §9）。带文字（只用于横线）时文字是 0.93em、次要文字色，两段线隔 space.md；
/// 文字与两段线是代码组合的子元素：<c>rune|Separator &gt; Border#PART_Line</c>（两段都叫这个名字）、<c>rune|Separator &gt; rune|Text#PART_Label</c>。
/// 竖线在 Flex 里自己带 <c>Flex.AlignSelf=stretch</c>（主题里设），Flex 按 center 对齐时也随行高拉伸。
/// </summary>
public class Separator : Control
{
    /// <summary>方向，默认 horizontal。</summary>
    public static readonly StyledProperty<SeparatorOrientation> OrientationProperty = AvaloniaProperty.Register<Separator, SeparatorOrientation>(nameof(Orientation));

    /// <summary>只是视觉装饰（默认）：读屏看不到；带文字时文字照样给读屏。</summary>
    public static readonly StyledProperty<bool> DecorativeProperty = AvaloniaProperty.Register<Separator, bool>(nameof(Decorative), true);

    /// <summary>带文字时文字的位置，默认 center。</summary>
    public static readonly StyledProperty<SeparatorLabelAlign> LabelAlignProperty = AvaloniaProperty.Register<Separator, SeparatorLabelAlign>(nameof(LabelAlign));

    /// <summary>线上的文字（只用于横线）。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Separator, object?>(nameof(Content));

    /// <summary>线的颜色（主题里是 borderMuted）。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<Separator, IBrush?>(nameof(Background));

    /// <summary>线宽（主题里是 borderWidths.xs）。</summary>
    public static readonly StyledProperty<double> ThicknessProperty = AvaloniaProperty.Register<Separator, double>(nameof(Thickness), 1);

    readonly List<Control> parts = [];
    Control? label;
    IDisposable? fontSize;

    static Separator()
    {
        AffectsMeasure<Separator>(OrientationProperty, LabelAlignProperty, ThicknessProperty, TextElement.FontSizeProperty);
        AffectsRender<Separator>(BackgroundProperty);
    }

    /// <summary>方向。</summary>
    public SeparatorOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>只是视觉装饰。</summary>
    public bool Decorative { get => GetValue(DecorativeProperty); set => SetValue(DecorativeProperty, value); }

    /// <summary>带文字时文字的位置。</summary>
    public SeparatorLabelAlign LabelAlign { get => GetValue(LabelAlignProperty); set => SetValue(LabelAlignProperty, value); }

    /// <summary>线上的文字。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>线的颜色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>线宽。</summary>
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    bool IsVertical => Orientation.Value == "vertical";

    /// <summary>带文字：有内容、横线。</summary>
    bool Labelled => label is not null;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == OrientationProperty || change.Property == LabelAlignProperty) Rebuild();
    }

    /// <summary>带文字时：线、文字、线（靠起始边时没有前面那段，靠结束边时没有后面那段）。</summary>
    void Rebuild()
    {
        foreach (var part in parts)
        {
            LogicalChildren.Remove(part);
            VisualChildren.Remove(part);
        }
        parts.Clear();
        label = null;
        if (Content is null || IsVertical)
        {
            InvalidateMeasure();
            return;
        }
        label = Content as Control ?? new Text { Text = Content.ToString(), Wrap = TextWrap.NoWrap };
        label.Name = SeparatorParts.Label;
        if (LabelAlign.Value != "start") parts.Add(Line());
        parts.Add(label);
        if (LabelAlign.Value != "end") parts.Add(Line());
        foreach (var part in parts)
        {
            LogicalChildren.Add(part);
            VisualChildren.Add(part);
        }
        InvalidateMeasure();

        Border Line()
        {
            var line = new Border { Name = SeparatorParts.Line, VerticalAlignment = VerticalAlignment.Center };
            line.Bind(Border.BackgroundProperty, this.GetObservable(BackgroundProperty));
            line.Bind(HeightProperty, this.GetObservable(ThicknessProperty));
            AutomationProperties.SetAccessibilityView(line, AccessibilityView.Raw);
            return line;
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 带文字时比所在的文字小一档（0.93em）
        if (this.GetVisualParent() is { } parent) fontSize = FontScale.Bind(this, parent, 0.93);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        fontSize?.Dispose();
        fontSize = null;
    }

    double Gap => 0.75 * GetValue(TextElement.FontSizeProperty);

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        if (!Labelled) return IsVertical ? new LayoutSize(Thickness, 0) : new LayoutSize(0, Thickness);
        foreach (var part in parts) part.Measure(LayoutSize.Infinity);
        var labelSize = label!.DesiredSize;
        var lines = parts.Count - 1;
        return new LayoutSize(labelSize.Width + lines * Gap, Math.Max(labelSize.Height, Thickness));
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        if (!Labelled) return finalSize;
        var labelWidth = label!.DesiredSize.Width;
        var lines = parts.Count - 1;
        var lineWidth = Math.Max(0, (finalSize.Width - labelWidth - lines * Gap) / lines);
        var x = 0d;
        foreach (var part in parts)
        {
            var width = part == label ? labelWidth : lineWidth;
            part.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Gap;
        }
        return finalSize;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (!Labelled) context.FillRectangle(Background ?? Brushes.Transparent, new Rect(Bounds.Size));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SeparatorAutomationPeer(this);

    /// <summary>装饰性的不进控件视图；有语义时是分隔符，带文字时以文字为名称。</summary>
    sealed class SeparatorAutomationPeer(Separator owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        protected override bool IsControlElementCore() => !owner.Decorative;

        protected override bool IsContentElementCore() => !owner.Decorative;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.label is TextBlock text ? text.Text : null;
    }
}

/// <summary>Separator 的部件名：带文字时的子元素，样式用子选择器 <c>rune|Separator &gt; Border#PART_Line</c>。</summary>
public static class SeparatorParts
{
    /// <summary>文字（TS 的 propsLabel）。</summary>
    public const string Label = "PART_Label";

    /// <summary>两段线（TS 的 propsLine），两段都叫这个名字。</summary>
    public const string Line = "PART_Line";
}

/// <summary>分隔线方向（vue-tsx-pro 的 SeparatorOrientations）。</summary>
/// <param name="Value">取值（horizontal、vertical）。</param>
[TypeConverter(typeof(VariantConverter<SeparatorOrientation>))]
public readonly record struct SeparatorOrientation(string Value) : IVariant<SeparatorOrientation>
{
    /// <summary>横线：上下两部分之间，占满容器宽度（默认）。</summary>
    public static readonly SeparatorOrientation Horizontal = new("horizontal");

    /// <summary>竖线：左右两部分之间，在 Flex 里随行高拉伸。</summary>
    public static readonly SeparatorOrientation Vertical = new("vertical");

    /// <inheritdoc />
    public static IReadOnlyList<SeparatorOrientation> Known { get; } = [Horizontal, Vertical];

    /// <inheritdoc />
    public static SeparatorOrientation Parse(string value)
    {
        var orientation = new SeparatorOrientation(value);
        Variants.WarnIfUnknown(orientation);
        return orientation;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>文字在横线上的位置（vue-tsx-pro 的 SeparatorLabelAligns）。</summary>
/// <param name="Value">取值（start、center、end）。</param>
[TypeConverter(typeof(VariantConverter<SeparatorLabelAlign>))]
public readonly record struct SeparatorLabelAlign(string Value) : IVariant<SeparatorLabelAlign>
{
    /// <summary>起始边：文字在前、线在后，像一节的小标题。</summary>
    public static readonly SeparatorLabelAlign Start = new("start");

    /// <summary>居中（默认）：两边各一段线，如"或者"。</summary>
    public static readonly SeparatorLabelAlign Center = new("center");

    /// <summary>结束边。</summary>
    public static readonly SeparatorLabelAlign End = new("end");

    /// <inheritdoc />
    public static IReadOnlyList<SeparatorLabelAlign> Known { get; } = [Start, Center, End];

    /// <inheritdoc />
    public static SeparatorLabelAlign Parse(string value)
    {
        var align = new SeparatorLabelAlign(value);
        Variants.WarnIfUnknown(align);
        return align;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
