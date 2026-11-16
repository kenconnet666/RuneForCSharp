using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>ThemeCustomization：设置换算成资源覆盖、套在元素上、生成 XAML；对比度检查。</summary>
public class ThemeCustomizationTests
{
    static Dictionary<string, object> Overrides(ThemeSettings settings, ThemeVariant variant, bool reducedMotion = false) =>
        ThemeCustomization.Overrides(settings, variant, reducedMotion).ToDictionary(pair => pair.Key, pair => pair.Value);

    static Color Hex(string css) => Color.Parse(css);

    [Fact]
    [Upstream("customize", "没有设置时什么都不覆盖，主题原样返回")]
    public Task No_settings_no_overrides() => HeadlessUi.Run(() =>
    {
        Assert.Empty(Overrides(new ThemeSettings(), ThemeVariant.Light));
        var window = new Window();
        ThemeCustomization.Apply(window, new ThemeSettings());
        Assert.Empty(window.Resources.MergedDictionaries);
    });

    [Fact]
    [Upstream("customize", "品牌色、中性色按 Radix 色阶整组替换；暗色取暗色色阶")]
    public Task Accent_and_neutral_replace_whole_sets() => HeadlessUi.Run(() =>
    {
        var light = Overrides(new ThemeSettings(Accent: "crimson", Neutral: "mauve"), ThemeVariant.Light);
        Assert.Equal(Hex(ThemePalette.Step("crimson", 9, false)), light[ThemeKeys.Color.Accent]);
        Assert.Equal(Hex(ThemePalette.Step("crimson", 9, false)), ((SolidColorBrush)light[ThemeKeys.Brush.Accent]).Color);
        // 亮色的页面着一点色（2 级），卡片是白色
        Assert.Equal(Hex(ThemePalette.Step("mauve", 2, false)), light[ThemeKeys.Color.Background]);
        Assert.Equal(Colors.White, light[ThemeKeys.Color.Surface]);
        var dark = Overrides(new ThemeSettings(Accent: "crimson"), ThemeVariant.Dark);
        Assert.Equal(Hex(ThemePalette.Step("crimson", 11, true)), dark[ThemeKeys.Color.AccentText]);
        // 自定义的暗色变体（午夜）也取暗色色阶
        Assert.Equal(dark[ThemeKeys.Color.AccentText], Overrides(new ThemeSettings(Accent: "crimson"), RuneThemeVariants.Midnight)[ThemeKeys.Color.AccentText]);
    });

    [Fact]
    [Upstream("customize", "圆角、字号按默认值等比缩放（圆角的 none、full 不动）；默认档位不覆盖")]
    public Task Radius_and_scaling_scale_defaults() => HeadlessUi.Run(() =>
    {
        var large = Overrides(new ThemeSettings(Radius: ThemeRadius.Large, Scaling: 1.1), ThemeVariant.Light);
        Assert.Equal(0.7, large[ThemeKeys.Radius.Md]);
        Assert.False(large.ContainsKey(ThemeKeys.Radius.Full));
        Assert.Equal(15.4, large[ThemeKeys.FontSize.Md]);
        Assert.Empty(Overrides(new ThemeSettings(Radius: ThemeRadius.Medium, Scaling: 1), ThemeVariant.Light));
    });

    [Fact]
    [Upstream("customize", "套在主题上时圆角按这套主题自己的取值缩放：自带圆角的柠檬主题上小 < 默认 < 大")]
    public Task Radius_scales_the_theme_own_values() => HeadlessUi.Run(() =>
    {
        // 柠檬主题的 md 是 0.75em：小 0.375，大 1.313
        Assert.Equal(0.375, Overrides(new ThemeSettings(Radius: ThemeRadius.Small), RuneThemeVariants.Lemon)[ThemeKeys.Radius.Md]);
        Assert.Equal(1.313, Overrides(new ThemeSettings(Radius: ThemeRadius.Large), RuneThemeVariants.Lemon)[ThemeKeys.Radius.Md]);
    });

    [Fact]
    [Upstream("customize", "动效按倍数缩放时长；用户开启“减少动态效果”时不覆盖")]
    public Task Motion_scales_durations_unless_reduced() => HeadlessUi.Run(() =>
    {
        Assert.Equal(TimeSpan.FromMilliseconds(72), Overrides(new ThemeSettings(Motion: ThemeMotion.Fast), ThemeVariant.Light)[ThemeKeys.Duration.Sm]);
        Assert.Empty(Overrides(new ThemeSettings(Motion: ThemeMotion.Slow), ThemeVariant.Light, reducedMotion: true));
    });

    [Fact]
    [Upstream("customize", "套在主题上：名字加 -custom，覆盖写进 overrides")]
    public Task Apply_puts_overrides_on_the_target() => HeadlessUi.Run(() =>
    {
        var probe = new Border();
        var window = HeadlessUi.Show(probe);
        ThemeCustomization.Apply(window, new ThemeSettings(Accent: "teal"), ThemeVariant.Light);
        Assert.True(probe.TryFindResource(ThemeKeys.Color.Accent, ThemeVariant.Light, out var accent));
        Assert.Equal(Hex(ThemePalette.Step("teal", 9, false)), accent);
        // 再套一次换掉上一次的，不叠加
        ThemeCustomization.Apply(window, new ThemeSettings(Accent: "crimson"), ThemeVariant.Light);
        Assert.Single(window.Resources.MergedDictionaries);
        ThemeCustomization.Apply(window, new ThemeSettings());
        Assert.Empty(window.Resources.MergedDictionaries);
    });

    [Fact]
    [Upstream("customize", "生成代码：defineTheme(基础主题, 覆盖)，字体名里的双引号原样保留在单引号字符串里")]
    public Task Code_is_a_resource_dictionary() => HeadlessUi.Run(() =>
    {
        var code = ThemeCustomization.Code(new ThemeSettings(Accent: "teal", Font: ThemeFont.SerifHeadings), ThemeVariant.Light);
        Assert.StartsWith("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"", code);
        Assert.Contains($"<Color x:Key=\"Rune.Color.Accent\">{Hex(ThemePalette.Step("teal", 9, false))}</Color>", code);
        Assert.Contains("<FontFamily x:Key=\"Rune.Font.Heading\">Noto Serif SC, ", code);
    });

    [Fact]
    public Task Contrast_passes_for_default_themes_and_flags_weak_accents() => HeadlessUi.Run(() =>
    {
        var probe = new Border();
        var window = HeadlessUi.Show(probe);
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            window.RequestedThemeVariant = variant;
            Assert.Empty(ThemeCustomization.CheckContrast(probe));
        }
        window.RequestedThemeVariant = ThemeVariant.Light;
        // 亮黄的实色上白字、深字都可能不到 4.5:1；浅色的中性色上次要文字也会不够
        ThemeCustomization.Apply(window, new ThemeSettings(Accent: "yellow"), ThemeVariant.Light);
        Assert.Contains(ThemeCustomization.CheckContrast(probe), issue => issue.Foreground is "accentText" or "accentContrast" or "accent");
    });
}
