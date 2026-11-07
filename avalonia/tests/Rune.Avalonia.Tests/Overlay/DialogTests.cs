using Avalonia;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Dialog：关闭的原因（关闭按钮、确认、Esc、点遮罩；代码里改 IsOpen 不报告）；BeforeClose 拿到原因、结果为 false 时不关；
/// 确认的任务进行中关不掉、结果为 false 时保持打开；焦点移进对话框、关上后还回去；面板按宽度档位居中、不超出窗口。
/// </summary>
public class DialogTests
{
    static void Invoke(Control button) => ((IInvokeProvider)button.Peer()).Invoke();

    static void Escape(Dialog dialog) =>
        dialog.Panel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = dialog.Panel });

    [Fact]
    [Upstream("Dialog", "关闭按钮是 close、确认是 confirm、Esc 是 escape")]
    public Task Close_confirm_and_escape_report_why() => HeadlessUi.Run(() =>
    {
        var dialog = new Dialog { Title = "编辑资料", Confirm = () => Task.FromResult(true) };
        var reasons = new List<(bool, OpenChangeReason)>();
        dialog.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        var window = HeadlessUi.Show(dialog);

        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        Invoke(dialog.CloseButton);
        HeadlessUi.Until(() => !dialog.IsOpen);
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        Invoke(dialog.Actions.Confirm);
        HeadlessUi.Until(() => !dialog.IsOpen);
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        Escape(dialog);
        HeadlessUi.Until(() => !dialog.IsOpen);
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        dialog.Scrim.RaiseEvent(new PointerPressedEventArgs(dialog.Scrim, new Pointer(1, PointerType.Mouse, true), dialog.Scrim, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        HeadlessUi.Until(() => !dialog.IsOpen);

        Assert.Equal([(false, OpenChangeReason.Close), (false, OpenChangeReason.Confirm), (false, OpenChangeReason.Escape), (false, OpenChangeReason.Outside)], reasons);
    });

    [Fact]
    [Upstream("Dialog", "beforeClose 拿到原因")]
    public Task Before_close_gets_the_reason_and_can_refuse() => HeadlessUi.Run(() =>
    {
        var asked = new List<OpenChangeReason>();
        var allow = false;
        var dialog = new Dialog { Title = "有未保存的改动", BeforeClose = reason => { asked.Add(reason); return Task.FromResult(allow); } };
        var window = HeadlessUi.Show(dialog);

        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        Escape(dialog);
        HeadlessUi.Flush(window);
        Assert.True(dialog.IsOpen);
        allow = true;
        Invoke(dialog.CloseButton);
        HeadlessUi.Until(() => !dialog.IsOpen);
        Assert.Equal([OpenChangeReason.Escape, OpenChangeReason.Close], asked);
    });

    [Fact]
    public Task A_pending_confirm_keeps_it_open_and_false_stays_open() => HeadlessUi.Run(() =>
    {
        var gate = new TaskCompletionSource<bool>();
        var dialog = new Dialog { Title = "保存", Confirm = () => gate.Task };
        var window = HeadlessUi.Show(dialog);
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);

        Invoke(dialog.Actions.Confirm);
        HeadlessUi.Flush(window);
        Assert.True(dialog.Actions.Confirm.IsLoading);
        Assert.False(dialog.Actions.Cancel.IsEnabled);
        Escape(dialog);
        HeadlessUi.Flush(window);
        Assert.True(dialog.IsOpen); // 进行中关不掉
        gate.SetResult(false);
        HeadlessUi.Until(() => !dialog.Actions.Confirm.IsLoading);
        Assert.True(dialog.IsOpen); // 结果为 false：保持打开
    });

    [Fact]
    public Task Focus_moves_in_and_returns() => HeadlessUi.Run(() =>
    {
        var trigger = new Button { Content = "编辑" };
        var field = new TextField();
        var dialog = new Dialog { Title = "编辑资料", Content = field };
        var window = HeadlessUi.Show(new StackPanel { Children = { trigger, dialog } });
        trigger.Focus();

        // 同原生 dialog：按顺序第一个能聚焦的元素，标题行里的关闭按钮在正文之前
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        HeadlessUi.Until(() => dialog.CloseButton.IsFocused);
        dialog.IsOpen = false;
        HeadlessUi.Until(() => trigger.IsFocused);

        dialog.Closable = false;
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        HeadlessUi.Until(() => field.IsFocused || field.IsKeyboardFocusWithin);
        dialog.IsOpen = false;
    });

    [Fact]
    public Task The_panel_is_centered_at_its_size_within_the_window() => HeadlessUi.Run(() =>
    {
        var dialog = new Dialog { Title = "条款", Content = new Text { Text = "内容" } };
        var window = HeadlessUi.Show(dialog);
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        HeadlessUi.Until(() => dialog.Panel.Bounds.Width > 0);

        Assert.Equal(480, dialog.Panel.Bounds.Width); // xs
        Assert.Equal((800 - 480) / 2d, dialog.Panel.Bounds.X);
        Assert.Equal(dialog.Panel.ThemeColor("Rune.Brush.SurfaceRaised"), Lookup.ColorOf(dialog.Panel.Background));
        dialog.Size = DialogSize.Full;
        HeadlessUi.Flush(window);
        var em = global::Avalonia.Controls.Documents.TextElement.GetFontSize(dialog);
        Assert.Equal(800 - 2 * em, dialog.Panel.Bounds.Width, 0.5);
        dialog.IsOpen = false;
    });

    [Fact]
    public Task Draggable_moves_by_the_header_stays_in_the_window_and_recenters_on_open() => HeadlessUi.Run(() =>
    {
        var dialog = new Dialog { Title = "付款信息", Draggable = true, Content = new TextBlock { Text = "核对" } };
        var window = HeadlessUi.Show(dialog);
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        var root = (Visual)TopLevel.GetTopLevel(dialog.Panel)!;
        var pointer = new Pointer(1, PointerType.Mouse, true);
        var pressed = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        void Drag(global::Avalonia.Interactivity.Interactive source, Point from, Vector by)
        {
            source.RaiseEvent(new PointerPressedEventArgs(source, pointer, root, from, 0, pressed, KeyModifiers.None));
            dialog.Header.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, dialog.Header, pointer, root, from + by, 1, pressed, KeyModifiers.None));
            dialog.Header.RaiseEvent(new PointerReleasedEventArgs(dialog.Header, pointer, root, from + by, 2,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
            HeadlessUi.Flush(window);
        }
        var start = dialog.Panel.Bounds;
        var grip = dialog.Header.TranslatePoint(new Point(4, 4), root)!.Value;

        Drag(dialog.Header, grip, new Vector(60, 40));
        Assert.Equal(new Vector(60, 40), dialog.Offset); // 按住标题行拖
        Assert.Equal(start.Position + new Vector(60, 40), dialog.Panel.Bounds.Position);
        Drag(dialog.CloseButton, dialog.CloseButton.TranslatePoint(new Point(4, 4), root)!.Value, new Vector(30, 30));
        Assert.Equal(new Vector(60, 40), dialog.Offset); // 从关闭按钮上按下不拖
        Drag(dialog.Header, dialog.Header.TranslatePoint(new Point(4, 4), root)!.Value, new Vector(5000, 5000));
        var layer = (Control)dialog.Panel.Parent!;
        Assert.Equal(layer.Bounds.Width, dialog.Panel.Bounds.Right, 1); // 不超出窗口
        Assert.Equal(layer.Bounds.Height, dialog.Panel.Bounds.Bottom, 1);

        dialog.IsOpen = false;
        HeadlessUi.Flush(window);
        dialog.IsOpen = true;
        HeadlessUi.Flush(window);
        Assert.Equal(default, dialog.Offset); // 每次打开回到正中
        Assert.Equal(start, dialog.Panel.Bounds);
        dialog.IsOpen = false;
    });
}
