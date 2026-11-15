using System.ComponentModel.DataAnnotations;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>StepsForm："下一步"只校验这一步的字段、通过才走，换步后焦点到内容上；上一步；提交没通过时跳到第一个有错的步骤并标成出错；回车；只读、不可用。</summary>
public class StepsFormTests
{
    public sealed class Account
    {
        [Required(ErrorMessage = "至少 2 个字"), MinLength(2, ErrorMessage = "至少 2 个字")]
        public string Company { get; set; } = "";

        [Required(ErrorMessage = "至少 8 位"), MinLength(8, ErrorMessage = "至少 8 位")]
        public string Password { get; set; } = "";
    }

    static (StepsForm Form, FormController<Account> Controller, List<Account> Submitted) Create(bool readOnly = false, bool disabled = false)
    {
        var submitted = new List<Account>();
        var controller = new FormController<Account>(new Account(), () => Lang.ZhCn)
        {
            OnSubmit = values =>
            {
                submitted.Add(values);
                return Task.CompletedTask;
            },
        };
        var form = new StepsForm
        {
            Controller = controller,
            IsReadOnly = readOnly,
            IsDisabled = disabled,
            Steps =
            [
                new("企业信息", ["Company"], () => new Field { Path = "Company", Label = "企业名称", Content = new TextField() }),
                new("账号", ["Password"], () => new Field { Path = "Password", Label = "密码", Content = new TextField() }),
            ],
        };
        return (form, controller, submitted);
    }

    static Button Part(StepsForm form, string name) => form.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == name);

    static Control Panel(StepsForm form) => form.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == StepsFormParts.Panel);

    static void Click(Button button) => button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    static void Settle(Window window, Func<bool> done)
    {
        HeadlessUi.Until(done);
        HeadlessUi.Flush(window);
    }

    [Fact]
    [Upstream("StepsForm", "下一步只校验这一步：没通过留在原地并显示错误；通过后到下一步、焦点在内容上；上一步回来")]
    public Task Next_validates_only_this_step() => HeadlessUi.Run(() =>
    {
        var (form, controller, _) = Create();
        var window = HeadlessUi.Show(form);

        Click(Part(form, StepsFormParts.Next));
        Settle(window, () => controller.VisibleError("Company") is not null);
        Assert.Equal(0, form.Current);
        Assert.Equal("至少 2 个字", controller.VisibleError("Company"));
        // 第二步的字段不受影响
        Assert.Null(controller.VisibleError("Password"));

        controller.SetValue("Company", "华辰精密");
        Click(Part(form, StepsFormParts.Next));
        Settle(window, () => form.Current == 1 && Panel(form).IsFocused);
        Assert.Equal("第 2 步，共 2 步", AutomationProperties.GetName(Panel(form)));
        Assert.True(Part(form, StepsFormParts.Submit).IsVisible);
        Assert.False(Part(form, StepsFormParts.Next).IsVisible);

        Click(Part(form, StepsFormParts.Previous));
        Settle(window, () => form.Current == 0);
    });

    [Fact]
    [Upstream("StepsForm", "提交没通过：跳到第一个有错的步骤，步骤条上标成出错；都通过才调用 onSubmit")]
    public Task Failed_submit_jumps_to_the_first_step_with_errors() => HeadlessUi.Run(() =>
    {
        var (form, controller, submitted) = Create();
        var window = HeadlessUi.Show(form);
        controller.SetValue("Company", "华辰精密");
        Click(Part(form, StepsFormParts.Next));
        Settle(window, () => form.Current == 1);

        // 第二步时把第一步的值改坏（如服务端退回后），再提交
        controller.SetValue("Company", "华");
        controller.SetValue("Password", "12345678");
        Click(Part(form, StepsFormParts.Submit));
        Settle(window, () => form.Current == 0);
        Assert.Empty(submitted);
        var steps = form.GetLogicalDescendants().OfType<Steps>().Single();
        Assert.Equal(StepState.Error, steps.Items![0].Status);

        controller.SetValue("Company", "华辰精密");
        Click(Part(form, StepsFormParts.Next));
        Settle(window, () => form.Current == 1);
        Assert.Empty(submitted);
        Click(Part(form, StepsFormParts.Submit));
        Settle(window, () => submitted.Count == 1);
        Assert.Equal(("华辰精密", "12345678"), (submitted[0].Company, submitted[0].Password));
    });

    [Fact]
    [Upstream("StepsForm", "不在最后一步时表单的提交是下一步：只校验这一步，没通过时焦点到出错的控件上，不调用 onSubmit")]
    public Task Enter_is_next_until_the_last_step() => HeadlessUi.Run(() =>
    {
        var (form, controller, submitted) = Create();
        var window = HeadlessUi.Show(form);
        var box = form.GetVisualDescendants().OfType<TextField>().Single();
        box.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(window, () => controller.VisibleError("Company") is not null);
        // 只校验了这一步：后面步骤的字段没有报错，也没有提交
        Assert.Null(controller.VisibleError("Password"));
        Assert.Empty(submitted);
        Assert.Equal(0, controller.SubmitCount);
        Assert.Equal(0, form.Current);
    });

    [Fact]
    [Upstream("StepsForm", "只读是回看：不校验就能到下一步，哪一步都能点，最后一步没有提交按钮，回车不提交")]
    public Task Read_only_reviews_without_validating() => HeadlessUi.Run(() =>
    {
        var (form, controller, submitted) = Create(readOnly: true);
        var window = HeadlessUi.Show(form);

        Click(Part(form, StepsFormParts.Next));
        Settle(window, () => form.Current == 1);
        Assert.Null(controller.VisibleError("Company"));
        Assert.False(Part(form, StepsFormParts.Submit).IsVisible);
        Assert.True(Part(form, StepsFormParts.Previous).IsVisible);
        var steps = form.GetLogicalDescendants().OfType<Steps>().Single();
        Assert.All(steps.Items!, item => Assert.False(item.IsDisabled));
        var box = form.GetVisualDescendants().OfType<TextField>().Single();
        box.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        HeadlessUi.Flush(window);
        Assert.Empty(submitted);
        Assert.Contains(":readonly", box.Classes);
    });

    [Fact]
    [Upstream("StepsForm", "不可用：各步照常切换，提交按钮不可用")]
    public Task Disabled_still_switches_steps() => HeadlessUi.Run(() =>
    {
        var (form, _, submitted) = Create(disabled: true);
        var window = HeadlessUi.Show(form);

        Click(Part(form, StepsFormParts.Next));
        Settle(window, () => form.Current == 1);
        Assert.False(Part(form, StepsFormParts.Submit).IsEnabled);
        Assert.False(Panel(form).IsEnabled);
        Assert.Empty(submitted);
    });
}
