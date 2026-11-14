using System.Runtime.CompilerServices;

namespace Rune.WinForms;

/// <summary>
/// 相连的一组（vue-tsx-pro 的 AttachedContext）：组给里面的控件写上要去掉圆角的角，控件画外壳时收掉。
/// 按钮家族记在 ButtonCore 上，输入框、选择框（ControlShell 画的外壳）记在这里。
/// </summary>
static class Attached
{
    static readonly ConditionalWeakTable<Control, StrongBox<AttachedCorners>> Corners = [];

    /// <summary>控件去掉圆角的角；不在组里时是 None。</summary>
    public static AttachedCorners Get(Control control) =>
        control is IGroupButton button ? button.Core.Attached : Corners.TryGetValue(control, out var box) ? box.Value : AttachedCorners.None;

    /// <summary>写去掉圆角的角并重画。</summary>
    public static void Set(Control control, AttachedCorners corners)
    {
        if (control is IGroupButton button)
        {
            button.Core.Attached = corners;
            return;
        }
        if (Get(control) == corners) return;
        if (corners == AttachedCorners.None) Corners.Remove(control);
        else Corners.AddOrUpdate(control, new StrongBox<AttachedCorners>(corners));
        control.Invalidate();
    }
}
