using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// Tooltip：平台的 ToolTip 换成 vue-tsx-pro 的外观与默认时机；位置 12 种、与触发器隔开 offset、放不下时翻到对面；
/// 箭头贴着朝向触发器的边；Esc 隐藏。
/// </summary>
public class TooltipTests
{
    /// <summary>
    /// 屏幕上的窗口（弹出层会被挪进屏幕：屏幕外的窗口核对不了位置），中间一个按钮。提示的样式在应用资源里才找得到
    /// （同实际用法：RuneTheme 合并进 Application.Resources），测完拿掉。
    /// </summary>
    static void WithButton(Action<Window, Rune.Wpf.Button, ToolTip> body)
    {
        var theme = new RuneTheme();
        Application.Current.Resources.MergedDictionaries.Add(theme);
        var button = new Rune.Wpf.Button { Content = "保存", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var tip = new ToolTip { Content = "说明" };
        button.ToolTip = tip;
        var window = new Window
        {
            Content = button, Width = 400, Height = 300, Left = 200, Top = 200,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
        };
        try
        {
            window.Show();
            WpfUi.Flush();
            body(window, button, tip);
        }
        finally
        {
            tip.IsOpen = false;
            window.Close();
            Application.Current.Resources.MergedDictionaries.Remove(theme);
        }
    }

    static TooltipChrome Open(Rune.Wpf.Button button, ToolTip tip)
    {
        tip.IsOpen = false;
        tip.PlacementTarget = button;
        tip.IsOpen = true;
        WpfUi.Flush();
        WpfUi.Wait(TimeSpan.FromMilliseconds(50));
        return (TooltipChrome)tip.Template.FindName("PART_Chrome", tip);
    }

    static Rect Screen(FrameworkElement element) =>
        new(Tooltip.Screen(element, new Point()), Tooltip.Screen(element, new Point(element.ActualWidth, element.ActualHeight)));

    [Fact]
    public Task The_platform_tooltip_gets_the_inverse_panel_and_upstream_timing() => WpfUi.Run(() => WithButton((_, button, tip) =>
    {
        Assert.Equal((500, 300), (ToolTipService.GetInitialShowDelay(button), ToolTipService.GetBetweenShowDelay(button)));
        var chrome = Open(button, tip);
        var panel = chrome.PanelPart;
        var em = (double)button.FindResource("Rune.FontSize.Sm");
        Assert.Equal(button.ThemeColor("Rune.Brush.InverseSurface"), Lookup.ColorOf(panel.Background));
        Assert.Equal(button.ThemeColor("Rune.Brush.InverseText"), Lookup.ColorOf(tip.Foreground));
        Assert.Equal(em, tip.FontSize);
        Assert.False(tip.HasDropShadow);
        Assert.Equal(new Thickness(0.5 * em, 0.25 * em, 0.5 * em, 0.25 * em), panel.Padding);
        Assert.Equal(20 * em, panel.MaxWidth);
        Assert.Equal((double)button.FindResource("Rune.Radius.Sm") * em, panel.CornerRadius.TopLeft, 3);
        Assert.Equal(Visibility.Collapsed, chrome.ArrowPart.Visibility);
    }));

    [Fact]
    public Task Placements_keep_the_offset_and_alignment() => WpfUi.Run(() => WithButton((_, button, tip) =>
    {
        var chrome = Open(button, tip);
        var panel = Screen(chrome.PanelPart);
        var target = Screen(button);
        Assert.Equal(6, target.Top - panel.Bottom, 1.0);
        Assert.Equal(target.Left + target.Width / 2, panel.Left + panel.Width / 2, 1.0);

        Tooltip.SetPlacement(button, FloatingPlacement.RightStart);
        Tooltip.SetOffset(button, 10);
        chrome = Open(button, tip);
        panel = Screen(chrome.PanelPart);
        Assert.Equal(10, panel.Left - target.Right, 1.0);
        Assert.Equal(target.Top, panel.Top, 1.0);

        Tooltip.SetPlacement(button, FloatingPlacement.BottomEnd);
        chrome = Open(button, tip);
        panel = Screen(chrome.PanelPart);
        Assert.Equal(10, panel.Top - target.Bottom, 1.0);
        Assert.Equal(target.Right, panel.Right, 1.0);
    }));

    [Fact]
    public Task The_arrow_sits_on_the_edge_facing_the_trigger() => WpfUi.Run(() => WithButton((_, button, tip) =>
    {
        Tooltip.SetPlacement(button, FloatingPlacement.Bottom);
        Tooltip.SetArrow(button, true);
        var chrome = Open(button, tip);
        var arrow = chrome.ArrowPart;
        var size = arrow.ActualWidth;
        Assert.Equal(Visibility.Visible, arrow.Visibility);
        Assert.Equal(0.625 * tip.FontSize, size, 3);

        var panel = Screen(chrome.PanelPart);
        var target = Screen(button);
        // 距离加上箭头伸出的一段；箭头跨在面板的上边上、对准按钮中心
        Assert.Equal(6 + 0.7 * size, panel.Top - target.Bottom, 1.0);
        Assert.Equal("bottom", chrome.ArrowSide);
        // 箭头的布局位置（旋转之前的方块）相对面板
        var slot = LayoutInformation.GetLayoutSlot(arrow);
        var panelSlot = LayoutInformation.GetLayoutSlot(chrome.PanelPart);
        Assert.Equal(panelSlot.Top - size / 2, slot.Top, 3);
        Assert.Equal(target.Left + target.Width / 2, panel.Left + slot.Left - panelSlot.Left + size / 2, 1.0);
    }));

    [Fact]
    public Task Escape_hides_the_tooltip() => WpfUi.Run(() => WithButton((window, button, tip) =>
    {
        Open(button, tip);
        Assert.True(tip.IsOpen);
        var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        window.RaiseEvent(escape);
        Assert.True(escape.Handled);
        Assert.False(tip.IsOpen);
    }));
}
