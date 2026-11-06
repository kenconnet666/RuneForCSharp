using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 按键（vue-tsx-pro 的 Kbd）：快捷键的说明，组合键写成几个 Kbd。无窗口元素，放进窗体用 <see cref="KbdControl"/>。
/// <code>
/// new KbdControl { Text = "Ctrl" };
/// </code>
/// 字号是宿主的 0.85 倍（CSS 的 <c>font-size: 0.85em</c>），等宽字体；其余尺寸按自己的字号算：最小宽度 1.6em、
/// 左右内边距 space.xs、圆角 radii.sm、行高 snug；描边 1px、底边 2px，看起来像一个按键。
/// </summary>
public class Kbd : Element
{
    /// <summary>字号相对宿主的倍数。</summary>
    const float FontScale = 0.85f;

    readonly Text text = new() { Mono = true, Wrap = TextWrap.NoWrap, Align = TextAlign.Center };

    /// <summary>按键上的文字。</summary>
    public string Content { get => text.Content; set { text.Content = value; InvalidateMeasure(); } }

    /// <summary>按键里面的环境：字号 0.85 倍。</summary>
    static ElementContext Inner(ElementContext context) =>
        context with { Font = Fonts.Get(context.Font.FontFamily.Name, context.Font.SizeInPoints * FontScale, context.Font.Style), ForeColor = context.Theme.Colors.Text };

    /// <summary>描边：上、左、右 borderWidths.xs，底边 borderWidths.sm（设备像素）。</summary>
    static (float Side, float Bottom) Borders(ElementContext context) => (context.Scale(Tokens.BorderWidth.Xs), context.Scale(Tokens.BorderWidth.Sm));

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var inner = Inner(context);
        var em = inner.Em;
        var (side, bottom) = Borders(context);
        var content = text.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        var width = Math.Max(1.6f * em, content.Width + 2 * Tokens.Space.Xs * em) + 2 * side;
        var height = Math.Max(Tokens.LineHeight.Snug * em, content.Height) + side + bottom;
        return new Size((int)MathF.Ceiling(width), (int)MathF.Ceiling(height));
    }

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var inner = Inner(context);
        var (side, bottom) = Borders(context);
        var colors = context.Theme.Colors;
        var radius = (Radius.Sm.In(context.Theme.Radii) ?? 0) * inner.Em;
        var outer = new RectangleF(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);
        var face = RectangleF.FromLTRB(outer.Left + side, outer.Top + side, outer.Right - side, outer.Bottom - bottom);
        var state = graphics.Save();
        try
        {
            // 描边色铺满整个按键，再在里面画表面：底边比其余三边厚
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Rounded(outer, radius))
            using (var brush = new SolidBrush(colors.Border))
                graphics.FillPath(brush, path);
            using (var path = Rounded(face, Math.Max(0, radius - side)))
            using (var brush = new SolidBrush(colors.Surface))
                graphics.FillPath(brush, path);
        }
        finally
        {
            graphics.Restore(state);
        }
        var size = text.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        text.Bounds = new Rectangle((int)MathF.Round(face.X), (int)MathF.Round(face.Y + (face.Height - size.Height) / 2), (int)MathF.Round(face.Width), size.Height);
        text.Paint(graphics, inner);
    }

    static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }
        var diameter = 2 * radius;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>放进窗体的按键（单元素宿主）：文字是 Text。读屏把它当作静态文字。</summary>
public class KbdControl : ElementControl<Kbd>
{
    /// <summary>新建：按键不能获得焦点。</summary>
    public KbdControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        AccessibleRole = AccessibleRole.StaticText;
    }

    /// <summary>按键上的文字。</summary>
    [Category("Appearance")]
    [Description("按键上的文字，如 Ctrl、K。")]
    [AllowNull]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }
}
