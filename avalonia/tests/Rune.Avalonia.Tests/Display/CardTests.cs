using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Card：页头（含 extra）、正文、页脚；加载中正文换成骨架，页头照常；读屏的项状态是加载中。</summary>
public class CardTests
{
    static Border Part(Card card, string name) => card.GetVisualDescendants().OfType<Border>().Single(b => b.Name == name);

    /// <summary>边等真实时间边处理投递回 UI 线程的任务（DelayedLoading 用 Task.Delay 等待）。</summary>
    static void Pump(TimeSpan duration)
    {
        for (var clock = System.Diagnostics.Stopwatch.StartNew(); clock.Elapsed < duration;)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Upstream("Card", "页头、正文、页脚各一层")]
    [Upstream("Card", "Card 的 extra 和 header 在同一个页头里")]
    [Fact]
    public Task Header_body_and_footer_are_separate_parts() => HeadlessUi.Run(() =>
    {
        var full = new Card { Header = "标题", Extra = "全部", Footer = "操作", Content = "内容" };
        var plain = new Card { Content = "内容" };
        var onlyExtra = new Card { Extra = "操作", Content = "内容" };
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { full, plain, onlyExtra } });

        Assert.True(Part(full, CardParts.Header).IsVisible);
        Assert.True(Part(full, CardParts.Footer).IsVisible);
        Assert.Equal(["标题", "全部"], Part(full, CardParts.Header).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.False(Part(plain, CardParts.Header).IsVisible); // 没有页头页脚时只有正文
        Assert.False(Part(plain, CardParts.Footer).IsVisible);
        Assert.True(Part(onlyExtra, CardParts.Header).IsVisible); // 只有 extra 也有页头
        // 内边距 space.lg（1em），页头与正文之间一条分隔线
        Assert.Equal(full.FontSize, Part(full, CardParts.Body).Padding.Left, 3);
        Assert.Equal(1, Part(full, CardParts.Header).BorderThickness.Bottom);
    });

    [Upstream("Card", "loading：正文换成对读屏隐藏的骨架")]
    [Fact]
    public Task Loading_swaps_the_body_for_a_skeleton() => HeadlessUi.Run(() =>
    {
        var card = new Card { Header = "标题", Content = "内容", IsLoading = true };
        var host = new StackPanel { Width = 300, Children = { card } };
        host.Styles.Add(new Style(x => x.OfType<Card>().Descendant().OfType<SkeletonText>().Name(CardParts.Skeleton)) { Setters = { new Setter(SkeletonText.LinesProperty, 4) } });
        HeadlessUi.Show(host);

        // 一开始就在加载：立即显示骨架（不等防闪烁的延迟）
        Assert.True(card.SkeletonShown);
        var skeleton = card.GetVisualDescendants().OfType<SkeletonText>().Single();
        Assert.Equal(4, skeleton.Children.Count);
        Assert.Equal(Provider.GetLang(card).Loading, ControlAutomationPeer.CreatePeerForElement(card).GetItemStatus());
        Assert.True(Part(card, CardParts.Header).IsVisible); // 页头照常显示

        card.IsLoading = false;
        Pump(TimeSpan.FromMilliseconds(600));
        Assert.False(card.SkeletonShown);
        Assert.Null(ControlAutomationPeer.CreatePeerForElement(card).GetItemStatus());
    });

    [Upstream("Card", "骨架换回正文时正文淡入")]
    [Fact]
    public Task Content_fades_in_only_after_a_skeleton_was_shown() => HeadlessUi.Run(() =>
    {
        var card = new Card { Content = "内容" };
        HeadlessUi.Show(card);
        Assert.False(card.Revealing); // 一开始就有正文：不淡入

        card.IsLoading = true;
        Pump(TimeSpan.FromMilliseconds(300));
        Assert.True(card.SkeletonShown);
        card.IsLoading = false;
        Pump(TimeSpan.FromMilliseconds(600));
        Assert.False(card.SkeletonShown);
        Assert.True(card.Revealing); // 出现过骨架：换回正文时淡入
    });

    [Fact]
    public Task Footer_layouts_fill_the_row_and_single_controls_sit_at_the_end() => HeadlessUi.Run(() =>
    {
        var row = new Flex { Justify = FlexJustify.Between, Children = { new Button { Content = "删除" }, new Button { Content = "发布" } } };
        var single = new Button { Content = "查看" };
        var (withRow, withButton) = (new Card { Footer = row, Content = "正文" }, new Card { Footer = single, Content = "正文" });
        HeadlessUi.Show(new StackPanel { Width = 400, Children = { withRow, withButton } });

        Assert.True(row.Bounds.Width > 300); // 布局容器撑满页脚（减去内边距），两端对齐由它自己排
        Assert.True(single.TranslatePoint(new Point(single.Bounds.Width, 0), withButton)!.Value.X > 350); // 单个控件靠结束边
        Assert.True(single.Bounds.Width < 100);
    });
}
