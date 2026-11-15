using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Lightbox：铺满窗口；← / → 切换（Loop 时绕回）、Esc 关闭并还回焦点；打开后整张放进视口；+ / − / 0 缩放与比例；名称与按钮的读屏名称。</summary>
public class LightboxTests
{
    static LightboxItem[] Items(int count) => [.. Enumerable.Range(1, count).Select(i => new LightboxItem(ImageTests.Picture($"l{i}"), $"图纸 {i}"))];

    static void Escape(Lightbox lightbox) =>
        lightbox.Viewport.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(lightbox.Viewport)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    [Fact]
    [Upstream("Lightbox", "←→ 切换，Esc 关闭并把焦点还回去")]
    public Task Arrows_switch_and_escape_closes_returning_focus() => WpfUi.Run(() =>
    {
        var opener = new System.Windows.Controls.Button { Content = "预览" };
        var lightbox = new Lightbox { Items = Items(3) };
        var host = ImageTests.Host(opener, lightbox);
        WpfUi.Show(host);
        opener.Focus();
        lightbox.IsOpen = true;
        WpfUi.Until(() => lightbox.Viewport.IsKeyboardFocused);

        var area = ((FrameworkElement)lightbox.Panel.Parent).ActualWidth;
        Assert.Equal(area, lightbox.Panel.ActualWidth);
        Assert.Equal("图片预览", lightbox.Panel.Peer().GetName());
        Assert.Equal(AutomationControlType.Image, lightbox.Viewport.Peer().GetAutomationControlType());
        Assert.Equal("图纸 1", lightbox.Viewport.Peer().GetName());
        Assert.Equal(Visibility.Collapsed, lightbox.Sides.Previous.Visibility);
        ImageTests.Press(lightbox.Viewport, Key.Right);
        ImageTests.Press(lightbox.Viewport, Key.Right);
        Assert.Equal(2, lightbox.Index);
        ImageTests.Press(lightbox.Viewport, Key.Left);
        Assert.Equal(1, lightbox.Index);
        Assert.Equal("放大", lightbox.Tools.ZoomIn.Peer().GetName());
        Assert.Equal("下载", lightbox.Tools.Download.Peer().GetName());

        Escape(lightbox);
        WpfUi.Until(() => !lightbox.IsOpen);
        WpfUi.Flush();
        Assert.True(opener.IsKeyboardFocused);
    });

    [Fact]
    public Task Loop_wraps_around_and_download_can_be_hidden() => WpfUi.Run(() =>
    {
        var items = Items(2);
        items[1] = items[1] with { Downloadable = false };
        var lightbox = new Lightbox { Items = items, Loop = true };
        WpfUi.Show(ImageTests.Host(lightbox));
        lightbox.IsOpen = true;
        WpfUi.Until(() => lightbox.Viewport.ActualWidth > 0);

        Assert.Equal(Visibility.Visible, lightbox.Sides.Previous.Visibility);
        Assert.Equal(Visibility.Visible, lightbox.Tools.Download.Visibility);
        ImageTests.Invoke(lightbox.Sides.Previous);
        Assert.Equal(1, lightbox.Index);
        Assert.Equal(Visibility.Collapsed, lightbox.Tools.Download.Visibility);
        ImageTests.Press(lightbox.Viewport, Key.Right);
        Assert.Equal(0, lightbox.Index);
        lightbox.Close();
    });

    [Fact]
    public Task Opening_fits_the_picture_and_keys_zoom() => WpfUi.Run(() =>
    {
        var lightbox = new Lightbox { Items = Items(1) };
        WpfUi.Show(ImageTests.Host(lightbox));
        lightbox.IsOpen = true;
        WpfUi.Until(() => lightbox.Labels.Zoom.Text is { Length: > 0 });

        // 小图（40×30）不放大：原大居中
        var state = lightbox.View.State;
        Assert.Equal(1, state.Scale);
        Assert.Equal((lightbox.Viewport.ActualWidth - 40) / 2, state.X, 3);
        Assert.Equal("100%", lightbox.Labels.Zoom.Text);
        ImageTests.Press(lightbox.Viewport, Key.OemPlus);
        Assert.Equal(1.25, lightbox.View.State.Scale);
        Assert.Equal("125%", lightbox.Labels.Zoom.Text);
        ImageTests.Press(lightbox.Viewport, Key.OemMinus);
        ImageTests.Press(lightbox.Viewport, Key.OemMinus);
        Assert.Equal(0.8, lightbox.View.State.Scale, 6);
        ImageTests.Press(lightbox.Viewport, Key.D0);
        Assert.Equal(1, lightbox.View.State.Scale);
        lightbox.Close();
    });
}
