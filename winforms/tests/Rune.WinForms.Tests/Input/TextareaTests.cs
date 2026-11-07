namespace Rune.WinForms.Tests;

/// <summary>
/// TextareaControl 与 TextFieldControl 的 Format：清空、字数；高度在 Rows 与 MaxRows 行之间，Fixed 时固定；
/// 格式按位置分组、过滤字符、截到最长，Value 是原文，输入时重新分组、光标放回原来的字符后面。
/// </summary>
public class TextareaTests
{
    [Fact]
    [Upstream("Textarea", "有内容时显示清空按钮，清空后回到空字符串")]
    public Task Clearing_a_textarea_empties_it() => WinFormsUi.Run(() =>
    {
        var note = new TextareaControl { Text = "第一行\r\n第二行", Clearable = true, Width = 300 };
        using var form = WinFormsUi.Show(note);

        Assert.True(note.ClearButton.Visible);
        note.ClearButton.PerformClick();
        Assert.Equal("", note.Text);
        Assert.False(note.ClearButton.Visible);
    });

    [Fact]
    [Upstream("Textarea", "Textarea：showCount 没有上限时只显示字数")]
    public Task Without_a_limit_the_count_shows_the_length() => WinFormsUi.Run(() =>
    {
        var note = new TextareaControl { Text = "abc", ShowCount = true, Width = 300 };
        using var form = WinFormsUi.Show(new FieldControl { Label = "简介", Description = "写点什么", Content = note, Width = 320 });

        Assert.True(note.Count.Visible);
        Assert.Equal(3, note.Count.Count);
        Assert.Null(note.Count.Max);
        Assert.Equal("简介", note.AccessibilityObject.Name);
    });

    [Fact]
    public Task The_height_grows_between_rows_and_max_rows() => WinFormsUi.Run(() =>
    {
        var note = new TextareaControl { Rows = 2, MaxRows = 4, Width = 300 };
        using var form = WinFormsUi.Show(note);
        var empty = note.Height;

        note.Text = string.Join("\r\n", Enumerable.Range(1, 3));
        WinFormsUi.Pump();
        var three = note.Height;
        note.Text = string.Join("\r\n", Enumerable.Range(1, 10));
        WinFormsUi.Pump();
        var ten = note.Height;

        Assert.True(three > empty);
        Assert.True(ten > three);
        Assert.Equal(ScrollBars.Vertical, note.Input.ScrollBars);
        note.Text = string.Join("\r\n", Enumerable.Range(1, 12));
        WinFormsUi.Pump();
        Assert.Equal(ten, note.Height);

        note.Fixed = true;
        WinFormsUi.Pump();
        Assert.Equal(empty, note.Height);
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
    public Task A_formatted_field_shows_groups_and_keeps_the_raw_value() => WinFormsUi.Run(() =>
    {
        var phone = new TextFieldControl { Format = TextFormat.Phone, Value = "1380000", Width = 300 };
        using var form = WinFormsUi.Show(phone);
        var changes = 0;
        phone.ValueChanged += (_, _) => changes++;
        Assert.Equal("138 0000", phone.Text);

        // 打进最后四位（同键盘输入：在光标处替换选中的文字）：重新分组后光标仍在末尾
        phone.Input.SelectionStart = phone.Text.Length;
        phone.Input.SelectedText = "1111";
        Assert.Equal("13800001111", phone.Value);
        Assert.Equal("138 0000 1111", phone.Text);
        Assert.Equal(13, phone.Input.SelectionStart);
        Assert.Equal(1, changes);

        // 在中间插字：光标停在新字符后面，不跳到最后；超出 11 位的截掉
        phone.Input.SelectionStart = 4;
        phone.Input.SelectionLength = 0;
        phone.Input.SelectedText = "9";
        Assert.Equal("13890000111", phone.Value);
        Assert.Equal(5, phone.Input.SelectionStart);
    });
}
