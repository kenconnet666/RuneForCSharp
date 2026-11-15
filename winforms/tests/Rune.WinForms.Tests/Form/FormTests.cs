using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// FormControl 与写了 Path 的 FieldControl：错误取自控制器、提交不通过时聚焦第一个出错的控件、失焦上报；必填与约束来自模型的特性；
/// 标签布局与只读用表单的（字段上写了的优先）；唯一的控件自动双向绑定（控件上写了值时不接管）。
/// </summary>
public class FormTests
{
    public sealed class Profile
    {
        [Required]
        public string Email { get; set; } = "";

        [StringLength(10)]
        public string Nickname { get; set; } = "";

        [Range(1, 9)]
        public int? Count { get; set; }

        public bool Agree { get; set; }
    }

    static FormController<Profile> Controller(Profile? initial = null) => new(initial ?? new Profile(), () => Lang.ZhCn);

    static (FormControl Form, FlexControl Panel, FieldControl Email, TextFieldControl EmailBox, FieldControl Nickname, TextFieldControl NicknameBox) Create(FormController<Profile> controller)
    {
        var emailBox = new TextFieldControl();
        var nicknameBox = new TextFieldControl();
        var nickname = new FieldControl { Path = "Nickname", Label = "昵称", Content = nicknameBox };
        var email = new FieldControl { Path = "Email", Label = "邮箱", Content = emailBox };
        var panel = new FlexControl { Direction = FlexDirection.Column, AutoSize = true };
        panel.Controls.AddRange([nickname, email]);
        var form = new FormControl { Controller = controller, Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        return (form, panel, email, emailBox, nickname, nicknameBox);
    }

    [Fact]
    [Upstream("Field", "Field 按 name 显示表单的错误；提交不通过时焦点移到第一个出错的控件")]
    public Task Fields_show_the_form_errors_and_a_failed_submit_focuses_the_first() => WinFormsUi.Run(() =>
    {
        var controller = Controller();
        var (form, _, email, emailBox, _, _) = Create(controller);
        using var window = WinFormsUi.Show(form);

        Assert.False(email.Parts.Error.Visible);
        var submit = form.SubmitAsync();
        WinFormsUi.Until(() => submit.IsCompleted);
        Assert.False(submit.Result);
        Assert.True(email.Parts.Error.Visible);
        Assert.True(emailBox.Invalid);
        Assert.True(emailBox.Input.Focused);
        Assert.Equal("邮箱", Assert.Single(controller.Fields, entry => entry.Key == "Email").Value.Label);
    });

    [Fact]
    [Upstream("Form", "焦点离开字段时报告失焦，显示这个字段的错误；form.Field 就是 Field（name 带类型）")]
    public Task Leaving_a_field_reports_the_blur() => WinFormsUi.Run(() =>
    {
        var (form, _, email, emailBox, _, nicknameBox) = Create(Controller());
        using var window = WinFormsUi.Show(form);

        emailBox.Input.Focus();
        nicknameBox.Input.Focus();
        WinFormsUi.Until(() => email.Parts.Error.Visible);
        Assert.True(emailBox.Invalid);
    });

    [Fact]
    [Upstream("Form", "schema 推出约束：必填标记、输入框的 type 与 maxlength、NumberField 的范围；onSubmit 拿到解析后的值")]
    public Task Constraints_come_from_the_model() => WinFormsUi.Run(() =>
    {
        var (form, panel, email, _, _, nicknameBox) = Create(Controller());
        var count = new NumberFieldControl();
        var limited = new TextFieldControl { MaxLength = 4 };
        panel.Controls.AddRange([
            new FieldControl { Path = "Count", Label = "数量", Content = count },
            new FieldControl { Path = "Nickname", Label = "简称", Content = limited },
        ]);
        using var window = WinFormsUi.Show(form);

        Assert.True(email.Parts.Label.IsRequired);
        Assert.Equal(10, nicknameBox.MaxLength);
        Assert.Equal((1m, 9m), (count.Min, count.Max));
        Assert.Equal(4, limited.MaxLength); // 控件上自己写的优先
    });

    [Fact]
    [Upstream("Form", "Form 统一把标签放到起始边：控件、说明、错误包进控件列；Field 上写的优先")]
    public Task The_form_sets_the_label_layout_and_fields_can_override() => WinFormsUi.Run(() =>
    {
        var (form, _, _, emailBox, nickname, nicknameBox) = Create(Controller());
        form.LabelPlacement = FieldLabelPlacement.Start;
        form.LabelWidth = 120;
        nickname.LabelPlacement = FieldLabelPlacement.Top;
        using var window = WinFormsUi.Show(form);

        Assert.True(emailBox.Left >= form.LogicalToDeviceUnits(120)); // 标签一列，控件在右边
        Assert.Equal(0, nicknameBox.Left);
    });

    [Fact]
    [Upstream("Form", "Form 只读、不可用下发到字段与不在 Field 里的控件；控件上显式写的优先")]
    public Task Read_only_and_disabled_reach_fields_and_loose_controls() => WinFormsUi.Run(() =>
    {
        var (form, panel, _, emailBox, _, nicknameBox) = Create(Controller());
        var loose = new TextFieldControl();
        var editable = new TextFieldControl { IsReadOnly = false };
        panel.Controls.AddRange([loose, editable]);
        using var window = WinFormsUi.Show(form);

        form.IsReadOnly = true;
        Assert.All(new[] { emailBox, nicknameBox, loose }, box => Assert.True(box.Input.ReadOnly));
        Assert.False(editable.Input.ReadOnly);
        form.Enabled = false;
        Assert.False(emailBox.Input.CanSelect);
    });

    [Fact]
    [Upstream("form-binding", "只有一个控件、没写值时绑定 form.values：输入写回；Switch 写的是 checked")]
    public Task The_sole_control_binds_to_the_field() => WinFormsUi.Run(() =>
    {
        var controller = Controller(new Profile { Nickname = "阿甲" });
        var (form, panel, _, emailBox, _, nicknameBox) = Create(controller);
        var agree = new SwitchControl();
        var count = new NumberFieldControl();
        panel.Controls.AddRange([
            new FieldControl { Path = "Agree", Label = "同意", Content = agree },
            new FieldControl { Path = "Count", Label = "数量", Content = count },
        ]);
        using var window = WinFormsUi.Show(form);

        Assert.Equal("阿甲", nicknameBox.Value);
        emailBox.Input.Text = "a@b.cn";
        Assert.Equal("a@b.cn", controller.Values.Email);
        agree.IsChecked = true;
        count.Value = 3;
        Assert.Equal((true, 3), (controller.Values.Agree, controller.Values.Count));
        controller.SetValue("Nickname", "阿乙");
        Assert.Equal("阿乙", nicknameBox.Value);
        controller.Reset();
        Assert.Equal(("阿甲", "", false), (nicknameBox.Value, emailBox.Value, agree.IsChecked));
    });

    [Fact]
    [Upstream("form-binding", "控件上自己写了值时不覆盖")]
    public Task A_control_with_its_own_value_is_left_alone() => WinFormsUi.Run(() =>
    {
        var controller = Controller(new Profile { Nickname = "阿甲" });
        var own = new TextFieldControl { Value = "自己的" };
        var form = new FormControl { Controller = controller };
        form.Controls.Add(new FieldControl { Path = "Nickname", Label = "昵称", Content = own });
        using var window = WinFormsUi.Show(form);

        Assert.Equal("自己的", own.Value);
        own.Value = "改了";
        Assert.Equal("阿甲", controller.Values.Nickname);
    });

    [Fact]
    public Task Enter_in_a_single_line_box_submits_and_reset_restores() => WinFormsUi.Run(() =>
    {
        var submitted = 0;
        var controller = new FormController<Profile>(new Profile { Email = "a@b.cn" }, () => Lang.ZhCn)
        {
            OnSubmit = _ =>
            {
                submitted++;
                return Task.CompletedTask;
            },
        };
        var (form, _, _, emailBox, _, _) = Create(controller);
        using var window = WinFormsUi.Show(form);

        var message = Message.Create(emailBox.Input.Handle, 0x0100, (nint)Keys.Enter, 0);
        typeof(Control).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(emailBox.Input, [message, Keys.Enter]);
        WinFormsUi.Until(() => submitted == 1);
        controller.SetValue("Email", "b@b.cn");
        form.ResetCommand.Execute(null);
        Assert.Equal("a@b.cn", emailBox.Value);
    });
}
