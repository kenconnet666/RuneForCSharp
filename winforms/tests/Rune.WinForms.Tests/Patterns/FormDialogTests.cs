using System.ComponentModel.DataAnnotations;

namespace Rune.WinForms.Tests;

/// <summary>
/// FormDialogControl：确认按钮就是提交；没通过校验时不提交、留着；提交成功后关闭；后端拒绝时说明显示在顶上、留着。
/// PromptAsync：必填没填、Validate 返回说明时报错、不提交；值去掉首尾空白；多选的初始值原样；OnSubmit 失败时留着；取消得到 null。
/// </summary>
public class FormDialogTests
{
    public sealed class Role
    {
        [Required(ErrorMessage = "请填名称")]
        public string Name { get; set; } = "";
    }

    static (FormDialogControl Dialog, TextFieldControl Name, Form Window) Create(Func<Role, Task> submit)
    {
        var name = new TextFieldControl();
        var dialog = new FormDialogControl
        {
            Title = "新建",
            ConfirmText = "保存",
            Controller = new FormController<Role>(new Role(), () => Lang.ZhCn) { OnSubmit = submit },
            Content = new FieldControl { Path = "Name", Label = "名称", Content = name },
        };
        var window = WinFormsUi.Show(dialog);
        dialog.IsOpen = true;
        WinFormsUi.Pump();
        return (dialog, name, window);
    }

