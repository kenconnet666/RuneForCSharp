namespace Rune.WinForms.Tests;

/// <summary>PopconfirmControl：浮层窗口以问题为名称、说明为描述，取消在确认之前且打开时聚焦；确认等任务完成再关、不算取消；结果为 false 或失败时保持打开；取消报告 Canceled。</summary>
public class PopconfirmTests
{
    /// <summary>屏幕上的窗体（浮层按屏幕定位、要激活）；打开以后交给 <paramref name="body"/>。</summary>
    static void WithPopconfirm(Func<Task<bool>> confirm, Action<PopconfirmControl, List<string>> body)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(300, 400), Size = new Size(400, 300),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        var trigger = new ButtonControl { Text = "删除" };
        using var popconfirm = new PopconfirmControl
        {
            Trigger = trigger, Title = "删除这条记录？", Description = "删除后无法恢复", Tone = StatusTone.Danger, ConfirmText = "删除", Confirm = confirm,
            Location = new Point(40, 160),
        };
        var canceled = new List<string>();
        popconfirm.Canceled += (_, _) => canceled.Add("canceled");
        form.Controls.Add(popconfirm);
        form.Show();
        form.Activate();
        WinFormsUi.Pump();
        trigger.PerformClick();
        WinFormsUi.Pump();
        try
        {
            body(popconfirm, canceled);
        }
        finally
        {
            popconfirm.IsOpen = false;
        }
    }

    [Fact]
    [Upstream("Popconfirm", "alertdialog 以标题为名称、说明为描述；取消在确认之前且自动聚焦")]
    public Task Named_by_the_title_and_cancel_comes_first_with_focus() => WinFormsUi.Run(() => WithPopconfirm(() => Task.FromResult(true), (popconfirm, _) =>
    {
        Assert.True(popconfirm.Surface.Visible);
        Assert.Equal("删除这条记录？", popconfirm.Surface.AccessibilityObject.Name);
        Assert.Equal("删除后无法恢复", popconfirm.Surface.AccessibilityObject.Description);
        var (cancel, confirm) = popconfirm.Actions;
        Assert.Equal(["取消", "删除"], cancel.Parent!.Controls.Cast<Control>().Select(control => control.Text));
        Assert.Equal(StatusTone.Danger, confirm.Tone);
        Assert.True(cancel.Focused);
    }));

    [Fact]
    [Upstream("Popconfirm", "确认返回 Promise：等它完成再关闭，不算取消")]
    public Task Confirm_waits_for_the_task_and_is_not_a_cancel() => WinFormsUi.Run(() =>
    {
        var finish = new TaskCompletionSource<bool>();
        WithPopconfirm(() => finish.Task, (popconfirm, canceled) =>
        {
            var (cancel, confirm) = popconfirm.Actions;
            confirm.PerformClick();
            WinFormsUi.Pump();
            Assert.True(popconfirm.IsOpen);
            Assert.False(cancel.Enabled);
            Assert.True(confirm.IsLoading);
            finish.SetResult(true);
            WinFormsUi.Until(() => !popconfirm.IsOpen);
            Assert.Empty(canceled);
        });
    });

    [Fact]
    [Upstream("Popconfirm", "确认返回 false 或失败时保持打开；点取消关闭并报告 onCancel")]
    public Task False_or_failure_stays_open_and_cancel_reports() => WinFormsUi.Run(() =>
    {
        WithPopconfirm(() => Task.FromResult(false), (refusing, _) =>
        {
            refusing.Actions.Confirm.PerformClick();
            WinFormsUi.Until(() => refusing.Actions.Cancel.Enabled);
            Assert.True(refusing.IsOpen);
        });
        WithPopconfirm(() => Task.FromException<bool>(new InvalidOperationException("网络错误")), (failing, canceled) =>
        {
            failing.Actions.Confirm.PerformClick();
            WinFormsUi.Until(() => failing.Actions.Cancel.Enabled);
            Assert.True(failing.IsOpen);

            failing.Actions.Cancel.PerformClick();
            WinFormsUi.Pump();
            Assert.False(failing.IsOpen);
            Assert.Equal(["canceled"], canceled);
        });
    });
}
