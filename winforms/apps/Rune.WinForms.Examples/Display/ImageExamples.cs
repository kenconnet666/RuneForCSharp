namespace Rune.WinForms.Examples;

/// <summary>ImageControl、ImageGroupControl、LightboxControl 的示例（vue-tsx-pro 文档站 display/Image）。图纸是上游示例的 SVG 截成的 PNG，复制在程序目录的 drawings 下。</summary>
static class ImageExamples
{
    public static Example[] All =>
    [
        new("Image", "gallery", "图纸相册：预览、缩放、旋转", "写了 Preview 的图片是按钮（名称“查看大图：alt”），点了（或 Enter）打开 LightboxControl：深色铺满窗体，滚轮以指针为中心缩放，拖动平移，双击放大 / 还原，旋转以 90° 为一档，打开、换一张、旋转后整张放进视口。PreviewSource、Caption、DownloadName 是大图地址、说明与下载的文件名。ImageGroupControl 里的图片共用一个预览，从点的那张开始，Loop 时到头从另一头继续。", Gallery),
        new("Image", "states", "尺寸、填充与加载失败", "控件的 Size 是框的大小，Fit 是图片在框里怎么放；读的时候框里是扫光的占位。地址错了时显示占位与 alt，读屏念出 alt 与“图片加载失败”；Fallback 换成自己的文字。", States),
    ];

    /// <summary>六张示例图纸：图号、零件名称。</summary>
    static readonly (string Code, string Title)[] Drawings =
    [
        ("DWG-1021", "显示器支架"),
        ("DWG-1034", "检修口盖板"),
        ("DWG-1047", "机柜导轨"),
        ("DWG-1052", "线束卡箍"),
        ("DWG-1066", "加强筋"),
        ("DWG-1078", "端盖"),
    ];

    static Uri Drawing(string code) => new($"drawings/{code}.png", UriKind.Relative);

    /// <summary>按逻辑像素给大小（示例按 96 DPI 写，跟着屏幕放大）。</summary>
    static ImageControl Sized(ImageControl image, int width, int height)
    {
        image.Size = image.MinimumSize = new Size(image.LogicalToDeviceUnits(width), image.LogicalToDeviceUnits(height));
        return image;
    }

    static Control Gallery()
    {
        var group = new ImageGroupControl { Loop = true };
        foreach (var (code, title) in Drawings)
            group.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
                Sized(new ImageControl { Source = Drawing(code), Alt = $"{title}（{code}）", Preview = true, Caption = $"{code} · {title}", DownloadName = $"{code}.png" }, 180, 135),
                new SmallControl { Text = title },
                new SmallControl { Text = code, Tone = TextTone.Muted }));
        return group;
    }

    static Control Labeled(Control image, string label) => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
        image, new SmallControl { Text = label, Tone = TextTone.Muted });

    static Control States()
    {
        var (code, title) = Drawings[0];
        return Build.Flex(new FlexControl { Gap = Space.Lg, Wrap = true, Align = FlexAlign.End },
        [
            .. new[] { ImageFit.Cover, ImageFit.Contain, ImageFit.None }.Select(fit =>
                Labeled(Sized(new ImageControl { Source = Drawing(code), Alt = $"{title}（{fit}）", Fit = fit }, 140, 140), $"Fit={fit}")),
            Labeled(Sized(new ImageControl { Source = Drawing(Drawings[2].Code), Alt = "圆形缩略图", Radius = Radius.Full }, 96, 96), "Radius=full"),
            Labeled(Sized(new ImageControl { Source = new Uri("drawings/missing.png", UriKind.Relative), Alt = "不存在的图纸" }, 140, 105), "加载失败"),
        ]);
    }
}
