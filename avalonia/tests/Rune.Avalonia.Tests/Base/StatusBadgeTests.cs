using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>StatusBadge：名字与色调取自状态映射，不认识的状态显示 key；没有图标的状态前画圆点。</summary>
public class StatusBadgeTests
{
    static readonly StatusMap Statuses = new()
    {
        ["draft"] = new("草稿", StatusTone.Neutral),
        ["approved"] = new("已通过", StatusTone.Success),
        ["shipped"] = new("已发货", StatusTone.Info, Lucide.Truck),
    };

    [Upstream("Badge", "StatusBadge：名字与色调取自状态映射")]
    [Fact]
    public Task Label_and_tone_come_from_the_status_map() => HeadlessUi.Run(() =>
    {
        var (draft, approved, unknown, shipped) = (new StatusBadge { Statuses = Statuses, Value = "draft" }, new StatusBadge { Statuses = Statuses, Value = "approved" },
            new StatusBadge { Statuses = Statuses, Value = "archived" }, new StatusBadge { Statuses = Statuses, Value = "shipped" });
        HeadlessUi.Show(new StackPanel { Children = { draft, approved, unknown, shipped } });

        Assert.Equal(("草稿", StatusTone.Neutral), ((string?)draft.Content, draft.Tone));
        Assert.Equal(("已通过", StatusTone.Success), ((string?)approved.Content, approved.Tone));
        Assert.Equal(("archived", StatusTone.Neutral), ((string?)unknown.Content, unknown.Tone)); // 不认识的：中性、名字就是 key
        Assert.True(draft.Dot);
        Assert.Contains(draft.GetLogicalChildren(), child => child is Border { Name: "PART_Dot" });
        Assert.DoesNotContain(shipped.GetLogicalChildren(), child => child is Border { Name: "PART_Dot" }); // 有图标时不画圆点
        Assert.Equal(1, Statuses.Order("approved"));
        Assert.Equal(-1, Statuses.Order("archived"));
    });
}
