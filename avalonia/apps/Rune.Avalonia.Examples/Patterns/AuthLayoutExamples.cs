using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;

namespace Rune.Avalonia.Examples;

/// <summary>AuthLayout 的示例（vue-tsx-pro 文档站 patterns/AuthLayout）。上游的 propsHero 在这里是按部件名（Border#PART_Hero）写的样式。</summary>
static class AuthLayoutExamples
{
    public static Example[] All =>
    [
        new("AuthLayout", "login", "登录页", "Hero 是品牌区，铺满它那一边；够 64rem 宽时左右分栏，窄了品牌区收成顶部一块，表单紧跟其后。不写 Hero 时表单居中、不分栏。", Login),
    ];

    static Control Login()
    {
        var layout = new AuthLayout
        {
            // 示例嵌在页面里，不占满一屏
            MinHeight = 384,
            Hero = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Md,
                Children =
                {
                    new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = Lucide.TrendingDown }, new Text { Text = "联合降本" } } },
                    new H1 { Text = "让每一笔降本都看得见", Size = TextSize.Xl },
                    new Paragraph { Text = "品牌区铺满左半边，窄屏时收成顶部一块。", Tone = TextTone.Muted },
                },
            },
            Content = new Card
            {
                Width = 384,
                Content = new Flex
                {
                    Direction = FlexDirection.Column,
                    Gap = Space.Md,
                    Children =
                    {
                        new H2 { Text = "账号登录", Size = TextSize.Lg },
                        new TextField { PlaceholderText = "工号" },
                        new TextField { Type = TextFieldType.Password, PlaceholderText = "密码" },
                        new Button { Content = "登录" },
                    },
                },
            },
        };
        // 品牌区的外观（上游 propsHero 的 sx）：accentSoft 底（跟着主题）、四周 xl（32）
        layout.Styles.Add(new Style(x => x.OfType<Border>().Name(AuthLayoutParts.Hero))
        {
            Setters = { new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Rune.Brush.AccentSoft")), new Setter(Border.PaddingProperty, new Thickness(32)) },
        });
        return layout;
    }
}
