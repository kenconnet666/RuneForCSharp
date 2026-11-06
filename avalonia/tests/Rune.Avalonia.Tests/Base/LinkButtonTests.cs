using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;

namespace Rune.Avalonia.Tests;

/// <summary>
/// LinkButton、IconLinkButton：外观同 Button、IconButton，点击先 Click 再打开链接（加载中不打开），读屏是超链接，
/// 外部链接显示外链图标（写了 IconEnd 时用写的）、名称后补"在新窗口打开"。
/// </summary>
public class LinkButtonTests
{
    sealed class RecordingLinkButton : LinkButton
    {
        public List<Uri> Opened { get; } = [];

        protected override Type StyleKeyOverride => typeof(LinkButton);

        protected override void Navigate(Uri href) => Opened.Add(href);
    }

    static Point Center(Visual visual, Visual root) => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    [Fact]
    public Task Clicking_runs_click_then_opens_the_link() => HeadlessUi.Run(() =>
    {
        var clicks = 0;
        var link = new RecordingLinkButton { Content = "文档", Href = new Uri("https://example.com/docs") };
        link.Click += (_, _) => clicks++;
        var window = HeadlessUi.Show(new StackPanel { Children = { link } });
        var at = Center(link, window);

        Assert.Equal((ButtonVariant.Solid, StatusTone.Accent), (link.Variant, link.Tone));
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Assert.Equal((1, new Uri("https://example.com/docs")), (clicks, Assert.Single(link.Opened)));

        link.IsLoading = true;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Assert.Single(link.Opened);
    });

    [Upstream("LinkButton", "渲染 a")]
    [Upstream("IconLinkButton", "外部链接")]

    [Fact]
    public Task External_links_show_the_icon_and_tell_screen_readers() => HeadlessUi.Run(() =>
    {
        var external = new LinkButton { Content = "Vue 文档", External = true };
        var custom = new LinkButton { Content = "下一步", External = true, IconEnd = Lucide.ArrowRight };
        var icon = new IconLinkButton { Icon = Lucide.Rss, External = true };
        AutomationProperties.SetName(icon, "订阅");
        var host = new StackPanel { Children = { external, custom, icon } };
        Provider.SetLang(host, Lang.ZhCn);
        HeadlessUi.Show(host);

        Assert.Equal((Lucide.ArrowUpRight, Lucide.ArrowRight), (external.IconEnd, custom.IconEnd));
        var peer = ControlAutomationPeer.CreatePeerForElement(external);
        Assert.Equal((AutomationControlType.Hyperlink, "Vue 文档（在新窗口打开）"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal("订阅（在新窗口打开）", ControlAutomationPeer.CreatePeerForElement(icon).GetName());
        Assert.Equal((ButtonVariant.Ghost, StatusTone.Neutral), (icon.Variant, icon.Tone));
    });
}
