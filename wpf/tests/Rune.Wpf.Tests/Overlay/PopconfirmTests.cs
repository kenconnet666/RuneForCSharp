using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Popconfirm：浮层以问题为名称、说明为帮助文字，取消在确认之前且打开时聚焦；确认等任务完成再关、不算取消；结果为 false 或失败时保持打开；取消报告 Canceled。</summary>
public class PopconfirmTests
{
    static (Popconfirm Popconfirm, List<string> Canceled) Show(Func<Task<bool>> confirm)
    {
        var canceled = new List<string>();
        var trigger = new Button { Content = "删除" };
        var popconfirm = new Popconfirm { Trigger = trigger, Title = "删除这条记录？", Description = "删除后无法恢复", Tone = StatusTone.Danger, ConfirmText = "删除", Confirm = confirm };
        popconfirm.Canceled += (_, _) => canceled.Add("canceled");
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(popconfirm);
        WpfUi.Show(host).Activate();
        Click(trigger);
        WpfUi.Flush();
        return (popconfirm, canceled);
    }

    static void Click(UIElement button) => ((IInvokeProvider)button.Peer().GetPattern(PatternInterface.Invoke)).Invoke();

    [Fact]
    [Upstream("Popconfirm", "alertdialog 以标题为名称、说明为描述；取消在确认之前且自动聚焦")]
    public Task Named_by_the_title_and_cancel_comes_first_with_focus() => WpfUi.Run(() =>
    {
        var (popconfirm, _) = Show(() => Task.FromResult(true));
        var peer = popconfirm.Panel.Peer();
        Assert.Equal("删除这条记录？", peer.GetName());
        Assert.Equal("删除后无法恢复", peer.GetHelpText());
        var (cancel, confirm) = popconfirm.Actions;
        Assert.Equal(["取消", "删除"], ((Panel)cancel.Parent).Children.Cast<Button>().Select(button => button.Content));
        Assert.Equal(StatusTone.Danger, confirm.Tone);
        WpfUi.Until(() => cancel.IsKeyboardFocused);
    });

    [Fact]
    [Upstream("Popconfirm", "确认返回 Promise：等它完成再关闭，不算取消")]
    public Task Confirm_waits_for_the_task_and_is_not_a_cancel() => WpfUi.Run(() =>
    {
        var finish = new TaskCompletionSource<bool>();
        var (popconfirm, canceled) = Show(() => finish.Task);
        var (cancel, confirm) = popconfirm.Actions;

        Click(confirm);
        WpfUi.Flush();
        Assert.True(popconfirm.IsOpen);
        Assert.False(cancel.IsEnabled);
        Assert.True(confirm.IsLoading);
        finish.SetResult(true);
        WpfUi.Until(() => !popconfirm.IsOpen);
        Assert.Empty(canceled);
    });

    [Fact]
    [Upstream("Popconfirm", "确认返回 false 或失败时保持打开；点取消关闭并报告 onCancel")]
    public Task False_or_failure_stays_open_and_cancel_reports() => WpfUi.Run(() =>
    {
        var (refusing, _) = Show(() => Task.FromResult(false));
        Click(refusing.Actions.Confirm);
        WpfUi.Until(() => refusing.Actions.Cancel.IsEnabled);
        Assert.True(refusing.IsOpen);
        refusing.IsOpen = false;

        var (failing, canceled) = Show(() => Task.FromException<bool>(new InvalidOperationException("网络错误")));
        Click(failing.Actions.Confirm);
        WpfUi.Until(() => failing.Actions.Cancel.IsEnabled);
        Assert.True(failing.IsOpen);

        Click(failing.Actions.Cancel);
        WpfUi.Flush();
        Assert.False(failing.IsOpen);
        Assert.Equal(["canceled"], canceled);
    });
}
