using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Rune.Avalonia;

/// <summary>
/// 相连的一组（vue-tsx-pro 的 AttachedContext）：组给里面的控件写上要去掉圆角的角，控件的圆角据此收掉（按钮家族在 coerce 里做）。
/// 组负责边框叠在一起、悬停与聚焦的那个浮到上面。
/// </summary>
public static class Attached
{
    /// <summary>去掉圆角的角；组排版时写，控件不在组里时是 None。</summary>
    public static readonly AttachedProperty<AttachedCorners> CornersProperty =
        AvaloniaProperty.RegisterAttached<Control, AttachedCorners>("Corners", typeof(Attached));

    /// <summary>读去掉圆角的角。</summary>
    public static AttachedCorners GetCorners(Control control) => control.GetValue(CornersProperty);

    /// <summary>写去掉圆角的角。</summary>
    public static void SetCorners(Control control, AttachedCorners value) => control.SetValue(CornersProperty, value);

    /// <summary>
    /// 在控件的静态构造里调用：外壳的圆角（主题按 Radius 算出的 CornerRadius）在强制回调里收掉组写上的角，放进、移出组时重算。
    /// 按钮家族、文字输入、选择框都接上：放进 ButtonGroup、ControlGroup 时相邻一侧不要圆角。
    /// </summary>
    public static void SquareCornersOf<T>() where T : TemplatedControl
    {
        TemplatedControl.CornerRadiusProperty.OverrideMetadata<T>(new StyledPropertyMetadata<CornerRadius>(
            coerce: (control, radius) => Square(radius, GetCorners((Control)control))));
        CornersProperty.Changed.AddClassHandler<T>((control, _) => control.CoerceValue(TemplatedControl.CornerRadiusProperty));
    }

    /// <summary>把这几个角的圆角收成 0。</summary>
    public static CornerRadius Square(CornerRadius radius, AttachedCorners corners) => corners == AttachedCorners.None
        ? radius
        : new CornerRadius(
            corners.HasFlag(AttachedCorners.TopLeft) ? 0 : radius.TopLeft,
            corners.HasFlag(AttachedCorners.TopRight) ? 0 : radius.TopRight,
            corners.HasFlag(AttachedCorners.BottomRight) ? 0 : radius.BottomRight,
            corners.HasFlag(AttachedCorners.BottomLeft) ? 0 : radius.BottomLeft);
}
