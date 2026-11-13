namespace Rune.WinForms.Tests;

/// <summary>AvatarGroupControl：超过 Max 的折叠成"+N"（Total 优先），读屏念"还有 N 人"；头像往前叠 0.5em；"+N"的文字。</summary>
public class AvatarGroupTests
{
    static readonly string[] Names = ["张三", "李四", "王五", "赵六", "钱七"];

    static AvatarGroupControl Group(int? max = null, int? total = null, int count = 5)
    {
        var group = new AvatarGroupControl { Max = max, Total = total };
        foreach (var name in Names.Take(count)) group.Avatars.Add(new Avatar { DisplayName = name });
        return group;
    }

    [Fact]
    [Upstream("AvatarGroup", "超过 max 的折叠成“+N”，读屏念“还有 N 人”")]
    public Task Avatars_past_max_fold_into_a_count() => WinFormsUi.Run(() =>
    {
        var group = Group(max: 3);
        group.AccessibleName = "协作者";
        using var form = WinFormsUi.Show(group);
        var em = group.Font.SizeInPoints * group.DeviceDpi / 72f;

        var accessible = group.AccessibilityObject;
        Assert.Equal((AccessibleRole.Grouping, "协作者"), (accessible.Role, accessible.Name));
        Assert.Equal(["张三", "李四", "王五", "还有 2 人"], Enumerable.Range(0, accessible.GetChildCount()).Select(index => accessible.GetChild(index)!.Name));
        Assert.Equal(AccessibleRole.Graphic, accessible.GetChild(3)!.Role);
        // 往前叠 0.5em；"+N"和头像一样大
        var (first, second) = (group.Shown[0].Bounds, group.Shown[1].Bounds);
        Assert.Equal(first.Right - (int)MathF.Round(0.5f * em), second.Left);
        Assert.Equal(first.Size, group.MoreBounds.Size);
        Assert.Equal(group.MoreBounds.Right, group.Width); // 大小跟着内容
    });

    [Fact]
    [Upstream("AvatarGroup", "propsMore、propsMoreText 落到“+N”与里面的文字上；文字仍对读屏隐藏")]
    public Task The_count_text_is_painted_and_read_as_a_sentence() => WinFormsUi.Run(() =>
    {
        var group = Group(max: 1, count: 2);
        using var form = WinFormsUi.Show(group);

        Assert.Equal("+1", group.MoreText.Content); // 画出来的文字
        Assert.Equal("还有 1 人", group.AccessibilityObject.GetChild(1)!.Name); // 读屏念完整的一句，不念"+1"
    });

    [Fact]
    [Upstream("AvatarGroup", "total 给了时按它算；没超过 max 时不折叠")]
    public Task Total_counts_the_rest_and_no_count_until_max_is_passed() => WinFormsUi.Run(() =>
    {
        var partial = Group(max: 3, total: 40, count: 4);
        var all = Group(max: 3, count: 1);
        using var form = WinFormsUi.Show(all, partial);

        Assert.Equal("+37", partial.MoreText.Content);
        Assert.Equal(1, all.AccessibilityObject.GetChildCount());
        Assert.True(all.MoreBounds.IsEmpty);
    });
}
