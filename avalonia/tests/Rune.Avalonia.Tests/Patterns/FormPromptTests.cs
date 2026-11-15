using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>FormDialog.PromptAsync：标题与各字段的标签；必填没填、Validate 返回说明时报错、不提交；值去掉首尾空白；多选的初始值原样；OnSubmit 失败时留着；取消得到 null。</summary>
public class FormPromptTests
{
    static (Task<IReadOnlyDictionary<string, object>?> Result, Dialog Dialog, List<Field> Fields) Open(FormPromptOptions options)
    {
        var owner = new Button { Content = "退回" };
        Provider.SetLang(owner, Lang.ZhCn);
        var window = HeadlessUi.Show(owner);
        var result = FormDialog.PromptAsync(owner, options);
        HeadlessUi.Flush(window);
        var dialog = Dialog.LastShown!;
        var fields = ((Flex)dialog.Content!).Children.OfType<Field>().ToList();
        return (result, dialog, fields);
    }

    static void Confirm(Dialog dialog) => ((IInvokeProvider)dialog.Actions.Confirm.Peer()).Invoke();

    [Fact]
    [Upstream("form-dialog", "渲染到 body：标题、各字段的标签；必填的没填时报错（不弹浏览器的气泡）、不提交")]
    public Task Required_fields_block_the_submit() => HeadlessUi.Run(() =>
    {
        var submitted = 0;
        var (result, dialog, fields) = Open(new("退回收件箱", [new("Reason", "原因") { Type = FormPromptFieldType.Textarea, Required = true }, new("Note", "备注")])
        {
            OnSubmit = _ =>
            {
                submitted++;
                return Task.CompletedTask;
            },
        });

        Assert.Equal(("退回收件箱", "原因", "备注"), (dialog.Title, fields[0].Label, fields[1].Label));
        Confirm(dialog);
        HeadlessUi.Until(() => fields[0].Error is not null);
        Assert.Equal(("必填", 0, true, false), (fields[0].Error, submitted, dialog.IsOpen, result.IsCompleted));
        dialog.Close();
    });

    [Fact]
    [Upstream("form-dialog", "填好后提交：值去掉首尾空白；validate 返回说明时报错")]
    public Task Values_are_trimmed_and_validate_can_refuse() => HeadlessUi.Run(() =>
    {
        var (result, dialog, fields) = Open(new("改派", [new("To", "改派给") { Validate = (value, _) => value == "自己" ? "不能改派给自己" : null }]));
        var box = (TextField)fields[0].Content!;

        box.Value = "自己";
        Confirm(dialog);
        HeadlessUi.Until(() => fields[0].Error is not null);
        Assert.Equal("不能改派给自己", fields[0].Error);
        box.Value = "  林小满 ";
        Confirm(dialog);
        HeadlessUi.Until(() => result.IsCompleted);
        Assert.Equal("林小满", result.Result!["To"]);
    });

    [Fact]
    [Upstream("form-dialog", "多选的初始值是数组，原样提交")]
    public Task Multiple_selection_defaults_are_submitted_as_is() => HeadlessUi.Run(() =>
    {
        var options = new[] { new ListboxOption("a", "甲"), new ListboxOption("b", "乙") };
        var (result, dialog, _) = Open(new("抄送", [new("Copy", "抄送给") { Type = FormPromptFieldType.Select, Multiple = true, Options = options, DefaultValue = new[] { "b" } }]));

        Confirm(dialog);
        HeadlessUi.Until(() => result.IsCompleted);
        Assert.Equal(["b"], (IReadOnlyList<string>)result.Result!["Copy"]);
    });

    [Fact]
    [Upstream("form-dialog", "多选的下拉（multiple）：必填时一个没选报“必填”、不提交")]
    public Task A_required_multiple_selection_needs_one() => HeadlessUi.Run(() =>
    {
        var (result, dialog, fields) = Open(new("抄送", [new("Copy", "抄送给") { Type = FormPromptFieldType.Select, Multiple = true, Required = true, Options = [new("a", "甲")] }]));

        Confirm(dialog);
        HeadlessUi.Until(() => fields[0].Error is not null);
        Assert.Equal("必填", fields[0].Error);
        Assert.False(result.IsCompleted);
        dialog.Close();
    });

    [Fact]
    [Upstream("form-dialog", "onSubmit 失败时对话框留着（open 不变）")]
    public Task A_failed_submit_keeps_the_dialog_and_cancel_gives_null() => HeadlessUi.Run(() =>
    {
        var (result, dialog, _) = Open(new("退回", [new("Reason", "原因") { DefaultValue = "重复" }]) { OnSubmit = _ => throw new InvalidOperationException("网络断了") });

        Confirm(dialog);
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(50));
        Assert.True(dialog.IsOpen);
        ((IInvokeProvider)dialog.Actions.Cancel.Peer()).Invoke();
        HeadlessUi.Until(() => result.IsCompleted);
        Assert.Null(result.Result);
    });
}
