namespace Rune.WinForms.Tests;

/// <summary>
/// PopoverControl：点触发器打开（原因 Trigger），焦点进到第一个能聚焦的控件；Esc 关上（原因 Escape），焦点回到触发器；代码里改 IsOpen 不报告；
/// 内容窗口是以触发器为名称的对话框，盖在画好的面板里面（内边距以内），底色是浮起的表面。
/// </summary>
public class PopoverTests
{
    /// <summary>屏幕上的窗体（浮层按屏幕定位、要激活，屏幕外的窗体核对不了）。</summary>
    static void WithPopover(Action<PopoverControl, ButtonControl, TextFieldControl> body)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(300, 300), Size = new Size(400, 300),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        var trigger = new ButtonControl { Text = "分享" };
        var field = new TextFieldControl { Text = "链接", Width = 200 };
        var content = new FlexControl { Direction = FlexDirection.Column, AutoSize = true };
        content.Controls.AddRange([new SmallControl { Text = "说明" }, field]);
        using var popover = new PopoverControl { Trigger = trigger, Content = content, Location = new Point(20, 20) };
        form.Controls.Add(popover);
        form.Show();
        form.Activate();
        WinFormsUi.Pump();
        try
        {
            body(popover, trigger, field);
        }
        finally
        {
            popover.IsOpen = false;
        }
    }

    [Fact]
    [Upstream("Popover", "Popover：点触发器是 trigger")]
    public Task The_trigger_opens_focus_moves_in_and_escape_returns_it() => WinFormsUi.Run(() => WithPopover((popover, trigger, field) =>
    {
        var reasons = new List<(bool, OpenChangeReason)>();
        popover.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));

        trigger.PerformClick();
        WinFormsUi.Pump();
        Assert.True(popover.IsOpen);
        Assert.True(popover.Surface.Visible);
        Assert.True(field.ContainsFocus);
        Assert.Equal(AccessibleRole.Dialog, popover.Surface.AccessibleRole);
        Assert.Equal("分享", popover.Surface.AccessibilityObject.Name);

        var escape = Message.Create(field.Input.Handle, 0x100, (IntPtr)Keys.Escape, IntPtr.Zero);
        field.Input.PreProcessMessage(ref escape);
        WinFormsUi.Pump();
        Assert.False(popover.IsOpen);
        Assert.False(popover.Surface.Visible);
        Assert.True(trigger.Focused);

        popover.IsOpen = true;
        WinFormsUi.Pump();
        popover.IsOpen = false;
        Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Escape)], reasons);
    }));

    [Fact]
    public Task The_content_sits_inside_the_padded_raised_panel_below_the_trigger() => WinFormsUi.Run(() => WithPopover((popover, trigger, _) =>
    {
        popover.Arrow = true;
        popover.IsOpen = true;
        WinFormsUi.Pump();

        var context = ElementContext.Of(popover);
        Assert.Equal(context.Theme.Colors.SurfaceRaised, popover.Surface.BackColor);
        var panel = popover.Backdrop!.Panel;
        var inset = (int)MathF.Round(Tokens.Space.Md * context.Em) + Math.Max(1, (int)MathF.Round(context.Scale(Tokens.BorderWidth.Xs)));
        Assert.Equal(Rectangle.Inflate(panel, -inset, -inset), popover.Surface.Bounds);
        // bottom-start：面板在触发器下面、起始边对齐，隔开 offset 加箭头伸出的一段
        var target = trigger.RectangleToScreen(trigger.ClientRectangle);
        Assert.Equal("bottom", popover.Backdrop.ArrowSide);
        Assert.Equal(target.Left, panel.Left);
        Assert.Equal(target.Bottom + (int)MathF.Round(context.Scale(6) + 0.7f * 0.625f * context.Em), panel.Top);
    }));
}
