using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Rune.WinForms;

/// <summary>
/// 分隔线（vue-tsx-pro 的 Separator）：borderWidths.xs 宽、borderMuted 色。无窗口元素，放进窗体用 <see cref="SeparatorControl"/>。
/// 带文字（只用于横线）时文字是 0.93em、次要文字色，两段线隔 space.md；<see cref="LabelAlign"/> 决定线在哪边。
/// </summary>
public class Separator : Element
{
    Text label = new() { Wrap = TextWrap.NoWrap, Tone = TextTone.Muted };

    /// <summary>方向；不写时 horizontal。</summary>
    public SeparatorOrientation Orientation { get => field.Value is null ? SeparatorOrientation.Horizontal : field; set { field = value; InvalidateMeasure(); } }

    /// <summary>带文字时文字的位置；不写时 center。</summary>
    public SeparatorLabelAlign LabelAlign { get => field.Value is null ? SeparatorLabelAlign.Center : field; set { field = value; Invalidate(); } }

    /// <summary>线上的文字（只用于横线）。</summary>
    public string Content { get => label.Content; set { label.Content = value; InvalidateMeasure(); } }

    /// <summary>文字元素（测试用）。</summary>
    internal Text LabelPart => label;

    /// <summary>文字的部件属性（TS 的 propsLabel），逐字段盖在默认的次要文字色上。</summary>
    public TextProps? PropsLabel
    {
        get;
        set
        {
            field = value;
            label = new Text { Content = label.Content, Wrap = TextWrap.NoWrap };
            new TextProps { Tone = TextTone.Muted }.Merge(value).ApplyTo(label);
            InvalidateMeasure();
        }
    }

    bool IsVertical => Orientation.Value == "vertical";

    bool Labelled => !IsVertical && Content.Length > 0;

    /// <summary>文字的环境：0.93em。</summary>
    static ElementContext LabelContext(ElementContext context) =>
        context with { Font = Fonts.Get(context.Font.FamilyName(), context.Font.SizeInPoints * 0.93f, context.Font.Style) };

    static int Thickness(ElementContext context) => Math.Max(1, (int)MathF.Round(context.Scale(Tokens.BorderWidth.Xs)));

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var thickness = Thickness(context);
        if (!Labelled) return IsVertical ? new Size(thickness, 0) : new Size(0, thickness);
        var inner = LabelContext(context);
        var text = label.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        var lines = LabelAlign.Value == "center" ? 2 : 1;
        return new Size(text.Width + lines * Gap(inner), Math.Max(text.Height, thickness));
    }

    static int Gap(ElementContext inner) => (int)MathF.Round(Tokens.Space.Md * inner.Em);

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        using var brush = new SolidBrush(context.Theme.Colors.BorderMuted);
        var thickness = Thickness(context);
        if (!Labelled)
        {
            graphics.FillRectangle(brush, Bounds);
            return;
        }
        var inner = LabelContext(context);
        var text = label.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        var gap = Gap(inner);
        var align = LabelAlign.Value;
        var lines = align == "center" ? 2 : 1;
        var lineWidth = Math.Max(0, (Bounds.Width - text.Width - lines * gap) / lines);
        // 从起始边排：线、文字、线（从右到左时镜像）
        var x = align == "start" ? 0 : lineWidth + gap;
        var labelBounds = new Rectangle(Bounds.X + Mirror(x, text.Width), Bounds.Y + (Bounds.Height - text.Height) / 2, text.Width, text.Height);
        label.Bounds = labelBounds;
        label.Paint(graphics, inner);
        var y = Bounds.Y + (Bounds.Height - thickness) / 2;
        if (align != "start") graphics.FillRectangle(brush, Bounds.X + Mirror(0, lineWidth), y, lineWidth, thickness);
        if (align != "end") graphics.FillRectangle(brush, Bounds.X + Mirror(x + text.Width + gap, lineWidth), y, Bounds.Width - x - text.Width - gap, thickness);

        int Mirror(int left, int width) => context.RightToLeft ? Bounds.Width - left - width : left;
    }
}

/// <summary>
/// 放进窗体的分隔线（vue-tsx-pro 的 Separator）：默认是装饰（读屏里没有角色）；分隔两组不同的内容、要读屏知道时 <see cref="Decorative"/> = false，
/// 读屏里是分隔符，带文字时以文字为名称。横线按停靠或父容器给的宽度画；竖线在 FlexControl 里随行高拉伸（自带 align-self: stretch）。
/// </summary>
public class SeparatorControl : ElementControl<Separator>, IFlexItem
{
    /// <summary>新建：不能获得焦点，装饰性。</summary>
    public SeparatorControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        UpdateRole();
    }

    /// <summary>方向。</summary>
    [Category("Appearance")]
    [Description("方向：horizontal（默认）、vertical。")]
    public SeparatorOrientation Orientation { get => Element.Orientation; set => Element.Orientation = value; }

    /// <summary>带文字时文字的位置。</summary>
    [Category("Appearance")]
    [Description("带文字时文字的位置：start、center（默认）、end。")]
    public SeparatorLabelAlign LabelAlign { get => Element.LabelAlign; set => Element.LabelAlign = value; }

    /// <summary>只是视觉装饰（默认）。</summary>
    [Category("Accessibility")]
    [Description("只是视觉装饰：读屏里没有角色；false 时是分隔符。")]
    [DefaultValue(true)]
    public bool Decorative { get; set { field = value; UpdateRole(); } } = true;

    /// <summary>线上的文字（只用于横线）。</summary>
    [AllowNull]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <summary>文字部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsLabel { get => Element.PropsLabel; set => Element.PropsLabel = value; }

    FlexAlign IFlexItem.AlignSelf => Orientation.Value == "vertical" ? FlexAlign.Stretch : default;

    bool ShouldSerializeOrientation() => Orientation != SeparatorOrientation.Horizontal;

    void ResetOrientation() => Orientation = default;

    bool ShouldSerializeLabelAlign() => LabelAlign != SeparatorLabelAlign.Center;

    void ResetLabelAlign() => LabelAlign = default;

    void UpdateRole() => AccessibleRole = Decorative ? AccessibleRole.None : AccessibleRole.Separator;

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }
}

/// <summary>分隔线方向（vue-tsx-pro 的 SeparatorOrientations）。</summary>
/// <param name="Value">取值（horizontal、vertical）。</param>
[TypeConverter(typeof(VariantConverter<SeparatorOrientation>))]
public readonly record struct SeparatorOrientation(string Value) : IVariant<SeparatorOrientation>
{
    /// <summary>横线：上下两部分之间，占满容器宽度（默认）。</summary>
    public static readonly SeparatorOrientation Horizontal = new("horizontal");

    /// <summary>竖线：左右两部分之间，在 FlexControl 里随行高拉伸。</summary>
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
