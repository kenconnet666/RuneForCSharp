using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// Popover：点触发器打开（原因 Trigger），焦点进到第一个能聚焦的元素；Esc 关上（原因 Escape），焦点回到触发器；代码里改 IsOpen 不报告；
/// 浮层以触发器为名称，面板是浮起的表面。
/// </summary>
public class PopoverTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Fact]
    [Upstream("Popover", "Popover：点触发器是 trigger")]
    public Task The_trigger_opens_focus_moves_in_and_escape_returns_it() => WpfUi.Run(() =>
    {
        var trigger = new Button { Content = "分享" };
        var field = new TextField { Text = "链接" };
        var popover = new Popover { Trigger = trigger, Content = new StackPanel { Children = { new Small { Text = "说明" }, field } } };
        var reasons = new List<(bool, OpenChangeReason)>();
        popover.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        var window = WpfUi.Show(Host(popover));
        window.Activate();

        ((IInvokeProvider)trigger.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        WpfUi.Until(() => field.IsKeyboardFocusWithin);
        Assert.True(popover.IsOpen);
        Assert.Equal("分享", popover.Panel.Peer().GetName());

        field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(field), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        WpfUi.Flush();
        Assert.False(popover.IsOpen);
        WpfUi.Until(() => trigger.IsKeyboardFocused);

        popover.IsOpen = true;
        WpfUi.Flush();
        popover.IsOpen = false;
        Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Escape)], reasons);
    });

    [Fact]
    public Task The_panel_is_a_raised_surface() => WpfUi.Run(() =>
    {
        var popover = new Popover { Trigger = new Button { Content = "打开" }, Content = new Small { Text = "内容" } };
        WpfUi.Show(Host(popover));
        popover.IsOpen = true;
        WpfUi.Flush();

        Assert.Equal(popover.Panel.ThemeColor("Rune.Brush.SurfaceRaised"), Lookup.ColorOf(popover.Panel.Background));
        Assert.Equal(popover.Panel.ThemeColor("Rune.Brush.Border"), Lookup.ColorOf(popover.Panel.BorderBrush));
        Assert.Equal(1, popover.Panel.BorderThickness.Top);
        Assert.True(popover.Panel.CornerRadius.TopLeft > 0);
        popover.IsOpen = false;
    });
}
