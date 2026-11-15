using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>ImageCropper：一开始盖满裁剪框（缩小按钮不可用）；输出指定大小；滚轮缩小不到盖不满；区域的名称与说明、键盘。裁剪的几何见 ImageCropGeometryTests。</summary>
public class ImageCropperTests
{
    static ImageCropper Show(ImageCropShape? shape = null)
    {
        var cropper = new ImageCropper { Source = ImageTests.Picture("crop"), Width = 400 };
        if (shape is { } value) cropper.Shape = value;
        AutomationProperties.SetName(cropper, "裁剪");
        HeadlessUi.Show(cropper);
        HeadlessUi.Until(() => cropper.Natural is not null);
        return cropper;
    }

    [Fact]
    [Upstream("ImageCropper", "盖满裁剪框；输出指定大小，矩形四角不透明")]
    public Task Covers_the_frame_and_outputs_the_requested_size() => HeadlessUi.Run(() =>
    {
        var cropper = Show();

        Assert.False(cropper.Tools.ZoomOut.IsEnabled);
        Assert.True(cropper.Tools.ZoomIn.IsEnabled);
        Assert.Equal(1, cropper.Zoom, 6);
        Assert.Equal("缩小", cropper.Tools.ZoomOut.Peer().GetName());
        using var bitmap = cropper.ToBitmap(new ImageCropOutput(200))!;
        Assert.Equal(new PixelSize(200, 200), bitmap.PixelSize);
        // headless 的位图不真编码（字节与透明的四角在 WPF、WinForms 的测试里核对）
        Assert.NotNull(cropper.Encode());
    });

    [Fact]
    [Upstream("ImageCropper", "圆形：四角透明、中间不透明；滚轮缩小也停在盖满")]
    public Task Wheel_cannot_zoom_out_past_cover() => HeadlessUi.Run(() =>
    {
        var cropper = Show(ImageCropShape.Round);
        var before = cropper.ToBitmap()!.PixelSize;
        cropper.ViewportPart.RaiseEvent(new PointerWheelEventArgs(cropper.ViewportPart, new Pointer(0, PointerType.Mouse, true), cropper, new Point(100, 100), 0,
            new PointerPointProperties(), KeyModifiers.None, new Vector(0, -5)) { RoutedEvent = InputElement.PointerWheelChangedEvent });

        Assert.Equal(before, cropper.ToBitmap()!.PixelSize);
        Assert.Equal(1, cropper.Zoom, 6);
    });

    [Fact]
    public Task Viewport_is_a_named_group_with_keys() => HeadlessUi.Run(() =>
    {
        var cropper = Show();
        var viewport = cropper.ViewportPart;

        Assert.Equal(AutomationControlType.Group, viewport.Peer().GetAutomationControlType());
        Assert.Equal("裁剪", viewport.Peer().GetName());
        Assert.Equal("拖动移动图片，滚轮或 + − 缩放，方向键微调", viewport.Peer().GetHelpText());
        viewport.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.OemPlus, Source = viewport });
        Assert.Equal(1.1, cropper.Zoom, 6);
        Assert.True(cropper.Tools.ZoomOut.IsEnabled);
        Assert.Equal(1.1, cropper.Tools.Zoom.Value, 6);
        ((IInvokeProvider)cropper.Tools.Reset.Peer()).Invoke();
        Assert.Equal(1, cropper.Zoom, 6);
        cropper.Tools.Zoom.Value = 4;
        Assert.Equal(4, cropper.Zoom, 6);
        Assert.False(cropper.Tools.ZoomIn.IsEnabled);
    });
}
