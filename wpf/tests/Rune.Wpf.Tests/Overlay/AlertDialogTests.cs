using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// AlertDialog 与命令式打开：没有关闭按钮、点遮罩不关、焦点先在取消上；ShowAsync 的原因（代码关上是 imperative、确认是 confirm），
/// 不在界面里的对话框借用 owner 处的主题；ConfirmAsync 得到是否确认或填的理由，必填没填时提示、不关；owner 离开界面时跟着关上。
/// </summary>
public class AlertDialogTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static void Invoke(UIElement button)
    {
        ((IInvokeProvider)button.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    [Fact]
    public Task An_alert_dialog_has_no_close_button_ignores_the_scrim_and_focuses_cancel() => WpfUi.Run(() =>
    {
        var dialog = new AlertDialog { Title = "删除项目？", Tone = StatusTone.Danger, ConfirmText = "删除" };
        WpfUi.Show(Host(dialog)).Activate();
        dialog.IsOpen = true;
        WpfUi.Flush();
        WpfUi.Until(() => dialog.Actions.Cancel.IsKeyboardFocused);

        Assert.False(dialog.CloseButton.IsVisible);
        Assert.True(dialog.Actions.Confirm.IsVisible); // 没写 Confirm 也有确认按钮
        dialog.Scrim.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
        WpfUi.Flush();
        Assert.True(dialog.IsOpen); // 点遮罩不关
        Invoke(dialog.Actions.Confirm);
        WpfUi.Until(() => !dialog.IsOpen);
    });

    [Fact]
    // 上游 open-change.test.tsx 的 useDialog：open 渲染对话框，content 拿到句柄；update 改标题；close 关上，closed 得到 imperative（不在 behavior 的组件分组里）
    public Task Showing_from_code_reports_imperative_and_confirm_and_borrows_the_owners_theme() => WpfUi.Run(() =>
    {
        var owner = new Button { Content = "查看" };
        WpfUi.Show(Host(owner));
        var reasons = new List<string>();
        var dialog = new Dialog { Title = "订单详情" };
        dialog.OpenChanged += (_, e) => reasons.Add($"{e.IsOpen}:{e.Reason}");
        var mark = new Button { Content = "标记发货" };
        mark.Click += (_, _) => dialog.Title = "已发货";
        dialog.Content = mark;

        var closed = dialog.ShowAsync(owner);
        WpfUi.Flush();
        Assert.True(dialog.IsOpen);
        Assert.NotNull(dialog.Panel.Background); // 主题在 owner 外面那层的资源里：借过来了
        Invoke(mark);
        Assert.Equal("已发货", dialog.Header.Children.OfType<H2>().First().Text);
        dialog.Close();
        WpfUi.Until(() => closed.IsCompleted);
        Assert.Equal(OpenChangeReason.Imperative, closed.Result);
        Assert.Equal(["True:Imperative", "False:Imperative"], reasons);
        Assert.Empty(dialog.Resources.MergedDictionaries); // 关上后还回去

        var submit = new Dialog { Title = "提交审批？", Confirm = () => Task.FromResult(true) };
        var result = submit.ShowAsync(owner);
        WpfUi.Flush();
        Invoke(submit.Actions.Confirm);
        WpfUi.Until(() => result.IsCompleted);
        Assert.Equal(OpenChangeReason.Confirm, result.Result);
    });

    [Fact]
    // 上游 confirm.test.tsx：必填没填：点确认提示必填、不关闭；填了确认得到理由，onConfirm 也拿到它
    public Task A_required_reason_blocks_confirm_until_filled() => WpfUi.Run(() =>
    {
        var owner = new Button { Content = "驳回" };
        WpfUi.Show(Host(owner)).Activate();
        string? seen = null;
        var result = AlertDialog.ConfirmAsync(owner,
            new ConfirmOptions { Title = "驳回这个申请？", Tone = StatusTone.Danger, ConfirmText = "驳回", Confirm = reason => Task.FromResult((seen = reason) is not null) },
            new ConfirmReason { Label = "驳回理由", IsRequired = true });
        WpfUi.Flush();
        var dialog = (AlertDialog)Dialog.LastShown!;
        var field = (Field)dialog.Content!;
        var input = (Textarea)field.Content!;
        WpfUi.Until(() => input.IsKeyboardFocused); // 要填理由时焦点先进输入框

        Invoke(dialog.Actions.Confirm);
        Assert.Equal("必填", field.Error);
        Assert.Null(seen);
        Assert.True(dialog.IsOpen);

        input.Text = "  附件缺少报价单  ";
        Invoke(dialog.Actions.Confirm);
        WpfUi.Until(() => result.IsCompleted);
        Assert.Equal("附件缺少报价单", result.Result);
        Assert.Equal("附件缺少报价单", seen);
    });

    [Fact]
    // 上游 confirm.test.tsx：取消得到 null；没给 reason 时照旧得到 false
    public Task Cancel_gives_null_with_a_reason_and_false_without() => WpfUi.Run(() =>
    {
        var owner = new Button { Content = "作废" };
        WpfUi.Show(Host(owner));
        var withReason = AlertDialog.ConfirmAsync(owner, new ConfirmOptions { Title = "作废？" }, new ConfirmReason { Label = "作废原因" });
        WpfUi.Flush();
        Invoke(Dialog.LastShown!.Actions.Cancel);
        WpfUi.Until(() => withReason.IsCompleted);
        Assert.Null(withReason.Result);

        var plain = AlertDialog.ConfirmAsync(owner, new ConfirmOptions { Title = "清空购物车？" });
        WpfUi.Flush();
        Assert.Null(Dialog.LastShown!.Content);
        Invoke(Dialog.LastShown!.Actions.Cancel);
        WpfUi.Until(() => plain.IsCompleted);
        Assert.False(plain.Result);
    });

    [Fact]
    // 上游 confirm.test.tsx：调用的组件卸载：开着的确认框跟着关上，等着结果的 confirm() 也拿到结果
    public Task Removing_the_owner_closes_the_confirmation() => WpfUi.Run(() =>
    {
        var owner = new Button { Content = "删除" };
        var host = Host(owner);
        WpfUi.Show(host);
        var result = AlertDialog.ConfirmAsync(owner, new ConfirmOptions { Title = "删除这条记录？" });
        WpfUi.Flush();
        var dialog = Dialog.LastShown!;
        Assert.True(dialog.IsOpen);

        host.Children.Remove(owner);
        WpfUi.Until(() => result.IsCompleted);
        Assert.False(result.Result);
        Assert.False(dialog.IsOpen);
    });
}
