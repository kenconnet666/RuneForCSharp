using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>ThemePalette 与 ThemeSettingsMath：默认主题的颜色就是 indigo、slate 按色阶表取的值；换品牌色整组替换、实色上的文字按对比度选；倍数与缩放。</summary>
public class ThemePaletteTests
{
    static string Key(string token) => "Rune.Color." + char.ToUpperInvariant(token[0]) + token[1..];

    /// <summary>主题里一个颜色 token 的取值（#rrggbb，或带透明度时 #aarrggbb）。</summary>
    static string ThemeColor(string token, ThemeVariant variant)
    {
        Assert.True(Application.Current!.TryGetResource(Key(token), variant, out var value), $"主题里没有 {Key(token)}");
        var color = (Color)value!;
        return color.A == 255 ? $"#{color.R:x2}{color.G:x2}{color.B:x2}" : $"#{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}";
    }

    static string Normalize(string css) => ColorMath.ParseColor(css) is { } rgba && rgba.A < 1
        ? $"#{(int)Math.Round(rgba.A * 255):x2}{ColorMath.ToHex(rgba with { A = 1 })[1..]}"
        : ColorMath.ToHex(css)!;

    [Fact]
    [Upstream("palette", "亮色与暗色取值都与 Radix 色阶一致：品牌色、中性色按 paletteColors 生成（为对比度调整过的单独列出）")]
    public Task Default_theme_colors_follow_the_palette() => HeadlessUi.Run(() =>
    {
        // 暗色的实色悬停为对比度调亮过（上游 adjusted），不在色阶里
        foreach (var (variant, dark, adjusted) in new[] { (ThemeVariant.Light, false, Array.Empty<string>()), (ThemeVariant.Dark, true, ["accentHover"]) })
            foreach (var (token, value) in ThemePalette.Colors("indigo", "slate", dark))
                if (!adjusted.Contains(token)) Assert.Equal((token, Normalize(value)), (token, ThemeColor(token, variant)));
    });

    [Fact]
    [Upstream("palette", "换品牌色：整组按同样的级别替换；实色上的文字按对比度选白字或深色字")]
    public void Changing_accent_replaces_the_whole_set()
    {
        var crimson = ThemePalette.Colors("crimson", null, dark: false);
        Assert.Equal(ThemePalette.Step("crimson", 9, false), crimson["accent"]);
        Assert.Equal(ThemePalette.Step("crimson", 11, false), crimson["accentText"]);
        Assert.Equal("#ffffff", ThemePalette.Colors("indigo", null, false)["accentContrast"]);
        Assert.NotEqual("#ffffff", ThemePalette.Colors("yellow", null, false)["accentContrast"]);
        foreach (var accent in new[] { "crimson", "yellow", "teal" })
        {
            var colors = ThemePalette.Colors(accent, null, false);
            var chosen = ColorMath.ContrastRatio(colors["accentContrast"], colors["accent"]);
            var best = new[] { "#ffffff", "#1c2024" }.Max(text => ColorMath.ContrastRatio(text, colors["accent"]));
            Assert.True(chosen >= 4.5 || Math.Abs(chosen - best) < 1e-9, $"{accent}：{chosen}");
        }
        // 只给品牌色时不动中性色
        Assert.False(crimson.ContainsKey("background"));
    }

    [Fact]
    public void Neutral_light_uses_white_surfaces_and_dark_uses_dark_steps()
    {
        var light = ThemePalette.Colors(null, "mauve", dark: false);
        Assert.Equal((ThemePalette.Step("mauve", 2, false), "#ffffff"), (light["background"], light["surface"]));
        var dark = ThemePalette.Colors("crimson", null, dark: true);
        Assert.Equal(ThemePalette.Step("crimson", 11, true), dark["accentText"]);
        Assert.Equal(ThemePalette.Swatch("teal", false), ThemePalette.Step("teal", 9, false));
        Assert.Equal(25, ThemePalette.Accents.Count);
        Assert.Equal(["slate", "gray", "mauve", "sage", "olive", "sand"], ThemePalette.Neutrals);
    }

    [Fact]
    public void Factors_and_scaling_follow_upstream()
    {
        Assert.Equal(0.7, ThemeSettingsMath.Scale(0.4, ThemeSettingsMath.RadiusFactor(ThemeRadius.Large)));
        Assert.Equal(15.4, ThemeSettingsMath.Scale(14, 1.1));
        Assert.Equal(72, ThemeSettingsMath.Scale(120, ThemeSettingsMath.MotionFactor(ThemeMotion.Fast)));
        Assert.Equal(1.313, ThemeSettingsMath.Scale(0.75, 1.75));
        Assert.False(ThemeSettingsMath.HasOverrides(new ThemeSettings()));
        Assert.False(ThemeSettingsMath.HasOverrides(new ThemeSettings(Radius: ThemeRadius.Medium, Scaling: 1)));
        Assert.False(ThemeSettingsMath.HasOverrides(new ThemeSettings(Motion: ThemeMotion.Slow), reducedMotion: true));
        Assert.True(ThemeSettingsMath.HasOverrides(new ThemeSettings(Accent: "teal")));
    }
}
