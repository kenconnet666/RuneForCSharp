namespace Rune.WinForms.Tests;

/// <summary>H1–H6：默认字号按级别、粗体（semibold 在 GDI 上画成粗体）、标题字体；Size 能改字号。</summary>
public class HeadingTests
{
    [Upstream("Heading", "每个组件渲染对应的标签")]
    [Upstream("Heading", "默认字号随级别不同")]
    [Fact]
    public Task Levels_have_their_defaults() => WinFormsUi.Run(() =>
    {
        Text[] levels = [new H1(), new H2(), new H3(), new H4(), new H5(), new H6()];
        Assert.Equal(new[] { Tokens.FontSize.Xxxl, Tokens.FontSize.Xxl, Tokens.FontSize.Xl, Tokens.FontSize.Lg, Tokens.FontSize.Md, Tokens.FontSize.Sm }, levels.Select(h => h.Size.Pixels!.Value));
        Assert.All(levels, h => Assert.Equal(TextWeight.Semibold, h.Weight));
        Assert.Equal(TextSize.Lg, new H2 { Size = TextSize.Lg }.Size);

        var heading = new H1Control { Text = "页面标题" };
        var body = new TextControl { Text = "页面标题" };
        using var form = WinFormsUi.Show(heading, body);
        Assert.True(heading.Height > body.Height);
    });
}
