using System.Runtime.InteropServices;

namespace Rune.WinForms.Tests;

/// <summary>
/// FieldControl：标签、说明、错误；字段的出错、必填、只读交给里面的控件（控件自己写的优先）；第一个控件认领标签（读屏名称、点标签聚焦）；
/// 标签在起始边时按基线对齐；标签只给读屏。
/// </summary>
public class FieldTests
{
    const uint WmLButtonDown = 0x0201, WmLButtonUp = 0x0202;

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    static void Click(Control control)
    {
        var position = (nint)((4 << 16) | 4);
        SendMessage(control.Handle, WmLButtonDown, 1, position);
        SendMessage(control.Handle, WmLButtonUp, 0, position);
    }

    static Point Offset(Control control, Control ancestor) => ancestor.PointToClient(control.PointToScreen(Point.Empty));

    [Fact]
    [Upstream("Field", "控件自己写了 id：标签的 for 跟着指向它；控件自己的 aria-describedby 与 Field 的说明拼在一起")]
    [Upstream("Field", "不在 Field 里：控件上写的 aria-describedby 照常落到 input")]
    public Task Text_fields_in_a_field_are_described_by_it() => WinFormsUi.Run(() =>
    {
        var inside = new TextFieldControl { Name = "phone" };
        var field = new FieldControl { Label = "联系电话", Description = "只用于送货", Content = inside, Dock = DockStyle.Top };
        var outside = new TextFieldControl { Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(field, outside);

        Assert.Equal("联系电话", inside.AccessibilityObject.Name); // 控件自己有名字时标签照样指向它
        Assert.Equal("只用于送货", inside.AccessibilityObject.Help);
        Assert.Null(outside.AccessibilityObject.Help);
    });

    [Fact]
    [Upstream("Field", "标签的 for 指向控件")]
    public Task Label_description_and_error_are_tied_to_the_control() => WinFormsUi.Run(() =>
    {
        var (email, sms) = (new CheckboxControl { Value = "email", Text = "邮件" }, new CheckboxControl { Value = "sms", Text = "短信" });
        var group = new CheckboxGroupControl();
        group.Controls.AddRange([email, sms]);
        var field = new FieldControl { Label = "通知方式", Description = "至少选一种", Error = "请至少选一种", IsRequired = true, Content = group };
        using var form = WinFormsUi.Show(field);

        Assert.Equal("通知方式", group.AccessibilityObject.Name); // 组以字段的标签为名称
        Assert.Equal("至少选一种 请至少选一种", group.AccessibilityObject.Help);
        Assert.True(field.Parts.Label.IsRequired); // 必填的星号
        Assert.True(field.Parts.Error.Visible);
        Assert.True(email.Element.Invalid); // 有错误：控件出错

        Click(field.Parts.Label);
        Assert.True(email.Focused); // 点标签聚焦组里的第一项
    });

    [Fact]
    [Upstream("Field", "控件上显式写的状态优先于 Field")]
    public Task Explicit_control_state_beats_the_field_and_invalid_false_hides_the_error() => WinFormsUi.Run(() =>
    {
        var quietSwitch = new SwitchControl();
        var quiet = new FieldControl { Label = "名字", Error = "必填", IsInvalid = false, Content = quietSwitch, Dock = DockStyle.Top };
        var own = new SwitchControl { IsInvalid = false, IsReadOnly = false };
        var loud = new FieldControl { Label = "开关", Error = "出错", IsReadOnly = true, Content = own, Dock = DockStyle.Top };
        var inherited = new SwitchControl();
        var readOnly = new FieldControl { IsReadOnly = true, Content = inherited, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(quiet, loud, readOnly);

        Assert.False(quiet.Parts.Error.Visible);
        Assert.False(quietSwitch.Element.Invalid);
        Assert.False(own.Element.Invalid); // 控件上写了 false
        Assert.False(own.Element.ReadOnly);
        Assert.True(inherited.Element.ReadOnly); // 没写：取字段的
        Assert.True(inherited.AccessibilityObject.State.HasFlag(AccessibleStates.ReadOnly));
    });

    [Fact]
    [Upstream("Field", "Field 的 error、说明、标签写成 cond")]
    public Task Empty_label_description_and_error_count_as_not_written() => WinFormsUi.Run(() =>
    {
        var checkbox = new CheckboxControl { Text = "记住我" };
        var field = new FieldControl { Label = "", Description = "", Error = "", Content = checkbox };
        using var form = WinFormsUi.Show(field);

        Assert.False(field.Parts.Label.Visible);
        Assert.False(field.Parts.Description.Visible);
        Assert.False(field.Parts.Error.Visible);
        Assert.False(checkbox.Element.Invalid);
        Assert.Null(checkbox.AccessibilityObject.Help);
    });

    [Fact]
    public Task Only_the_first_control_takes_the_field_label() => WinFormsUi.Run(() =>
    {
        var (first, second) = (new CheckboxControl(), new CheckboxControl { Text = "委派" });
        var row = new FlexControl();
        row.Controls.AddRange([first, second]);
        var field = new FieldControl { Label = "允许的操作", Content = row };
        using var form = WinFormsUi.Show(field);

        Assert.Equal("允许的操作", first.AccessibilityObject.Name);
        Assert.Equal("委派", second.AccessibilityObject.Name);
        Assert.Same(first, field.Parts.Label.Target);
    });

    [Fact]
    public Task Start_placement_puts_the_label_in_a_column_on_the_controls_baseline() => WinFormsUi.Run(() =>
    {
        var toggle = new SwitchControl { Text = "开工前三天通知店长" };
        var field = new FieldControl { Label = "通知门店", LabelPlacement = FieldLabelPlacement.Start, Description = "可多选", Content = toggle };
        using var form = WinFormsUi.Show(field);
        var label = field.Parts.Label;
        var em = field.Font.Size * field.DeviceDpi / 72f;

        Assert.Equal(8 * em + Tokens.Space.Md * em, Offset(toggle, field).X, 1f); // 标签列 8em + 列距 md
        Assert.Equal(8 * em, Offset(label, field).X + label.Width, 1f); // 标签贴着控件（end）
        Assert.InRange(FieldControl.FirstBaseline(label)!.Value + label.Top - (FieldControl.FirstBaseline(toggle)!.Value + toggle.Top), -1, 1); // 第一行基线对齐
        Assert.True(field.Parts.Description.Left >= toggle.Left); // 说明在控件列
    });

    [Fact]
    public Task Hidden_label_names_the_control_without_taking_space() => WinFormsUi.Run(() =>
    {
        var group = new RadioGroupControl { ItemsSource = new[] { "日", "周" } };
        var field = new FieldControl { Label = "周期", IsLabelHidden = true, LabelPlacement = FieldLabelPlacement.Start, Content = group };
        using var form = WinFormsUi.Show(field);

        Assert.False(field.Parts.Label.Visible);
        Assert.Equal(0, group.Left); // 不占标签列
        Assert.Equal("周期", group.AccessibilityObject.Name);
    });
}
