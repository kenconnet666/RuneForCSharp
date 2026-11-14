namespace Rune.WinForms.Tests;

/// <summary>LoadingOverlayControl：读屏的"忙"立即生效、指针立即挡住；遮罩防闪烁，过了 delay 才出现，结束后至少显示一会儿再拿掉；内容一直在。</summary>
public class LoadingOverlayTests
{
    static LoadingOverlayControl Overlay(string content)
    {
        var overlay = new LoadingOverlayControl();
        overlay.Controls.Add(new ParagraphControl { Text = content });
        return overlay;
    }

    [Fact]
    [Upstream("LoadingOverlay", "开始加载：aria-busy 立即写上，遮罩过了 delay 才出现")]
    public Task Busy_at_once_overlay_after_the_delay() => WinFormsUi.Run(() =>
    {
        var overlay = Overlay("内容");
        using var form = WinFormsUi.Show(overlay);

        overlay.IsLoading = true;
        Assert.True(overlay.AccessibilityObject.State.HasFlag(AccessibleStates.Busy));
        Assert.Equal("加载中", overlay.AccessibilityObject.Description);
        // 指针立即挡住，遮罩还没出现
        Assert.True(overlay.Blocking);
        Assert.False(overlay.OverlayShown);
        Assert.True(overlay.Overlay.Bounds.Width > 0);
        WinFormsUi.Until(() => overlay.OverlayShown);

        overlay.IsLoading = false;
        Assert.False(overlay.AccessibilityObject.State.HasFlag(AccessibleStates.Busy));
        // 出现后至少显示 500 毫秒
        Assert.True(overlay.OverlayShown);
        WinFormsUi.Until(() => !overlay.Blocking);
        Assert.Equal("内容", overlay.Controls[0].Text);
    });

    [Fact]
    [Upstream("LoadingOverlay", "挂载时已在加载：遮罩立即出现，说明文字可以改")]
    public Task Already_loading_shows_at_once_with_the_label() => WinFormsUi.Run(() =>
    {
        var overlay = Overlay("内容");
        overlay.IsLoading = true;
        overlay.Label = "正在保存";
        using var form = WinFormsUi.Show(overlay);

        Assert.True(overlay.OverlayShown);
        Assert.Equal("正在保存", overlay.AccessibilityObject.Description);
    });

    [Fact]
    public Task A_quick_load_never_shows_the_overlay() => WinFormsUi.Run(() =>
    {
        var overlay = Overlay("内容");
        using var form = WinFormsUi.Show(overlay);

        overlay.IsLoading = true;
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(50));
        overlay.IsLoading = false;
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(300));
        Assert.False(overlay.OverlayShown);
        Assert.False(overlay.Blocking);
    });

    [Fact]
    public Task The_overlay_window_follows_the_control() => WinFormsUi.Run(() =>
    {
        var overlay = Overlay("内容");
        overlay.IsLoading = true;
        using var form = WinFormsUi.Show(overlay);

        Assert.Equal(overlay.RectangleToScreen(overlay.ClientRectangle), overlay.Overlay.Bounds);
        overlay.Controls[0].Text = "更长的内容，更长的内容，更长的内容";
        WinFormsUi.Pump();
        Assert.Equal(overlay.RectangleToScreen(overlay.ClientRectangle), overlay.Overlay.Bounds);
    });
}
