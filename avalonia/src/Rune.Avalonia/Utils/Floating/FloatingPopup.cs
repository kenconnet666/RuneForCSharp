using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Media.Transformation;

namespace Rune.Avalonia;

/// <summary>
/// 浮层（Tooltip、Popover）共用的平台适配：<see cref="FloatingPlacement"/> 换成平台弹出层的位置，箭头贴着朝向触发器的那条边。
/// </summary>
static class FloatingPopup
{
    /// <summary>换成平台弹出层的位置；从右到左排版时上下两边的 start / end 镜像。</summary>
    /// <param name="placement">位置。</param>
    /// <param name="rightToLeft">触发器是从右到左排版。</param>
    public static PlacementMode ToPlacementMode(FloatingPlacement placement, bool rightToLeft)
    {
        // 上下两边的 start 是行的起始边：从左到右时是左边
        var startIsLeft = !rightToLeft;
        return (placement.Side, placement.Align) switch
        {
            ("top", "start") => startIsLeft ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.TopEdgeAlignedRight,
            ("top", "end") => startIsLeft ? PlacementMode.TopEdgeAlignedRight : PlacementMode.TopEdgeAlignedLeft,
            ("bottom", "start") => startIsLeft ? PlacementMode.BottomEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedRight,
            ("bottom", "end") => startIsLeft ? PlacementMode.BottomEdgeAlignedRight : PlacementMode.BottomEdgeAlignedLeft,
            ("bottom", _) => PlacementMode.Bottom,
            ("left", "start") => PlacementMode.LeftEdgeAlignedTop,
            ("left", "end") => PlacementMode.LeftEdgeAlignedBottom,
            ("left", _) => PlacementMode.Left,
            ("right", "start") => PlacementMode.RightEdgeAlignedTop,
            ("right", "end") => PlacementMode.RightEdgeAlignedBottom,
            ("right", _) => PlacementMode.Right,
            _ => PlacementMode.Top,
        };
    }

    /// <summary>
    /// 箭头（旋转 45° 的小方块，放在面板所在的 Canvas 层里）贴着朝向触发器的那条边、对准触发器的中心（离面板两端至少一个箭头宽，不画到圆角外），尖角朝向触发器。
    /// 边按面板与触发器在屏幕上的实际位置判断：空间不够时平台把浮层翻到了另一边。
    /// </summary>
    /// <param name="arrow">箭头（边长取它的 Width：刚显示出来时还没排版）。</param>
    /// <param name="panel">箭头所在的层（与面板一样大）。</param>
    /// <param name="target">触发器。</param>
    public static void PlaceArrow(Control arrow, Visual panel, Visual target)
    {
        if (Screen(panel) is not { } box || Screen(target) is not { } anchor) return;
        var size = arrow.Width;
        var side = box.Bottom <= anchor.Top + 0.5 ? "top"
            : box.Top >= anchor.Bottom - 0.5 ? "bottom"
            : box.Right <= anchor.Left + 0.5 ? "left"
            : "right";
        var vertical = side is "top" or "bottom";
        var cross = vertical
            ? Math.Clamp(anchor.Center.X - box.X - size / 2, size, Math.Max(size, box.Width - 2 * size))
            : Math.Clamp(anchor.Center.Y - box.Y - size / 2, size, Math.Max(size, box.Height - 2 * size));
        var (left, top, angle) = side switch
        {
            "top" => (cross, box.Height - size / 2, 45),
            "bottom" => (cross, -size / 2, 225),
            "left" => (box.Width - size / 2, cross, -45),
            _ => (-size / 2, cross, 135),
        };
        Canvas.SetLeft(arrow, left);
        Canvas.SetTop(arrow, top);
        arrow.RenderTransform = TransformOperations.Parse($"rotate({angle}deg)");
    }

    /// <summary>元素在屏幕上的矩形（与设备无关的像素）。</summary>
    static Rect? Screen(Visual visual)
    {
        if (TopLevel.GetTopLevel(visual) is not { } top) return null;
        var origin = visual.PointToScreen(default);
        return new Rect(origin.X / top.RenderScaling, origin.Y / top.RenderScaling, visual.Bounds.Width, visual.Bounds.Height);
    }
}
