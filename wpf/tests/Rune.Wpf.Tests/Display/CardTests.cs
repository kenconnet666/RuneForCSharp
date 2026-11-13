using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Card：页头（含 extra）、正文、页脚；加载中正文换成骨架，页头照常；读屏的项状态是加载中。</summary>
public class CardTests
{
    static StackPanel Themed(params UIElement[] children)
    {
        var host = new StackPanel { Width = 300 };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static Border Part(Card card, PartKey key) => WpfUi.Descendants(card).OfType<Border>().Single(border => border.Style == card.FindResource(key));

    [Fact]
    public Task A_taller_card_grows_its_body_and_keeps_the_footer_at_the_bottom() => WpfUi.Run(() =>
    {
        var card = new Card { Header = "标题", Footer = "操作", Content = "内容", Height = 300 };
        WpfUi.Show(Themed(card));

        var footer = Part(card, CardParts.Footer);
        Assert.Equal(299, footer.TranslatePoint(new Point(0, footer.ActualHeight), card).Y, 0); // 页脚贴着底边（里面一像素的描边）
        Assert.True(Part(card, CardParts.Body).ActualHeight > 100); // 正文撑开
    });

    [Fact]
    public Task Padding_none_keeps_the_body_flush_but_pads_header_and_footer() => WpfUi.Run(() =>
    {
        var card = new Card { Header = "标题", Footer = "操作", Content = "内容", Padding = Space.None };
        WpfUi.Show(Themed(card));

        var em = TextElement.GetFontSize(card);
        Assert.Equal(new Thickness(0), Part(card, CardParts.Body).Padding);
        Assert.Equal(new Thickness(0.75 * em, 0.5 * em, 0.75 * em, 0.5 * em), Part(card, CardParts.Header).Padding); // 上下 sm、左右 md
        Assert.Equal(Part(card, CardParts.Header).Padding, Part(card, CardParts.Footer).Padding);
    });

    static IEnumerable<string> Texts(DependencyObject root) => WpfUi.Descendants(root).OfType<TextBlock>().Where(text => text.IsVisible).Select(text => text.Text);

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
        var layers = WpfUi.Descendants(full).OfType<Border>().Take(2).ToList();
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
        Assert.Equal(4, WpfUi.Descendants(card).OfType<SkeletonText>().Single().Children.Count);
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

    [Fact]
    public Task Footer_layouts_fill_the_row_and_single_controls_sit_at_the_end() => WpfUi.Run(() =>
    {
        var row = new Flex { Justify = FlexJustify.Between, Children = { new Button { Content = "删除" }, new Button { Content = "发布" } } };
        var single = new Button { Content = "查看" };
        var (withRow, withButton) = (new Card { Footer = row, Content = "正文" }, new Card { Footer = single, Content = "正文" });
        var host = Themed(withRow, withButton);
        host.Width = 400;
        WpfUi.Show(host);

        Assert.True(row.ActualWidth > 300); // 布局容器撑满页脚（减去内边距），两端对齐由它自己排
        Assert.True(single.TranslatePoint(new Point(single.ActualWidth, 0), withButton).X > 350); // 单个控件靠结束边
        Assert.True(single.ActualWidth < 100);
    });
}
