using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Rune.Wpf.Tests;

/// <summary>NativeSelect：字符串的值就是文字、带组的按组排（组名是选不了的标题）、没选时显示占位；选了写回值、改值跟着选中；只读时别的选项选不了、不打开；放进 Field 取标签、出错。</summary>
public class NativeSelectTests
{
    static readonly object[] Cities =
    [
        new ListboxOption("sh", "上海") { Group = "华东" }, new ListboxOption("gz", "广州") { Group = "华南" },
        new ListboxOption("hz", "杭州") { Group = "华东" }, "其他",
        new ListboxOption("hk", "香港") { Group = "华南", IsDisabled = true },
    ];

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static IReadOnlyList<NativeSelectItem> Rows(NativeSelect select) => [.. select.ItemsSource.Cast<NativeSelectItem>()];

    [Fact]
    [Upstream("NativeSelect", "选项与分组")]
    public Task Strings_and_groups_with_a_placeholder() => WpfUi.Run(() =>
    {
        var select = new NativeSelect { Options = Cities, PlaceholderText = "选择城市" };
        WpfUi.Show(Host(select));
        var rows = Rows(select);

        // 没有组的在前，各组按第一次出现的顺序；组名是一行选不了的标题
        Assert.Equal(["其他", "华东", "上海", "杭州", "华南", "广州", "香港"], rows.Select(row => (string)row.Content));
        Assert.False(rows[1].IsEnabled);
        Assert.Equal(NativeSelectRow.Group, rows[1].Row);
        Assert.Equal(NativeSelectRow.Grouped, rows[2].Row);
        Assert.Equal("其他", rows[0].Tag); // 字符串的值就是文字
        Assert.False(rows[6].IsEnabled); // 不可用的选项
        Assert.Null(select.SelectedItem);
        Assert.True(select.ShowsPlaceholder);
        Assert.True(((TextBlock)select.Template.FindName("PART_Placeholder", select)).IsVisible);
    });

    [Fact]
    [Upstream("NativeSelect", "选了写回值")]
    public Task Picking_writes_the_value_and_the_value_selects() => WpfUi.Run(() =>
    {
        var select = new NativeSelect { Options = Cities };
        WpfUi.Show(Host(select));
        var rows = Rows(select);

        select.SelectedItem = rows[3];
        Assert.Equal("hz", select.Value);
        Assert.False(select.ShowsPlaceholder);
        select.Value = "gz";
        Assert.Equal(rows[5], select.SelectedItem);
        select.Value = null;
        Assert.Null(select.SelectedItem);
        Assert.True(select.ShowsPlaceholder);
    });

    [Fact]
    [Upstream("NativeSelect", "只读：别的选项选不了")]
    public Task Read_only_locks_the_other_options() => WpfUi.Run(() =>
    {
        var own = new NativeSelect { Options = Cities, Value = "sh", IsReadOnly = true };
        var inField = new NativeSelect { Options = Cities, Value = "sh" };
        WpfUi.Show(Host(own, new Field { Label = "城市", IsReadOnly = true, Content = inField }));

        foreach (var select in new[] { own, inField })
        {
            var rows = Rows(select);
            Assert.True(rows[2].IsEnabled); // 当前的值照常显示
            Assert.False(rows[3].IsEnabled);
            Assert.True(select.ShowsReadOnly);
            select.IsDropDownOpen = true;
            Assert.False(select.IsDropDownOpen); // 只读时不打开
            Assert.False(((ToggleButton)select.Template.FindName("PART_Toggle", select)).IsHitTestVisible);
        }
        own.IsReadOnly = false;
        Assert.True(Rows(own)[3].IsEnabled);
        own.IsDropDownOpen = true;
        WpfUi.Flush();
        Assert.True(((Popup)own.Template.FindName("PART_Popup", own)).IsOpen);
    });

    [Fact]
    [Upstream("NativeSelect", "放进 Field")]
    public Task Field_names_it_and_marks_it_invalid() => WpfUi.Run(() =>
    {
        var select = new NativeSelect { Options = Cities };
        WpfUi.Show(Host(new Field { Label = "收货城市", Error = "请选择城市", Content = select }));

        var peer = select.Peer();
        Assert.Equal("收货城市", peer.GetName());
        Assert.Contains("请选择城市", peer.GetHelpText());
        Assert.Equal(AutomationControlType.ComboBox, peer.GetAutomationControlType());
        Assert.True(select.ShowsInvalid);
    });
}
