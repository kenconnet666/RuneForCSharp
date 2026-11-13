using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>AvatarGroup：超过 Max 的折叠成"+N"（Total 优先），读屏念"还有 N 人"；头像往前叠 0.5em；"+N"的部件。</summary>
public class AvatarGroupTests
{
    static readonly string[] Names = ["张三", "李四", "王五", "赵六", "钱七"];

    static AvatarGroup Group(int? max = null, int? total = null, int count = 5)
    {
        var group = new AvatarGroup { Max = max, Total = total };
        group.Children.AddRange(Names.Take(count).Select(name => new Avatar { DisplayName = name }));
        return group;
    }

    [Fact]
    [Upstream("AvatarGroup", "超过 max 的折叠成“+N”，读屏念“还有 N 人”")]
    public Task Avatars_past_max_fold_into_a_count() => HeadlessUi.Run(() =>
    {
        var group = Group(max: 3);
        AutomationProperties.SetName(group, "协作者");
        HeadlessUi.Show(new StackPanel { Children = { group } });

        var peer = group.Peer();
        Assert.Equal((AutomationControlType.Group, "协作者"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal(["张三", "李四", "王五", "还有 2 人"], peer.GetChildren().Select(child => child.GetName())); // 外圈不挡住读屏
        Assert.Equal(4, group.Shown.Count);
        Assert.Same(group.More, group.Shown[^1]);
        var more = group.More.Peer();
        Assert.Equal((AutomationControlType.Image, "还有 2 人"), (more.GetAutomationControlType(), more.GetName()));
        // 往前叠 0.5em：第二个头像的左边在第一个右边往回 0.5em
        var (first, second) = (group.Shown[0], group.Shown[1]);
        Assert.Equal(first.TranslatePoint(new Point(first.Bounds.Width, 0), group)!.Value.X - 0.5 * group.FontSize, second.TranslatePoint(default, group)!.Value.X, 3);
        Assert.Equal(first.Bounds.Width, group.More.Bounds.Width, 3); // "+N"和头像一样大
    });

    [Fact]
    [Upstream("AvatarGroup", "propsMore、propsMoreText 落到“+N”与里面的文字上；文字仍对读屏隐藏")]
    public Task The_count_has_parts_and_its_text_is_hidden_from_screen_readers() => HeadlessUi.Run(() =>
    {
        var group = Group(max: 1, count: 2);
        HeadlessUi.Show(new StackPanel { Children = { group } });

        Assert.Equal(AvatarGroupParts.More, group.More.Name);
        var text = Assert.IsType<Text>(group.More.Child);
        Assert.Equal((AvatarGroupParts.MoreText, "+1"), (text.Name, text.Text));
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(text));
        Assert.Null(group.More.Peer().GetChildren().FirstOrDefault());
    });

    [Fact]
    [Upstream("AvatarGroup", "total 给了时按它算；没超过 max 时不折叠")]
    public Task Total_counts_the_rest_and_no_count_until_max_is_passed() => HeadlessUi.Run(() =>
    {
        var partial = Group(max: 3, total: 40, count: 4);
        var all = Group(max: 3, count: 1);
        HeadlessUi.Show(new StackPanel { Children = { partial, all } });

        Assert.Equal("+37", ((Text)partial.More.Child!).Text);
        Assert.Single(all.Shown);
    });
}
