using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 组合输入（vue-tsx-pro 的 ControlGroup）：输入框、选择框、按钮连成一条，共用边框：币种 + 金额、协议 + 域名、搜索框 + 搜索按钮。
/// <code>
/// var group = new ControlGroupControl();
/// group.Controls.AddRange([new SelectControl { ItemsSource = currencies, Value = "USD", AccessibleName = "币种" }, new NumberFieldControl { Value = 1200, AccessibleName = "金额" }]);
/// var field = new FieldControl { Label = "合同金额", Content = group };
/// </code>
/// 里面的输入框外壳与按钮家族去掉相邻一侧的圆角、边框叠在一起，聚焦的那个浮到上面。默认占满给的宽度，输入框分掉剩余宽度，
/// 选择框、按钮按内容宽；<see cref="Inline"/> 时按内容宽。按钮与输入框一样高：不另留焦点环的边距，环画在按钮里面。
/// 放在 FieldControl 里时组是字段的控件：标签、说明给组（读屏的名称与帮助文字），点标签聚焦第一个控件；里面的各个控件不再拿标签，各自写名称。
/// </summary>
[ToolboxItem(true)]
[Description("组合输入：输入框、选择框、按钮连成一条。")]
public class ControlGroupControl : AttachedGroupControl, IFieldControl
{
    /// <summary>不占满一行（行内排列，按内容宽），默认占满。</summary>
    [Category("Layout")]
    [DefaultValue(false)]
    public bool Inline { get; set { field = value; Restyle(); } }

    /// <inheritdoc />
    protected override bool Fills => !Inline;

    /// <summary>输入框分掉剩余宽度（TS 的 attached 横排时 flex-grow: 1）；选择框、按钮按内容宽。</summary>
    protected override bool Grows(Control child) => !Inline && child is TextInputControl;

    /// <summary>按钮与输入框外壳一样高、一样宽（输入框的外壳铺满控件），不给焦点环留位置。</summary>
    protected override Padding RingRoom(Control button, bool first, bool last) => Padding.Empty;

    void IFieldControl.FieldChanged() => AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ControlGroupAccessibleObject(this);

    /// <summary>读屏：一个组；没写名称时取所在字段的标签，帮助文字是字段的说明与错误（TS 的 aria-labelledby、aria-describedby 指向组）。</summary>
    sealed class ControlGroupAccessibleObject(ControlGroupControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name
        {
            get => base.Name is { Length: > 0 } name ? name : FieldControl.Of(owner) is { } outer && outer.Owns(owner) ? outer.Label : null;
            set => base.Name = value;
        }

        public override string? Description => FieldControl.DescribedBy(owner, base.Description);
    }
}
