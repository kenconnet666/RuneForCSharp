namespace Rune.WinForms;

/// <summary>浮层（提示、弹出框、引导的卡片）贴着触发器的定位：设备像素的矩形换给孪生文件 <see cref="FloatingGeometry"/> 算。</summary>
static class FloatingLayout
{
    /// <summary>面板在屏幕上的位置与最终的边（同上游 computePlacement，见 <see cref="FloatingGeometry.Place"/>）。</summary>
    public static (string Side, Rectangle Panel) Position(FloatingPlacement placement, bool rightToLeft, Rectangle target, Size panel, float gap, Rectangle area, bool crossAxisFallback = false)
    {
        var (side, box) = FloatingGeometry.Place(placement, rightToLeft, Box(target), panel.Width, panel.Height, MathF.Round(gap), Box(area), crossAxisFallback);
        return (side, new Rectangle((int)Math.Round(box.X), (int)Math.Round(box.Y), panel.Width, panel.Height));
    }

    static FloatingBox Box(Rectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}
