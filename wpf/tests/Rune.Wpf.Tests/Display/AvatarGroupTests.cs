using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>AvatarGroup：超过 Max 的折叠成"+N"（Total 优先），读屏念"还有 N 人"；头像往前叠 0.5em；"+N"的部件。</summary>
public class AvatarGroupTests
{
    static readonly string[] Names = ["张三", "李四", "王五", "赵六", "钱七"];

    static AvatarGroup Group(int? max = null, int? total = null, int count = 5)
    {
        var group = new AvatarGroup { Max = max, Total = total };
        foreach (var name in Names.Take(count)) group.Children.Add(new Avatar { DisplayName = name });
        return group;
    }

    static void Show(params AvatarGroup[] groups)
    {
        var host = new StackPanel();
        foreach (var group in groups) host.Children.Add(group);
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
    }

    [Fact]
    [Upstream("AvatarGroup", "超过 max 的折叠成“+N”，读屏念“还有 N 人”")]
    public Task Avatars_past_max_fold_into_a_count() => WpfUi.Run(() =>
    {
        var group = Group(max: 3);
        AutomationProperties.SetName(group, "协作者");
        Show(group);

        var peer = UIElementAutomationPeer.CreatePeerForElement(group);
        Assert.Equal((AutomationControlType.Group, "协作者"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal(["张三", "李四", "王五", "还有 2 人"], peer.GetChildren().Select(child => child.GetName())); // 外圈不挡住读屏
        Assert.Same(group.More, group.Shown[^1]);
        Assert.Equal(AutomationControlType.Image, UIElementAutomationPeer.CreatePeerForElement(group.More).GetAutomationControlType());
        // 往前叠 0.5em：第二个头像的左边在第一个右边往回 0.5em
        var (first, second) = ((FrameworkElement)group.Shown[0], (FrameworkElement)group.Shown[1]);
        Assert.Equal(first.TranslatePoint(new Point(first.ActualWidth, 0), group).X - 0.5 * group.FontSize, second.TranslatePoint(default, group).X, 3);
        Assert.Equal(first.ActualWidth, group.More.ActualWidth, 3); // "+N"和头像一样大
    });

    [Fact]
    [Upstream("AvatarGroup", "propsMore、propsMoreText 落到“+N”与里面的文字上；文字仍对读屏隐藏")]
    public Task The_count_has_parts_and_its_text_is_hidden_from_screen_readers() => WpfUi.Run(() =>
    {
        var group = Group(max: 1, count: 2);
        Show(group);

        Assert.Equal("PART_More", group.More.Name);
        var text = Assert.IsType<Text>(group.More.Child);
        Assert.Equal(("PART_MoreText", "+1"), (text.Name, text.Text));
        Assert.Null(UIElementAutomationPeer.CreatePeerForElement(group.More).GetChildren());
    });

    [Fact]
    [Upstream("AvatarGroup", "total 给了时按它算；没超过 max 时不折叠")]
    public Task Total_counts_the_rest_and_no_count_until_max_is_passed() => WpfUi.Run(() =>
    {
        var partial = Group(max: 3, total: 40, count: 4);
        var all = Group(max: 3, count: 1);
        Show(partial, all);

        Assert.Equal("+37", ((Text)partial.More.Child).Text);
        Assert.Single(all.Shown);
    });
}
