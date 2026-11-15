using System.ComponentModel.DataAnnotations;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>FormDialog：确认按钮就是提交；没通过校验时不提交、留着；提交成功后关闭；后端拒绝时说明显示在顶上、留着。</summary>
public class FormDialogTests
{
    public sealed class Role
    {
        [Required(ErrorMessage = "请填名称")]
        public string Name { get; set; } = "";
    }

    static (FormDialog Dialog, TextField Name, Window Window) Create(Func<Role, Task> submit)
    {
        var name = new TextField();
        var dialog = new FormDialog
        {
            Title = "新建",
            ConfirmText = "保存",
            Controller = new FormController<Role>(new Role(), () => Lang.ZhCn) { OnSubmit = submit },
            Content = new Field { Path = "Name", Label = "名称", Content = name },
        };
        var window = HeadlessUi.Show(dialog);
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        return (dialog, name, window);
    }

    static void Confirm(FormDialog dialog) => ((IInvokeProvider)dialog.Actions.Confirm.Peer()).Invoke();

    [Fact]
    [Upstream("FormDialog", "没通过校验：不提交，对话框留着，错误汇总出现")]
    public Task Invalid_values_are_not_submitted_and_the_dialog_stays() => HeadlessUi.Run(() =>
    {
        var calls = 0;
        var (dialog, _, window) = Create(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Confirm(dialog);
        HeadlessUi.Until(() => dialog.Summary.IsVisible);
        HeadlessUi.Flush(window);
        Assert.Equal((0, true), (calls, dialog.IsOpen));
        Assert.Contains("名称：请填名称", dialog.Summary.Items.Cast<Link>().Select(link => (string)link.Content!));
    });

    [Fact]
    [Upstream("FormDialog", "提交成功：拿到字段的值，对话框关闭")]
    public Task A_successful_submit_gets_the_values_and_closes() => HeadlessUi.Run(() =>
    {
        Role? submitted = null;
        var (dialog, name, _) = Create(values =>
        {
            submitted = values;
            return Task.CompletedTask;
        });

        name.Value = "联调";
        Confirm(dialog);
        HeadlessUi.Until(() => !dialog.IsOpen);
        Assert.Equal("联调", submitted?.Name);
    });

    [Fact]
    [Upstream("FormDialog", "后端拒绝（带业务错误码）：说明显示在表单顶上，对话框留着")]
    public Task A_rejected_submit_shows_the_reason_on_top_and_stays() => HeadlessUi.Run(() =>
    {
        var (dialog, name, _) = Create(_ => throw new FormSubmitException(form: "角色名称已存在: 联调"));

        name.Value = "联调";
        Confirm(dialog);
        HeadlessUi.Until(() => dialog.FormErrorAlert.IsVisible);
        Assert.True(dialog.IsOpen);
        Assert.Equal("角色名称已存在: 联调", dialog.FormErrorAlert.Description);
    });
}
