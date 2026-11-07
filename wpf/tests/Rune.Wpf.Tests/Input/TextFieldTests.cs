using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>
/// TextField：清空后写回空字符串并聚焦；不可用、只读时没有清空按钮；密码框的显示按钮名称不变、切换显示；在 Field 里以标签为名称、沿用出错，
/// 控件上写的状态优先；字数计数的上限取 MaxLength，超出时标记 IsOver，读屏念完整的句子。
/// </summary>
public class TextFieldTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static void Invoke(UIElement element)
    {
        ((IInvokeProvider)element.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("TextField", "未声明的属性落到 input；外壳用 propsRoot；清空后写回并聚焦 input")]
    public Task Clearing_writes_back_an_empty_string_and_focuses_the_input() => WpfUi.Run(() =>
    {
        var field = new TextField { Text = "设计规范", Clearable = true };
        var window = WpfUi.Show(Host(field));
        window.Activate();
        var clear = field.Part<ClearButton>(TextFieldParts.Clear);

        Assert.Equal(Visibility.Visible, clear.Visibility);
        Invoke(clear);
        Assert.Equal("", field.Text);
        Assert.True(field.IsKeyboardFocusWithin);
        Assert.Equal(Visibility.Collapsed, clear.Visibility);
    });

    [Fact]
    [Upstream("TextField", "不可用、只读时不显示清空按钮")]
    public Task Disabled_or_read_only_fields_have_no_clear_button() => WpfUi.Run(() =>
    {
        var disabled = new TextField { Text = "a", Clearable = true, IsEnabled = false };
        var readOnly = new TextField { Text = "a", Clearable = true, IsReadOnly = true };
        WpfUi.Show(Host(disabled, readOnly));

        Assert.Equal(Visibility.Collapsed, disabled.Part<ClearButton>(TextFieldParts.Clear).Visibility);
        Assert.Equal(Visibility.Collapsed, readOnly.Part<ClearButton>(TextFieldParts.Clear).Visibility);
    });

    [Fact]
    [Upstream("TextField", "密码框：切换按钮名称不变")]
    public Task The_password_reveal_button_keeps_its_name_and_toggles() => WpfUi.Run(() =>
    {
        var field = new TextField { Text = "secret", Type = TextFieldType.Password };
        WpfUi.Show(Host(field));
        var reveal = field.Part<IconButton>(TextFieldParts.Reveal);
        var password = field.Part<PasswordBox>(TextFieldParts.Password);

        Assert.Equal(Visibility.Visible, password.Visibility);
        Assert.Equal("secret", password.Password);
        Assert.Equal("显示密码", reveal.Peer().GetName());
        Invoke(reveal);
        Assert.True(field.RevealPassword);
        Assert.Equal(Visibility.Collapsed, password.Visibility);
        Assert.Equal("显示密码", reveal.Peer().GetName());
    });

    [Fact]
    [Upstream("Field", "控件上显式写的状态优先于 Field")]
    [Upstream("Field", "标签的 for 指向控件")]
    public Task Fields_name_the_control_and_explicit_states_win() => WpfUi.Run(() =>
    {
        var inField = new TextField { Text = "admin" };
        var overridden = new TextField { Text = "admin", IsInvalid = false };
        WpfUi.Show(Host(new Field { Label = "用户名", Error = "已被占用", Content = inField }, new Field { Label = "别名", Error = "已被占用", Content = overridden }));

        Assert.Equal("用户名", inField.Peer().GetName());
        Assert.True(inField.ShowsInvalid);
        Assert.False(overridden.ShowsInvalid);
    });

    [Fact]
    [Upstream("TextField", "showCount：上限取 maxlength")]
    public Task The_count_takes_its_limit_from_max_length() => WpfUi.Run(() =>
    {
        var field = new TextField { Text = "hel", ShowCount = true, MaxLength = 2 };
        WpfUi.Show(Host(field));
        var count = field.Part<CharacterCount>(TextFieldParts.Count);

        Assert.Equal("3 / 2", count.Text);
        Assert.True(count.IsOver);
        Assert.Equal("已输入 3 个字符，最多 2 个", count.Peer().GetName());
        field.Text = "hi";
        WpfUi.Flush();
        Assert.False(count.IsOver);
    });
}
