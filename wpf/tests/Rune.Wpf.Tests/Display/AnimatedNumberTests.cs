using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>AnimatedNumber：按语言格式化；读屏只读终值；时长为 0 时直接到位；Trend 按正负着色。</summary>
public class AnimatedNumberTests
{
    static AutomationPeer Peer(AnimatedNumber number) => UIElementAutomationPeer.CreatePeerForElement(number);

    static void Show(params AnimatedNumber[] numbers)
    {
        var host = new StackPanel();
        foreach (var number in numbers) host.Children.Add(number);
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
    }

    [Fact]
    [Upstream("AnimatedNumber", "金额默认取语言包的币种；滚动中的数字对读屏隐藏，另有终值文字")]
    public Task Currency_defaults_to_the_language_and_screen_readers_get_the_final_text() => WpfUi.Run(() =>
    {
        var number = new AnimatedNumber { Value = 1234.5m, Format = new NumericFormat { Style = NumericStyle.Currency } };
        Show(number);

        Assert.Equal("¥1,234.50", number.ShownText);
        Assert.Equal((AutomationControlType.Text, "¥1,234.50"), (Peer(number).GetAutomationControlType(), Peer(number).GetName()));
        Assert.Null(Peer(number).GetChildren()); // 滚动中的数字不另念
    });

    [Fact]
    [Upstream("AnimatedNumber", "值变化后（时长为 0）直接到位；前后缀是部件")]
    public Task Without_duration_the_value_jumps_and_affixes_are_parts() => WpfUi.Run(() =>
    {
        var number = new AnimatedNumber { Value = 1, Prefix = "共 ", Suffix = " 人", Duration = TimeSpan.Zero };
        Show(number);

        number.Value = 2048;
        WpfUi.Flush();
        Assert.Equal("2,048", number.ShownText);
        Assert.Equal("共 2,048 人", Peer(number).GetName());
    });

    [Fact]
    [Upstream("AnimatedNumber", "trend：data-trend 按正负写 up / down / flat")]
    public Task Trend_follows_the_sign() => WpfUi.Run(() =>
    {
        AnimatedNumber[] numbers = [new() { Value = 0.12m, Trend = true }, new() { Value = -3, Trend = true }, new() { Value = 0, Trend = true, Tone = TextTone.Muted }];
        Show(numbers[0], numbers[1], numbers[2]);

        Assert.Equal(["up", "down", "flat"], numbers.Select(number => number.Direction));
        Assert.Equal([TextTone.Success, TextTone.Danger, TextTone.Muted], numbers.Select(number => number.ShownTone));
    });

    [Fact]
    [Upstream("AnimatedNumber", "bigint、Decimal：停下时与读屏的终值按原值格式化，不丢精度；trend 按大小比较")]
    public Task Large_and_tiny_values_keep_their_precision() => WpfUi.Run(() =>
    {
        var big = new AnimatedNumber { Value = 9_007_199_254_740_993m };
        var tiny = new AnimatedNumber { Value = -0.000000000000000001m, Trend = true };
        Show(big, tiny);

        Assert.Equal("9,007,199,254,740,993", big.ShownText);
        Assert.Equal("down", tiny.Direction);
    });
}

/// <summary>NumericFormat：Intl.NumberFormat 的取舍、小数位、符号、缩放与单位。</summary>
public class NumericFormatTests
{
    static string Zh(NumericFormat format, decimal value) => format.Format(value, "zh-CN", "CNY");

    [Fact]
    public void Formats_like_intl()
    {
        Assert.Equal("1,234.5", Zh(new(), 1234.5m));
        Assert.Equal("1,234.568", Zh(new(), 1234.5678m)); // 默认最多 3 位
        Assert.Equal("¥0.30", Zh(new() { Style = NumericStyle.Currency }, 0.1m + 0.2m)); // 十进制精确
        Assert.Equal("$0.30", new NumericFormat { Style = NumericStyle.Currency }.Format(0.3m, "en-US", "USD"));
        Assert.Equal("12%", Zh(new() { Style = NumericStyle.Percent }, 0.123m));
        Assert.Equal("+12.3%", Zh(new() { Style = NumericStyle.Percent, MaximumFractionDigits = 1, SignDisplay = NumericSign.ExceptZero }, 0.123m));
        Assert.Equal("0%", Zh(new() { Style = NumericStyle.Percent, SignDisplay = NumericSign.ExceptZero }, 0.001m));
        Assert.Equal("-1.5", Zh(new() { MinimumFractionDigits = 1 }, -1.5m));
        Assert.Equal("15.54 万元", Zh(new() { Scale = 10_000, MaximumFractionDigits = 2, Suffix = "万元" }, 155353.2m));
        Assert.Equal("1235", Zh(new() { UseGrouping = false, MaximumFractionDigits = 0 }, 1234.5m)); // 四舍五入远离 0
    }
}
