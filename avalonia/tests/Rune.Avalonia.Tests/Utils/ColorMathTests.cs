namespace Rune.Avalonia.Tests;

/// <summary>颜色工具：解析与格式化（与 culori 逐字相同）、半透明叠加、WCAG 对比度、实色上的文字色、HSV。</summary>
public class ColorMathTests
{
    [Fact]
    [Upstream("color", "解析 #hex、rgb()、颜色名；格式化成 #hex，有透明度时带 alpha；解析不了返回 undefined")]
    public void Parses_and_formats_hex()
    {
        Assert.Equal("#3e63dd", ColorMath.ToHex("#3E63DD"));
        Assert.Equal("#ff0000", ColorMath.ToHex("rgb(255, 0, 0)"));
        Assert.Equal("#ffffff", ColorMath.ToHex("white"));
        Assert.Equal("#00000080", ColorMath.ToHex("rgba(0, 0, 0, 0.5)"));
        Assert.Null(ColorMath.ParseColor("不是颜色"));
    }

    [Fact]
    [Upstream("color", "对比度：黑白 21:1，相同颜色 1:1；半透明前景按叠到背景上的颜色算")]
    public void Contrast_ratio()
    {
        Assert.Equal(21, ColorMath.ContrastRatio("#000000", "#ffffff"), 5);
        Assert.Equal(1, ColorMath.ContrastRatio("#3e63dd", "#3e63dd"), 5);
        // 同 vitest 的 toBeCloseTo(x, 1)：差小于 0.05
        Assert.InRange(ColorMath.ContrastRatio("rgba(0, 0, 0, 0.5)", "#ffffff") - ColorMath.ContrastRatio("#808080", "#ffffff"), -0.05, 0.05);
        Assert.Contains("无法解析的颜色", Assert.Throws<ArgumentException>(() => ColorMath.ContrastRatio("nope", "#fff")).Message);
    }

    [Fact]
    [Upstream("color", "实色上的文字：白字够 4.5:1 用白字，否则用对比度更高的深色")]
    public void Readable_text()
    {
        Assert.Equal("#ffffff", ColorMath.ReadableText("#3e63dd"));
        Assert.Equal("#1c2024", ColorMath.ReadableText("#ffe629"));
    }

    [Fact]
    [Upstream("color", "withAlpha：同一个颜色换透明度")]
    public void With_alpha() => Assert.Equal("rgba(252, 252, 253, 0.8)", ColorMath.WithAlpha("#fcfcfd", 0.8));

    /// <summary>与 culori 4 的 formatHex、formatRgb、formatHsl 逐字对过（node 里跑出来的）。</summary>
    [Theory]
    [InlineData("#3e63dd", "#3e63dd", "rgb(62, 99, 221)", "hsl(226.04, 70.04%, 55.49%)")]
    [InlineData("rgba(62, 99, 221, 0.5)", "#3e63dd80", "rgba(62, 99, 221, 0.5)", "hsla(226.04, 70.04%, 55.49%, 0.5)")]
    [InlineData("hsl(120 50% 40% / 30%)", "#3399334d", "rgba(51, 153, 51, 0.3)", "hsla(120, 50%, 40%, 0.3)")]
    [InlineData("rebeccapurple", "#663399", "rgb(102, 51, 153)", "hsl(270, 50%, 40%)")]
    [InlineData("#abc8", "#aabbcc88", "rgba(170, 187, 204, 0.53)", "hsla(210, 25%, 73.33%, 0.53)")]
    [InlineData("rgb(10% 20% 30%)", "#1a334d", "rgb(26, 51, 77)", "hsl(210, 50%, 20%)")]
    public void Formats_like_culori(string value, string hex, string rgb, string hsl)
    {
        Assert.Equal(hex, ColorMath.FormatColor(value, ColorFormat.Hex));
        Assert.Equal(rgb, ColorMath.FormatColor(value, ColorFormat.Rgb));
        Assert.Equal(hsl, ColorMath.FormatColor(value, ColorFormat.Hsl));
    }

    [Fact]
    public void Hsv_round_trips()
    {
        var hsva = ColorMath.ToHsva("#3e63dd")!.Value;
        Assert.Equal(226.0377358490566, hsva.H, 10);
        Assert.Equal(0.7194570135746606, hsva.S, 10);
        Assert.Equal(0.8666666666666667, hsva.V, 10);
        Assert.Equal("#3e63dd", ColorMath.HsvaToHex(hsva));
        Assert.Equal("#3e63dd80", ColorMath.HsvaToHex(hsva with { A = 0.5 }));
        // 灰色没有色相：记 0
        Assert.Equal(0, ColorMath.ToHsva("#808080")!.Value.H);
    }
}
