using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>UserButton：头像 + 名字 + 箭头；有说明时多一行、占满一行、换成上下双箭头；Compact 只剩头像；不要箭头。</summary>
public class UserButtonTests
{
    static StackPanel Themed(params UIElement[] children)
    {
        var host = new StackPanel { Width = 300 };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static T Part<T>(UserButton button, string name) where T : FrameworkElement => WpfUi.Descendants(button).OfType<T>().Single(e => e.Name == name);

    [Upstream("UserButton", "头像、名字、箭头；名称来自看得见的名字")]
    [Fact]
    public Task Avatar_name_and_indicator_and_the_name_is_the_accessible_name() => WpfUi.Run(() =>
    {
        var button = new UserButton { DisplayName = "林小满" };
        WpfUi.Show(Themed(button));

        Assert.Equal("林小满", Part<Text>(button, "PART_Name").Text);
        Assert.Equal("林小满", Part<Avatar>(button, "PART_Avatar").DisplayName);
        Assert.Equal(Visibility.Visible, Part<Icon>(button, "PART_Indicator").Visibility);
        Assert.Equal(Lucide.ChevronDown, Part<Icon>(button, "PART_Indicator").Data);
        Assert.Equal(Visibility.Collapsed, Part<Text>(button, "PART_Description").Visibility);
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        Assert.Equal((AutomationControlType.Button, "林小满"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.True(button.ActualWidth < 300); // 胶囊：按内容的宽
        Assert.True(button.CornerRadius.TopLeft >= button.ActualHeight / 2 - 0.5, $"{button.CornerRadius.TopLeft} / {button.ActualHeight}");
        Assert.Equal(0.93 * button.FontSize, Part<Text>(button, "PART_Name").FontSize, 6);
    });

    [Upstream("UserButton", "有说明时多一行；compact 只剩头像，名字成了 aria-label")]
    [Fact]
    public Task A_description_spans_the_row_and_compact_keeps_only_the_avatar() => WpfUi.Run(() =>
    {
        var full = new UserButton { DisplayName = "林小满", Description = "采购部 · 管理员" };
        var compact = new UserButton { DisplayName = "周一航", Compact = true };
        WpfUi.Show(Themed(full, compact));

        Assert.Equal("采购部 · 管理员", Part<Text>(full, "PART_Description").Text);
        Assert.Equal(Visibility.Visible, Part<Text>(full, "PART_Description").Visibility);
        Assert.Equal(Lucide.ChevronsUpDown, Part<Icon>(full, "PART_Indicator").Data);
        Assert.Equal(300, full.ActualWidth); // 有说明：占满一行
        Assert.Equal("采购部 · 管理员", UIElementAutomationPeer.CreatePeerForElement(full).GetHelpText());

        Assert.Equal(Visibility.Collapsed, Part<StackPanel>(compact, "PART_Text").Visibility);
        Assert.Equal(Visibility.Collapsed, Part<Icon>(compact, "PART_Indicator").Visibility);
        Assert.Equal("周一航", UIElementAutomationPeer.CreatePeerForElement(compact).GetName());
        Assert.Equal(compact.Padding.Left, compact.Padding.Right); // 四周一样
    });

    [Upstream("UserButton", "indicator={false} 不要箭头；部件属性合并到对应的节点")]
    [Fact]
    public Task ShowIndicator_false_hides_the_arrow_and_a_custom_icon_replaces_it() => WpfUi.Run(() =>
    {
        var none = new UserButton { DisplayName = "林小满", ShowIndicator = false };
        var custom = new UserButton { DisplayName = "林小满", Indicator = Lucide.Settings };
        WpfUi.Show(Themed(none, custom));

        Assert.Equal(Visibility.Collapsed, Part<Icon>(none, "PART_Indicator").Visibility);
        Assert.Equal(Lucide.Settings, Part<Icon>(custom, "PART_Indicator").Data);
    });

    [Fact]
    [Upstream("UserButton", "当菜单的触发器")]
    public Task As_a_menu_trigger_it_reports_its_state() => WpfUi.Run(() =>
    {
        var user = new UserButton { DisplayName = "林小满" };
        var menu = new Menu { Trigger = user, Items = { new MenuItem { Content = "退出登录" } } };
        var host = new System.Windows.Controls.StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(menu);
        WpfUi.Show(host);
        IExpandCollapseProvider Expand(UserButton button) => (IExpandCollapseProvider)button.Peer().GetPattern(PatternInterface.ExpandCollapse);

        Assert.Equal(ExpandCollapseState.Collapsed, Expand(user).ExpandCollapseState); // aria-expanded="false"
        menu.IsOpen = true;
        WpfUi.Flush();
        Assert.Equal(ExpandCollapseState.Expanded, Expand(user).ExpandCollapseState);
        menu.IsOpen = false;
        Assert.Null(new UserButton().Peer().GetPattern(PatternInterface.ExpandCollapse)); // 不是触发器时没有展开模式
    });
}
