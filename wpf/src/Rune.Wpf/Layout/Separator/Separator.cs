using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 分隔线（vue-tsx-pro 的 Separator）：默认是装饰（读屏看不到）；分隔两组不同的内容、要读屏知道时 <c>Decorative="False"</c>。
/// <code>
/// &lt;rune:Separator /&gt;
/// &lt;rune:Flex Align="center" Gap="sm"&gt;A&lt;rune:Separator Orientation="vertical" /&gt;B&lt;/rune:Flex&gt;   &lt;!-- 竖线随行高拉伸 --&gt;
/// &lt;rune:Separator&gt;或者&lt;/rune:Separator&gt;                       &lt;!-- 横线中间带文字 --&gt;
/// &lt;rune:Separator LabelAlign="start"&gt;更改后&lt;/rune:Separator&gt;   &lt;!-- 文字靠起始边，线在后面 --&gt;
/// </code>
/// 线是 borderWidths.xs 宽、borderMuted 色，自己画（DESIGN §9）。带文字（只用于横线）时文字是 0.93em、次要文字色，两段线隔 space.md；
/// 文字与两段线是代码组合的子元素，引用部件样式键 <see cref="SeparatorParts.Label"/>、<see cref="SeparatorParts.Line"/>。
/// 竖线在 Flex 里自己带 <c>Flex.AlignSelf=stretch</c>（主题样式的触发器），Flex 按 center 对齐时也随行高拉伸。
/// </summary>
[ContentProperty(nameof(Content))]
public class Separator : Control
{
    /// <summary>方向，默认 horizontal。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(SeparatorOrientation), typeof(Separator), new FrameworkPropertyMetadata(default(SeparatorOrientation), OnStructureChanged));

    /// <summary>只是视觉装饰（默认）：读屏看不到；带文字时文字照样给读屏。</summary>
    public static readonly DependencyProperty DecorativeProperty = DependencyProperty.Register(
        nameof(Decorative), typeof(bool), typeof(Separator), new FrameworkPropertyMetadata(true));

    /// <summary>带文字时文字的位置，默认 center。</summary>
    public static readonly DependencyProperty LabelAlignProperty = DependencyProperty.Register(
        nameof(LabelAlign), typeof(SeparatorLabelAlign), typeof(Separator), new FrameworkPropertyMetadata(default(SeparatorLabelAlign), OnStructureChanged));

    /// <summary>线上的文字（只用于横线）。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(Separator), new FrameworkPropertyMetadata(null, OnStructureChanged));

    /// <summary>线宽（主题里是 borderWidths.xs）。</summary>
    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(Separator), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    readonly List<FrameworkElement> parts = [];
    FrameworkElement? label;

    static Separator()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Separator), new FrameworkPropertyMetadata(typeof(Separator)));
        FocusableProperty.OverrideMetadata(typeof(Separator), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Separator), new FrameworkPropertyMetadata(false));
        BackgroundProperty.OverrideMetadata(typeof(Separator), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        // 带文字时比所在的文字小一档（0.93em）
        EmSize.Register(typeof(Separator), fontScale: 0.93);
    }

    /// <summary>新建：先按默认字号算一次相对字号（字号一直是默认值时没有变化回调，同 Code）。</summary>
    public Separator() => CoerceValue(FontSizeProperty);

    /// <summary>方向。</summary>
    public SeparatorOrientation Orientation { get => (SeparatorOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>只是视觉装饰。</summary>
    public bool Decorative { get => (bool)GetValue(DecorativeProperty); set => SetValue(DecorativeProperty, value); }

    /// <summary>带文字时文字的位置。</summary>
    public SeparatorLabelAlign LabelAlign { get => (SeparatorLabelAlign)GetValue(LabelAlignProperty); set => SetValue(LabelAlignProperty, value); }

    /// <summary>线上的文字。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>线宽。</summary>
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    bool IsVertical => Orientation.Value == "vertical";

    double Gap => 0.75 * FontSize;

    /// <inheritdoc />
    protected override int VisualChildrenCount => parts.Count;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => parts[index];

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => parts.GetEnumerator();

    static void OnStructureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Separator)d).Rebuild();

    /// <summary>带文字时：线、文字、线（靠起始边时没有前面那段，靠结束边时没有后面那段）。</summary>
    void Rebuild()
    {
        foreach (var part in parts)
        {
            RemoveLogicalChild(part);
            RemoveVisualChild(part);
        }
        parts.Clear();
        label = null;
        if (Content is not null && !IsVertical)
        {
            if (Content is FrameworkElement element) label = element;
            else
            {
                // 文字：部件样式键的 TargetType 是 rune:Text，使用方给的元素不套
                label = new Text { Text = Content.ToString(), Wrap = TextWrap.NoWrap };
                label.SetResourceReference(StyleProperty, SeparatorParts.Label);
            }
            if (LabelAlign.Value != "start") parts.Add(Line());
            parts.Add(label);
            if (LabelAlign.Value != "end") parts.Add(Line());
            foreach (var part in parts)
            {
                AddVisualChild(part);
                AddLogicalChild(part);
            }
        }
        InvalidateMeasure();
        InvalidateVisual();

        Border Line()
        {
            var line = new LineSegment { VerticalAlignment = VerticalAlignment.Center };
            line.SetResourceReference(StyleProperty, SeparatorParts.Line);
            return line;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        if (label is null) return IsVertical ? new Size(Thickness, 0) : new Size(0, Thickness);
        foreach (var part in parts) part.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new Size(label.DesiredSize.Width + (parts.Count - 1) * Gap, Math.Max(label.DesiredSize.Height, Thickness));
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        if (label is null) return arrangeBounds;
        var labelWidth = label.DesiredSize.Width;
        var lines = parts.Count - 1;
        var lineWidth = Math.Max(0, (arrangeBounds.Width - labelWidth - lines * Gap) / lines);
        var x = 0d;
        foreach (var part in parts)
        {
            var width = part == label ? labelWidth : lineWidth;
            part.Arrange(new Rect(x, 0, width, arrangeBounds.Height));
            x += width + Gap;
        }
        return arrangeBounds;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        if (label is null && Background is { } brush) drawingContext.DrawRectangle(brush, null, new Rect(RenderSize));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SeparatorAutomationPeer(this);

    /// <summary>两段线：读屏看不到。</summary>
    sealed class LineSegment : Border
    {
        protected override AutomationPeer? OnCreateAutomationPeer() => null;
    }

    /// <summary>装饰性的不是控件元素；有语义时是分隔符，带文字时以文字为名称。</summary>
    sealed class SeparatorAutomationPeer(Separator owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        protected override string GetClassNameCore() => nameof(Separator);

        protected override bool IsControlElementCore() => !owner.Decorative;

        protected override bool IsContentElementCore() => !owner.Decorative;

        protected override AutomationOrientation GetOrientationCore() => owner.IsVertical ? AutomationOrientation.Vertical : AutomationOrientation.Horizontal;

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.label is TextBlock text ? text.Text : "";
    }
}

/// <summary>Separator 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:SeparatorParts.Line}" TargetType="Border"&gt;</c>。</summary>
public static class SeparatorParts
{
    /// <summary>文字（TS 的 propsLabel）：内容是文字时是 rune:Text。</summary>
    public static readonly PartKey Label = new(typeof(Separator), nameof(Label), typeof(Text));

    /// <summary>两段线（TS 的 propsLine）。</summary>
    public static readonly PartKey Line = new(typeof(Separator), nameof(Line), typeof(Border));
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
