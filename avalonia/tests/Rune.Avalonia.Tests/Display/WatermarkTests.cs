using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Watermark：内容在下、水印层在上（不挡点击、读屏不念）；平铺单元斜对角两份；整个窗口时水印层在窗口的浮层里。</summary>
public class WatermarkTests
{
    [Fact]
    [Upstream("Watermark", "内容在下、水印层在上")]
    public Task Layer_sits_above_the_content_and_stays_out_of_the_way() => HeadlessUi.Run(() =>
    {
        var content = new Border { Height = 200 };
        var watermark = new Watermark { Text = "林小满\n2026-10-24", Child = content };
        HeadlessUi.Show(watermark);

        Assert.Contains(watermark.Layer, watermark.GetVisualChildren());
        Assert.True(watermark.Layer.ZIndex > content.ZIndex);
        Assert.False(watermark.Layer.IsHitTestVisible);
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(watermark.Layer));
        Assert.Equal(watermark.Bounds.Size, watermark.Layer.Bounds.Size);
        Assert.Equal(["林小满", "2026-10-24"], WatermarkTiles.Lines(watermark.Text));
    });

    [Fact]
    public void Tiles_put_two_copies_on_the_diagonal_and_cover_the_area()
    {
        var tiles = WatermarkTiles.Layout(500, 300, 200, 120).ToList();
        // 一块单元 400 × 240：横向两块、竖向两块
        Assert.Equal(4, tiles.Count);
        Assert.Equal(new WatermarkTile(0, 0, 400, 240, 100, 60, 300, 180), tiles[0]);
        Assert.Equal(new WatermarkTile(400, 240, 400, 240, 500, 300, 700, 420), tiles[3]);
        Assert.Equal(-21, WatermarkTiles.LineOffset(0, 2, 42));
        Assert.Empty(WatermarkTiles.Layout(500, 300, 0, 120));
    }

    [Fact]
    [Upstream("Watermark", "内容变了重新画")]
    public Task Content_follows_the_text_font_size_and_image() => HeadlessUi.Run(() =>
    {
        var watermark = new Watermark();
        HeadlessUi.Show(watermark);
        Assert.Null(watermark.Content());

        watermark.Text = "内部资料";
        var single = watermark.Content()!.Value.Size;
        Assert.True(single.Width > 0);
        Assert.Equal(14 * 1.4, single.Height, 3);

        watermark.Text = "内部资料\n林小满";
        Assert.Equal(2 * 14 * 1.4, watermark.Content()!.Value.Size.Height, 3);
        watermark.FontSize = 20;
        Assert.Equal(2 * 20 * 1.4, watermark.Content()!.Value.Size.Height, 3);

        watermark.Image = new WriteableBitmap(new PixelSize(4, 4), new Vector(96, 96));
        Assert.Equal(new Size(120, 40), watermark.Content()!.Value.Size); // 有图片时只画图片
    });

    [Fact]
    [Upstream("Watermark", "fullscreen 且不包内容")]
    public Task Fullscreen_covers_the_window_from_the_overlay_layer() => HeadlessUi.Run(() =>
    {
        var watermark = new Watermark { Text = "内部资料", Fullscreen = true };
        // 测试的窗口没有模板：给一个带浮层（VisualLayerManager）的，同桌面主题里的窗口
        var window = new Window
        {
            Width = 800, Height = 600, Content = new StackPanel { Children = { watermark } },
            Template = new FuncControlTemplate<Window>((owner, _) => new VisualLayerManager
            {
                EnableOverlayLayer = true,
                Child = new ContentPresenter { Name = "PART_ContentPresenter", [!ContentPresenter.ContentProperty] = owner[!ContentControl.ContentProperty] },
            }),
        };
        window.Show();
        HeadlessUi.Flush(window);
        var overlay = OverlayLayer.GetOverlayLayer(watermark)!;

        Assert.Equal(overlay, watermark.Layer.GetVisualParent());
        Assert.Equal(overlay.Bounds.Size, watermark.Layer.Bounds.Size);
        Assert.Equal(window.ClientSize, watermark.Layer.Bounds.Size);

        watermark.Fullscreen = false;
        HeadlessUi.Flush(window);
        Assert.Equal(watermark, watermark.Layer.GetVisualParent());
    });
}
