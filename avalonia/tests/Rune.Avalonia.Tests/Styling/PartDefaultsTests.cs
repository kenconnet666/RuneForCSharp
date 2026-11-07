using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>
/// 代码组合控件的部件默认外观写在主题样式里（DESIGN §7）：默认值在，使用方实例的部件样式盖得过。
/// 以前写成本地值或模板优先级时，使用方的样式盖不过。
/// </summary>
public class PartDefaultsTests
{
    /// <summary>给实例加一条部件样式（TS 的 propsXxx）。</summary>
    static TOwner Styled<TOwner, TPart>(TOwner owner, string name, AvaloniaProperty property, object value) where TOwner : Control where TPart : Control
    {
        owner.Styles.Add(new Style(x => x.OfType<TPart>().Name(name)) { Setters = { new Setter(property, value) } });
        return owner;
    }

    [Fact]
    public Task Empty_icon_title_and_description() => HeadlessUi.Run(() =>
    {
        var plain = new Empty { Title = "还没有订单", Description = "下单后会显示在这里。" };
        var styled = Styled<Empty, Icon>(new Empty { Title = "还没有订单" }, EmptyParts.Icon, Icon.ToneProperty, TextTone.Danger);
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });

        Assert.Equal(TextTone.Muted, plain.Part<Icon>(EmptyParts.Icon).Tone);
        Assert.Equal(1.5, plain.Part<Icon>(EmptyParts.Icon).StrokeWidth); // strokeWidths.sm
        Assert.Equal(TextWeight.Medium, plain.Part<Text>(EmptyParts.Title).Weight);
        Assert.Equal(TextTone.Muted, plain.Part<Paragraph>(EmptyParts.Description).Tone);
        Assert.Equal(TextTone.Danger, styled.Part<Icon>(EmptyParts.Icon).Tone);
    });

    [Fact]
    public Task Indicator_badge_and_count() => HeadlessUi.Run(() =>
    {
        var plain = new Indicator { Count = 3, Content = new Border { Width = 20, Height = 20 } };
        var styled = Styled<Indicator, Border>(new Indicator { Count = 3, Content = new Border { Width = 20, Height = 20 } }, IndicatorParts.Badge, Border.BackgroundProperty, Brushes.Purple);
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });

        Assert.Same(plain.Palette!.Solid, plain.Part<Border>(IndicatorParts.Badge).Background);
        var count = plain.Part<TextBlock>(IndicatorParts.Count);
        Assert.Equal((FontWeight.SemiBold, TextAlignment.Center), (count.FontWeight, count.TextAlignment));
        Assert.Same(plain.Palette.Contrast, count.Foreground);
        Assert.Same(Brushes.Purple, styled.Part<Border>(IndicatorParts.Badge).Background);
    });

    [Fact]
    public Task Avatar_image_and_fallback() => HeadlessUi.Run(() =>
    {
        var plain = new Avatar { DisplayName = "吴悠" };
        var styled = Styled<Avatar, Image>(new Avatar { DisplayName = "吴悠" }, AvatarParts.Image, Image.StretchProperty, Stretch.Uniform);
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });

        Assert.Equal(Stretch.UniformToFill, plain.Part<Image>(AvatarParts.Image).Stretch);
        Assert.Equal(global::Avalonia.Layout.HorizontalAlignment.Center, plain.Part<ContentPresenter>(AvatarParts.Fallback).HorizontalAlignment);
        Assert.Equal(Stretch.Uniform, styled.Part<Image>(AvatarParts.Image).Stretch);
    });

    [Fact]
    public Task Card_header_and_footer_dividers() => HeadlessUi.Run(() =>
    {
        var plain = new Card { Header = "页头", Footer = "页脚", Content = "正文" };
        var styled = Styled<Card, Border>(new Card { Header = "页头", Content = "正文" }, CardParts.Header, Border.BorderThicknessProperty, new Thickness(0));
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });

        Assert.Equal(new Thickness(0, 0, 0, 1), plain.Part<Border>(CardParts.Header).BorderThickness);
        Assert.Equal(new Thickness(0, 1, 0, 0), plain.Part<Border>(CardParts.Footer).BorderThickness);
        Assert.NotNull(plain.Part<Border>(CardParts.Header).BorderBrush);
        Assert.Equal(new Thickness(0), styled.Part<Border>(CardParts.Header).BorderThickness);
    });

    [Fact]
    public Task Separator_lines_follow_the_separator() => HeadlessUi.Run(() =>
    {
        var plain = new Separator { Content = "或" };
        var styled = Styled<Separator, Border>(new Separator { Content = "或" }, SeparatorParts.Line, Border.BackgroundProperty, Brushes.Red);
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { plain, styled } });

        var line = plain.Part<Border>(SeparatorParts.Line);
        Assert.Same(plain.Background, line.Background);
        Assert.Equal(plain.Thickness, line.Height);
        Assert.Same(Brushes.Red, styled.Part<Border>(SeparatorParts.Line).Background);
    });

    [Fact]
    public Task Badge_dot_follows_the_text_color() => HeadlessUi.Run(() =>
    {
        var plain = new Badge { Dot = true, Content = "运行中" };
        var styled = Styled<Badge, Border>(new Badge { Dot = true, Content = "运行中" }, BadgeParts.Dot, Border.BackgroundProperty, Brushes.Red);
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });

        Assert.Same(plain.Foreground, plain.Part<Border>(BadgeParts.Dot).Background);
        Assert.Same(Brushes.Red, styled.Part<Border>(BadgeParts.Dot).Background);
    });
}
