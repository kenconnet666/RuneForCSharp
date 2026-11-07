using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 输入类控件的外壳（vue-tsx-pro 的 inputRecipe、controlParts）：<see cref="TextInputControl"/>、<see cref="SelectControl"/> 共用。
/// 外壳控件高度、左右 space.md；surface 底（soft、只读时 fill）、xs 宽的描边（border，悬停 borderStrong；soft 时没有；只读 borderMuted；出错 danger）、圆角；
/// 里面有焦点时 2 像素的焦点环压住边框（出错时 danger）。
/// </summary>
static class ControlShell
{
    /// <summary>外壳的底色。</summary>
    /// <param name="colors">主题的颜色。</param>
    /// <param name="variant">样式。</param>
    /// <param name="readOnly">只读。</param>
    public static Color Fill(ThemeColors colors, ControlVariant variant, bool readOnly) => variant == ControlVariant.Soft || readOnly ? colors.Fill : colors.Surface;

    /// <summary>左右 space.md 的内边距、控件高度的最小尺寸。</summary>
    /// <param name="host">外壳。</param>
    /// <param name="em">外壳的字号（设备像素）。</param>
    public static void Layout(Control host, float em)
    {
        host.Padding = new Padding((int)MathF.Round(0.75f * em), 0, (int)MathF.Round(0.75f * em), 0);
        host.MinimumSize = new Size(0, (int)MathF.Ceiling(ControlHeight.Of(ElementContext.Of(host), em, Tokens.Size.Control)));
    }

    /// <summary>
    /// 框里的小按钮（清空、显示密码）：比独立的图标按钮紧凑，1.5em（不小于 24 设备无关像素）见方，四周照旧留着画焦点环的 Padding。
    /// </summary>
    /// <param name="button">按钮。</param>
    /// <param name="em">外壳的字号（设备像素）。</param>
    public static void FitInnerButton(Control button, float em)
    {
        var side = (int)MathF.Round(MathF.Max(1.5f * em, ElementContext.Of(button).Scale(Tokens.Size.MinTarget)));
        button.AutoSize = false;
        button.Size = new Size(side + button.Padding.Horizontal, side + button.Padding.Vertical);
    }

    /// <summary>画外壳（底色、描边、焦点环）。</summary>
    /// <param name="graphics">画布。</param>
    /// <param name="host">外壳。</param>
    /// <param name="em">外壳的字号（设备像素）。</param>
    /// <param name="radius">圆角档位。</param>
    /// <param name="variant">样式。</param>
    /// <param name="state">只读、出错、悬停、里面有焦点。</param>
    public static void Paint(Graphics graphics, Control host, float em, Radius radius, ControlVariant variant, (bool ReadOnly, bool Invalid, bool Hovered, bool Focused) state)
    {
        var theme = ProviderControl.ThemeOf(host);
        var colors = theme.Colors;
        var (width, height) = (host.Width, host.Height);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var corner = Math.Min((radius.In(theme.Radii) ?? 0) * em, height / 2f);
        using (var path = Drawing.RoundedRectangle(new RectangleF(0, 0, width, height), corner))
        using (var fill = new SolidBrush(Fill(colors, variant, state.ReadOnly)))
            graphics.FillPath(fill, path);
        var line = host.LogicalToDeviceUnits(1);
        Color? border = state.Invalid ? colors.Danger : state.ReadOnly ? colors.BorderMuted : variant == ControlVariant.Soft ? null : state.Hovered ? colors.BorderStrong : colors.Border;
        if (border is { } stroke)
        {
            using var outline = Drawing.RoundedRectangle(new RectangleF(line / 2f, line / 2f, width - line, height - line), Math.Max(0, corner - line / 2f));
            using var pen = new Pen(stroke, line);
            graphics.DrawPath(pen, outline);
        }
        if (!state.Focused) return;
        var ring = host.LogicalToDeviceUnits(2);
        using var focus = Drawing.RoundedRectangle(new RectangleF(ring / 2f, ring / 2f, width - ring, height - ring), Math.Max(0, corner - ring / 2f));
        using var focusPen = new Pen(state.Invalid ? colors.Danger : colors.FocusRing, ring);
        graphics.DrawPath(focusPen, focus);
    }
}
