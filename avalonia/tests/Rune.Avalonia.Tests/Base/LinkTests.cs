using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>Link：默认品牌色文字、总有下划线；点击用系统打开（不可用时不打开）；外部链接有图标、读屏补"在新窗口打开"。</summary>
public class LinkTests
{
    sealed class RecordingLink : Link
    {
        public List<Uri> Opened { get; } = [];

        protected override Type StyleKeyOverride => typeof(Link);

        protected override void Navigate(Uri href) => Opened.Add(href);
    }

    static Point Center(Visual visual, Visual root) => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    [Upstream("Link", "普通链接")]
    [Upstream("Link", "外部链接")]
    [Fact]
    public Task Opens_with_the_system_and_external_links_say_so() => HeadlessUi.Run(() =>
    {
        var link = new RecordingLink { Content = "文档", Href = new Uri("https://example.com/docs") };
        var external = new RecordingLink { Content = "Vue 文档", Href = new Uri("https://vuejs.org"), External = true };
        var window = HeadlessUi.Show(new StackPanel { Children = { link, external } });

        Application.Current!.TryGetResource("Rune.Brush.AccentText", ThemeVariant.Light, out var accent);
        Assert.Same(accent, link.Foreground);
        Assert.Contains(":underline-always", link.Classes);
        window.MouseDown(Center(link, window), MouseButton.Left);
        window.MouseUp(Center(link, window), MouseButton.Left);
        Assert.Equal(new Uri("https://example.com/docs"), Assert.Single(link.Opened));

        var peer = ControlAutomationPeer.CreatePeerForElement(external);
        Assert.Equal(AutomationControlType.Hyperlink, peer.GetAutomationControlType());
        Assert.EndsWith(Provider.GetLang(external).LinkNewWindow, peer.GetName());

        link.IsEnabled = false;
        link.FocusableWhenDisabled = true;
        window.MouseDown(Center(link, window), MouseButton.Left);
        window.MouseUp(Center(link, window), MouseButton.Left);
        Assert.Single(link.Opened);
    });

    [Fact]
    public Task Text_is_underlined_by_the_underline_variant() => HeadlessUi.Run(() =>
    {
        var always = new Link { Content = "按钮" };
        var none = new Link { Content = "次要", Underline = LinkUnderline.None };
        // 文字加图标：里面的文字也带下划线（TextDecorations 继承）
        var mixed = new Link { Content = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Children = { new TextBlock { Text = "查看设计规范" }, new Icon { Data = Lucide.ExternalLink } } } };
        HeadlessUi.Show(new StackPanel { Children = { always, none, mixed } });
        static TextDecorationCollection? Decorations(Link link) => global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(link).OfType<TextBlock>().First().TextDecorations;

        Assert.Equal(TextDecorations.Underline, Decorations(always));
        Assert.Null(Decorations(none));
        Assert.Equal(TextDecorations.Underline, Decorations(mixed));
    });
}
