using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>正文列表：圆点、编号、图标代替圆点；项之间的间距与字号档位。</summary>
public class ListTests
{
    static string? Marker(ListItem item) => item.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == ListItemParts.Marker).Text;

    static Icon IconPart(ListItem item) => item.GetVisualDescendants().OfType<Icon>().First(i => i.Name == ListItemParts.Icon);

    [Fact]
    public Task Bullets_numbers_and_icons() => HeadlessUi.Run(() =>
    {
        var bullets = new List { Children = { new ListItem { Content = "一" }, new ListItem { Content = "二" } } };
        var numbers = new OrderedList { Start = 3, Children = { new ListItem { Content = "三" }, new ListItem { Content = "四" } } };
        var nested = new OrderedList { Children = { new ListItem { Content = "第一步" } } };
        var excluded = new ListItem { Icon = Lucide.X, Content = nested };
        var icons = new List { Icon = Lucide.Check, Children = { new ListItem { Content = "包含" }, excluded } };
        HeadlessUi.Show(new StackPanel { Children = { bullets, numbers, icons } });

        Assert.Equal(["•", "•"], bullets.Children.Cast<ListItem>().Select(Marker));
        Assert.Equal(["3.", "4."], numbers.Children.Cast<ListItem>().Select(Marker));
        var included = (ListItem)icons.Children[0];
        Assert.True(IconPart(included).IsVisible);
        Assert.Equal(Lucide.Check, IconPart(included).Data);
        // 逐项覆盖；有序列表里不沿用外层的图标
        Assert.Equal(Lucide.X, IconPart(excluded).Data);
        var step = (ListItem)nested.Children[0];
        Assert.Equal("1.", Marker(step));
        Assert.False(IconPart(step).IsVisible);
    });

    [Fact]
    public Task Text_is_indented_past_the_marker_and_items_keep_the_gap() => HeadlessUi.Run(() =>
    {
        var bullets = new List { Children = { new ListItem { Content = "一" }, new ListItem { Content = "二" } } };
        var roomy = new List { Gap = Space.Md, Size = TextSize.Lg, Children = { new ListItem { Content = "一" } } };
        var icons = new List { Icon = Lucide.Check, Children = { new ListItem { Content = "包含" } } };
        HeadlessUi.Show(new StackPanel { Children = { bullets, roomy, icons } });
        var em = ((ListItem)bullets.Children[0]).FontSize;

        Assert.Equal(0.25 * em, bullets.Spacing, 3);
        Assert.Equal(16, ((ListItem)roomy.Children[0]).FontSize);
        Assert.Equal(0.75 * 16, roomy.Spacing, 3);
        // 记号占 1.5em，文字再隔 space.xs；带图标时不缩进，图标后隔 space.sm
        var content = bullets.Children[0].GetVisualDescendants().OfType<ContentPresenter>().First();
        Assert.Equal(1.75 * em, content.Bounds.X, 3);
        var item = (ListItem)icons.Children[0];
        var iconContent = item.GetVisualDescendants().OfType<ContentPresenter>().First();
        Assert.Equal(IconPart(item).Bounds.Width + 0.5 * em, iconContent.Bounds.X, 3);
    });

    [Fact]
    public Task Lists_and_items_are_list_elements_for_screen_readers() => HeadlessUi.Run(() =>
    {
        var item = new ListItem { Content = "一" };
        var list = new List { Children = { item } };
        HeadlessUi.Show(list);

        Assert.Equal(global::Avalonia.Automation.Peers.AutomationControlType.List, global::Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(list).GetAutomationControlType());
        Assert.Equal(global::Avalonia.Automation.Peers.AutomationControlType.ListItem, global::Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(item).GetAutomationControlType());
        Assert.Equal(item, list.GetLogicalChildren().Single());
    });
}
