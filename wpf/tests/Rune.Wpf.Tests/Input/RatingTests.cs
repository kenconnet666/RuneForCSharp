using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Rating：单选组里每个分数一个单选项（名称"3 星"）；点选、再点清空、半星；方向键换分数；只读时是一张图，名称里总有分数。</summary>
public class RatingTests
{
    static ISelectionItemProvider Item(RatingOption option) => (ISelectionItemProvider)option.Peer().GetPattern(PatternInterface.SelectionItem);

    static void Select(RatingOption option) => Item(option).Select();

    static void Press(UIElement target, Key key) =>
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static RatingOption Option(Rating rating, double score) => rating.Options.First(option => option.Score == score);

    [Fact]
    [Upstream("Rating", "radiogroup 以 Field 的标签为名称；每颗星一个 radio，名称是“n 星”")]
    public Task Field_names_the_group_and_each_star_is_a_radio() => WpfUi.Run(() =>
    {
        var rating = new Rating();
        WpfUi.Show(Host(new Field { Label = "交付质量", Content = rating }));

        var peer = rating.Peer();
        Assert.Equal("交付质量", peer.GetName());
        Assert.Equal(["1 星", "2 星", "3 星", "4 星", "5 星"], rating.Options.Select(option => option.Peer().GetName()));
        Assert.All(rating.Options, option => Assert.Equal(AutomationControlType.RadioButton, option.Peer().GetAutomationControlType()));
        // Tab 只落在一个上：没选时第一个
        Assert.Equal([true, false, false, false, false], rating.Options.Select(KeyboardNavigation.GetIsTabStop));
    });

    [Fact]
    [Upstream("Rating", "点选写回分数；再点同一个清空；clearable={false} 时不清空")]
    public Task Picking_writes_the_score_and_picking_again_clears() => WpfUi.Run(() =>
    {
        var rating = new Rating();
        var kept = new Rating { Value = 2, Clearable = false };
        WpfUi.Show(Host(rating, kept));

        Select(Option(rating, 4));
        Assert.Equal(4, rating.Value);
        Assert.Equal([100, 100, 100, 100, 0], Enumerable.Range(1, 5).Select(rating.Filled));
        Assert.True(Item(Option(rating, 4)).IsSelected);
        Press(Option(rating, 4), Key.Space);
        Assert.Null(rating.Value);

        Press(Option(kept, 2), Key.Space);
        Assert.Equal(2, kept.Value);
    });

    [Fact]
    [Upstream("Rating", "半星：每颗星两个 radio；3.5 星时第四颗是半颗")]
    public Task Half_stars_have_two_options_each() => WpfUi.Run(() =>
    {
        var rating = new Rating { Value = 3.5, AllowHalf = true };
        WpfUi.Show(Host(rating));

        Assert.Equal(10, rating.Options.Count);
        Assert.True(Option(rating, 3.5).IsChecked);
        Assert.Equal("3.5 星", Option(rating, 3.5).Peer().GetName());
        Assert.Equal([100, 100, 100, 50, 0], Enumerable.Range(1, 5).Select(rating.Filled));
        // 半颗在起始边：3.5 在第四颗的左半边
        Assert.True(Option(rating, 3.5).TranslatePoint(new Point(), rating).X < Option(rating, 4).TranslatePoint(new Point(), rating).X);
    });

    [Fact]
    public Task Arrow_keys_move_and_wrap() => WpfUi.Run(() =>
    {
        var rating = new Rating { Value = 5 };
        WpfUi.Show(Host(rating));

        Press(Option(rating, 5), Key.Right);
        Assert.Equal(1, rating.Value);
        Press(Option(rating, 1), Key.Left);
        Assert.Equal(5, rating.Value);
        Press(Option(rating, 5), Key.Up);
        Assert.Equal(4, rating.Value);
        Assert.True(KeyboardNavigation.GetIsTabStop(Option(rating, 4)));
    });

    [Fact]
    [Upstream("Rating", "只读：一个 role=")]
    public Task Read_only_is_an_image_with_the_score() => WpfUi.Run(() =>
    {
        var rating = new Rating { Value = 4.5, IsReadOnly = true, AllowHalf = true };
        var empty = new Rating { IsReadOnly = true };
        WpfUi.Show(Host(rating, empty));

        Assert.Equal(AutomationControlType.Image, rating.Peer().GetAutomationControlType());
        Assert.Equal("4.5 星，满分 5 星", rating.Peer().GetName());
        Assert.Empty(rating.Options);
        Assert.Equal("未评分", empty.Peer().GetName());
    });

    [Fact]
    [Upstream("Rating", "只读时名称里总有分数：自己的 aria-label 后面接上，在 Field 里时标签后面接上")]
    public Task Read_only_names_keep_the_score() => WpfUi.Run(() =>
    {
        var own = new Rating { Value = 4, IsReadOnly = true };
        AutomationProperties.SetName(own, "交付质量");
        var inField = new Rating { Value = 3 };
        WpfUi.Show(Host(own, new Field { Label = "响应速度", IsReadOnly = true, Content = inField }));

        Assert.Equal("交付质量 4 星，满分 5 星", own.Peer().GetName());
        Assert.Equal("响应速度 3 星，满分 5 星", inField.Peer().GetName());
    });
}
