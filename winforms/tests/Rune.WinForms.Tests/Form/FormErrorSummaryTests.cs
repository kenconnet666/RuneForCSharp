using System.ComponentModel.DataAnnotations;

namespace Rune.WinForms.Tests;

/// <summary>FormErrorSummaryControl：提交失败后列出显示着的错误（按字段顺序，带字段名），焦点在汇总上；点链接跳到控件；改对一项就少一项；整份表单的错误在最前。</summary>
public class FormErrorSummaryTests
{
    public sealed class Application
    {
        [Required(ErrorMessage = "请填写姓名")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "邮箱格式不对"), EmailAddress(ErrorMessage = "邮箱格式不对")]
        public string Email { get; set; } = "";

        [Range(typeof(bool), "true", "true", ErrorMessage = "请同意协议")]
        public bool Agree { get; set; }
    }

    static FormControl Create(IFormController controller, params Control[] children)
    {
        var column = new FlexControl { Direction = FlexDirection.Column, AutoSize = true, Dock = DockStyle.Top };
        column.Controls.AddRange(children);
        var form = new FormControl { Controller = controller, Dock = DockStyle.Fill };
        form.Controls.Add(column);
        return form;
    }

    [Fact]
    [Upstream("FormErrorSummary", "提交失败后列出显示着的错误（按字段顺序，带字段名），焦点在汇总上；点链接跳到控件；改对一项就少一项")]
    public Task A_failed_submit_lists_the_errors_and_links_jump_to_fields() => WinFormsUi.Run(() =>
    {
        var controller = new FormController<Application>(new Application(), () => Lang.ZhCn);
        var summary = new FormErrorSummaryControl();
        var name = new TextFieldControl();
        var form = Create(controller,
            summary,
            new FieldControl { Path = "Name", Label = "姓名", Content = name },
            new FieldControl { Path = "Email", Label = "邮箱", Content = new TextFieldControl() },
            new FieldControl { Path = "Agree", Label = "同意协议", Content = new CheckboxControl { Text = "我已阅读" } });
        using var window = WinFormsUi.Show(form);

        Assert.False(summary.Visible);
        var submit = form.SubmitAsync();
        WinFormsUi.Until(() => submit.IsCompleted && summary.Focused);
        Assert.Equal("有 3 项需要修改", summary.TitleText);
        var links = summary.Items.Cast<LinkControl>().ToList();
        Assert.Equal(["姓名：请填写姓名", "邮箱：邮箱格式不对", "同意协议：请同意协议"], links.Select(link => link.Text));
        typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(links[0], [EventArgs.Empty]);
        Assert.True(name.Input.Focused);

        controller.SetValue("Name", "林小满");
        Assert.Equal("有 2 项需要修改", summary.TitleText);
    });

    [Fact]
    public Task The_form_error_comes_first_without_a_link() => WinFormsUi.Run(() =>
    {
        var controller = new FormController<Application>(new Application { Name = "林", Email = "a@b.cn", Agree = true }, () => Lang.ZhCn)
        {
            OnSubmit = _ => throw new FormSubmitException(form: "数据已被他人修改"),
        };
        var summary = new FormErrorSummaryControl { Title = count => $"{count} 个问题" };
        var form = Create(controller, summary, new FieldControl { Path = "Name", Label = "姓名", Content = new TextFieldControl() });
        using var window = WinFormsUi.Show(form);

        var submit = form.SubmitAsync();
        WinFormsUi.Until(() => submit.IsCompleted && summary.Visible);
        Assert.Equal("1 个问题", summary.TitleText);
        Assert.Equal("数据已被他人修改", Assert.IsType<TextControl>(Assert.Single(summary.Items)).Text);
    });
}
