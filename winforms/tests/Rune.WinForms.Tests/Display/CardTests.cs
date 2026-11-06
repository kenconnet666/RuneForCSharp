namespace Rune.WinForms.Tests;

/// <summary>CardControl：页头（文字与 Extra）、正文（子控件）、页脚；加载中正文换成骨架，读屏状态带"忙"。</summary>
public class CardTests
{
    static CardControl Show(CardControl card, out Form form)
    {
        card.Dock = DockStyle.Top;
        form = WinFormsUi.Show(card);
        card.PerformLayout();
        return card;
    }

    [Upstream("Card", "页头、正文、页脚各一层")]
    [Upstream("Card", "Card 的 extra 和 header 在同一个页头里")]
    [Fact]
    public Task Header_body_and_footer_are_stacked_with_padding() => WinFormsUi.Run(() =>
    {
        var extra = new LinkControl { Text = "全部" };
        var action = new ButtonControl { Text = "查看" };
        var content = new ParagraphControl { Text = "内容" };
        var card = new CardControl { Header = "订单 #1024", Extra = extra, Footer = action };
        card.Controls.Add(content);
        Show(card, out var form);
        using var _ = form;
        var inset = (int)MathF.Round(card.Font.SizeInPoints * card.DeviceDpi / 72f); // space.lg = 1em

        Assert.True(extra.Right > content.Right - 1); // Extra 在页头的结束边
        Assert.True(content.Top > extra.Bottom); // 正文在页头下面
        Assert.True(action.Top > content.Bottom + inset); // 页脚在正文下面，中间有内边距与分隔线
        Assert.Equal(card.ClientSize.Width - content.Right, content.Left); // 正文左右对称
        Assert.True(content.Left >= inset);
        Assert.Equal(AccessibleRole.Grouping, card.AccessibilityObject.Role);
        Assert.Equal("订单 #1024", card.AccessibilityObject.Name);
    });

    [Upstream("Card", "loading：正文换成对读屏隐藏的骨架")]
    [Fact]
    public Task Loading_swaps_the_body_for_a_skeleton() => WinFormsUi.Run(() =>
    {
        var content = new ParagraphControl { Text = "内容" };
        var card = new CardControl { Header = "标题", IsLoading = true };
        card.Skeleton.Lines = 4;
        card.Controls.Add(content);
        Show(card, out var form);
        using var _ = form;

        // 一开始就在加载：立即显示骨架（不等防闪烁的延迟）
        Assert.True(card.SkeletonShown);
        Assert.False(content.Visible);
        Assert.Equal(4, card.Skeleton.Controls.Count);
        Assert.True(card.AccessibilityObject.State.HasFlag(AccessibleStates.Busy));

        card.IsLoading = false;
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(600));
        Assert.False(card.SkeletonShown);
        Assert.True(content.Visible);
        Assert.False(card.AccessibilityObject.State.HasFlag(AccessibleStates.Busy));
    });
}
