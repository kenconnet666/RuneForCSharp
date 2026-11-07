using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>IconButton 的示例（vue-tsx-pro 文档站 base/IconButton）。states 要等 Tooltip 与 focusableWhenDisabled，presets 要等 TextField。</summary>
static class IconButtonExamples
{
    public static Example[] All =>
    [
        new("IconButton", "toolbar", "基础用法", "宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色）；外观选项同 Button。没有文字，必须写 AutomationProperties.Name，读屏才知道它是做什么的。", () => new IconButtonToolbar()),
        new("IconButton", "links", "图标链接", "点了去别处的图标用 IconLinkButton：读屏念\"链接\"；桌面没有站内地址，Href 写绝对地址、用系统打开，External 显示在新窗口打开并告诉读屏。", Links),
    ];

    static Control Links() => new Flex
    {
        // 顶层的一行贴着上边：面板默认被宿主拉满（Flex 的说明）
        Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = true, VerticalAlignment = VerticalAlignment.Top,
        Children =
        {
            new Text { Text = "© 2026 vue-tsx-pro", Size = TextSize.Sm, Tone = TextTone.Muted },
            new Flex
            {
                Gap = Space.Xs,
                Children =
                {
                        Link("https://example.com/guide/getting-started", false, Lucide.BookOpen, "文档"),
                        Link("https://example.com/feed.xml", true, Lucide.Rss, "订阅"),
                        Link("https://youtube.com", true, Lucide.Video, "视频教程"),
                        Link("mailto:hi@example.com", false, Lucide.Mail, "发邮件"),
                },
            },
        },
    };

    static IconLinkButton Link(string href, bool external, IconData icon, string name)
    {
        var link = new IconLinkButton { Href = new Uri(href), External = external, Icon = icon };
        AutomationProperties.SetName(link, name);
        return link;
    }
}

/// <summary>IconButton/toolbar。</summary>
public partial class IconButtonToolbar : UserControl
{
    /// <summary>加载示例。</summary>
    public IconButtonToolbar() => InitializeComponent();
}
