namespace Rune.WinForms.Examples;

/// <summary>ImageCropperControl 的示例（vue-tsx-pro 文档站 input/ImageCropper）。图纸同 Image 的示例（docs/site/drawings）。</summary>
static class ImageCropperExamples
{
    public static Example[] All =>
    [
        new("ImageCropper", "avatar", "裁成圆形头像", "Shape=round 是圆形裁剪框，输出的图四角透明；Encode(new ImageCropFormat { Size = new(256) }) 输出 256 × 256 的 PNG。上游配 Upload 的 transform（选好图片先弹出裁剪），这里直接裁一张图纸。", Avatar),
        new("ImageCropper", "cover", "封面", "Aspect 是裁剪框的宽高比（宽 ÷ 高）。缩小到正好盖满裁剪框为止，最多放大到它的 MaxZoom 倍（默认 4）；拖动时框里不会露出空白。ToBitmap 拿到裁好的位图，Encode 编码成 PNG 或 JPEG（Type、Quality），只写宽或高时另一边按宽高比算，都不写时按图片原本的像素。", Cover),
    ];

    static Uri Drawing(string code) => new($"drawings/{code}.png", UriKind.Relative);

    /// <summary>显示裁好的图（字节解码成位图，放进平台的 PictureBox）。</summary>
    static void ShowResult(PictureBox box, byte[] bytes)
    {
        var old = box.Image;
        using (var stream = new MemoryStream(bytes)) box.Image = new Bitmap(stream);
        old?.Dispose();
        box.Visible = true;
    }

    // 头像：圆形裁剪框，点“使用这张”裁出 256 × 256 的 PNG，右边显示结果
    static Control Avatar()
    {
        var cropper = new ImageCropperControl { Source = Drawing("DWG-1021"), Shape = ImageCropShape.Round, AccessibleName = "裁剪头像" };
        cropper.MinimumSize = new Size(cropper.LogicalToDeviceUnits(360), 0);
        var result = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "裁好的头像", BackColor = Color.Transparent };
        result.Size = new Size(result.LogicalToDeviceUnits(96), result.LogicalToDeviceUnits(96));
        var use = new ButtonControl { Text = "使用这张", TextSize = TextSize.Sm };
        use.Click += (_, _) =>
        {
            if (cropper.Encode(new ImageCropFormat { Size = new(256) }) is { } png) ShowResult(result, png);
        };
        return Build.Flex(new FlexControl { Gap = Space.Lg, Align = FlexAlign.Start },
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Start }, cropper, use),
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs }, result, new SmallControl { Text = "256 × 256 PNG", Tone = TextTone.Muted }));
    }

    // 封面：16:9 的框，裁出 1280 × 720 的 JPEG，下面显示结果与大小
    static Control Cover()
    {
        var cropper = new ImageCropperControl { Source = Drawing("DWG-1052"), Aspect = 16d / 9, AccessibleName = "裁剪封面" };
        var size = new SmallControl { Tone = TextTone.Muted };
        var result = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, AccessibleName = "裁好的封面", Visible = false };
        result.Size = new Size(result.LogicalToDeviceUnits(480), result.LogicalToDeviceUnits(270));
        var crop = new ButtonControl { Text = "裁出 1280 × 720", Icon = Lucide.Crop, TextSize = TextSize.Sm };
        crop.Click += (_, _) =>
        {
            if (cropper.Encode(new ImageCropFormat { Size = new(1280), Type = ImageCropFileType.Jpeg, Quality = 0.9 }) is not { } jpeg) return;
            ShowResult(result, jpeg);
            size.Text = $"JPEG，{jpeg.Length / 1000d:0.0} kB";
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Dock = DockStyle.Top },
            cropper, Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm }, crop, size), result);
    }
}
