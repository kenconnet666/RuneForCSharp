using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// DialogControl：关闭的原因（关闭按钮、确认、Esc、点遮罩；代码里改 IsOpen 不报告）；BeforeClose 拿到原因、结果为 false 时不关；
/// 确认的任务进行中关不掉、结果为 false 时保持打开；焦点移进对话框、关上后还回去；遮罩铺满窗体，面板按宽度档位居中。
/// </summary>
public class DialogTests
{
    static void Raise(Control control, string method, object args) =>
        control.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance, [args.GetType()])!.Invoke(control, [args]);

    static void Escape(DialogControl dialog) => Raise(dialog.Surface.Form, "ProcessDialogKey", Keys.Escape);

    static void ClickScrim(DialogControl dialog) =>
        typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(dialog.Scrim, [new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)]);

    /// <summary>屏幕上的窗体（遮罩与面板按屏幕定位）。</summary>
    static void WithForm(Action<Form> body, params Control[] controls)
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
            body(form);
        }
        finally
        {
            foreach (var dialog in controls.OfType<DialogControl>()) dialog.IsOpen = false;
        }
    }

    static void Open(DialogControl dialog)
    {
        dialog.IsOpen = true;
        WinFormsUi.Pump();
    }

    static void Until(Func<bool> condition)
    {
        for (var clock = System.Diagnostics.Stopwatch.StartNew(); !condition(); WinFormsUi.Wait(TimeSpan.FromMilliseconds(10)))
            if (clock.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("等了 3 秒条件仍不成立");
    }

    [Fact]
    public Task Draggable_stays_in_the_form_and_recenters_on_open() => WinFormsUi.Run(() =>
    {
        var dialog = new DialogControl { Title = "付款信息", Draggable = true, Content = new TextControl { Text = "核对" } };
        WithForm(form =>
        {
            Open(dialog);
            var area = form.RectangleToScreen(form.ClientRectangle);
            var start = dialog.Surface.Backdrop!.Panel;

            dialog.DragTo(new Point(60, 40));
            Assert.Equal(new Point(start.X + 60, start.Y + 40), dialog.Surface.Backdrop.Panel.Location);
            dialog.DragTo(new Point(5000, 5000));
            Assert.Equal(area.Right, dialog.Surface.Backdrop.Panel.Right); // 不超出宿主窗体
            Assert.Equal(area.Bottom, dialog.Surface.Backdrop.Panel.Bottom);

            dialog.IsOpen = false;
            WinFormsUi.Pump();
            Open(dialog);
            Assert.Equal(Point.Empty, dialog.Offset); // 每次打开回到正中
            Assert.Equal(start, dialog.Surface.Backdrop.Panel);
        }, dialog);
    });

    [Fact]
    [Upstream("Dialog", "关闭按钮是 close、确认是 confirm、Esc 是 escape")]
    public Task Close_confirm_and_escape_report_why() => WinFormsUi.Run(() =>
    {
        var dialog = new DialogControl { Title = "编辑资料", Confirm = () => Task.FromResult(true) };
        var reasons = new List<(bool, OpenChangeReason)>();
        dialog.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        WithForm(_ =>
        {
            Open(dialog);
            dialog.Buttons.Close.PerformClick();
            Until(() => !dialog.IsOpen);
            Open(dialog);
            dialog.Buttons.Confirm.PerformClick();
            Until(() => !dialog.IsOpen);
            Open(dialog);
            Escape(dialog);
            Until(() => !dialog.IsOpen);
            Open(dialog);
            ClickScrim(dialog);
            Until(() => !dialog.IsOpen);
        }, dialog);
        Assert.Equal([(false, OpenChangeReason.Close), (false, OpenChangeReason.Confirm), (false, OpenChangeReason.Escape), (false, OpenChangeReason.Outside)], reasons);
    });

    [Fact]
    [Upstream("Dialog", "beforeClose 拿到原因")]
    public Task Before_close_gets_the_reason_and_can_refuse() => WinFormsUi.Run(() =>
    {
        var asked = new List<OpenChangeReason>();
        var allow = false;
        var dialog = new DialogControl { Title = "有未保存的改动", BeforeClose = reason => { asked.Add(reason); return Task.FromResult(allow); } };
        WithForm(_ =>
        {
            Open(dialog);
            Escape(dialog);
            WinFormsUi.Pump();
            Assert.True(dialog.IsOpen);
            allow = true;
            dialog.Buttons.Close.PerformClick();
            Until(() => !dialog.IsOpen);
        }, dialog);
        Assert.Equal([OpenChangeReason.Escape, OpenChangeReason.Close], asked);
    });

    [Fact]
    public Task A_pending_confirm_keeps_it_open_and_false_stays_open() => WinFormsUi.Run(() =>
    {
        var gate = new TaskCompletionSource<bool>();
        var dialog = new DialogControl { Title = "保存", Confirm = () => gate.Task };
        WithForm(_ =>
        {
            Open(dialog);
            dialog.Buttons.Confirm.PerformClick();
            Assert.True(dialog.Buttons.Confirm.IsLoading);
            Assert.False(dialog.Buttons.Cancel.Enabled);
            Escape(dialog);
            WinFormsUi.Pump();
            Assert.True(dialog.IsOpen); // 进行中关不掉
            gate.SetResult(false);
            Until(() => !dialog.Buttons.Confirm.IsLoading);
            Assert.True(dialog.IsOpen); // 结果为 false：保持打开
        }, dialog);
    });

    [Fact]
    public Task Focus_moves_in_and_returns() => WinFormsUi.Run(() =>
    {
        var trigger = new ButtonControl { Text = "编辑" };
        var field = new TextFieldControl { Width = 300 };
        var dialog = new DialogControl { Title = "编辑资料", Content = field };
        WithForm(form =>
        {
            trigger.Focus();
            Open(dialog);
            Assert.True(dialog.Buttons.Close.Focused); // 同原生 dialog：标题行里的关闭按钮在正文之前
            dialog.IsOpen = false;
            WinFormsUi.Pump();
            Assert.True(trigger.Focused);

            dialog.Closable = false;
            Open(dialog);
            Assert.True(field.ContainsFocus);
        }, trigger, dialog);
    });

    [Fact]
    public Task The_scrim_covers_the_form_and_the_panel_is_centered_at_its_size() => WinFormsUi.Run(() =>
    {
        var dialog = new DialogControl { Title = "条款", Content = new TextControl { Text = "内容" } };
        WithForm(form =>
        {
            Open(dialog);
            var area = form.RectangleToScreen(form.ClientRectangle);
            Assert.Equal(area, dialog.Scrim.Panel);
            var panel = dialog.Surface.Backdrop!.Panel;
            Assert.Equal((int)MathF.Round(480 * dialog.DeviceDpi / 96f), panel.Width);
            Assert.Equal(area.Left + (area.Width - panel.Width) / 2, panel.Left);
            Assert.Equal(0, dialog.Height); // 自己不参与排版
        }, dialog);
    });
}
