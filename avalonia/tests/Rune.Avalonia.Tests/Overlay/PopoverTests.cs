using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Popover：点触发器打开（原因 Trigger），焦点进到第一个能聚焦的元素；Esc 关上（原因 Escape），焦点回到触发器；代码里改 IsOpen 也报告；
/// 浮层以触发器为名称，面板是浮起的表面。
/// </summary>
public class PopoverTests
{
    [Fact]
    [Upstream("Popover", "Popover：点触发器是 trigger")]
    public Task The_trigger_opens_focus_moves_in_and_escape_returns_it() => HeadlessUi.Run(() =>
    {
        var trigger = new Button { Content = "分享" };
        var field = new TextField { Text = "链接" };
        var popover = new Popover { Trigger = trigger, Content = new StackPanel { Children = { new Small { Text = "说明" }, field } } };
        var reasons = new List<(bool, OpenChangeReason)>();
        popover.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        var window = HeadlessUi.Show(popover);

        ((IInvokeProvider)trigger.Peer()).Invoke();
        HeadlessUi.Flush(window);
        HeadlessUi.Until(() => field.IsFocused || field.IsKeyboardFocusWithin);
        Assert.True(popover.IsOpen);
        Assert.Equal("分享", popover.Panel.Peer().GetName());

        field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = field });
        HeadlessUi.Flush(window);
        Assert.False(popover.IsOpen);
        HeadlessUi.Until(() => trigger.IsFocused);

        popover.IsOpen = true;
        HeadlessUi.Flush(window);
        popover.IsOpen = false;
        Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Escape), (true, OpenChangeReason.Programmatic), (false, OpenChangeReason.Programmatic)], reasons);
    });

    [Fact]
    public Task The_panel_is_a_raised_surface() => HeadlessUi.Run(() =>
    {
        var popover = new Popover { Trigger = new Button { Content = "打开" }, Content = new Small { Text = "内容" }, IsOpen = true };
        var window = HeadlessUi.Show(popover);
        HeadlessUi.Flush(window);

        Assert.Equal(popover.Panel.ThemeColor("Rune.Brush.SurfaceRaised"), Lookup.ColorOf(popover.Panel.Background));
        Assert.Equal(popover.Panel.ThemeColor("Rune.Brush.Border"), Lookup.ColorOf(popover.Panel.BorderBrush));
        Assert.Equal(1, popover.Panel.BorderThickness.Top);
        Assert.True(popover.Panel.CornerRadius.TopLeft > 0);
    });
}
