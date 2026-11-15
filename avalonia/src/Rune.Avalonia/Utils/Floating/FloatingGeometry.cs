namespace Rune.Avalonia;

/// <summary>一个矩形（与平台无关：FloatingGeometry 的输入与结果）。</summary>
/// <param name="X">左边。</param>
/// <param name="Y">上边。</param>
/// <param name="Width">宽。</param>
/// <param name="Height">高。</param>
public readonly record struct FloatingBox(double X, double Y, double Width, double Height)
{
    /// <summary>右边。</summary>
    public double Right => X + Width;

    /// <summary>下边。</summary>
    public double Bottom => Y + Height;
}

/// <summary>浮层贴着目标的位置（vue-tsx-pro floating 里的 computePlacement，与平台无关）。</summary>
public static class FloatingGeometry
{
    /// <summary>与可用范围的边至少留出的距离。</summary>
    public const double ViewportPadding = 8;

    /// <summary>
    /// 浮层的位置与最终的边：按想要的一侧放，放不下换对侧（<paramref name="crossAxisFallback"/> 时再试垂直的两侧），都放不下选溢出最少的一侧；
    /// 最后只沿对齐的方向挪回可用范围里（不压到目标上），比可用范围还宽时贴着起始边。
    /// </summary>
    /// <param name="placement">放在哪一边、怎么对齐。</param>
    /// <param name="rightToLeft">从右到左排版：start / end 对齐镜像。</param>
    /// <param name="anchor">目标（与可用范围同一坐标）。</param>
    /// <param name="width">浮层宽。</param>
    /// <param name="height">浮层高。</param>
    /// <param name="gap">离目标多远。</param>
    /// <param name="viewport">可用范围（屏幕、窗口的客户区）。</param>
    /// <param name="crossAxisFallback">上下（左右）都放不下时试另外两边（Tour 的卡片）。</param>
    public static (string Side, FloatingBox Panel) Place(FloatingPlacement placement, bool rightToLeft, FloatingBox anchor, double width, double height, double gap, FloatingBox viewport, bool crossAxisFallback = false)
    {
        var (left, top, right, bottom) = (viewport.X + ViewportPadding, viewport.Y + ViewportPadding, viewport.Right - ViewportPadding, viewport.Bottom - ViewportPadding);
        FloatingBox At(string side)
        {
            var x = placement.Align switch
            {
                "start" => rightToLeft ? anchor.Right - width : anchor.X,
                "end" => rightToLeft ? anchor.X : anchor.Right - width,
                _ => anchor.X + (anchor.Width - width) / 2,
            };
            // 左右两边的 start / end 是上下，不随书写方向镜像
            var y = placement.Align switch
            {
                "start" => anchor.Y,
                "end" => anchor.Bottom - height,
                _ => anchor.Y + (anchor.Height - height) / 2,
            };
            return side switch
            {
                "bottom" => new FloatingBox(x, anchor.Bottom + gap, width, height),
                "left" => new FloatingBox(anchor.X - gap - width, y, width, height),
                "right" => new FloatingBox(anchor.Right + gap, y, width, height),
                _ => new FloatingBox(x, anchor.Y - gap - height, width, height),
            };
        }
        // 这一侧还差多少放不下（≤ 0 放得下）
        double Overflow(string side)
        {
            var box = At(side);
            return side switch
            {
                "bottom" => box.Bottom - bottom,
                "left" => left - box.X,
                "right" => box.Right - right,
                _ => top - box.Y,
            };
        }
        var wanted = placement.Side;
        var opposite = wanted switch { "bottom" => "top", "left" => "right", "right" => "left", _ => "bottom" };
        string[] perpendicular = wanted is "top" or "bottom" ? rightToLeft ? ["left", "right"] : ["right", "left"] : ["bottom", "top"];
        string[] candidates = crossAxisFallback ? [wanted, opposite, .. perpendicular] : [wanted, opposite];
        var chosen = candidates.FirstOrDefault(side => Overflow(side) <= 0) ?? candidates.MinBy(Overflow)!;
        var panel = At(chosen);
        panel = chosen is "top" or "bottom"
            ? panel with { X = Math.Max(left, Math.Min(panel.X, right - width)) }
            : panel with { Y = Math.Max(top, Math.Min(panel.Y, bottom - height)) };
        return (chosen, panel);
    }
}
