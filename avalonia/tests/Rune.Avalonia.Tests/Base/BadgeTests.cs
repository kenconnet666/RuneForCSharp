using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Badge：默认值在主题里、Provider 的样式能改；样式 × 色调取主题的画刷并随主题切换；图标、圆点是真的子元素，
/// 子选择器能改它们；尺寸按 em；读屏直接读里面的文字。
/// </summary>
public class BadgeTests
{
    static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    static Color Token(string key, ThemeVariant theme)
    {
        Application.Current!.TryGetResource(key, theme, out var value);
        return BrushColor((IBrush?)value);
    }

    [Fact]
    public Task Defaults_come_from_the_theme_and_styles_can_change_them() => HeadlessUi.Run(() =>
    {
        var plain = new Badge { Content = "草稿" };
        var styled = new Badge { Content = "新功能" };
        var host = new StackPanel { Children = { plain, new Border { Child = styled } } };
        host.Children[1].Styles.Add(new Style(x => x.OfType<Badge>()) { Setters = { new Setter(Badge.VariantProperty, StatusVariant.Solid), new Setter(Badge.ToneProperty, StatusTone.Accent) } });
        HeadlessUi.Show(host);

        Assert.Equal((StatusVariant.Soft, StatusTone.Neutral, TextSize.Xs, Radius.Sm), (plain.Variant, plain.Tone, plain.Size, plain.Radius));
        Assert.Equal(12, plain.FontSize);
        Assert.Equal(Token("Rune.Brush.Text", ThemeVariant.Light), BrushColor(plain.Foreground));
        Assert.Equal(Token("Rune.Brush.AccentContrast", ThemeVariant.Light), BrushColor(styled.Foreground));
    });

    [Fact]
    public Task Colors_follow_variant_tone_and_theme() => HeadlessUi.Run(() =>
    {
        var badge = new Badge { Content = "已失败", Variant = StatusVariant.Outline, Tone = StatusTone.Danger };
        var window = HeadlessUi.Show(new StackPanel { Children = { badge } });
        try
        {
            Assert.Equal(Token("Rune.Brush.DangerText", ThemeVariant.Light), BrushColor(badge.Foreground));
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            HeadlessUi.Flush(window);
            Assert.Equal(Token("Rune.Brush.DangerText", ThemeVariant.Dark), BrushColor(badge.Foreground));
            // 文字是子元素，颜色从 Badge 继承
            var text = badge.GetLogicalChildren().OfType<Text>().Single();
            Assert.Equal(BrushColor(badge.Foreground), BrushColor(text.Foreground));
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    });

    [Fact]
    public Task Icon_and_dot_are_real_parts_that_child_selectors_reach() => HeadlessUi.Run(() =>
    {
        var withIcon = new Badge { Content = "已通过", Icon = Lucide.CircleCheck, Dot = true };
        var withDot = new Badge { Content = "运行中", Dot = true };
        var host = new StackPanel { Children = { withIcon, withDot } };
        host.Styles.Add(new Style(x => x.OfType<Badge>().Child().OfType<Icon>().Name(BadgeParts.Icon)) { Setters = { new Setter(Icon.StrokeWidthProperty, 3d) } });
        HeadlessUi.Show(host);

        var icon = withIcon.GetLogicalChildren().OfType<Icon>().Single();
        Assert.Equal(3, icon.StrokeWidth);
        Assert.Empty(withIcon.GetLogicalChildren().OfType<Border>()); // 有图标时不画圆点
        var dot = withDot.GetLogicalChildren().OfType<Border>().Single();
        Assert.Equal(BadgeParts.Dot, dot.Name);
        Assert.Equal(6, dot.Bounds.Width); // 0.5em，字号 12
    });

    [Fact]
    public Task Sizes_are_in_em() => HeadlessUi.Run(() =>
    {
        var badge = new Badge { Content = "x", Size = TextSize.Lg };
        HeadlessUi.Show(new StackPanel { Children = { badge } });

        Assert.True(badge.Bounds.Height >= 1.75 * 16); // 最小高度 1.75em
    });

    [Fact]
    public Task Screen_readers_read_the_text_inside() => HeadlessUi.Run(() =>
    {
        var badge = new Badge { Content = "已发货" };
        HeadlessUi.Show(new StackPanel { Children = { badge } });

        var peer = ControlAutomationPeer.CreatePeerForElement(badge);
        Assert.False(peer.IsControlElement());
        Assert.Contains(peer.GetChildren(), child => child.GetName() == "已发货");
    });

    [Fact]
    public Task Colors_come_from_the_shared_palette_which_styles_can_swap() => HeadlessUi.Run(() =>
    {
        // 与按钮同一张色表：样式档位取调色板的字段；不带伪类的样式换掉调色板，颜色跟着换
        Application.Current!.TryGetResource("Rune.Palette.Success", ThemeVariant.Light, out var success);
        var solid = new Badge { Content = "3", Variant = StatusVariant.Solid, Tone = StatusTone.Danger };
        var swapped = new Badge { Content = "已通过", Variant = StatusVariant.Surface };
        var host = new StackPanel { Children = { solid, swapped } };
        host.Styles.Add(new Style(x => x.OfType<Badge>().Name("swapped")) { Setters = { new Setter(Badge.PaletteProperty, success) } });
        swapped.Name = "swapped";
        HeadlessUi.Show(host);

        Assert.Equal(Token("Rune.Brush.DangerContrast", ThemeVariant.Light), BrushColor(solid.Foreground));
        Assert.Equal(Token("Rune.Brush.Danger", ThemeVariant.Light), BrushColor(solid.Background));
        Assert.Null(solid.BorderBrush);
        Assert.Equal(Token("Rune.Brush.SuccessText", ThemeVariant.Light), BrushColor(swapped.Foreground));
        Assert.Equal(Token("Rune.Brush.SuccessSoft", ThemeVariant.Light), BrushColor(swapped.Background));
        Assert.Equal(Token("Rune.Brush.SuccessBorder", ThemeVariant.Light), BrushColor(swapped.BorderBrush));
    });
}
