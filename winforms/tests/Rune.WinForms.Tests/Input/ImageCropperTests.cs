using System.Drawing.Imaging;
using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>ImageCropperControl：一开始盖满裁剪框（缩小按钮不可用）；输出指定大小，矩形四角不透明、圆形四角透明；滚轮缩小不到盖不满；区域的名称与说明、键盘。</summary>
public class ImageCropperTests
{
    /// <summary>一张不透明的 PNG（40×30）。</summary>
    static Uri Picture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rune-crop-{Guid.NewGuid():N}.png");
        using (var bitmap = new Bitmap(40, 30))
        {
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.SteelBlue);
            bitmap.Save(path, ImageFormat.Png);
        }
        return new Uri(path);
    }

    static ImageCropperControl Show(ImageCropShape? shape, out Form form)
    {
        var cropper = new ImageCropperControl { Source = Picture(), AccessibleName = "裁剪" };
        if (shape is { } value) cropper.Shape = value;
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        var stack = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Top };
        stack.Controls.Add(cropper);
        host.Controls.Add(stack);
        form = WinFormsUi.Show(host);
        WinFormsUi.Until(() => cropper.Natural is not null);
        return cropper;
    }

    static void Raise(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [args]);

    [Fact]
    [Upstream("ImageCropper", "盖满裁剪框；输出指定大小，矩形四角不透明")]
    public Task Covers_the_frame_and_outputs_the_requested_size() => WinFormsUi.Run(() =>
    {
        var cropper = Show(null, out var form);
        using var _ = form;

        Assert.False(cropper.Tools.ZoomOut.Enabled);
        Assert.True(cropper.Tools.ZoomIn.Enabled);
        Assert.Equal("缩小", cropper.Tools.ZoomOut.AccessibleName);
        using var bitmap = cropper.ToBitmap(new ImageCropOutput(200))!;
        Assert.Equal(new Size(200, 200), bitmap.Size);
        Assert.Equal(255, bitmap.GetPixel(1, 1).A);
        var png = cropper.Encode()!;
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        var jpeg = cropper.Encode(new ImageCropFormat { Type = ImageCropFileType.Jpeg, Quality = 0.9 })!;
        Assert.Equal([0xFF, 0xD8], jpeg[..2]);
    });

    [Fact]
    [Upstream("ImageCropper", "圆形：四角透明、中间不透明；滚轮缩小也停在盖满")]
    public Task Round_has_clear_corners_and_wheel_stops_at_cover() => WinFormsUi.Run(() =>
    {
        var cropper = Show(ImageCropShape.Round, out var form);
        using var _ = form;
        using (var bitmap = cropper.ToBitmap(new ImageCropOutput(100))!)
        {
            Assert.Equal(0, bitmap.GetPixel(1, 1).A);
            Assert.Equal(255, bitmap.GetPixel(50, 50).A);
        }

        var before = cropper.ToBitmap()!.Width;
        Raise(cropper.ViewportPart, "OnMouseWheel", new HandledMouseEventArgs(MouseButtons.None, 0, 100, 100, -600));
        Assert.Equal(before, cropper.ToBitmap()!.Width);
        Assert.Equal(1, cropper.Zoom, 6);
    });

    [Fact]
    public Task Viewport_is_a_named_group_with_keys() => WinFormsUi.Run(() =>
    {
        var cropper = Show(null, out var form);
        using var _ = form;
        var viewport = cropper.ViewportPart;

        Assert.Equal(AccessibleRole.Grouping, viewport.AccessibilityObject.Role);
        Assert.Equal("裁剪", viewport.AccessibilityObject.Name);
        Assert.Equal("拖动移动图片，滚轮或 + − 缩放，方向键微调", viewport.AccessibilityObject.Description);
        Raise(viewport, "OnKeyDown", new KeyEventArgs(Keys.Oemplus));
        Assert.Equal(1.1, cropper.Zoom, 6);
        Assert.True(cropper.Tools.ZoomOut.Enabled);
        Assert.Equal(1.1, cropper.Tools.Zoom.Value, 2);
        cropper.Tools.Reset.PerformClick();
        Assert.Equal(1, cropper.Zoom, 6);
        cropper.Tools.Zoom.Value = 4;
        Assert.Equal(4, cropper.Zoom, 6);
        Assert.False(cropper.Tools.ZoomIn.Enabled);
    });
}
