using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>
/// LinkButton、IconLinkButton：外观同 Button、IconButton，点击先 Click 再打开链接（加载中不打开），读屏是超链接，
/// 外部链接显示外链图标（写了 IconEnd 时用写的）、名称后补"在新窗口打开"。
/// </summary>
public class LinkButtonTests
{
    sealed class RecordingLinkButton : LinkButton
    {
        public List<Uri> Opened { get; } = [];

        protected override void Navigate(Uri href) => Opened.Add(href);
    }

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Fact]
    public Task Clicking_runs_click_then_opens_the_link() => WpfUi.Run(() =>
    {
        var clicks = 0;
        var link = new RecordingLinkButton { Content = "文档", Href = new Uri("https://example.com/docs") };
        link.Click += (_, _) => clicks++;
        WpfUi.Show(Host(link));
        var invoke = (IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(link).GetPattern(PatternInterface.Invoke);

        Assert.Equal((ButtonVariant.Solid, StatusTone.Accent), (link.Variant, link.Tone));
        invoke.Invoke();
        WpfUi.Flush();
        Assert.Equal((1, new Uri("https://example.com/docs")), (clicks, Assert.Single(link.Opened)));

        link.IsLoading = true;
        invoke.Invoke();
        WpfUi.Flush();
        Assert.Single(link.Opened);
    });

    [Fact]
    public Task External_links_show_the_icon_and_tell_screen_readers() => WpfUi.Run(() =>
    {
        var external = new LinkButton { Content = "Vue 文档", External = true };
        var custom = new LinkButton { Content = "下一步", External = true, IconEnd = Lucide.ArrowRight };
        var icon = new IconLinkButton { Icon = Lucide.Rss, External = true };
        AutomationProperties.SetName(icon, "订阅");
        var host = Host(external, custom, icon);
        Provider.SetLang(host, Lang.ZhCn);
        WpfUi.Show(host);

        Assert.Equal((Lucide.ArrowUpRight, Lucide.ArrowRight), (external.IconEnd, custom.IconEnd));
        var peer = UIElementAutomationPeer.CreatePeerForElement(external);
        Assert.Equal((AutomationControlType.Hyperlink, "Vue 文档（在新窗口打开）"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal("订阅（在新窗口打开）", UIElementAutomationPeer.CreatePeerForElement(icon).GetName());
        Assert.Equal((ButtonVariant.Ghost, StatusTone.Neutral), (icon.Variant, icon.Tone));
    });
}
