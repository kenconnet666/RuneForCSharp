using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Card：页头（含 extra）、正文、页脚；加载中正文换成骨架，页头照常；读屏的项状态是加载中。</summary>
public class CardTests
{
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    static StackPanel Themed(params UIElement[] children)
    {
        var host = new StackPanel { Width = 300 };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static IEnumerable<string> Texts(DependencyObject root) => Descendants(root).OfType<TextBlock>().Where(text => text.IsVisible).Select(text => text.Text);

    [Upstream("Card", "页头、正文、页脚各一层")]
    [Upstream("Card", "Card 的 extra 和 header 在同一个页头里")]
    [Fact]
    public Task Header_body_and_footer_are_separate_parts() => WpfUi.Run(() =>
    {
        var full = new Card { Header = "标题", Extra = "全部", Footer = "操作", Content = "内容" };
        var plain = new Card { Content = "内容" };
        var onlyExtra = new Card { Extra = "操作", Content = "内容" };
        WpfUi.Show(Themed(full, plain, onlyExtra));

        Assert.Equal(["标题", "全部", "内容", "操作"], Texts(full));
        Assert.Equal(["内容"], Texts(plain)); // 没有页头页脚时只有正文
        Assert.Equal(["操作", "内容"], Texts(onlyExtra)); // 只有 extra 也有页头
        Assert.Equal(((SolidColorBrush)full.FindResource("Rune.Brush.Surface")).Color, ((SolidColorBrush)full.Background!).Color);
        // 阴影层与表面叠在一起（阴影在后面）
        var layers = Descendants(full).OfType<Border>().Take(2).ToList();
        Assert.Equal(layers[0].TranslatePoint(default, full), layers[1].TranslatePoint(default, full));
        Assert.Equal(layers[0].RenderSize, layers[1].RenderSize);
    });

    [Upstream("Card", "loading：正文换成对读屏隐藏的骨架")]
    [Fact]
    public Task Provider_part_styles_reach_the_skeleton() => WpfUi.Run(() =>
    {
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf" Width="300">
              <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style x:Key="{x:Static rune:CardParts.Skeleton}" TargetType="rune:SkeletonText"><Setter Property="Lines" Value="4" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <rune:Card x:Name="card" Header="标题" IsLoading="True">内容</rune:Card>
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        WpfUi.Show(root);
        var card = (Card)root.FindName("card");

        // 一开始就在加载：立即显示骨架（不等防闪烁的延迟），页头照常显示
        Assert.True(card.SkeletonShown);
        Assert.Equal(4, Descendants(card).OfType<SkeletonText>().Single().Children.Count);
        Assert.Equal(["标题"], Texts(card));
        Assert.Equal(Provider.GetLang(card).Loading, UIElementAutomationPeer.CreatePeerForElement(card).GetItemStatus());

        card.IsLoading = false;
        WpfUi.Wait(TimeSpan.FromMilliseconds(600));
        WpfUi.Flush();
        Assert.False(card.SkeletonShown);
        Assert.Equal(["标题", "内容"], Texts(card));
    });

    [Upstream("Card", "骨架换回正文时正文淡入")]
    [Fact]
    public Task Content_fades_in_only_after_a_skeleton_was_shown() => WpfUi.Run(() =>
    {
        var card = new Card { Content = "内容" };
        WpfUi.Show(Themed(card));
        Assert.False(card.Revealing); // 一开始就有正文：不淡入

        card.IsLoading = true;
        WpfUi.Wait(TimeSpan.FromMilliseconds(300));
        Assert.True(card.SkeletonShown);
        card.IsLoading = false;
        WpfUi.Wait(TimeSpan.FromMilliseconds(600));
        Assert.False(card.SkeletonShown);
        Assert.True(card.Revealing); // 出现过骨架：换回正文时淡入
    });
}
