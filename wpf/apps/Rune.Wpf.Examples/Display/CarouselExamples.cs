using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>Carousel 的示例（vue-tsx-pro 文档站 display/Carousel）。图纸同 Image 的示例（docs/site/drawings）；代码里直接放控件，XAML 里一般写 ItemTemplate。</summary>
static class CarouselExamples
{
    public static Example[] All =>
    [
        new("Carousel", "autoplay", "自动播放", "Autoplay 是间隔（TS 的 autoplay: true 是 5 秒）：指针停在上面或键盘焦点进来时暂停，底部的按钮可以停下；拖动、Shift + 滚轮、焦点在张片区时 ← / → 也能换。系统关闭动画时一开始就是暂停的。", Drawings),
        new("Carousel", "thumbnails", "图片组与缩略图", "Index 双向：下面一排缩略图点了跳过去，滑动大图时缩略图跟着高亮。", Thumbnails),
        new("Carousel", "per-view", "一次看几张", "SlidesPerView 是一次看几张（2.5：露出下一张的一半提示还能滑），Gap 是张与张的间距；不循环（Loop=False）时到头那一侧的按钮不可用。", Products),
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
    static FrameworkElement Picture((string Code, string Title) part, string alt) => new AspectRatio
    {
        Ratio = 1.5,
        Child = new Image { Source = Drawing(part.Code), Alt = alt, Fit = ImageFit.Contain, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch },
    };

    static Carousel Named(Carousel carousel, string name)
    {
        AutomationProperties.SetName(carousel, name);
        return carousel;
    }

    // 图纸轮播：每 5 秒换一张，指针停在上面或焦点进来时暂停
    static FrameworkElement Drawings() => Named(new Carousel
    {
        ItemsSource = Parts.Select(part => Picture(part, $"{part.Title}（{part.Code}）")).ToArray(),
        Autoplay = Carousel.DefaultAutoplay,
    }, "零件图纸");

    // 图片组：Index 双向绑定，下面一排缩略图点了跳过去，滑动大图时缩略图跟着高亮
    static FrameworkElement Thumbnails()
    {
        var carousel = Named(new Carousel { ItemsSource = Parts.Select(part => Picture(part, part.Title)).ToArray(), Dots = false }, "图纸");
        var caption = new Small { Tone = TextTone.Muted };
        var thumbs = new Flex { Gap = Space.Xs, Wrap = FlexWrap.Wrap };
        for (var index = 0; index < Parts.Length; index++)
        {
            var page = index;
            var thumb = new ButtonBase { Content = new Image { Source = Drawing(Parts[index].Code), Width = 72, Height = 48 } };
            AutomationProperties.SetName(thumb, Parts[index].Title);
            thumb.Click += (_, _) => carousel.Index = page;
            thumbs.Children.Add(thumb);
        }
        void Sync()
        {
            caption.Text = $"{Parts[carousel.Index].Title}（{carousel.Index + 1} / {Parts.Length}）";
            // 当前的那张不透明，其余淡一些
            for (var index = 0; index < thumbs.Children.Count; index++) thumbs.Children[index].Opacity = index == carousel.Index ? 1 : 0.6;
        }
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Carousel.IndexProperty, typeof(Carousel)).AddValueChanged(carousel, (_, _) => Sync());
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { carousel, thumbs, caption } };
    }

    static readonly string[] Prices = ["¥ 128.00", "¥ 46.50", "¥ 312.00", "¥ 18.90", "¥ 75.00", "¥ 240.00"];

    // 一次看两张半：露出下一张的一半提示还能滑；不循环，到头时那一侧的按钮不可用
    static FrameworkElement Products() => Named(new Carousel
    {
        ItemsSource = Enumerable.Range(0, Parts.Length).Select(index => new Card
        {
            Padding = Space.Sm,
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Xs,
                Children =
                {
                    Picture(Parts[index], Parts[index].Title),
                    new Text { Text = Parts[index].Title, Weight = TextWeight.Medium },
                    new Flex { Justify = FlexJustify.Between, Children = { new Small { Text = Parts[index].Code, Tone = TextTone.Muted }, new Small { Text = Prices[index] } } },
                },
            },
        }).ToArray(),
        SlidesPerView = 2.5,
        Gap = 16,
        Loop = false,
        Dots = false,
    }, "推荐零件");
}
