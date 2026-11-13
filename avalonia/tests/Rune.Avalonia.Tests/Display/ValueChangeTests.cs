using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>ValueChange：原值划掉、箭头、新值，读屏念"原值 改为 新值"；没有原值时只有新值。</summary>
public class ValueChangeTests
{
    static Text TextIn(ValueChange change, string part) =>
        change.GetVisualDescendants().OfType<Border>().First(border => border.Name == part).GetVisualDescendants().OfType<Text>().Single();

    [Fact]
    [Upstream("ValueChange", "有原值：del、改为、ins")]
    public Task Before_is_struck_out_and_read_as_changed_to() => HeadlessUi.Run(() =>
    {
        var change = new ValueChange { Before = "1280", After = "1281" };
        HeadlessUi.Show(new StackPanel { Children = { change } });

        var before = TextIn(change, ValueChangeParts.Before);
        Assert.Equal(("1280", TextDecorationLocation.Strikethrough), (before.Text, before.TextDecorations![0].Location));
        Assert.Equal("1281", TextIn(change, ValueChangeParts.After).Text);
        Assert.True(change.GetVisualDescendants().OfType<Icon>().Single().IsVisible);
        Assert.Equal((AutomationControlType.Text, "1280 改为 1281"), (change.Peer().GetAutomationControlType(), change.Peer().GetName()));
    });

    [Fact]
    [Upstream("ValueChange", "没有原值：只有新值")]
    public Task Without_before_only_the_new_value_shows() => HeadlessUi.Run(() =>
    {
        var change = new ValueChange { After = "新值" };
        HeadlessUi.Show(new StackPanel { Children = { change } });

        Assert.False(change.GetVisualDescendants().OfType<Icon>().Single().IsVisible);
        Assert.Equal("新值", change.Peer().GetName());
    });
}
