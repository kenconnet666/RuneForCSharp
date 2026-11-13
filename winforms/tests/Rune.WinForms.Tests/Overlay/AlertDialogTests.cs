using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// AlertDialogControl 与命令式打开：没有关闭按钮、点遮罩不关、焦点先在取消上；ShowAsync 的原因（代码关上是 imperative、确认是 confirm），
/// 不在界面里的对话框借 owner 的作用域与字体；ConfirmAsync 得到是否确认或填的理由，必填没填时提示、不关；owner 从界面上拿掉时跟着关上。
/// </summary>
public class AlertDialogTests
{
    static void ClickScrim(DialogControl dialog) =>
        typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(dialog.Scrim, [new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)]);

    /// <summary>屏幕上的窗体（遮罩与面板按屏幕定位）。</summary>
    static void WithForm(Action<Form, FlexControl> body, params Control[] controls)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(200, 200), Size = new Size(800, 600),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        var stack = new FlexControl { Direction = FlexDirection.Column, Dock = DockStyle.Fill };
        stack.Controls.AddRange(controls);
        form.Controls.Add(stack);
        form.Show();
        form.Activate();
        WinFormsUi.Pump();
        try
        {
            body(form, stack);
        }
        finally
        {
            if (DialogControl.LastShown is { IsOpen: true } open) open.Close();
            foreach (var dialog in controls.OfType<DialogControl>()) dialog.IsOpen = false;
        }
    }

    static void Until(Func<bool> condition)
    {
        for (var clock = System.Diagnostics.Stopwatch.StartNew(); !condition(); WinFormsUi.Wait(TimeSpan.FromMilliseconds(10)))
            if (clock.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("等了 3 秒条件仍不成立");
    }

    [Fact]
    public Task An_alert_dialog_has_no_close_button_ignores_the_scrim_and_focuses_cancel() => WinFormsUi.Run(() =>
    {
        var dialog = new AlertDialogControl { Title = "删除项目？", Tone = StatusTone.Danger, ConfirmText = "删除" };
        WithForm((_, _) =>
        {
            dialog.IsOpen = true;
            WinFormsUi.Pump();
            Until(() => dialog.Buttons.Cancel.Focused);
            Assert.False(dialog.Buttons.Close.Visible);
            Assert.True(dialog.Buttons.Confirm.Visible); // 没写 Confirm 也有确认按钮
            ClickScrim(dialog);
            WinFormsUi.Pump();
            Assert.True(dialog.IsOpen); // 点遮罩不关
            dialog.Buttons.Confirm.PerformClick();
            Until(() => !dialog.IsOpen);
        }, dialog);
    });

    [Fact]
    // 上游 open-change.test.tsx 的 useDialog：open 渲染对话框，content 拿到句柄；update 改标题；close 关上，closed 得到 imperative（不在 behavior 的组件分组里）
    public Task Showing_from_code_reports_imperative_and_confirm() => WinFormsUi.Run(() =>
    {
        var owner = new ButtonControl { Text = "查看", Font = new Font("Microsoft YaHei UI", 12f) };
        WithForm((_, _) =>
        {
            var reasons = new List<string>();
            var dialog = new DialogControl { Title = "订单详情", Content = new TextControl { Text = "内容" } };
            dialog.OpenChanged += (_, e) => reasons.Add($"{e.IsOpen}:{e.Reason}");

            var closed = dialog.ShowAsync(owner);
            WinFormsUi.Pump();
            Assert.True(dialog.IsOpen);
            Assert.Equal(owner.Font, dialog.Font); // 不在界面里：借 owner 的字体
            dialog.Title = "已发货";
            dialog.Close();
            Until(() => closed.IsCompleted);
            Assert.Equal(OpenChangeReason.Imperative, closed.Result);
            Assert.Equal(["True:Imperative", "False:Imperative"], reasons);

            var submit = new DialogControl { Title = "提交审批？", Confirm = () => Task.FromResult(true) };
            var result = submit.ShowAsync(owner);
            WinFormsUi.Pump();
            submit.Buttons.Confirm.PerformClick();
            Until(() => result.IsCompleted);
            Assert.Equal(OpenChangeReason.Confirm, result.Result);
        }, owner);
    });

    [Fact]
    // 上游 confirm.test.tsx：必填没填：点确认提示必填、不关闭；填了确认得到理由，onConfirm 也拿到它
    public Task A_required_reason_blocks_confirm_until_filled() => WinFormsUi.Run(() =>
    {
        var owner = new ButtonControl { Text = "驳回" };
        WithForm((_, _) =>
        {
            string? seen = null;
            var result = AlertDialogControl.ConfirmAsync(owner,
                new ConfirmOptions { Title = "驳回这个申请？", Tone = StatusTone.Danger, ConfirmText = "驳回", Confirm = reason => Task.FromResult((seen = reason) is not null) },
                new ConfirmReason { Label = "驳回理由", IsRequired = true });
            WinFormsUi.Pump();
            var dialog = (AlertDialogControl)DialogControl.LastShown!;
            var field = (FieldControl)dialog.Content!;
            var input = (TextareaControl)field.Content!;

            dialog.Buttons.Confirm.PerformClick();
            WinFormsUi.Pump();
            Assert.Equal("必填", field.Error);
            Assert.Null(seen);
            Assert.True(dialog.IsOpen);

            input.Text = "  附件缺少报价单  ";
            dialog.Buttons.Confirm.PerformClick();
            Until(() => result.IsCompleted);
            Assert.Equal("附件缺少报价单", result.Result);
            Assert.Equal("附件缺少报价单", seen);
        }, owner);
    });

    [Fact]
    // 上游 confirm.test.tsx：取消得到 null；没给 reason 时照旧得到 false
    public Task Cancel_gives_null_with_a_reason_and_false_without() => WinFormsUi.Run(() =>
    {
        var owner = new ButtonControl { Text = "作废" };
        WithForm((_, _) =>
        {
            var withReason = AlertDialogControl.ConfirmAsync(owner, new ConfirmOptions { Title = "作废？" }, new ConfirmReason { Label = "作废原因" });
            WinFormsUi.Pump();
            DialogControl.LastShown!.Buttons.Cancel.PerformClick();
            Until(() => withReason.IsCompleted);
            Assert.Null(withReason.Result);

            var plain = AlertDialogControl.ConfirmAsync(owner, new ConfirmOptions { Title = "清空购物车？" });
            WinFormsUi.Pump();
            Assert.Null(DialogControl.LastShown!.Content);
            DialogControl.LastShown!.Buttons.Cancel.PerformClick();
            Until(() => plain.IsCompleted);
            Assert.False(plain.Result);
        }, owner);
    });

    [Fact]
    // 上游 confirm.test.tsx：调用的组件卸载：开着的确认框跟着关上，等着结果的 confirm() 也拿到结果
    public Task Removing_the_owner_closes_the_confirmation() => WinFormsUi.Run(() =>
    {
        var owner = new ButtonControl { Text = "删除" };
        WithForm((_, stack) =>
        {
            var result = AlertDialogControl.ConfirmAsync(owner, new ConfirmOptions { Title = "删除这条记录？" });
            WinFormsUi.Pump();
            var dialog = DialogControl.LastShown!;
            Assert.True(dialog.IsOpen);

            stack.Controls.Remove(owner);
            Until(() => result.IsCompleted);
            Assert.False(result.Result);
            Assert.False(dialog.IsOpen);
        }, owner);
    });
}
