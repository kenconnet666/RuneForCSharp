using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>正文列表：圆点、编号、图标代替圆点；项之间的间距与字号档位；图标的部件样式。</summary>
public class ListTests
{
    static T Part<T>(ListItem item, string name) where T : FrameworkElement => (T)item.Template.FindName(name, item);

    static string? Marker(ListItem item) => Part<TextBlock>(item, "PART_Marker").Text;

    static bool MarkerShown(ListItem item) => Part<TextBlock>(item, "PART_Marker").Visibility == Visibility.Visible;

    static Icon IconPart(ListItem item) => Part<Icon>(item, "PART_Icon");

    static StackPanel Themed(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Fact]
    public Task Bullets_numbers_and_icons() => WpfUi.Run(() =>
    {
        var bullets = new List { Children = { new ListItem { Content = "一" }, new ListItem { Content = "二" } } };
        var numbers = new OrderedList { Start = 3, Children = { new ListItem { Content = "三" }, new ListItem { Content = "四" } } };
        var nested = new OrderedList { Children = { new ListItem { Content = "第一步" } } };
        var excluded = new ListItem { Icon = Lucide.X, Content = nested };
        var included = new ListItem { Content = "包含" };
        var icons = new List { Icon = Lucide.Check, Children = { included, excluded } };
        WpfUi.Show(Themed(bullets, numbers, icons));

        Assert.Equal(["•", "•"], bullets.Children.Cast<ListItem>().Select(Marker));
        Assert.Equal(["3.", "4."], numbers.Children.Cast<ListItem>().Select(Marker));
        Assert.Equal(Visibility.Visible, IconPart(included).Visibility);
        Assert.False(MarkerShown(included));
        Assert.Equal(Lucide.Check, IconPart(included).Data);
        // 逐项覆盖；有序列表里不沿用外层的图标
        Assert.Equal(Lucide.X, IconPart(excluded).Data);
        var step = (ListItem)nested.Children[0];
        Assert.Equal("1.", Marker(step));
        Assert.Equal(Visibility.Collapsed, IconPart(step).Visibility);
    });

    [Fact]
    public Task Text_is_indented_past_the_marker_and_items_keep_the_gap() => WpfUi.Run(() =>
    {
        var first = new ListItem { Content = "一" };
        var second = new ListItem { Content = "二" };
        var bullets = new List { Children = { first, second } };
        var roomy = new List { Gap = Space.Md, Size = TextSize.Lg, Children = { new ListItem { Content = "一" } } };
        var iconItem = new ListItem { Content = "包含" };
        var icons = new List { Icon = Lucide.Check, Children = { iconItem } };
        WpfUi.Show(Themed(bullets, roomy, icons));
        var em = first.FontSize;

        Assert.Equal(first.ActualHeight + 0.25 * em, second.TranslatePoint(default, bullets).Y, 3);
        Assert.Equal(16, ((ListItem)roomy.Children[0]).FontSize);
        // 记号占 1.5em，文字再隔 space.xs；带图标时不缩进，图标后隔 space.sm
        Assert.Equal(1.75 * em, Part<ContentPresenter>(first, "PART_ContentPresenter").TranslatePoint(default, first).X, 3);
        Assert.Equal(IconPart(iconItem).ActualWidth + 0.5 * em, Part<ContentPresenter>(iconItem, "PART_ContentPresenter").TranslatePoint(default, iconItem).X, 3);
    });

    [Fact]
    public Task Icon_part_style_from_the_list_resources() => WpfUi.Run(() =>
    {
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf">
              <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style x:Key="{x:Static rune:ListParts.Icon}" TargetType="rune:Icon"><Setter Property="Tone" Value="success" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <rune:List Icon="{x:Static rune:Lucide.Check}">
                  <rune:ListItem x:Name="item">不限项目</rune:ListItem>
                </rune:List>
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        WpfUi.Show(root);

        var icon = IconPart((ListItem)root.FindName("item"));
        Assert.Equal(TextTone.Success, icon.Tone);
    });

    [Fact]
    public Task Lists_and_items_are_list_elements_for_screen_readers() => WpfUi.Run(() =>
    {
        var item = new ListItem { Content = "一" };
        var list = new List { Children = { item } };
        WpfUi.Show(Themed(list));

        var peer = UIElementAutomationPeer.CreatePeerForElement(list);
        Assert.Equal(AutomationControlType.List, peer.GetAutomationControlType());
        var itemPeer = Assert.Single(peer.GetChildren());
        Assert.Equal(AutomationControlType.ListItem, itemPeer.GetAutomationControlType());
        // 读屏走控件视图：记号不在树里，装饰图标不是控件元素
        Assert.Equal(["一"], itemPeer.GetChildren().Where(child => child.IsControlElement()).Select(child => child.GetName()));
    });
}
