namespace Rune.WinForms;

/// <summary>浮层（提示、弹出框）贴着触发器的定位。</summary>
static class FloatingLayout
{
    /// <summary>与屏幕边缘至少留出的距离。</summary>
    const int ViewportPadding = 8;

    /// <summary>
    /// 面板在屏幕上的位置与最终的边（同上游 computePlacement）：先试要的那一边，放不下时翻到对面；两边都放不下时选溢出少（空间大）的一边；
    /// 最后沿对齐的方向贴着屏幕边挪回来，比屏幕还宽时贴着起始边。
    /// </summary>
    public static (string Side, Rectangle Panel) Position(FloatingPlacement placement, bool rightToLeft, Rectangle target, Size panel, float gap, Rectangle area)
    {
        Rectangle At(string side)
        {
            var horizontalStart = rightToLeft ? target.Right - panel.Width : target.Left;
            var horizontalEnd = rightToLeft ? target.Left : target.Right - panel.Width;
            var x = placement.Align switch
            {
                "start" => horizontalStart,
                "end" => horizontalEnd,
                _ => target.Left + (target.Width - panel.Width) / 2,
            };
            var y = placement.Align switch
            {
                "start" => target.Top,
                "end" => target.Bottom - panel.Height,
                _ => target.Top + (target.Height - panel.Height) / 2,
            };
            var distance = (int)MathF.Round(gap);
            return side switch
            {
                "bottom" => new Rectangle(x, target.Bottom + distance, panel.Width, panel.Height),
                "left" => new Rectangle(target.Left - distance - panel.Width, y, panel.Width, panel.Height),
                "right" => new Rectangle(target.Right + distance, y, panel.Width, panel.Height),
                _ => new Rectangle(x, target.Top - distance - panel.Height, panel.Width, panel.Height),
            };
        }
        // 这一边还差多少放不下（≤ 0 放得下）
        int Overflow(string side)
        {
            var rect = At(side);
            return side switch
            {
                "bottom" => rect.Bottom - (area.Bottom - ViewportPadding),
                "left" => area.Left + ViewportPadding - rect.Left,
                "right" => rect.Right - (area.Right - ViewportPadding),
                _ => area.Top + ViewportPadding - rect.Top,
            };
        }
        var wanted = placement.Side;
        var opposite = wanted switch { "bottom" => "top", "left" => "right", "right" => "left", _ => "bottom" };
        var side = Overflow(wanted) <= 0 || Overflow(wanted) <= Overflow(opposite) ? wanted : opposite;
        var rect = At(side);
        // 贴着屏幕边挪回来（只挪对齐的那条轴，不压到触发器上）
        if (side is "top" or "bottom")
            rect.X = Math.Clamp(rect.X, area.Left + ViewportPadding, Math.Max(area.Left + ViewportPadding, area.Right - ViewportPadding - rect.Width));
        else
            rect.Y = Math.Clamp(rect.Y, area.Top + ViewportPadding, Math.Max(area.Top + ViewportPadding, area.Bottom - ViewportPadding - rect.Height));
        return (side, rect);
    }
}
