using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;

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
        Assert.Equal(TextDecorations.Underline, link.TextDecorations);
        Assert.NotNull(external.IconPart);
        Assert.Same(external.IconPart, ((InlineUIContainer)external.Inlines.LastInline).Child); // 图标在文字后面
        typeof(Hyperlink).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(link, null);
        Assert.Equal(new Uri("https://example.com/docs"), Assert.Single(link.Opened));

        var peer = ContentElementAutomationPeer.CreatePeerForElement(external);
        Assert.Equal(AutomationControlType.Hyperlink, peer.GetAutomationControlType());
        Assert.EndsWith(Provider.GetLang(text).LinkNewWindow, peer.GetName());
    });
}
