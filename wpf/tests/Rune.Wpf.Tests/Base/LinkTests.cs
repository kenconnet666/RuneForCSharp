using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Link：行内的超链接，默认品牌色、总有下划线；点击用系统打开；外部链接末尾有图标、读屏补"在新窗口打开"。</summary>
public class LinkTests
{
    sealed class RecordingLink : Link
    {
        public RecordingLink() => DefaultStyleKey = typeof(Link);


        public List<Uri> Opened { get; } = [];

        protected override void Navigate(Uri href) => Opened.Add(href);
    }

    [Upstream("Link", "普通链接")]
    [Upstream("Link", "外部链接")]
    [Fact]
    public Task Opens_with_the_system_and_external_links_say_so() => WpfUi.Run(() =>
    {
        var link = new RecordingLink { Href = new Uri("https://example.com/docs") };
        link.Inlines.Add(new Run("文档"));
        var external = new RecordingLink { Href = new Uri("https://vuejs.org"), External = true };
        external.Inlines.Add(new Run("Vue 文档"));
        var text = new Paragraph();
        text.Inlines.AddRange(new Inline[] { link, new Run(" 与 "), external });
        var host = new StackPanel { Children = { text } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);

        Assert.Same(host.FindResource("Rune.Brush.AccentText"), link.Foreground);
        Assert.Equal(TextDecorationLocation.Underline, Assert.Single(link.TextDecorations).Location);
        // 下划线平时是半透明的文字色（悬停时实色）
        var stroke = (SolidColorBrush)Assert.Single(link.TextDecorations).Pen.Brush;
        Assert.Equal((((SolidColorBrush)link.Foreground).Color, 0.5), (stroke.Color, stroke.Opacity));
        Assert.NotNull(external.IconPart);
        Assert.Same(external.IconPart, ((InlineUIContainer)external.Inlines.LastInline).Child); // 图标在文字后面
        typeof(Hyperlink).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(link, null);
        Assert.Equal(new Uri("https://example.com/docs"), Assert.Single(link.Opened));

        var peer = ContentElementAutomationPeer.CreatePeerForElement(external);
        Assert.Equal(AutomationControlType.Hyperlink, peer.GetAutomationControlType());
        Assert.EndsWith(Provider.GetLang(text).LinkNewWindow, peer.GetName());
    });

    [Fact]
    [Upstream("Link", "下划线：容器（LinkUnderlineScope、卡片页头）给的默认是 hover")]
    public Task Containers_give_hover_underline_unless_the_link_says_otherwise() => WpfUi.Run(() =>
    {
        static (Link Link, Paragraph Text) Make(string text, LinkUnderline? underline = null)
        {
            var link = new Link();
            link.Inlines.Add(new Run(text));
            if (underline is { } value) link.Underline = value;
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(link);
            return (link, paragraph);
        }
        var body = Make("正文");
        var scoped = Make("容器里");
        var explicitAlways = Make("容器里显式", LinkUnderline.Always);
        var scope = new StackPanel { Children = { scoped.Text, explicitAlways.Text } };
        Link.SetUnderlineScope(scope, LinkUnderline.Hover);
        var header = Make("页头");
        var cardBody = Make("卡片正文");
        var host = new StackPanel { Children = { body.Text, scope, new Card { Header = header.Text, Content = cardBody.Text } } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);

        Assert.Equal(TextDecorationLocation.Underline, Assert.Single(body.Link.TextDecorations).Location);
        Assert.Equal(LinkUnderline.Hover, scoped.Link.Underline); // 容器给 hover：没指着时没有下划线
        Assert.Null(scoped.Link.TextDecorations);
        Assert.Equal(TextDecorationLocation.Underline, Assert.Single(explicitAlways.Link.TextDecorations).Location); // 显式写的优先
        Assert.Null(header.Link.TextDecorations); // 卡片页头
        Assert.Equal(TextDecorationLocation.Underline, Assert.Single(cardBody.Link.TextDecorations).Location); // 卡片正文照旧
    });
}
