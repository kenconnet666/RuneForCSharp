using System.ComponentModel.DataAnnotations;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>FormErrorSummary：提交失败后列出显示着的错误（按字段顺序，带字段名），焦点在汇总上；点链接跳到控件；改对一项就少一项；整份表单的错误在最前。</summary>
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

    [Fact]
    [Upstream("FormErrorSummary", "提交失败后列出显示着的错误（按字段顺序，带字段名），焦点在汇总上；点链接跳到控件；改对一项就少一项")]
    public Task A_failed_submit_lists_the_errors_and_links_jump_to_fields() => HeadlessUi.Run(() =>
    {
        var controller = new FormController<Application>(new Application(), () => Lang.ZhCn);
        var summary = new FormErrorSummary();
        var name = new TextField();
        var form = new Form
        {
            Controller = controller,
            Child = new StackPanel
            {
                Children =
                {
                    summary,
                    new Field { Path = "Name", Label = "姓名", Content = name },
                    new Field { Path = "Email", Label = "邮箱", Content = new TextField() },
                    new Field { Path = "Agree", Label = "同意协议", Content = new Checkbox { Content = "我已阅读" } },
                },
            },
        };
        var window = HeadlessUi.Show(form);

        Assert.False(summary.IsVisible);
        var submit = form.SubmitAsync();
        HeadlessUi.Until(() => submit.IsCompleted && summary.IsFocused);
        Assert.Equal("有 3 项需要修改", summary.TitleText);
        var links = summary.Items.Cast<Link>().ToList();
        Assert.Equal(["姓名：请填写姓名", "邮箱：邮箱格式不对", "同意协议：请同意协议"], links.Select(link => (string)link.Content!));
        links[0].RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(name.IsFocused);

        controller.SetValue("Name", "林小满");
        HeadlessUi.Flush(window);
        Assert.Equal("有 2 项需要修改", summary.TitleText);
    });

    [Fact]
    public Task The_form_error_comes_first_without_a_link() => HeadlessUi.Run(() =>
    {
        var controller = new FormController<Application>(new Application { Name = "林", Email = "a@b.cn", Agree = true }, () => Lang.ZhCn)
        {
            OnSubmit = _ => throw new FormSubmitException(form: "数据已被他人修改"),
        };
        var summary = new FormErrorSummary { Title = count => $"{count} 个问题" };
        var form = new Form { Controller = controller, Child = new StackPanel { Children = { summary, new Field { Path = "Name", Label = "姓名", Content = new TextField() } } } };
        HeadlessUi.Show(form);

        var submit = form.SubmitAsync();
        HeadlessUi.Until(() => submit.IsCompleted && summary.IsVisible);
        Assert.Equal("1 个问题", summary.TitleText);
        Assert.Equal("数据已被他人修改", Assert.IsType<Text>(Assert.Single(summary.Items)).Text);
    });
}
