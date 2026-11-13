using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>ValueChange：原值划掉、箭头、新值，读屏念"原值 改为 新值"；没有原值时只有新值。</summary>
public class ValueChangeTests
{
    static void Show(ValueChange change)
    {
        var host = new StackPanel { Children = { change } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
    }

    static Text TextIn(ValueChange change, string part) =>
        WpfUi.Descendants(WpfUi.Descendants(change).OfType<Border>().First(border => border.Name == part)).OfType<Text>().Single();

    [Fact]
    [Upstream("ValueChange", "有原值：del、改为、ins")]
    public Task Before_is_struck_out_and_read_as_changed_to() => WpfUi.Run(() =>
    {
        var change = new ValueChange { Before = "1280", After = "1281" };
        Show(change);

        var before = TextIn(change, "PART_Before");
        Assert.Equal(("1280", TextDecorationLocation.Strikethrough), (before.Text, before.TextDecorations[0].Location));
        Assert.Equal("1281", TextIn(change, "PART_After").Text);
        Assert.Equal(Visibility.Visible, WpfUi.Descendants(change).OfType<Icon>().Single().Visibility);
        var peer = UIElementAutomationPeer.CreatePeerForElement(change);
        Assert.Equal((AutomationControlType.Text, "1280 改为 1281"), (peer.GetAutomationControlType(), peer.GetName()));
    });

    [Fact]
    [Upstream("ValueChange", "没有原值：只有新值")]
    public Task Without_before_only_the_new_value_shows() => WpfUi.Run(() =>
    {
        var change = new ValueChange { After = "新值" };
        Show(change);

        Assert.Equal(Visibility.Collapsed, WpfUi.Descendants(change).OfType<Icon>().Single().Visibility);
        Assert.Equal("新值", UIElementAutomationPeer.CreatePeerForElement(change).GetName());
    });
}
