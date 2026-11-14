using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>NumberField：失焦时夹到范围、对齐步长，解析不了恢复；编辑时去掉格式；方向键加减；缩放单位；只读时不显示加减按钮。解析与对齐的规则在 Avalonia 的测试里（孪生文件）。</summary>
public class NumberFieldTests
{
    static (NumberField Field, TextBox Other) Show(NumberField field)
    {
        var other = new TextBox();
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(field);
        host.Children.Add(other);
        WpfUi.Show(host);
        return (field, other);
    }

    static void Press(NumberField field, Key key) =>
        field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(field)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    [Fact]
    public Task Typing_commits_on_blur_clamped_and_snapped() => WpfUi.Run(() =>
    {
        var (field, other) = Show(new NumberField { Value = 2, Min = 1, Max = 99, Step = 0.5m });
        Keyboard.Focus(field);
        Assert.Equal("2", field.Text);

        field.Text = "120.3";
        Keyboard.Focus(other);
        Assert.Equal(99, field.Value);

        Keyboard.Focus(field);
        field.Text = "abc"; // 解析不了：恢复原值
        Keyboard.Focus(other);
        Assert.Equal(99, field.Value);

        Keyboard.Focus(field);
        field.Text = "";
        Keyboard.Focus(other);
        Assert.Null(field.Value);
    });

    [Fact]
    public Task Keys_step_and_jump_to_the_ends() => WpfUi.Run(() =>
    {
        var (field, _) = Show(new NumberField { Min = 1, Max = 5 });
        Keyboard.Focus(field);
        Press(field, Key.Up); // 空着时从 Min 开始
        Assert.Equal(1, field.Value);
        Press(field, Key.Up);
        Assert.Equal(2, field.Value);
        Assert.Equal("2", field.Text);
        Press(field, Key.End);
        Assert.Equal(5, field.Value);
        Assert.False(field.Buttons.Increment!.IsEnabled);
        Press(field, Key.PageDown);
        Assert.Equal(1, field.Value);
        Assert.False(field.Buttons.Decrement!.IsEnabled);
    });

    [Fact]
    public Task Scaled_formats_edit_in_the_scaled_unit() => WpfUi.Run(() =>
    {
        var (field, other) = Show(new NumberField { Value = 155353.2m, Format = new NumericFormat { Scale = 10_000, MaximumFractionDigits = 2 }, Step = 100 });
        Assert.Equal("15.54", field.Text);
        Keyboard.Focus(field);
        Assert.Equal("15.53532", field.Text);
        field.Text = "20";
        Keyboard.Focus(other);
        Assert.Equal(200000, field.Value);
    });

    [Fact]
    public Task Reads_as_a_spinner_and_hides_steppers_when_read_only() => WpfUi.Run(() =>
    {
        var (field, _) = Show(new NumberField { Value = 3, Min = 0, Max = 10 });
        var peer = field.Peer();
        Assert.Equal(AutomationControlType.Spinner, peer.GetAutomationControlType());
        Assert.Equal(3, ((IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue)).Value);
        Assert.Equal(Provider.GetLang(field).NumberIncrement, AutomationProperties.GetName(field.Buttons.Increment!));

        field.IsReadOnly = true;
        Assert.Equal(Visibility.Collapsed, field.Buttons.Increment!.Visibility);
    });
}
