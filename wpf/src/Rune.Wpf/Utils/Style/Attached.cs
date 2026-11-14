using System.Windows;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 相连的一组（vue-tsx-pro 的 AttachedContext）：组给里面的控件写上要去掉圆角的角，控件的圆角据此收掉
/// （<see cref="EmSize.CoerceCornerRadius"/> 里做，按钮家族、文字输入都走它）。组负责边框叠在一起、悬停与聚焦的那个浮到上面。
/// </summary>
public static class Attached
{
    /// <summary>去掉圆角的角；组排版时写，控件不在组里时是 None。</summary>
    public static readonly DependencyProperty CornersProperty = DependencyProperty.RegisterAttached(
        "Corners", typeof(AttachedCorners), typeof(Attached), new PropertyMetadata(AttachedCorners.None, (d, _) => EmSize.RefreshCornerRadius(d)));

    /// <summary>读去掉圆角的角。</summary>
    public static AttachedCorners GetCorners(DependencyObject d) => (AttachedCorners)d.GetValue(CornersProperty);

    /// <summary>写去掉圆角的角。</summary>
    public static void SetCorners(DependencyObject d, AttachedCorners value) => d.SetValue(CornersProperty, value);

    /// <summary>把这几个角的圆角收成 0。</summary>
    public static CornerRadius Square(CornerRadius radius, AttachedCorners corners) => corners == AttachedCorners.None
        ? radius
        : new CornerRadius(
            corners.HasFlag(AttachedCorners.TopLeft) ? 0 : radius.TopLeft,
            corners.HasFlag(AttachedCorners.TopRight) ? 0 : radius.TopRight,
            corners.HasFlag(AttachedCorners.BottomRight) ? 0 : radius.BottomRight,
            corners.HasFlag(AttachedCorners.BottomLeft) ? 0 : radius.BottomLeft);

    /// <summary>四个角各自圆角的矩形（RectangleGeometry 只有一种圆角）：裁剪波纹用。</summary>
    internal static Geometry RoundedRect(Rect rect, CornerRadius r)
    {
        if (r.TopLeft == r.TopRight && r.TopRight == r.BottomRight && r.BottomRight == r.BottomLeft) return new RectangleGeometry(rect, r.TopLeft, r.TopLeft);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            void Corner(double radius, Point end) => context.ArcTo(end, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            context.BeginFigure(new Point(rect.Left + r.TopLeft, rect.Top), true, true);
            context.LineTo(new Point(rect.Right - r.TopRight, rect.Top), true, false);
            Corner(r.TopRight, new Point(rect.Right, rect.Top + r.TopRight));
            context.LineTo(new Point(rect.Right, rect.Bottom - r.BottomRight), true, false);
            Corner(r.BottomRight, new Point(rect.Right - r.BottomRight, rect.Bottom));
            context.LineTo(new Point(rect.Left + r.BottomLeft, rect.Bottom), true, false);
            Corner(r.BottomLeft, new Point(rect.Left, rect.Bottom - r.BottomLeft));
            context.LineTo(new Point(rect.Left, rect.Top + r.TopLeft), true, false);
            Corner(r.TopLeft, new Point(rect.Left + r.TopLeft, rect.Top));
        }
        geometry.Freeze();
        return geometry;
    }
}
