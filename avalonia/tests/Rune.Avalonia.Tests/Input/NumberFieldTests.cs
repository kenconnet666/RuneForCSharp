using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>NumberField：解析认全角与千分位；失焦时夹到范围、对齐步长，解析不了恢复；编辑时去掉格式；方向键加减；缩放单位；清空写回 null；到头时按钮不可用。</summary>
public class NumberFieldTests
{
    [Theory]
    [InlineData("1,234.5", "1234.5")]
    [InlineData("１２３．５", "123.5")]
    [InlineData(" -7 ", "-7")]
    [InlineData(".5", "0.5")]
    [InlineData("", null)]
    public void Parsing_accepts_grouping_and_full_width(string text, string? expected)
    {
        Assert.True(NumberRules.TryParse(text, "zh-CN", out var value));
        Assert.Equal(expected, value?.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Garbage_does_not_parse() => Assert.False(NumberRules.TryParse("12a", "zh-CN", out _));

    [Fact]
    public void Snapping_aligns_to_min_plus_steps_and_clamps()
    {
        Assert.Equal(0.3m, NumberRules.Snap(0.1m + 0.1m + 0.1m, 0.1m, null, null));
        Assert.Equal(1.5m, NumberRules.Snap(1.26m, 0.5m, 0.5m, null));
        Assert.Equal(10, NumberRules.Snap(12, 1, 1, 10));
    }

    static (NumberField Field, TextBox Other) Show(NumberField field)
    {
        var other = new TextBox();
        HeadlessUi.Show(new StackPanel { Children = { field, other } });
        return (field, other);
    }

    static void Press(NumberField field, Key key) => field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    [Fact]
    public Task Typing_commits_on_blur_clamped_and_snapped() => HeadlessUi.Run(() =>
    {
        var (field, other) = Show(new NumberField { Value = 2, Min = 1, Max = 99, Step = 0.5m });
        field.Focus();
        Assert.Equal("2", field.Text);

        field.Text = "120.3";
        other.Focus();
        Assert.Equal(99, field.Value);

        field.Focus();
        field.Text = "abc"; // 解析不了：恢复原值
        other.Focus();
        Assert.Equal(99, field.Value);

        field.Focus();
        field.Text = "";
        other.Focus();
        Assert.Null(field.Value);
    });

    [Fact]
    public Task Keys_step_and_jump_to_the_ends() => HeadlessUi.Run(() =>
    {
        var (field, _) = Show(new NumberField { Min = 1, Max = 5 });
        field.Focus();
        Press(field, Key.Up); // 空着时从 Min 开始
        Assert.Equal(1, field.Value);
        Press(field, Key.Up);
        Assert.Equal(2, field.Value);
        Assert.Equal("2", field.Text); // 编辑中的文字跟着换
        Press(field, Key.End);
        Assert.Equal(5, field.Value);
        Assert.False(field.Buttons.Increment!.IsEnabled);
        Press(field, Key.PageDown);
        Assert.Equal(1, field.Value);
        Assert.False(field.Buttons.Decrement!.IsEnabled);
    });

    [Fact]
    public Task Scaled_formats_edit_in_the_scaled_unit() => HeadlessUi.Run(() =>
    {
        var (field, other) = Show(new NumberField { Value = 155353.2m, Format = new NumericFormat { Scale = 10_000, MaximumFractionDigits = 2 }, Step = 100 });
        Assert.Equal("15.54", field.Text);
        field.Focus();
        Assert.Equal("15.53532", field.Text);
        field.Text = "20";
        other.Focus();
        Assert.Equal(200000, field.Value);

        var percent = new NumberField { Value = 0.85m, Format = new NumericFormat { Style = NumericStyle.Percent } };
        HeadlessUi.Show(percent);
        Assert.Equal("85%", percent.Text);
    });

    [Fact]
    public Task Reads_as_a_spinner_and_hides_steppers_when_read_only() => HeadlessUi.Run(() =>
    {
        var (field, _) = Show(new NumberField { Value = 3, Min = 0, Max = 10 });
        var peer = field.Peer();
        Assert.Equal(AutomationControlType.Spinner, peer.GetAutomationControlType());
        Assert.Equal(3, ((IRangeValueProvider)peer).Value);
        Assert.Equal(Provider.GetLang(field).NumberIncrement, global::Avalonia.Automation.AutomationProperties.GetName(field.Buttons.Increment!));

        field.IsReadOnly = true;
        Assert.False(field.Buttons.Increment!.IsVisible);
    });
}
