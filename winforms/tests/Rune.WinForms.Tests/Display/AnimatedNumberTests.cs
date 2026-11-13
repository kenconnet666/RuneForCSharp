namespace Rune.WinForms.Tests;

/// <summary>AnimatedNumberControl：按语言格式化；读屏只读终值；时长为 0 时直接到位；Trend 按正负着色。</summary>
public class AnimatedNumberTests
{
    [Fact]
    [Upstream("AnimatedNumber", "金额默认取语言包的币种；滚动中的数字对读屏隐藏，另有终值文字")]
    public Task Currency_defaults_to_the_language_and_screen_readers_get_the_final_text() => WinFormsUi.Run(() =>
    {
        var number = new AnimatedNumberControl { Value = 1234.5m, Format = new NumericFormat { Style = NumericStyle.Currency } };
        using var form = WinFormsUi.Show(number);

        Assert.Equal("¥1,234.50", number.ShownText);
        Assert.Equal((AccessibleRole.StaticText, "¥1,234.50"), (number.AccessibilityObject.Role, number.AccessibilityObject.Name));
    });

    [Fact]
    [Upstream("AnimatedNumber", "值变化后（时长为 0）直接到位；前后缀是部件")]
    public Task Without_duration_the_value_jumps_and_affixes_are_painted() => WinFormsUi.Run(() =>
    {
        var number = new AnimatedNumberControl { Value = 1, Prefix = "共 ", Suffix = " 人", Duration = TimeSpan.Zero };
        using var form = WinFormsUi.Show(number);

        number.Value = 2048;
        WinFormsUi.Paint(number);
        Assert.Equal("2,048", number.ShownText);
        Assert.Equal("共 2,048 人", number.AccessibilityObject.Name);
    });

    [Fact]
    [Upstream("AnimatedNumber", "trend：data-trend 按正负写 up / down / flat")]
    public Task Trend_follows_the_sign() => WinFormsUi.Run(() =>
    {
        AnimatedNumberControl[] numbers = [new() { Value = 0.12m, Trend = true }, new() { Value = -3, Trend = true }, new() { Value = 0, Trend = true, Tone = TextTone.Muted }];
        using var form = WinFormsUi.Show(numbers);

        Assert.Equal(["up", "down", "flat"], numbers.Select(number => number.Direction));
        Assert.Equal([TextTone.Success, TextTone.Danger, TextTone.Muted], numbers.Select(number => number.ShownTone));
    });

    [Fact]
    [Upstream("AnimatedNumber", "bigint、Decimal：停下时与读屏的终值按原值格式化，不丢精度；trend 按大小比较")]
    public Task Large_and_tiny_values_keep_their_precision() => WinFormsUi.Run(() =>
    {
        var big = new AnimatedNumberControl { Value = 9_007_199_254_740_993m };
        var tiny = new AnimatedNumberControl { Value = -0.000000000000000001m, Trend = true };
        using var form = WinFormsUi.Show(big, tiny);

        Assert.Equal("9,007,199,254,740,993", big.ShownText);
        Assert.Equal("down", tiny.Direction);
    });

    [Fact]
    public Task Value_changes_tween_and_settle_on_the_exact_text() => WinFormsUi.Run(() =>
    {
        var number = new AnimatedNumberControl { Value = 10 };
        using var form = WinFormsUi.Show(number);

        number.Value = 20.25m;
        WinFormsUi.Until(() => !Animator.IsRunning);
        WinFormsUi.Paint(number);
        Assert.Equal("20.25", number.ShownText);
    });
}
