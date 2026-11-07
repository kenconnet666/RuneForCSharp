using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// TextField：清空后写回空字符串并聚焦；不可用、只读时没有清空按钮；密码框的显示按钮名称不变、切换显示；在 Field 里以标签为名称、沿用出错，
/// 控件上写的状态优先；字数计数的上限取 MaxLength，超出时标记 :over，读屏念完整的句子。
/// </summary>
public class TextFieldTests
{
    static T Part<T>(Control owner, string name) where T : Control => owner.GetVisualDescendants().OfType<T>().First(part => part.Name == name);

    static void Invoke(Control control) => ((IInvokeProvider)control.Peer()).Invoke();

    [Fact]
    [Upstream("TextField", "有内容时显示清空按钮，清空后回到空字符串")]
    [Upstream("TextField", "未声明的属性落到 input；外壳用 propsRoot；清空后写回并聚焦 input")]
    public Task Clearing_writes_back_an_empty_string_and_focuses_the_input() => HeadlessUi.Run(() =>
    {
        var field = new TextField { Text = "设计规范", Clearable = true };
        var window = HeadlessUi.Show(field);
        var clear = Part<ClearButton>(field, TextFieldParts.Clear);

        Assert.True(clear.IsVisible);
        Invoke(clear);
        HeadlessUi.Flush(window);
        Assert.Equal("", field.Text);
        Assert.True(field.IsFocused);
        Assert.False(clear.IsVisible);
    });

    [Fact]
    [Upstream("TextField", "不可用、只读时不显示清空按钮")]
    public Task Disabled_or_read_only_fields_have_no_clear_button() => HeadlessUi.Run(() =>
    {
        var disabled = new TextField { Text = "a", Clearable = true, IsEnabled = false };
        var readOnly = new TextField { Text = "a", Clearable = true, IsReadOnly = true };
        HeadlessUi.Show(new StackPanel { Children = { disabled, readOnly } });

        Assert.False(Part<ClearButton>(disabled, TextFieldParts.Clear).IsVisible);
        Assert.False(Part<ClearButton>(readOnly, TextFieldParts.Clear).IsVisible);
    });

    [Fact]
    [Upstream("TextField", "密码框：切换按钮名称不变")]
    public Task The_password_reveal_button_keeps_its_name_and_toggles() => HeadlessUi.Run(() =>
    {
        var field = new TextField { Text = "secret", Type = TextFieldType.Password };
        var window = HeadlessUi.Show(field);
        var reveal = Part<IconButton>(field, TextFieldParts.Reveal);

        Assert.Equal('•', field.PasswordChar);
        Assert.Equal("显示密码", reveal.Peer().GetName());
        Invoke(reveal);
        HeadlessUi.Flush(window);
        Assert.True(field.RevealPassword);
        Assert.Equal("显示密码", reveal.Peer().GetName());
    });

    [Fact]
    [Upstream("Field", "控件上显式写的状态优先于 Field")]
    [Upstream("Field", "标签的 for 指向控件")]
    public Task Fields_name_the_control_and_explicit_states_win() => HeadlessUi.Run(() =>
    {
        var inField = new TextField { Text = "admin" };
        var overridden = new TextField { Text = "admin", IsInvalid = false };
        HeadlessUi.Show(new StackPanel
        {
            Children = { new Field { Label = "用户名", Error = "已被占用", Content = inField }, new Field { Label = "别名", Error = "已被占用", Content = overridden } },
        });

        Assert.Equal("用户名", inField.Peer().GetName());
        Assert.Contains(":invalid", inField.Classes);
        Assert.DoesNotContain(":invalid", overridden.Classes);
    });

    [Fact]
    [Upstream("TextField", "showCount：上限取 maxlength")]
    public Task The_count_takes_its_limit_from_max_length() => HeadlessUi.Run(() =>
    {
        var field = new TextField { Text = "hello", ShowCount = true, MaxLength = 3 };
        var window = HeadlessUi.Show(field);
        var count = Part<CharacterCount>(field, TextFieldParts.Count);

        Assert.Equal("5 / 3", count.Text);
        Assert.Contains(":over", count.Classes);
        Assert.Equal("已输入 5 个字符，最多 3 个", count.Peer().GetName());
        field.Text = "hi";
        HeadlessUi.Flush(window);
        Assert.DoesNotContain(":over", count.Classes);
    });
}
