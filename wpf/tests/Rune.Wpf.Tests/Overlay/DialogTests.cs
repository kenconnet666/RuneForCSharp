using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// Dialog：关闭的原因（关闭按钮、确认、Esc、点遮罩；代码里改 IsOpen 不报告）；BeforeClose 拿到原因、结果为 false 时不关；
/// 确认的任务进行中关不掉、结果为 false 时保持打开；焦点移进对话框、关上后还回去；面板按宽度档位居中、不超出窗口。
/// </summary>
public class DialogTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static void Invoke(UIElement button) => ((IInvokeProvider)button.Peer().GetPattern(PatternInterface.Invoke)).Invoke();

    static void Escape(Dialog dialog) =>
        dialog.Panel.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog.Panel), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    static void Open(Dialog dialog)
    {
        dialog.IsOpen = true;
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("Dialog", "关闭按钮是 close、确认是 confirm、Esc 是 escape")]
    public Task Close_confirm_and_escape_report_why() => WpfUi.Run(() =>
    {
        var dialog = new Dialog { Title = "编辑资料", Confirm = () => Task.FromResult(true) };
        var reasons = new List<(bool, OpenChangeReason)>();
        dialog.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        WpfUi.Show(Host(dialog));

        Open(dialog);
        Invoke(dialog.CloseButton);
        WpfUi.Until(() => !dialog.IsOpen);
        Open(dialog);
        Invoke(dialog.Actions.Confirm);
        WpfUi.Until(() => !dialog.IsOpen);
        Open(dialog);
        Escape(dialog);
        WpfUi.Until(() => !dialog.IsOpen);
        Open(dialog);
        dialog.Scrim.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
        WpfUi.Until(() => !dialog.IsOpen);

        Assert.Equal([(false, OpenChangeReason.Close), (false, OpenChangeReason.Confirm), (false, OpenChangeReason.Escape), (false, OpenChangeReason.Outside)], reasons);
    });

    [Fact]
    [Upstream("Dialog", "beforeClose 拿到原因")]
    public Task Before_close_gets_the_reason_and_can_refuse() => WpfUi.Run(() =>
    {
        var asked = new List<OpenChangeReason>();
        var allow = false;
        var dialog = new Dialog { Title = "有未保存的改动", BeforeClose = reason => { asked.Add(reason); return Task.FromResult(allow); } };
        WpfUi.Show(Host(dialog));

        Open(dialog);
        Escape(dialog);
        WpfUi.Flush();
        Assert.True(dialog.IsOpen);
        allow = true;
        Invoke(dialog.CloseButton);
        WpfUi.Until(() => !dialog.IsOpen);
        Assert.Equal([OpenChangeReason.Escape, OpenChangeReason.Close], asked);
    });

    [Fact]
    public Task A_pending_confirm_keeps_it_open_and_false_stays_open() => WpfUi.Run(() =>
    {
        var gate = new TaskCompletionSource<bool>();
        var dialog = new Dialog { Title = "保存", Confirm = () => gate.Task };
        WpfUi.Show(Host(dialog));
        Open(dialog);

        Invoke(dialog.Actions.Confirm);
        WpfUi.Flush();
        Assert.True(dialog.Actions.Confirm.IsLoading);
        Assert.False(dialog.Actions.Cancel.IsEnabled);
        Escape(dialog);
        WpfUi.Flush();
        Assert.True(dialog.IsOpen); // 进行中关不掉
        gate.SetResult(false);
        WpfUi.Until(() => !dialog.Actions.Confirm.IsLoading);
        Assert.True(dialog.IsOpen); // 结果为 false：保持打开
        dialog.IsOpen = false;
    });

    [Fact]
    public Task Focus_moves_in_and_returns() => WpfUi.Run(() =>
    {
        var trigger = new Button { Content = "编辑" };
        var field = new TextField();
        var dialog = new Dialog { Title = "编辑资料", Content = field };
        var window = WpfUi.Show(Host(trigger, dialog));
        window.Activate();
        trigger.Focus();
        WpfUi.Until(() => trigger.IsKeyboardFocused);

        // 同原生 dialog：按顺序第一个能聚焦的元素，标题行里的关闭按钮在正文之前
        Open(dialog);
        WpfUi.Until(() => dialog.CloseButton.IsKeyboardFocused);
        dialog.IsOpen = false;
        WpfUi.Until(() => trigger.IsKeyboardFocused);

        dialog.Closable = false;
        Open(dialog);
        WpfUi.Until(() => field.IsKeyboardFocusWithin);
        dialog.IsOpen = false;
    });

    [Fact]
    public Task The_panel_is_centered_at_its_size_within_the_window() => WpfUi.Run(() =>
    {
        var dialog = new Dialog { Title = "条款", Content = new Text { Text = "内容" } };
        var host = Host(dialog);
        WpfUi.Show(host);
        Open(dialog);
        WpfUi.Until(() => dialog.Panel.ActualWidth > 0);

        Assert.Equal(480, dialog.Panel.ActualWidth); // xs
        Assert.Equal((host.ActualWidth - 480) / 2, dialog.Panel.TranslatePoint(new Point(), (UIElement)dialog.Panel.Parent).X, 0.5);
        Assert.Equal(dialog.Panel.ThemeColor("Rune.Brush.SurfaceRaised"), Lookup.ColorOf(dialog.Panel.Background));
        dialog.Size = DialogSize.Full;
        WpfUi.Flush();
        Assert.Equal(host.ActualWidth - 2 * System.Windows.Documents.TextElement.GetFontSize(dialog), dialog.Panel.ActualWidth, 0.5);
        Assert.Equal(0, dialog.ActualHeight); // 自己不参与排版
        dialog.IsOpen = false;
    });
}
