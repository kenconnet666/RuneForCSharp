namespace Rune.WinForms.Tests;

/// <summary>StatusBadgeControl：名字与色调取自状态映射，不认识的状态显示 key；没有图标的状态前画圆点。</summary>
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
    public Task Label_and_tone_come_from_the_status_map() => WinFormsUi.Run(() =>
    {
        var (draft, approved, unknown, shipped) = (new StatusBadgeControl { Statuses = Statuses, Value = "draft" }, new StatusBadgeControl { Statuses = Statuses, Value = "approved" },
            new StatusBadgeControl { Statuses = Statuses, Value = "archived" }, new StatusBadgeControl { Statuses = Statuses, Value = "shipped" });
        using var form = WinFormsUi.Show(draft, approved, unknown, shipped);

        Assert.Equal(("草稿", StatusTone.Neutral), (draft.Text, draft.Tone));
        Assert.Equal(("已通过", StatusTone.Success), (approved.Text, approved.Tone));
        Assert.Equal(("archived", StatusTone.Neutral), (unknown.Text, unknown.Tone)); // 不认识的：中性、名字就是 key
        Assert.True(draft.Dot);
        Assert.Equal(Lucide.Truck, shipped.Icon); // 有图标时画图标，不画圆点
        Assert.Equal("已发货", shipped.AccessibilityObject.Name);
    });
}
