using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// TooltipProvider：位置 12 种、放不下时翻到对面、贴着屏幕边挪回来；显示时面板与触发器隔开 offset，箭头朝向触发器；
/// 悬停等 Delay、刚关掉时直接显示；按下、失焦、Esc 隐藏；键盘聚焦立即显示；提示文字是控件的说明。
/// </summary>
public class TooltipTests
{
    static readonly Rectangle Area = new(0, 0, 1000, 800);

    [Fact]
    public void Placements_flip_when_the_side_does_not_fit_and_shift_back_on_screen()
    {
        var target = new Rectangle(400, 400, 100, 30);
        var panel = new Size(80, 24);

        var (side, rect) = TooltipProvider.Position(FloatingPlacement.Top, false, target, panel, 6, Area);
        Assert.Equal(("top", new Rectangle(410, 370, 80, 24)), (side, rect));
        (side, rect) = TooltipProvider.Position(FloatingPlacement.BottomStart, false, target, panel, 6, Area);
        Assert.Equal(("bottom", new Rectangle(400, 436, 80, 24)), (side, rect));
        (side, rect) = TooltipProvider.Position(FloatingPlacement.BottomStart, true, target, panel, 6, Area);
        Assert.Equal(new Rectangle(420, 436, 80, 24), rect); // 从右到左：start 是右边
        (side, rect) = TooltipProvider.Position(FloatingPlacement.RightEnd, false, target, panel, 6, Area);
        Assert.Equal(("right", new Rectangle(506, 406, 80, 24)), (side, rect));

        // 上面放不下：翻到下面
        (side, rect) = TooltipProvider.Position(FloatingPlacement.Top, false, target with { Y = 10 }, panel, 6, Area);
        Assert.Equal(("bottom", 46), (side, rect.Y));
        // 贴着屏幕左边：挪回来，与边缘隔 8
        (_, rect) = TooltipProvider.Position(FloatingPlacement.Top, false, target with { X = 0, Width = 20 }, panel, 6, Area);
        Assert.Equal(8, rect.X);
    }

    /// <summary>屏幕上的窗体（提示按屏幕定位，屏幕外的窗体核对不了位置）。</summary>
    static void WithButton(Action<Form, ButtonControl, TooltipProvider> body)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(300, 300), Size = new Size(400, 300),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        var before = new ButtonControl { Text = "前一个", Location = new Point(10, 10) };
        var button = new ButtonControl { Text = "保存", Location = new Point(150, 130) };
        form.Controls.AddRange([before, button]);
        using var tips = new TooltipProvider();
        tips.SetTooltip(button, "保存草稿");
        form.Show();
        WinFormsUi.Pump();
        try
        {
            body(form, button, tips);
        }
        finally
        {
            tips.Hide();
        }
    }

    /// <summary>按钮画出来的那一块（控件四周留着焦点环的 Padding）。</summary>
    static Rectangle Face(Control button) => button.RectangleToScreen(new Rectangle(
        button.Padding.Left, button.Padding.Top, button.Width - button.Padding.Horizontal, button.Height - button.Padding.Vertical));

    [Fact]
    public Task Showing_puts_the_panel_offset_from_the_trigger_with_the_arrow_facing_it() => WinFormsUi.Run(() => WithButton((_, button, tips) =>
    {
        TooltipEventArgs? opened = null;
        tips.Opened += (_, e) => opened = e;
        tips.Show(button);
        var window = TooltipProvider.Window!;
        Assert.True(window.Visible);
        Assert.Same(button, opened?.Control);
        Assert.Same(button, tips.Current);
        var face = Face(button);
        var gap = button.LogicalToDeviceUnits(6);
        Assert.InRange(face.Top - window.Panel.Bottom, gap - 1, gap + 1);
        Assert.InRange(window.Panel.Left + window.Panel.Width / 2 - (face.Left + face.Width / 2), -1, 1);
        Assert.Null(window.ArrowSide);
        Assert.Equal("保存草稿", button.AccessibleDescription);

        tips.Hide();
        tips.SetTooltipPlacement(button, FloatingPlacement.Bottom);
        tips.SetTooltipArrow(button, true);
        tips.Show(button);
        Assert.Equal("bottom", window.ArrowSide);
        var arrow = 0.625f * TextSize.Sm.Pixels!.Value * button.DeviceDpi / 96f;
        Assert.InRange(window.Panel.Top - face.Bottom, gap + 0.7f * arrow - 1, gap + 0.7f * arrow + 1);
    }));

    [Fact]
    public Task Hover_waits_for_the_delay_and_pressing_hides() => WinFormsUi.Run(() => WithButton((_, button, tips) =>
    {
        tips.Delay = 150;
        Raise(button, "OnMouseEnter", EventArgs.Empty);
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(60));
        Assert.Null(tips.Current);
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(200));
        Assert.Same(button, tips.Current);

        Raise(button, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
        Assert.Null(tips.Current);

        // 刚关掉：再进来直接显示
        Raise(button, "OnMouseEnter", EventArgs.Empty);
        Assert.Same(button, tips.Current);

        tips.Active = false;
        Assert.Null(tips.Current);
        Raise(button, "OnMouseEnter", EventArgs.Empty);
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(200));
        Assert.Null(tips.Current);
    }));

    [Fact]
    public Task Keyboard_focus_shows_it_and_escape_or_blur_hides_it() => WinFormsUi.Run(() => WithButton((form, button, tips) =>
    {
        var filter = new TooltipProvider.InputFilter();
        var tab = Message.Create(button.Handle, 0x100, (IntPtr)Keys.Tab, IntPtr.Zero);
        var escape = Message.Create(button.Handle, 0x100, (IntPtr)Keys.Escape, IntPtr.Zero);
        var click = Message.Create(button.Handle, 0x201, IntPtr.Zero, IntPtr.Zero);

        // 指针点出来的焦点不显示
        filter.PreFilterMessage(ref click);
        form.Controls[0].Focus();
        button.Focus();
        Assert.Null(tips.Current);

        filter.PreFilterMessage(ref tab);
        form.Controls[0].Focus();
        button.Focus();
        Assert.Same(button, tips.Current);

        Assert.True(filter.PreFilterMessage(ref escape)); // 吃掉这次 Esc
        Assert.Null(tips.Current);
        Assert.False(filter.PreFilterMessage(ref escape)); // 没有提示时照常传下去

        form.Controls[0].Focus();
        button.Focus();
        Assert.Same(button, tips.Current);
        form.Controls[0].Focus();
        Assert.Null(tips.Current);
    }));

    static void Raise(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [args]);
}
