using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>LoadingOverlay：读屏的"忙"立即生效、指针立即挡住；遮罩防闪烁，过了 delay 才出现，结束后至少显示一会儿再拿掉；内容一直在。</summary>
public class LoadingOverlayTests
{
    [Fact]
    [Upstream("LoadingOverlay", "开始加载：aria-busy 立即写上，遮罩过了 delay 才出现")]
    public Task Busy_at_once_overlay_after_the_delay() => HeadlessUi.Run(() =>
    {
        var content = new Paragraph { Text = "内容" };
        var overlay = new LoadingOverlay { Content = content };
        var window = HeadlessUi.Show(overlay);

        overlay.IsLoading = true;
        Assert.Equal("busy", AutomationProperties.GetItemStatus(overlay));
        Assert.True(overlay.Blocker.IsVisible);
        Assert.False(overlay.Overlay.IsVisible);
        HeadlessUi.Until(() => overlay.Overlay.IsVisible);
        Assert.False(overlay.Blocker.IsVisible);
        Assert.Equal("加载中", overlay.Spinner.Peer().GetName());

        overlay.IsLoading = false;
        Assert.Null(AutomationProperties.GetItemStatus(overlay));
        // 出现后至少显示 500 毫秒
        Assert.True(overlay.Overlay.IsVisible);
        HeadlessUi.Until(() => !overlay.Overlay.IsVisible);
        Assert.Same(content, ((global::Avalonia.Controls.Presenters.ContentPresenter)((Panel)overlay.Overlay.Parent!).Children[0]).Content);
        HeadlessUi.Flush(window);
    });

    [Fact]
    [Upstream("LoadingOverlay", "挂载时已在加载：遮罩立即出现，说明文字可以改")]
    public Task Already_loading_shows_at_once_with_the_label() => HeadlessUi.Run(() =>
    {
        var overlay = new LoadingOverlay { IsLoading = true, Label = "正在保存", Blur = true, Content = new Paragraph { Text = "内容" } };
        var window = HeadlessUi.Show(overlay);
        HeadlessUi.Flush(window);

        Assert.True(overlay.Overlay.IsVisible);
        Assert.Equal("正在保存", overlay.Spinner.Peer().GetName());
        Assert.NotNull(((Panel)overlay.Overlay.Parent!).Children[0].Effect);
    });

    [Fact]
    public Task A_quick_load_never_shows_the_overlay() => HeadlessUi.Run(() =>
    {
        var overlay = new LoadingOverlay { Content = new Paragraph { Text = "内容" } };
        HeadlessUi.Show(overlay);

        overlay.IsLoading = true;
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(50));
        overlay.IsLoading = false;
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(300));
        Assert.False(overlay.Overlay.IsVisible);
        Assert.False(overlay.Blocker.IsVisible);
    });
}
