using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 状态标记（vue-tsx-pro 的 StatusBadge）：名字、色调、图标取自状态映射（<see cref="StatusMap"/>），没有图标的状态前画圆点。
/// <code>
/// var status = new StatusBadgeControl { Statuses = TaskStatuses, Value = task.Status };
/// </code>
/// 不认识的状态显示成中性色、名字就是 key。其余同 BadgeControl；色调、图标、文字由状态决定。
/// </summary>
public class StatusBadgeControl : BadgeControl
{
    /// <summary>新建：没有图标的状态前画圆点。</summary>
    public StatusBadgeControl() => Dot = true;

    /// <summary>状态映射。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public StatusMap? Statuses { get; set { field = value; ApplyStatus(); } }

    /// <summary>当前状态的 key。</summary>
    [Category("Appearance")]
    [Description("当前状态的 key；不认识的状态显示成中性色、名字就是 key。")]
    [DefaultValue(null)]
    public string? Value { get; set { field = value; ApplyStatus(); } }

    void ApplyStatus()
    {
        var status = (Statuses ?? []).Get(Value ?? "");
        Tone = status.Tone;
        Icon = status.Icon;
        Text = status.Label;
    }
}
