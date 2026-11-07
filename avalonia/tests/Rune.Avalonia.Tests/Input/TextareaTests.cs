using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Textarea 与 TextField 的 Format：清空、字数；高度在 Rows 与 MaxRows 行之间，Fixed 时固定；
/// 格式按位置分组、过滤字符、截到最长，Value 是原文，输入时重新分组、光标放回原来的字符后面。
/// </summary>
public class TextareaTests
{
    static T Part<T>(Control owner, string name) where T : Control => owner.GetVisualDescendants().OfType<T>().First(part => part.Name == name);

    [Fact]
    [Upstream("Textarea", "有内容时显示清空按钮，清空后回到空字符串")]
    public Task Clearing_a_textarea_empties_it() => HeadlessUi.Run(() =>
    {
        var note = new Textarea { Text = "第一行\n第二行", Clearable = true };
        var window = HeadlessUi.Show(note);
        var clear = Part<ClearButton>(note, TextInputParts.Clear);

        Assert.True(clear.IsVisible);
        ((IInvokeProvider)clear.Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.Equal("", note.Text);
        Assert.False(clear.IsVisible);
    });

    [Fact]
    [Upstream("Textarea", "Textarea：showCount 没有上限时只显示字数")]
    public Task Without_a_limit_the_count_shows_the_length() => HeadlessUi.Run(() =>
    {
        var note = new Textarea { Text = "abc", ShowCount = true };
        HeadlessUi.Show(new Field { Label = "简介", Description = "写点什么", Content = note });
        var count = Part<CharacterCount>(note, TextInputParts.Count);

        Assert.True(count.IsVisible);
        Assert.Equal(3, count.Count);
        Assert.Null(count.Max);
        Assert.Equal("简介", note.Peer().GetName());
    });

    [Fact]
    public Task The_height_grows_between_rows_and_max_rows() => HeadlessUi.Run(() =>
    {
        var note = new Textarea { Rows = 2, MaxRows = 4, Width = 300 };
        var window = HeadlessUi.Show(new StackPanel { Children = { note } });
        var line = note.LineHeight;
        var empty = note.Bounds.Height;

        note.Text = string.Join("\n", Enumerable.Range(1, 3));
        HeadlessUi.Flush(window);
        var three = note.Bounds.Height;
        note.Text = string.Join("\n", Enumerable.Range(1, 10));
        HeadlessUi.Flush(window);
        var ten = note.Bounds.Height;

        Assert.True(three > empty);
        // 最多 4 行（含上下内边距），最少 2 行加上下各 0.25em
        Assert.Equal(4 * line - (2 * line + 0.5 * note.FontSize), ten - empty, 1);

        note.Fixed = true;
        HeadlessUi.Flush(window);
        Assert.Equal(empty, note.Bounds.Height, 1);
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
    public Task A_formatted_field_shows_groups_and_keeps_the_raw_value() => HeadlessUi.Run(() =>
    {
        var phone = new TextField { Format = TextFormat.Phone, Value = "1380000" };
        var window = HeadlessUi.Show(phone);
        Assert.Equal("138 0000", phone.Text);

        // 打进最后四位：光标在末尾，重新分组后仍在末尾
        phone.Focus();
        phone.CaretIndex = phone.Text!.Length;
        window.KeyTextInput("1111");
        HeadlessUi.Flush(window);
        Assert.Equal("13800001111", phone.Value);
        Assert.Equal("138 0000 1111", phone.Text);
        Assert.Equal(13, phone.CaretIndex);

        // 在中间插字：光标停在新字符后面，不跳到最后；超出 11 位的截掉
        phone.CaretIndex = 4;
        window.KeyTextInput("9");
        HeadlessUi.Flush(window);
        Assert.Equal("13890000111", phone.Value);
        Assert.Equal(5, phone.CaretIndex);
    });
}
