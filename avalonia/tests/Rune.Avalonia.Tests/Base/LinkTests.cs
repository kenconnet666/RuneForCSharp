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
}
