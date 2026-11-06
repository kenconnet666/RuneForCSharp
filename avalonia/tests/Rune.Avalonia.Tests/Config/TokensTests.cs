using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>生成的 token（tools/token-gen）：类型对、按主题变体取到各自的值、自定义主题回落到亮暗。</summary>
public class TokensTests
{
    static object? Find(string key, ThemeVariant theme) =>
        Application.Current!.TryGetResource(key, theme, out var value) ? value : null;

    [Fact]
    public Task Shared_tokens_have_platform_types() => HeadlessUi.Run(() =>
    {
        Assert.Equal(14d, Find("Rune.FontSize.Md", ThemeVariant.Light));
        Assert.Equal(0.75d, Find("Rune.Space.Md", ThemeVariant.Light));
        Assert.Equal(FontWeight.Medium, Find("Rune.FontWeight.Medium", ThemeVariant.Light));
        Assert.Equal(TimeSpan.FromMilliseconds(200), Find("Rune.Duration.Md", ThemeVariant.Light));
        var easing = Assert.IsType<SplineEasing>(Find("Rune.Easing.Standard", ThemeVariant.Light));
        Assert.Equal((0.2, 0d, 0d, 1d), (easing.X1, easing.Y1, easing.X2, easing.Y2));
        // rem 换成设备无关像素：30rem → 480
        Assert.Equal(480d, Find("Rune.Size.Xs", ThemeVariant.Light));
    });

    [Fact]
    public Task Themed_tokens_follow_the_theme_variant() => HeadlessUi.Run(() =>
    {
        Assert.Equal(Color.Parse("#f9f9fb"), Find("Rune.Color.Background", ThemeVariant.Light));
        Assert.Equal(Color.Parse("#111113"), Find("Rune.Color.Background", ThemeVariant.Dark));
        Assert.Equal(Color.Parse("#f5efdf"), Assert.IsAssignableFrom<ISolidColorBrush>(Find("Rune.Brush.Background", RuneThemeVariants.Paper)).Color);
        Assert.Equal(0.4d, Find("Rune.Radius.Md", ThemeVariant.Light));
        Assert.Equal(0.25d, Find("Rune.Radius.Md", RuneThemeVariants.HighContrast));

        var shadow = Assert.IsType<BoxShadows>(Find("Rune.Shadow.Md", ThemeVariant.Light));
        Assert.Equal(2, shadow.Count);
        Assert.Contains("Segoe UI", Assert.IsType<FontFamily>(Find("Rune.Font.Sans", ThemeVariant.Light)).Name);
    });

    [Fact]
    public Task Every_built_in_theme_resolves_its_own_colors() => HeadlessUi.Run(() =>
    {
        Assert.Equal(16, RuneThemeVariants.All.Count);
        var colors = RuneThemeVariants.All.Select(theme => (Find("Rune.Color.Background", theme), Find("Rune.Color.Accent", theme))).ToList();
        Assert.DoesNotContain(colors, pair => pair.Item1 is null || pair.Item2 is null);
        // 每套主题的（底色, 品牌色）各不相同：说明每个变体都有自己的字典，而不是都回落到亮色（high-contrast 与 mono 的底色都是白）
        Assert.Equal(colors.Count, colors.Distinct().Count());
        Assert.All(RuneThemeVariants.All.Skip(2), theme => Assert.NotNull(theme.InheritVariant));
    });

    [Fact]
    public Task Controls_pick_up_the_scope_theme() => HeadlessUi.Run(() =>
    {
        var border = new Border();
        border[!Border.BackgroundProperty] = border.GetResourceObservable("Rune.Brush.Background").ToBinding();
        var scope = new ThemeVariantScope { RequestedThemeVariant = RuneThemeVariants.Midnight, Child = border };
        var window = HeadlessUi.Show(scope);

        Assert.Equal(Color.Parse("#0d1321"), Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color);

        scope.RequestedThemeVariant = ThemeVariant.Light;
        HeadlessUi.Flush(window);
        Assert.Equal(Color.Parse("#f9f9fb"), Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color);
    });

    [Fact]
    public Task Palettes_point_at_their_own_theme_brushes() => HeadlessUi.Run(() =>
    {
        foreach (var theme in RuneThemeVariants.All)
        {
            var accent = Assert.IsType<TonePalette>(Find("Rune.Palette.Accent", theme));
            var neutral = Assert.IsType<TonePalette>(Find("Rune.Palette.Neutral", theme));
            Assert.Same(Find("Rune.Brush.Accent", theme), accent.Solid);
            Assert.Same(Find("Rune.Brush.AccentText", theme), accent.Text);
            Assert.Same(Find("Rune.Brush.InverseSurfaceHover", theme), neutral.SolidHover);
        }
        // 派生色：中性实底悬停 = oklab 里反色表面 85% + 背景 15%（button-core 的 color-mix）
        Assert.Equal(Color.Parse("#383c40"), ((ISolidColorBrush)Find("Rune.Brush.InverseSurfaceHover", ThemeVariant.Light)!).Color);
    });
}
