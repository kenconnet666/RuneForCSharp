using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// Form 与写了 Path 的 Field：错误取自控制器、提交不通过时聚焦第一个出错的控件、失焦上报；必填与约束来自模型的特性；
/// 标签布局与只读从表单继承；唯一的控件自动双向绑定（控件上写了值时不接管）。
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

    static (Form Form, StackPanel Panel, Field Email, TextField EmailBox, Field Nickname, TextField NicknameBox) Create(FormController<Profile> controller)
    {
        var emailBox = new TextField();
        var nicknameBox = new TextField();
        var email = new Field { Path = "Email", Label = "邮箱", Content = emailBox };
        var nickname = new Field { Path = "Nickname", Label = "昵称", Content = nicknameBox };
        var panel = new StackPanel { Children = { nickname, email } };
        var form = new Form { Controller = controller, Child = panel };
        form.Resources.MergedDictionaries.Add(new RuneTheme());
        return (form, panel, email, emailBox, nickname, nicknameBox);
    }

    static void Show(Form form)
    {
        WpfUi.Show(form);
        WpfUi.Flush();
    }

    static bool ErrorShown(Field field) => field.Part<Flex>("PART_Error").Visibility == Visibility.Visible;

    [Fact]
    [Upstream("Field", "Field 按 name 显示表单的错误；提交不通过时焦点移到第一个出错的控件")]
    public Task Fields_show_the_form_errors_and_a_failed_submit_focuses_the_first() => WpfUi.Run(() =>
    {
        var controller = Controller();
        var (form, _, email, emailBox, _, _) = Create(controller);
        Show(form);

        Assert.False(ErrorShown(email));
        var submit = form.SubmitAsync();
        WpfUi.Until(() => submit.IsCompleted && emailBox.IsKeyboardFocusWithin);
        Assert.False(submit.Result);
        Assert.True(ErrorShown(email));
        Assert.True(emailBox.ShowsInvalid);
        Assert.Equal("邮箱", Assert.Single(controller.Fields, entry => entry.Key == "Email").Value.Label);
    });

    [Fact]
    [Upstream("Form", "焦点离开字段时报告失焦，显示这个字段的错误；form.Field 就是 Field（name 带类型）")]
    public Task Leaving_a_field_reports_the_blur() => WpfUi.Run(() =>
    {
        var (form, _, email, emailBox, _, nicknameBox) = Create(Controller());
        Show(form);

        emailBox.Focus();
        nicknameBox.Focus();
        WpfUi.Until(() => ErrorShown(email));
        Assert.True(emailBox.ShowsInvalid);
    });

    [Fact]
    [Upstream("Form", "schema 推出约束：必填标记、输入框的 type 与 maxlength、NumberField 的范围；onSubmit 拿到解析后的值")]
    public Task Constraints_come_from_the_model() => WpfUi.Run(() =>
    {
        var (form, panel, email, _, _, nicknameBox) = Create(Controller());
        var count = new NumberField();
        var limited = new TextField { MaxLength = 4 };
        panel.Children.Add(new Field { Path = "Count", Label = "数量", Content = count });
        panel.Children.Add(new Field { Path = "Nickname", Label = "简称", Content = limited });
        Show(form);

        Assert.True(email.Part<FieldLabel>("PART_Label").IsRequired);
        Assert.Equal(10, nicknameBox.MaxLength);
        Assert.Equal((1m, 9m), (count.Min, count.Max));
        Assert.Equal(4, limited.MaxLength); // 控件上自己写的优先
    });

    [Fact]
    [Upstream("Form", "Form 统一把标签放到起始边：控件、说明、错误包进控件列；Field 上写的优先")]
    public Task The_form_sets_the_label_layout_and_fields_can_override() => WpfUi.Run(() =>
    {
        var (form, _, email, emailBox, nickname, nicknameBox) = Create(Controller());
        form.LabelPlacement = FieldLabelPlacement.Start;
        form.LabelWidth = 120;
        nickname.LabelPlacement = FieldLabelPlacement.Top;
        Show(form);

        Assert.True(emailBox.TranslatePoint(default, email).X >= 120); // 标签一列，控件在右边
        Assert.Equal(0, nicknameBox.TranslatePoint(default, nickname).X);
    });

    [Fact]
    [Upstream("Form", "Form 只读、不可用下发到字段与不在 Field 里的控件；控件上显式写的优先")]
    public Task Read_only_and_disabled_reach_fields_and_loose_controls() => WpfUi.Run(() =>
    {
        var (form, panel, _, emailBox, _, nicknameBox) = Create(Controller());
        var loose = new TextField();
        var editable = new TextField { IsReadOnly = false };
        panel.Children.Add(loose);
        panel.Children.Add(editable);
        Show(form);

        form.IsReadOnly = true;
        WpfUi.Flush();
        Assert.All(new[] { emailBox, nicknameBox, loose }, box => Assert.True(box.ShowsReadOnly));
        Assert.False(editable.ShowsReadOnly);
        form.IsEnabled = false;
        Assert.False(emailBox.IsEnabled);
    });

    [Fact]
    [Upstream("form-binding", "只有一个控件、没写值时绑定 form.values：输入写回；Switch 写的是 checked")]
    public Task The_sole_control_binds_to_the_field() => WpfUi.Run(() =>
    {
        var controller = Controller(new Profile { Nickname = "阿甲" });
        var (form, panel, _, emailBox, _, nicknameBox) = Create(controller);
        var agree = new Switch();
        var count = new NumberField();
        panel.Children.Add(new Field { Path = "Agree", Label = "同意", Content = agree });
        panel.Children.Add(new Field { Path = "Count", Label = "数量", Content = count });
        Show(form);

        Assert.Equal("阿甲", nicknameBox.Value);
        emailBox.Text = "a@b.cn";
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
    public Task A_control_with_its_own_value_is_left_alone() => WpfUi.Run(() =>
    {
        var controller = Controller(new Profile { Nickname = "阿甲" });
        var own = new TextField { Value = "自己的" };
        var form = new Form { Controller = controller, Child = new Field { Path = "Nickname", Label = "昵称", Content = own } };
        form.Resources.MergedDictionaries.Add(new RuneTheme());
        Show(form);

        Assert.Equal("自己的", own.Value);
        own.Value = "改了";
        Assert.Equal("阿甲", controller.Values.Nickname);
    });

    [Fact]
    public Task Enter_in_a_single_line_box_submits_and_reset_restores() => WpfUi.Run(() =>
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
        Show(form);

        emailBox.Focus();
        emailBox.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(emailBox)!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Until(() => submitted == 1);
        controller.SetValue("Email", "b@b.cn");
        form.ResetCommand.Execute(null);
        Assert.Equal("a@b.cn", emailBox.Value);
    });
}
