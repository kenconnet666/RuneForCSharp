using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using AvaloniaImage = Avalonia.Controls.Image;

namespace Rune.Avalonia.Examples;

/// <summary>ImageCropper 的示例（vue-tsx-pro 文档站 input/ImageCropper）。图纸同 Image 的示例（docs/site/drawings）。</summary>
static class ImageCropperExamples
{
    public static Example[] All =>
    [
        new("ImageCropper", "avatar", "裁成圆形头像", "Shape=\"round\" 是圆形裁剪框，输出的图四角透明；Encode(new ImageCropFormat { Size = new(256) }) 输出 256 × 256 的 PNG。上游配 Upload 的 transform（选好图片先弹出裁剪），这里直接裁一张图纸。", Avatar),
        new("ImageCropper", "cover", "封面", "Aspect 是裁剪框的宽高比（宽 ÷ 高）。缩小到正好盖满裁剪框为止，最多放大到它的 MaxZoom 倍（默认 4）；拖动时框里不会露出空白。ToBitmap 拿到裁好的位图，Encode 编码成 PNG 或 JPEG（Type、Quality），只写宽或高时另一边按宽高比算，都不写时按图片原本的像素。", Cover),
    ];

    static Uri Drawing(string code) => new($"drawings/{code}.png", UriKind.Relative);

    static Bitmap Decode(byte[] bytes) => new(new MemoryStream(bytes));

    // 头像：圆形裁剪框，点“使用这张”裁出 256 × 256 的 PNG，右边显示结果
    static Control Avatar()
    {
        var cropper = new ImageCropper { Source = Drawing("DWG-1021"), Shape = ImageCropShape.Round };
        AutomationProperties.SetName(cropper, "裁剪头像");
        var result = new AvaloniaImage { Width = 96, Height = 96 };
        AutomationProperties.SetName(result, "裁好的头像");
        var use = new Button { Content = "使用这张", Size = TextSize.Sm, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
        use.Click += (_, _) =>
        {
            if (cropper.Encode(new ImageCropFormat { Size = new(256) }) is { } png) result.Source = Decode(png);
        };
        return new Flex
        {
            Gap = Space.Lg, Align = FlexAlign.Start,
            Children =
            {
                new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Width = 360, Children = { cropper, use } },
                new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { result, new Small { Text = "256 × 256 PNG", Tone = TextTone.Muted } } },
            },
        };
    }

    // 封面：16:9 的框，裁出 1280 × 720 的 JPEG，下面显示结果与大小
    static Control Cover()
    {
        var cropper = new ImageCropper { Source = Drawing("DWG-1052"), Aspect = 16d / 9 };
        AutomationProperties.SetName(cropper, "裁剪封面");
        var size = new Small { Tone = TextTone.Muted };
        var result = new AvaloniaImage { MaxWidth = 480, IsVisible = false };
        AutomationProperties.SetName(result, "裁好的封面");
        var crop = new Button { Content = "裁出 1280 × 720", Icon = Lucide.Crop, Size = TextSize.Sm };
        crop.Click += (_, _) =>
        {
            if (cropper.Encode(new ImageCropFormat { Size = new(1280), Type = ImageCropFileType.Jpeg, Quality = 0.9 }) is not { } jpeg) return;
            result.Source = Decode(jpeg);
            result.IsVisible = true;
            size.Text = $"JPEG，{jpeg.Length / 1000d:0.0} kB";
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm,
            Children = { cropper, new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { crop, size } }, result },
        };
    }
}
