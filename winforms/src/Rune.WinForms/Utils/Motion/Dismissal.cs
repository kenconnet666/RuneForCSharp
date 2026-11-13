namespace Rune.WinForms;

/// <summary>
/// 请使用方移除（vue-tsx-pro 的 useDismissible）：Tag 的移除、Alert 与 Banner 的关闭共用。子控件各画在自己的窗口上，不能半透明，
/// 所以没有离场的淡出：直接请使用方移除；焦点原来在组件里、组件被移除了时，交给同一容器里下一个（没有就上一个）能聚焦的控件。
/// </summary>
static class Dismissal
{
    /// <summary>请使用方移除，之后按需交出焦点。</summary>
    /// <param name="owner">要移除的组件。</param>
    /// <param name="requested">请使用方移除（发事件）。</param>
    public static void Request(Control owner, Action requested)
    {
        var focused = owner.ContainsFocus;
        var group = owner.Parent;
        var index = group?.Controls.IndexOf(owner) ?? -1;
        requested();
        if (!focused || group is null || owner.Parent == group) return;
        var siblings = group.Controls.Cast<Control>().ToList();
        var candidates = siblings.Skip(index).Concat(siblings.Take(index).Reverse());
        foreach (var candidate in candidates)
        {
            if (!candidate.Visible || !candidate.Enabled) continue;
            if (candidate.CanSelect)
            {
                candidate.Select();
                return;
            }
            if (candidate.SelectNextControl(null, true, true, true, false)) return;
        }
    }
}
