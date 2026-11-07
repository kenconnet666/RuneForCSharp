using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>
/// 代码组合控件的部件默认外观写在主题的部件样式里（DESIGN §7）：默认值在，Provider 同键的部件样式叠在上面、盖得过。
/// 以前写成本地值或绑定时，部件样式盖不过。
/// </summary>
public class PartDefaultsTests
{
    /// <summary>主题下面放两份：一份原样，一份包在给了部件样式的 Provider 里。</summary>
    static (T Plain, T Styled) Show<T>(Func<T> create, PartKey key, Type target, DependencyProperty property, object value) where T : FrameworkElement
    {
        var (plain, styled) = (create(), create());
        var host = new StackPanel { Width = 300 };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(plain);
        host.Children.Add(new Provider { Styles = new ResourceDictionary { [key] = new Style(target) { Setters = { new Setter(property, value) } } }, Child = styled });
        WpfUi.Show(host);
        return (plain, styled);
    }

    static T Part<T>(DependencyObject owner, Func<T, bool>? match = null) where T : DependencyObject =>
        WpfUi.Descendants(owner).OfType<T>().First(part => match?.Invoke(part) ?? true);

    [Fact]
    public Task Empty_icon_title_and_description() => WpfUi.Run(() =>
    {
        var (plain, styled) = Show(() => new Empty { Title = "还没有订单", Description = "下单后会显示在这里。" }, EmptyParts.Icon, typeof(Icon), Icon.ToneProperty, TextTone.Danger);

        Assert.Equal(TextTone.Muted, Part<Icon>(plain).Tone);
        Assert.Equal(1.5, Part<Icon>(plain).StrokeWidth); // strokeWidths.sm
        Assert.Equal(TextWeight.Medium, Part<Text>(plain, t => t is not Paragraph).Weight);
        Assert.Equal(TextTone.Muted, Part<Paragraph>(plain).Tone);
        Assert.Equal(TextTone.Danger, Part<Icon>(styled).Tone);
    });

    [Fact]
    public Task Indicator_badge_and_count() => WpfUi.Run(() =>
    {
        var (plain, styled) = Show(() => new Indicator { Count = 3, Content = new Border { Width = 20, Height = 20 } }, IndicatorParts.Badge, typeof(Border), Border.BackgroundProperty, Brushes.Purple);

        var badge = Part<Border>(plain, b => b.Name == "PART_Badge");
        Assert.Same(plain.BadgeBackground, badge.Background);
        Assert.Equal(new Thickness(2), badge.BorderThickness);
        var count = Part<TextBlock>(plain, t => t.Name == "PART_Count");
        Assert.Equal((FontWeights.SemiBold, TextAlignment.Center), (count.FontWeight, count.TextAlignment));
        Assert.Same(plain.BadgeForeground, count.Foreground);
        Assert.Same(Brushes.Purple, Part<Border>(styled, b => b.Name == "PART_Badge").Background);
    });

    [Fact]
    public Task Avatar_image_and_fallback() => WpfUi.Run(() =>
    {
        var (plain, styled) = Show(() => new Avatar { DisplayName = "吴悠" }, AvatarParts.Image, typeof(Image), Image.StretchProperty, Stretch.Uniform);

        Assert.Equal(Stretch.UniformToFill, Part<Image>(plain).Stretch);
        Assert.Equal(HorizontalAlignment.Center, Part<ContentPresenter>(plain).HorizontalAlignment);
        Assert.Equal(Stretch.Uniform, Part<Image>(styled).Stretch);
    });

    [Fact]
    public Task Card_header_and_footer_dividers() => WpfUi.Run(() =>
    {
        var (plain, styled) = Show(() => new Card { Header = "页头", Footer = "页脚", Content = "正文" }, CardParts.Header, typeof(Border), Border.BorderThicknessProperty, new Thickness(0));

        var borders = WpfUi.Descendants(plain).OfType<Border>().ToList();
        Assert.Contains(borders, b => b.BorderThickness == new Thickness(0, 0, 0, 1) && b.BorderBrush is not null);
        Assert.Contains(borders, b => b.BorderThickness == new Thickness(0, 1, 0, 0));
        Assert.DoesNotContain(WpfUi.Descendants(styled).OfType<Border>(), b => b.BorderThickness == new Thickness(0, 0, 0, 1));
    });

    [Fact]
    public Task Separator_lines_follow_the_separator() => WpfUi.Run(() =>
    {
        var (plain, styled) = Show(() => new Separator { Content = "或" }, SeparatorParts.Line, typeof(Border), Border.BackgroundProperty, Brushes.Red);

        var line = Part<Border>(plain);
        Assert.Same(plain.Background, line.Background);
        Assert.Equal(plain.Thickness, line.Height);
        Assert.Same(Brushes.Red, Part<Border>(styled).Background);
    });

    [Fact]
    public Task Badge_dot_follows_the_text_color_and_text_is_medium() => WpfUi.Run(() =>
    {
        var (plain, styled) = Show(() => new Badge { Dot = true, Content = "运行中" }, BadgeParts.Dot, typeof(Border), Border.BackgroundProperty, Brushes.Red);

        Assert.Same(plain.Foreground, Part<Border>(plain, b => b.Width > 0).Background);
        Assert.Equal(FontWeights.Medium, Part<Text>(plain).FontWeight); // 字重来自 Badge 的主题，不写在文字上
        Assert.Same(Brushes.Red, Part<Border>(styled, b => b.Width > 0).Background);
    });
}
