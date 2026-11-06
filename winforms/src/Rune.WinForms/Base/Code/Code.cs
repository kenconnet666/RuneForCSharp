using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 行内代码（vue-tsx-pro 的 Code）：等宽字体、浅底，字号是宿主的 0.9 倍，左右内边距 space.xs、圆角 radii.sm。
/// WinForms 的文字是整段画出来的，没有行内元素：Code 是单独的元素（像 Kbd），不跟着一段文字换行（DESIGN §14）。
/// </summary>
public class Code : Element
{
    readonly Text text = new() { Mono = true, Wrap = TextWrap.NoWrap };

    /// <summary>代码。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available) =>
        ChipText.Measure(text, Inner(context), Tokens.Space.Xs);

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context) =>
        ChipText.Paint(graphics, text, Inner(context), Bounds, Tokens.Space.Xs, Radius.Sm, context.Theme.Colors.Fill);

    static ElementContext Inner(ElementContext context) => ChipText.Scaled(context, 0.9f) with { ForeColor = context.Theme.Colors.Text };
}

/// <summary>
/// 高亮标记（vue-tsx-pro 的 Mark）：底色取主题的 highlight，文字颜色不变；左右内边距 0.15em、圆角 radii.xs。
/// 同 <see cref="Code"/>，是单独的元素，不跟着一段文字换行。
/// </summary>
public class Mark : Element
{
    readonly Text text = new() { Wrap = TextWrap.NoWrap };

    /// <summary>文字。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available) => ChipText.Measure(text, context, 0.15f);

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context) =>
        ChipText.Paint(graphics, text, context, Bounds, 0.15f, Radius.Xs, context.Theme.Colors.Highlight);
}

/// <summary>Code、Mark 共用：一段不换行的文字放在带底色、圆角的小盒子里，左右内边距按 em。</summary>
static class ChipText
{
    /// <summary>字号乘上倍数（CSS 的 <c>font-size: 0.9em</c>）。</summary>
    public static ElementContext Scaled(ElementContext context, float scale) =>
        context with { Font = Fonts.Get(context.Font.FontFamily.Name, context.Font.SizeInPoints * scale, context.Font.Style) };

    public static Size Measure(Text text, ElementContext context, float paddingInline)
    {
        var size = text.Measure(context, new Size(int.MaxValue, int.MaxValue));
        return new Size((int)MathF.Ceiling(size.Width + 2 * paddingInline * context.Em), size.Height);
    }

    public static void Paint(Graphics graphics, Text text, ElementContext context, Rectangle bounds, float paddingInline, Radius radius, Color background)
    {
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Drawing.RoundedRectangle(bounds, (radius.In(context.Theme.Radii) ?? 0) * context.Em);
            using var brush = new SolidBrush(background);
            graphics.FillPath(brush, path);
        }
        finally
        {
            graphics.Restore(state);
        }
        var pad = (int)MathF.Round(paddingInline * context.Em);
        text.Bounds = new Rectangle(bounds.X + pad, bounds.Y, Math.Max(0, bounds.Width - 2 * pad), bounds.Height);
        text.Paint(graphics, context);
    }
}

/// <summary>放进窗体的行内代码（单元素宿主）。</summary>
public class CodeControl : ElementControl<Code>
{
    /// <summary>新建：不能获得焦点，读屏当作静态文字。</summary>
    public CodeControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.StaticText;
    }

    /// <summary>代码。</summary>
    [Category("Appearance")]
    [Description("代码，如 button。")]
    [AllowNull]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }
}

/// <summary>放进窗体的高亮标记（单元素宿主）。</summary>
public class MarkControl : ElementControl<Mark>
{
    /// <summary>新建：不能获得焦点，读屏当作静态文字。</summary>
    public MarkControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.StaticText;
    }

    /// <summary>文字。</summary>
    [Category("Appearance")]
    [Description("要高亮的文字。")]
    [AllowNull]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }
}