    [Fact]
    [Upstream("FormDialog", "没通过校验：不提交，对话框留着，错误汇总出现")]
    public Task Invalid_values_are_not_submitted_and_the_dialog_stays() => WinFormsUi.Run(() =>
    {
        var calls = 0;
        var (dialog, _, window) = Create(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });
        using var _ = window;

        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Until(() => dialog.Summary.Visible);
        Assert.Equal((0, true), (calls, dialog.IsOpen));
        Assert.Contains("名称：请填名称", dialog.Summary.Items.Cast<LinkControl>().Select(link => link.Text));
        dialog.IsOpen = false;
    });

    [Fact]
    [Upstream("FormDialog", "提交成功：拿到字段的值，对话框关闭")]
    public Task A_successful_submit_gets_the_values_and_closes() => WinFormsUi.Run(() =>
    {
        Role? submitted = null;
        var (dialog, name, window) = Create(values =>
        {
            submitted = values;
            return Task.CompletedTask;
        });
        using var _ = window;

        name.Value = "联调";
        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Until(() => !dialog.IsOpen);
        Assert.Equal("联调", submitted?.Name);
    });

    [Fact]
    [Upstream("FormDialog", "后端拒绝（带业务错误码）：说明显示在表单顶上，对话框留着")]
    public Task A_rejected_submit_shows_the_reason_on_top_and_stays() => WinFormsUi.Run(() =>
    {
        var (dialog, name, window) = Create(_ => throw new FormSubmitException(form: "角色名称已存在: 联调"));
        using var _ = window;

        name.Value = "联调";
        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Until(() => dialog.FormErrorAlert.Visible);
        Assert.True(dialog.IsOpen);
        Assert.Equal("角色名称已存在: 联调", dialog.FormErrorAlert.Description);
        dialog.IsOpen = false;
    });

    static (Task<IReadOnlyDictionary<string, object>?> Result, DialogControl Dialog, List<FieldControl> Fields, Form Window) Prompt(FormPromptOptions options)
    {
        var owner = new ButtonControl { Text = "退回" };
        var window = WinFormsUi.Show(owner);
        var result = FormDialogControl.PromptAsync(owner, options);
        WinFormsUi.Pump();
        var dialog = DialogControl.LastShown!;
        var fields = dialog.Content!.Controls.OfType<FieldControl>().ToList();
        return (result, dialog, fields, window);
    }

    [Fact]
    [Upstream("form-dialog", "渲染到 body：标题、各字段的标签；必填的没填时报错（不弹浏览器的气泡）、不提交")]
    public Task Required_fields_block_the_submit() => WinFormsUi.Run(() =>
    {
        var submitted = 0;
        var (result, dialog, fields, window) = Prompt(new("退回收件箱", [new("Reason", "原因") { Type = FormPromptFieldType.Textarea, Required = true }, new("Note", "备注")])
        {
            OnSubmit = _ =>
            {
                submitted++;
                return Task.CompletedTask;
            },
        });
        using var _ = window;

        Assert.Equal(("退回收件箱", "原因", "备注"), (dialog.Title, fields[0].Label, fields[1].Label));
        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Until(() => fields[0].Error is not null);
        Assert.Equal((0, true, false), (submitted, dialog.IsOpen, result.IsCompleted));
        Assert.NotEmpty(fields[0].Error!);
        dialog.Close();
    });

    [Fact]
    [Upstream("form-dialog", "填好后提交：值去掉首尾空白；validate 返回说明时报错")]
    public Task Values_are_trimmed_and_validate_can_refuse() => WinFormsUi.Run(() =>
    {
        var (result, dialog, fields, window) = Prompt(new("改派", [new("To", "改派给") { Validate = (value, _) => value == "自己" ? "不能改派给自己" : null }]));
        using var _ = window;
        var box = (TextFieldControl)fields[0].Content!;

        box.Value = "自己";
        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Until(() => fields[0].Error is not null);
        Assert.Equal("不能改派给自己", fields[0].Error);
        box.Value = "  林小满 ";
        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Until(() => result.IsCompleted);
        Assert.Equal("林小满", result.Result!["To"]);
    });

    [Fact]
    [Upstream("form-dialog", "date 字段：日期选择（分段输入），初始值原样提交")]
    public Task Date_fields_submit_iso_dates() => WinFormsUi.Run(() =>
    {
        var (result, dialog, fields, window) = Prompt(new("调整计划", [new("Due", "计划完成") { Type = FormPromptFieldType.Date, DefaultValue = "2026-12-31", Min = "2026-11-01" }]));
        using var _ = window;
        var picker = Assert.IsType<DatePickerControl>(fields[0].Content);
        Assert.Equal("2026-11-01", picker.Min);

        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Until(() => result.IsCompleted);
        Assert.Equal("2026-12-31", result.Result!["Due"]);
    });

    [Fact]
    [Upstream("form-dialog", "多选的初始值是数组，原样提交")]
    public Task Multiple_selection_defaults_are_submitted_as_is() => WinFormsUi.Run(() =>
    {
        var options = new[] { new ListboxOption("a", "甲"), new ListboxOption("b", "乙") };
        var (result, dialog, _, window) = Prompt(new("抄送", [new("Copy", "抄送给") { Type = FormPromptFieldType.Select, Multiple = true, Options = options, DefaultValue = new[] { "b" } }]));
        using var _ = window;

        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Until(() => result.IsCompleted);
        Assert.Equal(["b"], (IReadOnlyList<string>)result.Result!["Copy"]);
    });

    [Fact]
    [Upstream("form-dialog", "多选的下拉（multiple）：必填时一个没选报“必填”、不提交")]
    public Task A_required_multiple_selection_needs_one() => WinFormsUi.Run(() =>
    {
        var (result, dialog, fields, window) = Prompt(new("抄送", [new("Copy", "抄送给") { Type = FormPromptFieldType.Select, Multiple = true, Required = true, Options = [new("a", "甲")] }]));
        using var _ = window;

        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Until(() => fields[0].Error is not null);
        Assert.False(result.IsCompleted);
        dialog.Close();
    });

    [Fact]
    [Upstream("form-dialog", "onSubmit 失败时对话框留着（open 不变）")]
    public Task A_failed_submit_keeps_the_dialog_and_cancel_gives_null() => WinFormsUi.Run(() =>
    {
        var (result, dialog, _, window) = Prompt(new("退回", [new("Reason", "原因") { DefaultValue = "重复" }]) { OnSubmit = _ => throw new InvalidOperationException("网络断了") });
        using var _ = window;

        dialog.Buttons.Confirm.PerformClick();
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(50));
        Assert.True(dialog.IsOpen);
        dialog.Buttons.Cancel.PerformClick();
        WinFormsUi.Until(() => result.IsCompleted);
        Assert.Null(result.Result);
    });
}
