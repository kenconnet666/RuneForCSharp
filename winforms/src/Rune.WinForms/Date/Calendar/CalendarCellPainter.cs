using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>一格日历的外观（画格子用，同 Avalonia、WPF 格子按钮的那几个状态）。</summary>
/// <param name="Marked">已选（区间时是两端）：accent 实底。</param>
/// <param name="Middle">区间中间：透明底、accentText 字。</param>
/// <param name="InRange">在区间里：底下画 accentSoft 的一条。</param>
/// <param name="BandLeft">那一条在左边是圆的（区间的开始、一行的开头）。</param>
/// <param name="BandRight">那一条在右边是圆的。</param>
/// <param name="Today">今天（本月、今年、本季度）：semibold 加 accentBorder 细圈。</param>
/// <param name="Muted">补齐的上下月、不可选的日期：textMuted。</param>
/// <param name="Unavailable">不可选的日期：划掉。</param>
/// <param name="Off">不能选：变淡。</param>
/// <param name="Hover">指针在上面。</param>
/// <param name="Highlighted">浅色底（DatePicker 选周时已选那一周的另外六天）：accentSoft 底、accentText 字。</param>
readonly record struct CalendarCellLook(bool Marked, bool Middle, bool InRange, bool BandLeft, bool BandRight, bool Today, bool Muted, bool Unavailable, bool Off, bool Hover, bool Highlighted = false);

/// <summary>画日历的格子（CalendarGridControl 的日期、月份、年份格与 QuarterCalendarControl 的季度格共用，同上游一个配方）。</summary>
static class CalendarCellPainter
{
    /// <summary>画底：区间的一条、填充、今天的细圈；返回这一格的文字色（已经按不能选、不可用变淡）。</summary>
    /// <param name="g">画布。</param>
    /// <param name="host">所在的控件（往上找背景色，混出变淡的颜色）。</param>
    /// <param name="colors">主题颜色。</param>
    /// <param name="radius">md 圆角（像素）。</param>
    /// <param name="bounds">格子。</param>
    /// <param name="look">外观。</param>
    /// <param name="enabled">整个日历可用。</param>
    public static Color PaintBackground(Graphics g, Control host, ThemeColors colors, float radius, RectangleF bounds, CalendarCellLook look, bool enabled)
    {
        var dim = Drawing.Dimmer(host, enabled, colors);
        var corner = MathF.Min(radius, bounds.Height / 2);
        if (look.InRange)
        {
            // 区间的底色画在格子上，相邻的格连成一条；两端与每行的首尾是圆的
            using var band = Band(bounds, look.BandLeft ? corner : 0, look.BandRight ? corner : 0);
            using var soft = new SolidBrush(dim(colors.AccentSoft));
            g.FillPath(soft, band);
        }
        var hover = look.Hover && !look.Off;
        Color? fill = look.Marked ? (hover ? colors.AccentHover : colors.Accent)
            : look.Middle ? (hover ? colors.AccentSoftHover : null)
            : look.Highlighted ? (hover ? colors.AccentSoftHover : colors.AccentSoft)
            : hover ? colors.FillHover : null;
        if (fill is { } color)
        {
            using var shape = Drawing.RoundedRectangle(bounds, corner);
            using var brush = new SolidBrush(dim(color));
            g.FillPath(brush, shape);
        }
        if (look.Today && !look.Marked)
        {
            using var ring = Drawing.RoundedRectangle(RectangleF.Inflate(bounds, -0.5f, -0.5f), corner);
            using var pen = new Pen(dim(colors.AccentBorder), 1);
            g.DrawPath(pen, ring);
        }
        var fore = look.Marked ? colors.AccentContrast : look.Middle || look.Highlighted ? colors.AccentText : look.Muted ? colors.TextMuted : colors.Text;
        // 不能选的变淡（opacities.disabled）；整个日历不可用时 dim 已经混过一次
        if (look.Off && enabled) fore = Drawing.Mix(BackOf(host, colors), fore, Tokens.Opacity.Disabled);
        return dim(fore);
    }

    /// <summary>画格子里的文字：今天加粗，不可选的划掉，居中、可以换行。</summary>
    public static void PaintText(Graphics g, string text, Font font, Rectangle bounds, Color fore, CalendarCellLook look)
    {
        var style = (look.Today ? FontStyle.Bold : FontStyle.Regular) | (look.Unavailable ? FontStyle.Strikeout : FontStyle.Regular);
        var shown = style == FontStyle.Regular ? font : Fonts.Get(font.FamilyName(), font.SizeInPoints, style);
        TextRenderer.DrawText(g, text, shown, bounds, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
    }

    /// <summary>键盘焦点的一圈（2px focusRing，压在格子边上）。</summary>
    public static void PaintFocus(Graphics g, Control host, ThemeColors colors, float radius, RectangleF bounds)
    {
        var width = 2 * host.DeviceDpi / 96f;
        using var ring = Drawing.RoundedRectangle(RectangleF.Inflate(bounds, -width / 2, -width / 2), MathF.Min(radius, bounds.Height / 2));
        using var pen = new Pen(colors.FocusRing, width);
        g.DrawPath(pen, ring);
    }

    static Color BackOf(Control host, ThemeColors colors)
    {
        for (Control? control = host; control is not null; control = control.Parent)
            if (control.BackColor.A == 255) return control.BackColor;
        return colors.Background;
    }

    static GraphicsPath Band(RectangleF bounds, float left, float right)
    {
        var path = new GraphicsPath();
        if (left > 0) path.AddArc(bounds.Left, bounds.Top, 2 * left, 2 * left, 180, 90);
        else path.AddLine(bounds.Left, bounds.Top, bounds.Left, bounds.Top);
        if (right > 0)
        {
            path.AddArc(bounds.Right - 2 * right, bounds.Top, 2 * right, 2 * right, 270, 90);
            path.AddArc(bounds.Right - 2 * right, bounds.Bottom - 2 * right, 2 * right, 2 * right, 0, 90);
        }
        else
        {
            path.AddLine(bounds.Right, bounds.Top, bounds.Right, bounds.Bottom);
        }
        if (left > 0) path.AddArc(bounds.Left, bounds.Bottom - 2 * left, 2 * left, 2 * left, 90, 90);
        else path.AddLine(bounds.Left, bounds.Bottom, bounds.Left, bounds.Bottom);
        path.CloseFigure();
        return path;
    }
}
