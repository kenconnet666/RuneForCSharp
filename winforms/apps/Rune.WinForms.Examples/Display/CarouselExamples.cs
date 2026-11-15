namespace Rune.WinForms.Examples;

/// <summary>CarouselControl 的示例（vue-tsx-pro 文档站 display/Carousel）。图纸同 Image 的示例（docs/site/drawings）。</summary>
static class CarouselExamples
{
    public static Example[] All =>
    [
        new("Carousel", "autoplay", "自动播放", "Autoplay 是间隔（TS 的 autoplay: true 是 5 秒）：指针停在上面或键盘焦点进来时暂停，底部的按钮可以停下；拖动、触摸板横向滑动（或 Shift + 滚轮）、焦点在张片区时 ← / → 也能换。系统关闭动画时一开始就是暂停的。", Drawings),
        new("Carousel", "thumbnails", "图片组与缩略图", "下面一排缩略图点了写 Index 跳过去；滑动大图时 IndexChanged 报告，当前那张缩略图的框变成强调色。", Thumbnails),
        new("Carousel", "per-view", "一次看几张", "SlidesPerView 是一次看几张（2.5：露出下一张的一半提示还能滑），Gap 是张与张的间距；不循环（Loop=false）时到头那一侧的按钮不可用。", Products),
    ];

    static readonly (string Code, string Title)[] Parts =
    [
        ("DWG-1021", "显示器支架"),
        ("DWG-1034", "检修口盖板"),
        ("DWG-1047", "机柜导轨"),
        ("DWG-1052", "线束卡箍"),
        ("DWG-1066", "加强筋"),
        ("DWG-1078", "端盖"),
    ];

    static Uri Drawing(string code) => new($"drawings/{code}.png", UriKind.Relative);

    /// <summary>3:2 的图纸，整张放进去。</summary>
    static Control Picture((string Code, string Title) part, string alt) => new AspectRatioControl
    {
        Ratio = 1.5,
        Controls = { new ImageControl { Source = Drawing(part.Code), Alt = alt, Fit = ImageFit.Contain } },
    };

    // 图纸轮播：每 5 秒换一张，指针停在上面或焦点进来时暂停
    static Control Drawings()
    {
        var carousel = new CarouselControl { AccessibleName = "零件图纸", Autoplay = CarouselControl.DefaultAutoplay, Dock = DockStyle.Top };
        foreach (var part in Parts) carousel.Slides.Add(Picture(part, $"{part.Title}（{part.Code}）"));
        return carousel;
    }

    // 图片组：下面一排缩略图点了跳过去，滑动大图时缩略图跟着高亮
    static Control Thumbnails()
    {
        var carousel = new CarouselControl { AccessibleName = "图纸", Dots = false };
        foreach (var part in Parts) carousel.Slides.Add(Picture(part, part.Title));
        var caption = new SmallControl { Tone = TextTone.Muted };
        var thumbs = new List<Panel>();
        for (var index = 0; index < Parts.Length; index++)
        {
            var page = index;
            var thumb = new ImageControl { Source = Drawing(Parts[index].Code), Alt = Parts[index].Title, Cursor = Cursors.Hand, Dock = DockStyle.Fill };
            thumb.Click += (_, _) => carousel.Index = page;
            // 外面一圈 2 像素的框：当前的那张是强调色（WinForms 的控件不能半透明，不用透明度区分）
            var frame = new Panel { Padding = new Padding(2), Controls = { thumb } };
            frame.Size = new Size(frame.LogicalToDeviceUnits(76), frame.LogicalToDeviceUnits(52));
            thumbs.Add(frame);
        }
        void Sync()
        {
            caption.Text = $"{Parts[carousel.Index].Title}（{carousel.Index + 1} / {Parts.Length}）";
            for (var index = 0; index < thumbs.Count; index++)
                thumbs[index].BackColor = index == carousel.Index ? ProviderControl.ThemeOf(carousel).Colors.Accent : Color.Transparent;
        }
        carousel.IndexChanged += (_, _) => Sync();
        carousel.ParentChanged += (_, _) => Sync();
        Sync();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Dock = DockStyle.Top },
            carousel, Build.Flex(new FlexControl { Gap = Space.Xs, Wrap = true }, [.. thumbs]), caption);
    }

    static readonly string[] Prices = ["¥ 128.00", "¥ 46.50", "¥ 312.00", "¥ 18.90", "¥ 75.00", "¥ 240.00"];

    // 一次看两张半：露出下一张的一半提示还能滑；不循环，到头时那一侧的按钮不可用
    static Control Products()
    {
        var carousel = new CarouselControl { AccessibleName = "推荐零件", SlidesPerView = 2.5, Gap = 16, Loop = false, Dots = false, Dock = DockStyle.Top };
        for (var index = 0; index < Parts.Length; index++)
        {
            var card = new CardControl { CardPadding = Space.Sm, AutoSize = true };
            card.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch, Dock = DockStyle.Top },
                Picture(Parts[index], Parts[index].Title),
                new TextControl { Text = Parts[index].Title, Weight = TextWeight.Medium },
                Build.Flex(new FlexControl { Justify = FlexJustify.Between },
                    new SmallControl { Text = Parts[index].Code, Tone = TextTone.Muted }, new SmallControl { Text = Prices[index] })));
            carousel.Slides.Add(card);
        }
        return carousel;
    }
}
