namespace Rune.WinForms.Tests;

/// <summary>
/// TextFieldControl：清空后写回空字符串并聚焦；不可用、只读时没有清空按钮；密码框的显示按钮名称不变、切换显示；在 FieldControl 里以标签为名称、沿用出错，
/// 控件上写的状态优先；字数计数的上限取 MaxLength，超出时用危险色，读屏念完整的句子。
/// </summary>
public class TextFieldTests
{
    [Fact]
    public void Soft_shells_darken_on_hover_and_read_only_stays_fill()
    {
        var colors = RuneTheme.Current.Value.Colors;
        Assert.Equal(colors.FillHover, ControlShell.Fill(colors, ControlVariant.Soft, readOnly: false, hovered: true));
        Assert.Equal(colors.Fill, ControlShell.Fill(colors, ControlVariant.Soft, readOnly: false, hovered: false));
        Assert.Equal(colors.Fill, ControlShell.Fill(colors, ControlVariant.Soft, readOnly: true, hovered: true));
        Assert.Equal(colors.Surface, ControlShell.Fill(colors, ControlVariant.Surface, readOnly: false, hovered: true));
    }

    [Fact]
    [Upstream("TextField", "未声明的属性落到 input；外壳用 propsRoot；清空后写回并聚焦 input")]
    public Task Clearing_writes_back_an_empty_string_and_focuses_the_input() => WinFormsUi.Run(() =>
    {
        var field = new TextFieldControl { Text = "设计规范", Clearable = true, Width = 300 };
        using var form = WinFormsUi.Show(field);

        Assert.True(field.ClearButton.Visible);
        field.ClearButton.PerformClick();
        Assert.Equal("", field.Text);
        Assert.True(field.Input.Focused || form.ActiveControl == field.Input || field.ContainsFocus || !form.ContainsFocus);
        Assert.False(field.ClearButton.Visible);
    });

    [Fact]
    [Upstream("TextField", "不可用、只读时不显示清空按钮")]
    public Task Disabled_or_read_only_fields_have_no_clear_button() => WinFormsUi.Run(() =>
    {
        var disabled = new TextFieldControl { Text = "a", Clearable = true, Enabled = false, Width = 300 };
        var readOnly = new TextFieldControl { Text = "a", Clearable = true, IsReadOnly = true, Width = 300, Top = 60 };
        using var form = WinFormsUi.Show(disabled, readOnly);

        Assert.False(disabled.ClearButton.Visible);
        Assert.False(readOnly.ClearButton.Visible);
        Assert.True(readOnly.Input.ReadOnly);
    });

    [Fact]
    [Upstream("TextField", "密码框：切换按钮名称不变")]
    public Task The_password_reveal_button_keeps_its_name_and_toggles() => WinFormsUi.Run(() =>
    {
        var field = new TextFieldControl { Text = "secret", Type = TextFieldType.Password, Width = 300 };
        using var form = WinFormsUi.Show(field);

        Assert.True(field.Input.UseSystemPasswordChar);
        Assert.Equal("显示密码", field.RevealButton.AccessibilityObject.Name);
        field.RevealButton.PerformClick();
        Assert.True(field.RevealPassword);
        Assert.False(field.Input.UseSystemPasswordChar);
        Assert.Equal("显示密码", field.RevealButton.AccessibilityObject.Name);
    });

    [Fact]
    [Upstream("Field", "控件上显式写的状态优先于 Field")]
    [Upstream("Field", "标签的 for 指向控件")]
    public Task Fields_name_the_control_and_explicit_states_win() => WinFormsUi.Run(() =>
    {
        var inField = new TextFieldControl { Text = "admin" };
        var overridden = new TextFieldControl { Text = "admin", IsInvalid = false };
        var first = new FieldControl { Label = "用户名", Error = "已被占用", Content = inField, Width = 300 };
        var second = new FieldControl { Label = "别名", Error = "已被占用", Content = overridden, Width = 300, Top = 120 };
        using var form = WinFormsUi.Show(first, second);

        Assert.Equal("用户名", inField.AccessibilityObject.Name);
        Assert.True(inField.Invalid);
        Assert.False(overridden.Invalid);
    });

    [Fact]
    [Upstream("TextField", "showCount：上限取 maxlength")]
    public Task The_count_takes_its_limit_from_max_length() => WinFormsUi.Run(() =>
    {
        var field = new TextFieldControl { ShowCount = true, MaxLength = 3, Width = 300 };
        using var form = WinFormsUi.Show(field);
        field.Text = "hel";

        Assert.Equal("3 / 3", field.Count.Text);
        Assert.False(field.Count.IsOver);
        Assert.Equal("已输入 3 个字符，最多 3 个", field.Count.AccessibleName);
        field.Count.Count = 5;
        Assert.True(field.Count.IsOver);
    });

    [Fact]
    public Task Clicking_a_part_that_cannot_take_focus_focuses_the_input() => WinFormsUi.Run(() =>
    {
        var field = new TextFieldControl { Icon = Lucide.Search, Clearable = true, Text = "规范", Width = 300 };
        var before = new TextFieldControl { Width = 300 };
        using var form = WinFormsUi.Show(before, field);
        before.Input.Focus();

        var icon = field.Controls.OfType<IconControl>().First(control => control.Visible);
        typeof(Control).GetMethod("OnMouseDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(icon, [new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)]);
        Assert.Equal(field.Input, form.ActiveControl);
    });
}
