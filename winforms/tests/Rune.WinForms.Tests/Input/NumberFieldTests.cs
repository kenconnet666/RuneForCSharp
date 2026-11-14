using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>NumberFieldControl：解析认全角与千分位；失焦时夹到范围、对齐步长，解析不了恢复；编辑时去掉格式；方向键加减；缩放单位；清空写回 null；到头时按钮不可用。</summary>
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

    /// <summary>放进窗口，后面跟一个可以把焦点挪过去的输入框（失焦提交）。</summary>
    static Form Show(NumberFieldControl field)
    {
        field.Width = 300;
        return WinFormsUi.Show(field, new TextBox { Width = 300, Top = 80 });
    }

    static Control Other(Form form) => form.Controls[1];

    static void Press(NumberFieldControl field, Keys key) =>
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(field.Input, [new KeyEventArgs(key)]);

    static void FocusOn(Form form, Control control)
    {
        form.ActiveControl = control;
        WinFormsUi.Pump();
    }

    [Fact]
    public Task Typing_commits_on_blur_clamped_and_snapped() => WinFormsUi.Run(() =>
    {
        var field = new NumberFieldControl { Value = 2, Min = 1, Max = 99, Step = 0.5m };
        using var form = Show(field);
        var other = Other(form);
        FocusOn(form, field.Input);
        Assert.Equal("2", field.Text);

        field.Text = "120.3";
        FocusOn(form, other);
        Assert.Equal(99, field.Value);

        FocusOn(form, field.Input);
        field.Text = "abc"; // 解析不了：恢复原值
        FocusOn(form, other);
        Assert.Equal(99, field.Value);
        Assert.Equal("99", field.Text);

        FocusOn(form, field.Input);
        field.Text = "";
        FocusOn(form, other);
        Assert.Null(field.Value);
    });

    [Fact]
    public Task Keys_step_and_jump_to_the_ends() => WinFormsUi.Run(() =>
    {
        var field = new NumberFieldControl { Min = 1, Max = 5 };
        using var form = Show(field);
        FocusOn(form, field.Input);
        Press(field, Keys.Up); // 空着时从 Min 开始
        Assert.Equal(1, field.Value);
        Press(field, Keys.Up);
        Assert.Equal(2, field.Value);
        Assert.Equal("2", field.Text); // 编辑中的文字跟着换
        Press(field, Keys.End);
        Assert.Equal(5, field.Value);
        Assert.False(field.Buttons.Increment.Enabled);
        Press(field, Keys.PageDown);
        Assert.Equal(1, field.Value);
        Assert.False(field.Buttons.Decrement.Enabled);
    });

    [Fact]
    public Task Scaled_formats_edit_in_the_scaled_unit() => WinFormsUi.Run(() =>
    {
        var field = new NumberFieldControl { Value = 155353.2m, Format = new NumericFormat { Scale = 10_000, MaximumFractionDigits = 2 }, Step = 100 };
        using var form = Show(field);
        var other = Other(form);
        FocusOn(form, other); // 窗口显示时焦点落在第一个输入框上
        Assert.Equal("15.54", field.Text);
        FocusOn(form, field.Input);
        Assert.Equal("15.53532", field.Text);
        field.Text = "20";
        FocusOn(form, other);
        Assert.Equal(200000, field.Value);

        var percent = new NumberFieldControl { Value = 0.85m, Format = new NumericFormat { Style = NumericStyle.Percent } };
        using var second = Show(percent);
        FocusOn(second, Other(second));
        Assert.Equal("85%", percent.Text);
    });

    [Fact]
    public Task Steppers_hold_to_repeat_and_hide_when_read_only() => WinFormsUi.Run(() =>
    {
        var field = new NumberFieldControl { Value = 3, Min = 0, Max = 10 };
        using var form = Show(field);
        Assert.Equal(AccessibleRole.SpinButton, field.AccessibilityObject.Role);
        Assert.Equal(ProviderControl.LangOf(field).NumberIncrement, field.Buttons.Increment.AccessibleName);

        // 按下走一步，随后的 Click 不再走；按住 400ms 后连续加
        var increment = field.Buttons.Increment;
        typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(increment, [new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)]);
        Assert.Equal(4, field.Value);
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(600));
        typeof(Control).GetMethod("OnMouseUp", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(increment, [new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)]);
        Assert.True(field.Value > 4);
        var held = field.Value;
        increment.PerformClick();
        Assert.Equal(held, field.Value);
        increment.PerformClick(); // 键盘、读屏触发的 Click 照常走一步
        Assert.Equal(held + 1, field.Value);

        field.IsReadOnly = true;
        Assert.False(increment.Visible);
    });
}
