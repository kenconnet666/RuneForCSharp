using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 展开收起的标题按钮（手风琴的标题、可折叠表单分组的标题）：悬停 fill 底，读屏是按钮、带展开或收起的状态（TS 的 aria-expanded）。
/// </summary>
class DisclosureTrigger : PressableFlex
{
    /// <summary>展开着（读屏的状态）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Expanded
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        }
    }

    /// <summary>悬停 fill 底（PressableFlex 默认是更深的 fillHover）。</summary>
    protected override void PaintSurface(Graphics graphics, ThemeTokens theme, float radius)
    {
        if (!Hovered) return;
        using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), radius);
        using var brush = new SolidBrush(theme.Colors.Fill);
        graphics.FillPath(brush, path);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new TriggerAccessibleObject(this);

    sealed class TriggerAccessibleObject(DisclosureTrigger owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.PushButton;

        public override AccessibleStates State => base.State | (owner.Expanded ? AccessibleStates.Expanded : AccessibleStates.Collapsed);

        public override string DefaultAction => owner.Expanded ? "收起" : "展开";

        public override void DoDefaultAction() => owner.InvokeOnClick(owner, EventArgs.Empty);
    }
}
