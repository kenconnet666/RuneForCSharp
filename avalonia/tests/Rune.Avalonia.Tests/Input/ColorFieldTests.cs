using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>ColorField：输入规范成 #hex 写回、写不成颜色时标为无效；色块是打开取色器的按钮；ColorPicker 的色板键盘与输入、部件。</summary>
public class ColorFieldTests
{
    static void Press(Control control, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = control });

    [Fact]
    [Upstream("ColorField", "输入颜色名、rgb() 后失焦写回 #hex；写不成颜色时无效、值不变，失焦后恢复")]
    public Task Typing_commits_hex_and_flags_invalid_text() => HeadlessUi.Run(() =>
    {
        var field = new ColorField { Value = "#3e63dd" };
        var other = new TextBox();
        var window = HeadlessUi.Show(new StackPanel { Children = { field, other } });

        field.Focus();
        field.Text = "rgb(255, 0, 0)";
        other.Focus();
        HeadlessUi.Flush(window);
        Assert.Equal("#ff0000", field.Value);

        field.Focus();
        field.Text = "不是颜色";
        HeadlessUi.Flush(window);
        Assert.Contains(":invalid", field.Classes);
        other.Focus();
        HeadlessUi.Flush(window);
        Assert.Equal("#ff0000", field.Value);
        Assert.Equal("#ff0000", field.Text);
        Assert.DoesNotContain(":invalid", field.Classes);
    });

    [Fact]
    [Upstream("ColorField", "色块是打开取色器的按钮，名称取语言包；只读时不能打开")]
    public Task Swatch_opens_the_picker() => HeadlessUi.Run(() =>
    {
        var field = new ColorField { Value = "#3e63dd" };
        var locked = new ColorField { Value = "#3e63dd", IsReadOnly = true };
        var window = HeadlessUi.Show(new StackPanel { Children = { field, locked } });
        Assert.Equal("选择颜色", AutomationProperties.GetName(field.SwatchPart));
        Assert.False(locked.SwatchPart.IsEnabled);

        ((IInvokeProvider)field.SwatchPart.Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.True(field.PopoverPart.IsOpen);
    });

    [Fact]
    [Upstream("ColorPicker", "ColorPicker：色板上 ←→ 改饱和度、↑↓ 改明度；输入 rgb() 回车写回 #hex")]
    public Task Picker_keyboard_and_input() => HeadlessUi.Run(() =>
    {
        var picker = new ColorPicker { Value = "#ff0000", Alpha = false, Recent = false };
        var window = HeadlessUi.Show(picker);
        Assert.Equal("饱和度与明度", AutomationProperties.GetName(picker.Thumb));
        Assert.Equal("饱和度 100%，明度 100%", AutomationProperties.GetItemStatus(picker.Thumb));
        Press(picker.Thumb, Key.Down, KeyModifiers.Shift);
        HeadlessUi.Flush(window);
        Assert.Equal("#e60000", picker.Value);

        picker.Input.Text = "rgb(0, 128, 255)";
        Press(picker.Input, Key.Enter);
        HeadlessUi.Flush(window);
        Assert.Equal("#0080ff", picker.Value);
    });

    [Fact]
    [Upstream("ColorPicker", "ColorPicker 的部件：各层带 data-part，部件属性合并到对应的节点上")]
    public Task Picker_parts_are_named() => HeadlessUi.Run(() =>
    {
        var picker = new ColorPicker { Value = "#3e63dd", Presets = ["#ff0000"] };
        HeadlessUi.Show(picker);
        var names = picker.GetVisualDescendants().OfType<Control>().Select(control => control.Name).ToHashSet();
        foreach (var part in new[]
                 {
                     ColorPickerParts.Root, ColorPickerParts.Area, ColorPickerParts.Thumb, ColorPickerParts.Hue, ColorPickerParts.Alpha, ColorPickerParts.Preview,
                     ColorPickerParts.Format, ColorPickerParts.Input, ColorPickerParts.Swatches, ColorPickerParts.Swatch,
                 })
            Assert.Contains(part, names);
    });

    [Fact]
    public Task Picking_a_preset_remembers_it_and_keeps_the_hue_on_gray() => HeadlessUi.Run(() =>
    {
        var picker = new ColorPicker { Value = "#3e63dd", Presets = ["#e5484d"] };
        var window = HeadlessUi.Show(picker);
        var swatch = picker.GetVisualDescendants().OfType<Control>().First(control => control.Name == ColorPickerParts.Swatch);
        ((IInvokeProvider)swatch.Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.Equal("#e5484d", picker.Value);
        Assert.Equal("#e5484d", ColorPicker.RecentColors[0]);

        // 拖到最左边（灰色）再回来：色相还在
        var hue = picker.GetVisualDescendants().OfType<Slider>().First(slider => slider.Name == ColorPickerParts.Hue).Value;
        for (var i = 0; i < 10; i++) Press(picker.Thumb, Key.Left, KeyModifiers.Shift);
        Assert.Equal(hue, picker.GetVisualDescendants().OfType<Slider>().First(slider => slider.Name == ColorPickerParts.Hue).Value);
    });

    [Fact]
    [Upstream("ColorPicker.browser", "点色板左下角是黑色；选预设后它进最近使用")]
    public Task Pressing_the_bottom_left_corner_is_black() => HeadlessUi.Run(() =>
    {
        var picker = new ColorPicker { Value = "#ff0000", Presets = ["#12a594"], Alpha = false };
        var window = HeadlessUi.Show(picker);
        var area = picker.GetVisualDescendants().OfType<Control>().First(control => control.Name == ColorPickerParts.Area);
        var corner = area.TranslatePoint(new global::Avalonia.Point(1, area.Bounds.Height - 1), window)!.Value;
        window.MouseDown(corner, global::Avalonia.Input.MouseButton.Left);
        window.MouseUp(corner, global::Avalonia.Input.MouseButton.Left);
        HeadlessUi.Flush(window);
        Assert.Matches("^#0[0-2]0[0-2]0[0-2]$", picker.Value);

        var preset = picker.GetVisualDescendants().OfType<Control>().First(control => control.Name == ColorPickerParts.Swatch && AutomationProperties.GetName(control) == "#12a594");
        ((IInvokeProvider)preset.Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.Equal("#12a594", picker.Value);
        Assert.Contains("#12a594", ColorPicker.RecentColors);
    });

    [Fact]
    [Upstream("ColorPicker.browser", "ColorField：点色块弹出取色器，在色板上按方向键写回")]
    public Task Field_swatch_opens_a_picker_that_writes_back() => HeadlessUi.Run(() =>
    {
        var field = new ColorField { Value = "#3e63dd" };
        var window = HeadlessUi.Show(field);
        ((IInvokeProvider)field.SwatchPart.Peer()).Invoke();
        HeadlessUi.Flush(window);
        var picker = (ColorPicker)field.PopoverPart.Content!;
        Press(picker.Thumb, Key.Down, KeyModifiers.Shift);
        HeadlessUi.Flush(window);
        Assert.NotEqual("#3e63dd", field.Value);
        Assert.Equal(field.Value, field.Text);
    });
}
