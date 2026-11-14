using System.Runtime.InteropServices;

namespace Rune.WinForms.Tests;

/// <summary>
/// WatermarkControl：水印画在属于窗体的分层窗口里，盖住控件露出来的部分（点击穿透、不置顶），跟着移动、隐藏；
/// 画出来的是水印的颜色，内容变了重画；整个窗体时盖住客户区。
/// </summary>
public class WatermarkTests
{
    const int GwlExStyle = -20, WsExTransparent = 0x20, WsExTopmost = 0x8;

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [Fact]
    [Upstream("Watermark", "内容在下、水印层在上")]
    public Task Overlay_covers_the_control_and_lets_clicks_through() => WinFormsUi.Run(() =>
    {
        var watermark = new WatermarkControl { Text = "林小满\n2026-10-24", AutoSize = false, Bounds =new Rectangle(20, 30, 300, 200) };
        var button = new System.Windows.Forms.Button { Text = "导出", Location = new Point(10, 10) };
        watermark.Controls.Add(button);
        using var form = WinFormsUi.Show(watermark);
        var overlay = watermark.Overlay;

        Assert.True(overlay.Visible);
        Assert.Equal(form, overlay.Owner);
        Assert.Equal(watermark.RectangleToScreen(watermark.ClientRectangle), overlay.Bounds);
        var style = GetWindowLong(overlay.Handle, GwlExStyle);
        Assert.NotEqual(0, style & WsExTransparent); // 点击穿透到下面的按钮
        Assert.Equal(0, style & WsExTopmost); // 不置顶：切到别的程序时不挡

        // 跟着移动；控件只露出一部分时只盖露出来的
        watermark.Location = new Point(600, 500);
        var visible = Rectangle.Intersect(watermark.RectangleToScreen(watermark.ClientRectangle), form.RectangleToScreen(form.ClientRectangle));
        GetWindowRect(overlay.Handle, out var r);
        Assert.Equal(visible, Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
        Assert.Equal(visible, overlay.Bounds);

        watermark.Visible = false;
        Assert.False(overlay.Visible);
    });

    [Fact]
    [Upstream("Watermark", "内容变了重新画")]
    public Task Draws_in_its_color_and_redraws_when_the_text_changes() => WinFormsUi.Run(() =>
    {
        var watermark = new WatermarkControl { AutoSize = false, Size = new Size(300, 200), FontSize = 28, Rotate = 0, Color = Color.Red };
        using var form = WinFormsUi.Show(watermark);
        var area = watermark.RectangleToScreen(watermark.ClientRectangle);
        Assert.Equal(0, RedPixels(watermark.Render(area, area.Location)));
        Assert.False(watermark.Overlay.Visible); // 没有内容时不显示水印窗口

        watermark.Text = "内部资料\n林小满";
        Assert.True(RedPixels(watermark.Render(area, area.Location)) > 0);

        watermark.Image = new Bitmap(4, 4);
        using var graphics = watermark.CreateGraphics();
        Assert.Equal(new SizeF(120, 40) * (watermark.DeviceDpi / 96f), watermark.ContentSize(graphics)!.Value); // 有图片时只画图片
    });

    [Fact]
    public Task Children_stack_and_set_the_height() => WinFormsUi.Run(() =>
    {
        var watermark = new WatermarkControl { Text = "内部资料" };
        watermark.Controls.Add(new Panel { Height = 40 });
        watermark.Controls.Add(new Panel { Height = 60 });
        Assert.Equal(100, watermark.GetPreferredSize(new Size(300, 0)).Height);
    });

    [Fact]
    [Upstream("Watermark", "fullscreen 且不包内容")]
    public Task Fullscreen_covers_the_form_client_area() => WinFormsUi.Run(() =>
    {
        var watermark = new WatermarkControl { Text = "内部资料", Fullscreen = true, Size = Size.Empty };
        using var form = WinFormsUi.Show(watermark);

        Assert.True(watermark.Overlay.Visible);
        Assert.Equal(form.RectangleToScreen(form.ClientRectangle), watermark.Overlay.Bounds);
    });

    /// <summary>红色像素数（水印颜色设成不透明的红，好数）。</summary>
    static int RedPixels(Bitmap bitmap)
    {
        using (bitmap)
        {
            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, y) is { A: > 100, R: > 100, G: < 60 }) count++;
            return count;
        }
    }
}
