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
    static T Part<T>(Control owner, string name) where T : Control => owner.GetLogicalDescendants().OfType<T>().First(part => part.Name == name);

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

        Assert.Equal(TextTone.Muted, Part<Icon>(plain, EmptyParts.Icon).Tone);
        Assert.Equal(1.5, Part<Icon>(plain, EmptyParts.Icon).StrokeWidth); // strokeWidths.sm
        Assert.Equal(TextWeight.Medium, Part<Text>(plain, EmptyParts.Title).Weight);
        Assert.Equal(TextTone.Muted, Part<Paragraph>(plain, EmptyParts.Description).Tone);
        Assert.Equal(TextTone.Danger, Part<Icon>(styled, EmptyParts.Icon).Tone);
    });

    [Fact]
    public Task Indicator_badge_and_count() => HeadlessUi.Run(() =>
    {
        var plain = new Indicator { Count = 3, Content = new Border { Width = 20, Height = 20 } };
        var styled = Styled<Indicator, Border>(new Indicator { Count = 3, Content = new Border { Width = 20, Height = 20 } }, IndicatorParts.Badge, Border.BackgroundProperty, Brushes.Purple);
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });

        Assert.Same(plain.Palette!.Solid, Part<Border>(plain, IndicatorParts.Badge).Background);
        var count = Part<TextBlock>(plain, IndicatorParts.Count);
        Assert.Equal((FontWeight.SemiBold, TextAlignment.Center), (count.FontWeight, count.TextAlignment));
        Assert.Same(plain.Palette.Contrast, count.Foreground);
        Assert.Same(Brushes.Purple, Part<Border>(styled, IndicatorParts.Badge).Background);
    });

    [Fact]
    public Task Avatar_image_and_fallback() => HeadlessUi.Run(() =>
    {
        var plain = new Avatar { DisplayName = "吴悠" };
        var styled = Styled<Avatar, Image>(new Avatar { DisplayName = "吴悠" }, AvatarParts.Image, Image.StretchProperty, Stretch.Uniform);
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });

        Assert.Equal(Stretch.UniformToFill, Part<Image>(plain, AvatarParts.Image).Stretch);
        Assert.Equal(global::Avalonia.Layout.HorizontalAlignment.Center, Part<ContentPresenter>(plain, AvatarParts.Fallback).HorizontalAlignment);
        Assert.Equal(Stretch.Uniform, Part<Image>(styled, AvatarParts.Image).Stretch);
    });

    [Fact]
    public Task Card_header_and_footer_dividers() => HeadlessUi.Run(() =>
    {
        var plain = new Card { Header = "页头", Footer = "页脚", Content = "正文" };
        var styled = Styled<Card, Border>(new Card { Header = "页头", Content = "正文" }, CardParts.Header, Border.BorderThicknessProperty, new Thickness(0));
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });

        Assert.Equal(new Thickness(0, 0, 0, 1), Part<Border>(plain, CardParts.Header).BorderThickness);
        Assert.Equal(new Thickness(0, 1, 0, 0), Part<Border>(plain, CardParts.Footer).BorderThickness);
        Assert.NotNull(Part<Border>(plain, CardParts.Header).BorderBrush);
        Assert.Equal(new Thickness(0), Part<Border>(styled, CardParts.Header).BorderThickness);
    });

    [Fact]
    public Task Separator_lines_follow_the_separator() => HeadlessUi.Run(() =>
    {
        var plain = new Separator { Content = "或" };
        var styled = Styled<Separator, Border>(new Separator { Content = "或" }, SeparatorParts.Line, Border.BackgroundProperty, Brushes.Red);
        HeadlessUi.Show(new StackPanel { Width = 300, Children = { plain, styled } });

        var line = Part<Border>(plain, SeparatorParts.Line);
        Assert.Same(plain.Background, line.Background);
        Assert.Equal(plain.Thickness, line.Height);
        Assert.Same(Brushes.Red, Part<Border>(styled, SeparatorParts.Line).Background);
    });

    [Fact]
    public Task Badge_dot_follows_the_text_color() => HeadlessUi.Run(() =>
    {
        var plain = new Badge { Dot = true, Content = "运行中" };
        var styled = Styled<Badge, Border>(new Badge { Dot = true, Content = "运行中" }, BadgeParts.Dot, Border.BackgroundProperty, Brushes.Red);
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });

        Assert.Same(plain.Foreground, Part<Border>(plain, BadgeParts.Dot).Background);
        Assert.Same(Brushes.Red, Part<Border>(styled, BadgeParts.Dot).Background);
    });
}
