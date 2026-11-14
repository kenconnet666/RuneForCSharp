using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rune.Wpf.Tests;

/// <summary>Watermark：内容在下、水印层在上（不挡点击）；画出来的是水印的颜色，内容变了重画；整个窗口时画在窗口内容的装饰层里。</summary>
public class WatermarkTests
{
    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel { Width = 400, Children = { child } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        return host;
    }

    [Fact]
    [Upstream("Watermark", "内容在下、水印层在上")]
    public Task Layer_sits_above_the_content_and_stays_out_of_the_way() => WpfUi.Run(() =>
    {
        var content = new Border { Height = 200, Background = Brushes.White };
        var watermark = new Watermark { Text = "林小满\n2026-10-24", Child = content };
        WpfUi.Show(Host(watermark));

        Assert.Equal(2, VisualTreeHelper.GetChildrenCount(watermark));
        Assert.Equal(watermark.Layer, VisualTreeHelper.GetChild(watermark, 1));
        Assert.False(watermark.Layer.IsHitTestVisible);
        Assert.Equal(watermark.RenderSize, watermark.Layer.RenderSize);
        // 点下去命中的是内容
        Assert.Equal(content, watermark.InputHitTest(new Point(100, 100)));
    });

    [Fact]
    [Upstream("Watermark", "内容变了重新画")]
    public Task Draws_in_its_color_and_redraws_when_the_text_changes() => WpfUi.Run(() =>
    {
        var watermark = new Watermark { Width = 300, Height = 200, FontSize = 28, Rotate = 0, Color = Colors.Red };
        WpfUi.Show(Host(watermark));
        Assert.Equal(0, RedPixels(watermark));

        watermark.Text = "内部资料\n林小满";
        WpfUi.Flush();
        Assert.True(RedPixels(watermark) > 0);
        Assert.Equal(2 * 28 * 1.4, watermark.Content(1)!.Value.Size.Height, 3);

        watermark.Image = new DrawingImage(new GeometryDrawing(Brushes.Blue, null, new RectangleGeometry(new Rect(0, 0, 4, 4))));
        Assert.Equal(new Size(120, 40), watermark.Content(1)!.Value.Size); // 有图片时只画图片
        WpfUi.Flush();
        Assert.Equal(0, RedPixels(watermark));
    });

    [Fact]
    [Upstream("Watermark", "fullscreen 且不包内容")]
    public Task Fullscreen_covers_the_window_content_from_the_adorner_layer() => WpfUi.Run(() =>
    {
        var watermark = new Watermark { Text = "内部资料", Fullscreen = true };
        var host = Host(watermark);
        var window = WpfUi.Show(host);

        var adorner = Assert.IsAssignableFrom<Adorner>(watermark.Layer);
        Assert.Equal(window.Content, adorner.AdornedElement);
        Assert.Equal(host.RenderSize, adorner.RenderSize);
        Assert.Equal(0, VisualTreeHelper.GetChildrenCount(watermark));

        watermark.Fullscreen = false;
        WpfUi.Flush();
        Assert.Equal(watermark, VisualTreeHelper.GetParent(watermark.Layer));
        Assert.Empty(AdornerLayer.GetAdornerLayer(host)?.GetAdorners(host) ?? []);
    });

    /// <summary>画出来的红色像素数（水印颜色设成不透明的红，好数）。</summary>
    static int RedPixels(FrameworkElement element)
    {
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        // BGRA：红色通道高、绿色通道低
        return Enumerable.Range(0, pixels.Length / 4).Count(i => pixels[i * 4 + 2] > 100 && pixels[i * 4 + 1] < 60);
    }
}
