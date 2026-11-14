using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>LoadingOverlay：读屏的"忙"立即生效、指针立即挡住；遮罩防闪烁，过了 delay 才出现，结束后至少显示一会儿再拿掉；内容一直在。</summary>
public class LoadingOverlayTests
{
    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(child);
        return host;
    }

    [Fact]
    [Upstream("LoadingOverlay", "开始加载：aria-busy 立即写上，遮罩过了 delay 才出现")]
    public Task Busy_at_once_overlay_after_the_delay() => WpfUi.Run(() =>
    {
        var content = new Paragraph { Text = "内容" };
        var overlay = new LoadingOverlay { Content = content };
        WpfUi.Show(Host(overlay));

        overlay.IsLoading = true;
        Assert.Equal("busy", AutomationProperties.GetItemStatus(overlay));
        Assert.Equal(Visibility.Visible, overlay.Blocker.Visibility);
        Assert.Equal(Visibility.Collapsed, overlay.Overlay.Visibility);
        WpfUi.Until(() => overlay.Overlay.Visibility == Visibility.Visible);
        Assert.Equal(Visibility.Collapsed, overlay.Blocker.Visibility);
        Assert.Equal("加载中", overlay.Spinner.Label);

        overlay.IsLoading = false;
        Assert.Equal("", AutomationProperties.GetItemStatus(overlay));
        // 出现后至少显示 500 毫秒
        Assert.Equal(Visibility.Visible, overlay.Overlay.Visibility);
        WpfUi.Until(() => overlay.Overlay.Visibility == Visibility.Collapsed);
        Assert.Same(content, overlay.ContentPart.Content);
    });

    [Fact]
    [Upstream("LoadingOverlay", "挂载时已在加载：遮罩立即出现，说明文字可以改")]
    public Task Already_loading_shows_at_once_with_the_label() => WpfUi.Run(() =>
    {
        var overlay = new LoadingOverlay { IsLoading = true, Label = "正在保存", Blur = true, Content = new Paragraph { Text = "内容" } };
        WpfUi.Show(Host(overlay));
        WpfUi.Flush();

        Assert.Equal(Visibility.Visible, overlay.Overlay.Visibility);
        Assert.Equal("正在保存", overlay.Spinner.Label);
        Assert.NotNull(overlay.ContentPart.Effect);
    });

    [Fact]
    public Task A_quick_load_never_shows_the_overlay() => WpfUi.Run(() =>
    {
        var overlay = new LoadingOverlay { Content = new Paragraph { Text = "内容" } };
        WpfUi.Show(Host(overlay));

        overlay.IsLoading = true;
        WpfUi.Wait(TimeSpan.FromMilliseconds(50));
        overlay.IsLoading = false;
        WpfUi.Wait(TimeSpan.FromMilliseconds(300));
        Assert.Equal(Visibility.Collapsed, overlay.Overlay.Visibility);
        Assert.Equal(Visibility.Collapsed, overlay.Blocker.Visibility);
    });
}
