using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// Textarea 与 TextField 的 Format：清空、字数；高度在 Rows 与 MaxRows 行之间，Fixed 时固定；
/// 格式按位置分组、过滤字符、截到最长，Value 是原文，输入时重新分组、光标放回原来的字符后面。
/// </summary>
public class TextareaTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    /// <summary>在光标处打字（同键盘输入：TextBox 插入文字、挪光标）。</summary>
    static void Type(TextBox box, string text)
    {
        box.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, text)) { RoutedEvent = TextCompositionManager.TextInputEvent });
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("Textarea", "有内容时显示清空按钮，清空后回到空字符串")]
    public Task Clearing_a_textarea_empties_it() => WpfUi.Run(() =>
    {
        var note = new Textarea { Text = "第一行\n第二行", Clearable = true };
        WpfUi.Show(Host(note));
        var clear = note.Part<ClearButton>(TextInputParts.Clear);

        Assert.Equal(Visibility.Visible, clear.Visibility);
        ((IInvokeProvider)clear.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.Equal("", note.Text);
        Assert.Equal(Visibility.Collapsed, clear.Visibility);
    });

    [Fact]
    [Upstream("Textarea", "Textarea：showCount 没有上限时只显示字数")]
    public Task Without_a_limit_the_count_shows_the_length() => WpfUi.Run(() =>
    {
        var note = new Textarea { Text = "abc", ShowCount = true };
        WpfUi.Show(Host(new Field { Label = "简介", Description = "写点什么", Content = note }));
        var count = note.Part<CharacterCount>(TextInputParts.Count);

        Assert.Equal(Visibility.Visible, count.Visibility);
        Assert.Equal(3, count.Count);
        Assert.Null(count.Max);
        Assert.Equal("简介", note.Peer().GetName());
    });

    [Fact]
    public Task The_height_grows_between_rows_and_max_rows() => WpfUi.Run(() =>
    {
        var note = new Textarea { Rows = 2, MaxRows = 4, Width = 300 };
        WpfUi.Show(Host(note));
        var line = TextBlock.GetLineHeight(note);
        var empty = note.ActualHeight;

        note.Text = string.Join("\n", Enumerable.Range(1, 3));
        WpfUi.Flush();
        var three = note.ActualHeight;
        note.Text = string.Join("\n", Enumerable.Range(1, 10));
        WpfUi.Flush();
        var ten = note.ActualHeight;

        Assert.True(three > empty);
        // 最多 4 行（含上下内边距），最少 2 行加上下各 0.25em
        Assert.Equal(4 * line - (2 * line + 0.5 * note.FontSize), ten - empty, 1);

        note.Fixed = true;
        WpfUi.Flush();
        Assert.Equal(empty, note.ActualHeight, 1);
    });
    [Fact]
    [Upstream("text-format", "手机号 3-4-4、身份证 6-8-4（X 转大写）、银行卡每 4 位")]
    public void Formats_group_filter_and_truncate()
    {
        Assert.Equal("138 0000 1111", TextFormat.Phone.Display("13800001111"));
        Assert.Equal("13800001111", TextFormat.Phone.Parse("138-0000 11112222"));
        Assert.Equal("11010519491231002X", TextFormat.IdCard.Parse("11010519491231002x"));
        Assert.Equal("110105 19491231 002X", TextFormat.IdCard.Display("11010519491231002X"));
        Assert.Equal("6222 0202 0011 2233 445", TextFormat.BankCard.Display("6222020200112233445"));
        Assert.Equal("12-34-5", TextFormat.Grouped([2], separator: "-").Display("12345"));
    }

    [Fact]
    [Upstream("text-format", "displayPosition：原文第 n 个字符之后在显示文字里的位置")]
    public void Display_positions_skip_separators()
    {
        Assert.Equal(3, TextFormat.Phone.DisplayPosition("13800001111", 3));
        Assert.Equal(5, TextFormat.Phone.DisplayPosition("13800001111", 4));
        Assert.Equal(13, TextFormat.Phone.DisplayPosition("13800001111", 11));
        Assert.Equal(0, TextFormat.Phone.DisplayPosition("13800001111", 0));
    }

    [Fact]
    [Upstream("text-format", "显示分组的文字、值是原文；输入时重新分组")]
    public Task A_formatted_field_shows_groups_and_keeps_the_raw_value() => WpfUi.Run(() =>
    {
        var phone = new TextField { Format = TextFormat.Phone, Value = "1380000" };
        var window = WpfUi.Show(Host(phone));
        Assert.Equal("138 0000", phone.Text);

        // 打进最后四位：光标在末尾，重新分组后仍在末尾
        window.Activate();
        phone.Focus();
        phone.CaretIndex = phone.Text.Length;
        Type(phone, "1111");
        Assert.Equal("13800001111", phone.Value);
        Assert.Equal("138 0000 1111", phone.Text);
        Assert.Equal(13, phone.CaretIndex);

        // 在中间插字：光标停在新字符后面，不跳到最后；超出 11 位的截掉
        phone.CaretIndex = 4;
        Type(phone, "9");
        Assert.Equal("13890000111", phone.Value);
        Assert.Equal(5, phone.CaretIndex);
    });
}
