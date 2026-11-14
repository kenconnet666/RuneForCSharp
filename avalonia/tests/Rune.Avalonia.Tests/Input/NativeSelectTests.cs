using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>NativeSelect：字符串的值就是文字、带组的按组排（组名是选不了的标题）、没选时显示占位；选了写回值、改值跟着选中；只读时别的选项选不了；放进 Field 取标签、出错。</summary>
public class NativeSelectTests
{
    static readonly object[] Cities =
    [
        new ListboxOption("sh", "上海") { Group = "华东" }, new ListboxOption("gz", "广州") { Group = "华南" },
        new ListboxOption("hz", "杭州") { Group = "华东" }, "其他",
        new ListboxOption("hk", "香港") { Group = "华南", IsDisabled = true },
    ];

    static IReadOnlyList<ComboBoxItem> Rows(NativeSelect select) => [.. select.ItemsSource!.Cast<ComboBoxItem>()];

    [Fact]
    [Upstream("NativeSelect", "选项与分组")]
    public Task Strings_and_groups_with_a_placeholder() => HeadlessUi.Run(() =>
    {
        var select = new NativeSelect { Options = Cities, PlaceholderText = "选择城市" };
        HeadlessUi.Show(select);
        var rows = Rows(select);

        // 没有组的在前，各组按第一次出现的顺序；组名是一行选不了的标题
        Assert.Equal(["其他", "华东", "上海", "杭州", "华南", "广州", "香港"], rows.Select(row => (string)row.Content!));
        Assert.False(rows[1].IsEnabled);
        Assert.Contains("group", rows[1].Classes);
        Assert.Equal("其他", rows[0].Tag); // 字符串的值就是文字
        Assert.False(rows[6].IsEnabled); // 不可用的选项
        Assert.Null(select.SelectedItem);
        Assert.Null(select.Value);
    });

    [Fact]
    [Upstream("NativeSelect", "选了写回值")]
    public Task Picking_writes_the_value_and_the_value_selects() => HeadlessUi.Run(() =>
    {
        var select = new NativeSelect { Options = Cities };
        HeadlessUi.Show(select);
        var rows = Rows(select);

        select.SelectedItem = rows[3];
        Assert.Equal("hz", select.Value);
        select.Value = "gz";
        Assert.Equal(rows[5], select.SelectedItem);
        select.Value = null;
        Assert.Null(select.SelectedItem);
    });

    [Fact]
    [Upstream("NativeSelect", "只读：别的选项选不了")]
    public Task Read_only_locks_the_other_options() => HeadlessUi.Run(() =>
    {
        var select = new NativeSelect { Options = Cities, Value = "sh", IsReadOnly = true };
        HeadlessUi.Show(select);
        var rows = Rows(select);

        Assert.True(rows[2].IsEnabled); // 当前的值照常显示
        Assert.False(rows[3].IsEnabled);
        Assert.Contains(":readonly", select.Classes);
        select.IsDropDownOpen = true;
        Assert.False(select.IsDropDownOpen); // 只读时不打开
        select.IsReadOnly = false;
        Assert.True(rows[3].IsEnabled);
    });

    [Fact]
    [Upstream("NativeSelect", "放进 Field")]
    public Task Field_names_it_and_marks_it_invalid() => HeadlessUi.Run(() =>
    {
        var select = new NativeSelect { Options = Cities };
        var field = new Field { Label = "收货城市", Error = "请选择城市", Content = select };
        HeadlessUi.Show(field);

        Assert.Equal("收货城市", ControlAutomationPeer.CreatePeerForElement(select).GetName());
        Assert.Contains(":invalid", select.Classes);
        Assert.Equal(AutomationControlType.ComboBox, ControlAutomationPeer.CreatePeerForElement(select).GetAutomationControlType());
    });
}
