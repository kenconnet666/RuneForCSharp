using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Rune.Wpf.Tests;

/// <summary>ImageCropper：一开始盖满裁剪框（缩小按钮不可用）；输出指定大小，矩形四角不透明、圆形四角透明；滚轮缩小不到盖不满；区域的名称与说明、键盘。</summary>
public class ImageCropperTests
{
    static ImageCropper Show(ImageCropShape? shape = null)
    {
        var cropper = new ImageCropper { Source = ImageTests.Picture("crop"), Width = 400 };
        if (shape is { } value) cropper.Shape = value;
        AutomationProperties.SetName(cropper, "裁剪");
        WpfUi.Show(ImageTests.Host(cropper));
        WpfUi.Until(() => cropper.Natural is not null);
        return cropper;
    }

    static byte AlphaAt(BitmapSource bitmap, int x, int y)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel[3];
    }

    static void Invoke(UIElement element)
    {
        ((IInvokeProvider)element.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("ImageCropper", "盖满裁剪框；输出指定大小，矩形四角不透明")]
    public Task Covers_the_frame_and_outputs_the_requested_size() => WpfUi.Run(() =>
    {
        var cropper = Show();

        Assert.False(cropper.Tools.ZoomOut.IsEnabled);
        Assert.True(cropper.Tools.ZoomIn.IsEnabled);
        Assert.Equal("缩小", cropper.Tools.ZoomOut.Peer().GetName());
        var bitmap = cropper.ToBitmap(new ImageCropOutput(200))!;
        Assert.Equal((200, 200), (bitmap.PixelWidth, bitmap.PixelHeight));
        Assert.Equal(255, AlphaAt(bitmap, 1, 1));
        var png = cropper.Encode()!;
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        var jpeg = cropper.Encode(new ImageCropFormat { Type = ImageCropFileType.Jpeg, Quality = 0.9 })!;
        Assert.Equal([0xFF, 0xD8], jpeg[..2]);
    });

    [Fact]
    [Upstream("ImageCropper", "圆形：四角透明、中间不透明；滚轮缩小也停在盖满")]
    public Task Round_has_clear_corners_and_wheel_stops_at_cover() => WpfUi.Run(() =>
    {
        var cropper = Show(ImageCropShape.Round);
        var bitmap = cropper.ToBitmap(new ImageCropOutput(100))!;
        Assert.Equal(0, AlphaAt(bitmap, 1, 1));
        Assert.Equal(255, AlphaAt(bitmap, 50, 50));

        var before = cropper.ToBitmap()!.PixelWidth;
        cropper.ViewportPart.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -600) { RoutedEvent = Mouse.MouseWheelEvent });
        Assert.Equal(before, cropper.ToBitmap()!.PixelWidth);
        Assert.Equal(1, cropper.Zoom, 6);
    });

    [Fact]
    public Task Viewport_is_a_named_group_with_keys() => WpfUi.Run(() =>
    {
        var cropper = Show();
        var viewport = cropper.ViewportPart;

        Assert.Equal(AutomationControlType.Group, viewport.Peer().GetAutomationControlType());
        Assert.Equal("裁剪", viewport.Peer().GetName());
        Assert.Equal("拖动移动图片，滚轮或 + − 缩放，方向键微调", viewport.Peer().GetHelpText());
        var source = PresentationSource.FromVisual(viewport)!;
        viewport.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.OemPlus) { RoutedEvent = Keyboard.KeyDownEvent });
        Assert.Equal(1.1, cropper.Zoom, 6);
        Assert.True(cropper.Tools.ZoomOut.IsEnabled);
        Assert.Equal(1.1, cropper.Tools.Zoom.Value, 6);
        Invoke(cropper.Tools.Reset);
        Assert.Equal(1, cropper.Zoom, 6);
        cropper.Tools.Zoom.Value = 4;
        Assert.Equal(4, cropper.Zoom, 6);
        Assert.False(cropper.Tools.ZoomIn.IsEnabled);
    });
}
