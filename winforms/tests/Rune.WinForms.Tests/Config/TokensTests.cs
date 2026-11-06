namespace Rune.WinForms.Tests;

/// <summary>生成的 token（tools/token-gen）：常量的单位、16 套主题各自的取值、阴影保留全部层。</summary>
public class TokensTests
{
    [Fact]
    public void Shared_tokens_keep_their_units()
    {
        Assert.Equal(14f, Tokens.FontSize.Md);
        Assert.Equal(0.75f, Tokens.Space.Md);
        Assert.Equal(500, Tokens.FontWeight.Medium);
        Assert.Equal(TimeSpan.FromMilliseconds(200), Tokens.Duration.Md);
        // rem 换成设备无关像素：30rem → 480
        Assert.Equal(480f, Tokens.Size.Xs);
        Assert.True(Easing.Standard.Ease(0.5) > 0.8);
    }

    [Fact]
    public void Every_built_in_theme_has_its_own_values()
    {
        Assert.Equal(16, BuiltinThemes.All.Count);
        Assert.Equal(ColorTranslator.FromHtml("#fcfcfd").ToArgb(), BuiltinThemes.Light.Colors.Background.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#111113").ToArgb(), BuiltinThemes.Dark.Colors.Background.ToArgb());
        Assert.Equal(0.25f, BuiltinThemes.HighContrast.Radii.Md);
        // high-contrast 与 mono 的底色都是白：比（底色, 品牌色）这一对
        var pairs = BuiltinThemes.All.Select(theme => (theme.Colors.Background, theme.Colors.Accent)).ToList();
        Assert.Equal(pairs.Count, pairs.Distinct().Count());
        Assert.Equal(["dark", "midnight", "cyberpunk", "big-screen", "ember"], BuiltinThemes.All.Where(t => t.IsDark).Select(t => t.Name));
    }

    [Fact]
    public void Shadows_keep_every_layer_and_css_alpha()
    {
        Assert.Empty(BuiltinThemes.Light.Shadows.None);
        var xs = BuiltinThemes.Light.Shadows.Xs;
        Assert.Equal(3, xs.Count);
        // CSS 的 #00005506：alpha 在最后（0x06），蓝是 0x55
        Assert.Equal(Color.FromArgb(0x06, 0, 0, 0x55), xs[0].Color);
        Assert.Equal(new ShadowLayer(0, 2, 1, -1, Color.FromArgb(13, 0, 0, 0)), xs[1]);
        Assert.Contains("Segoe UI", BuiltinThemes.Light.Fonts.Sans);
    }
}
