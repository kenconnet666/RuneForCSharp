using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>UserButton：头像 + 名字 + 箭头；有说明时多一行、占满一行、换成上下双箭头；Compact 只剩头像；不要箭头。</summary>
public class UserButtonTests
{
    static T Part<T>(UserButton button, string name) where T : Control => button.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);

    [Upstream("UserButton", "头像、名字、箭头；名称来自看得见的名字")]
    [Fact]
    public Task Avatar_name_and_indicator_and_the_name_is_the_accessible_name() => HeadlessUi.Run(() =>
    {
        var button = new UserButton { DisplayName = "林小满" };
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { button } });

        Assert.Equal("林小满", Part<Text>(button, UserButtonParts.Name).Text);
        Assert.Equal("林小满", Part<Avatar>(button, UserButtonParts.Avatar).DisplayName);
        Assert.True(Part<Icon>(button, UserButtonParts.Indicator).IsVisible);
        Assert.Equal(Lucide.ChevronDown, Part<Icon>(button, UserButtonParts.Indicator).Data);
        Assert.False(Part<Text>(button, UserButtonParts.Description).IsVisible);
        var peer = ControlAutomationPeer.CreatePeerForElement(button);
        Assert.Equal((AutomationControlType.Button, "林小满"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.True(button.Bounds.Width < 300); // 胶囊：按内容的宽
        Assert.True(button.CornerRadius.TopLeft > button.Bounds.Height / 2 - 0.5);
    });

    [Upstream("UserButton", "有说明时多一行；compact 只剩头像，名字成了 aria-label")]
    [Fact]
    public Task A_description_spans_the_row_and_compact_keeps_only_the_avatar() => HeadlessUi.Run(() =>
    {
        var full = new UserButton { DisplayName = "林小满", Description = "采购部 · 管理员" };
        var compact = new UserButton { DisplayName = "周一航", Compact = true };
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { full, compact } });

        Assert.Equal("采购部 · 管理员", Part<Text>(full, UserButtonParts.Description).Text);
        Assert.True(Part<Text>(full, UserButtonParts.Description).IsVisible);
        Assert.Equal(Lucide.ChevronsUpDown, Part<Icon>(full, UserButtonParts.Indicator).Data);
        Assert.Equal(300, full.Bounds.Width); // 有说明：占满一行
        Assert.Equal("采购部 · 管理员", ControlAutomationPeer.CreatePeerForElement(full).GetHelpText());

        Assert.False(Part<StackPanel>(compact, UserButtonParts.Text).IsVisible);
        Assert.False(Part<Icon>(compact, UserButtonParts.Indicator).IsVisible);
        Assert.Equal("周一航", ControlAutomationPeer.CreatePeerForElement(compact).GetName());
    });

    [Upstream("UserButton", "indicator={false} 不要箭头；部件属性合并到对应的节点")]
    [Fact]
    public Task ShowIndicator_false_hides_the_arrow_and_a_custom_icon_replaces_it() => HeadlessUi.Run(() =>
    {
        var none = new UserButton { DisplayName = "林小满", ShowIndicator = false };
        var custom = new UserButton { DisplayName = "林小满", Indicator = Lucide.Settings };
        HeadlessUi.Show(new StackPanel { Children = { none, custom } });

        Assert.False(Part<Icon>(none, UserButtonParts.Indicator).IsVisible);
        Assert.Equal(Lucide.Settings, Part<Icon>(custom, UserButtonParts.Indicator).Data);
    });
}
