using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Checklist：各项的状态写给读屏、超时的日期用危险色、不适用的不算进汇总；可以勾选时状态是复选框；inline 一行排开；icons 只有图标，整组一个说明。</summary>
public class ChecklistTests
{
    static ChecklistItem[] Items() =>
    [
        new() { Label = "验收报告", Status = ChecklistStatus.Done, Date = "10-12", Evidence = new Text { Text = "验证报告.pdf" } },
        new() { Label = "财务复核意见", Status = ChecklistStatus.Overdue, Owner = "财务部", Date = "计划 10-20" },
        new() { Label = "合同签订记录", Description = "谈定价格与生效日期", Owner = "采购部" },
        new() { Label = "海关备案", Status = ChecklistStatus.Skipped },
    ];

    static IEnumerable<string?> Texts(DependencyObject root) => WpfUi.Descendants(root).OfType<TextBlock>().Select(text => text.Text);

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static IToggleProvider Toggle(UIElement box) => (IToggleProvider)box.Peer().GetPattern(PatternInterface.Toggle);

    [Fact]
    [Upstream("Checklist", "一项一行：状态写给读屏")]
    public Task One_row_per_item_with_the_status_for_screen_readers() => WpfUi.Run(() =>
    {
        var items = Items();
        var checklist = new Checklist { Items = items };
        WpfUi.Show(Host(checklist));

        var rows = checklist.Rows;
        Assert.Equal([ChecklistStatus.Done, ChecklistStatus.Overdue, ChecklistStatus.Todo, ChecklistStatus.Skipped], rows.Select(row => row.Item.Status));
        Assert.Equal(AutomationControlType.ListItem, rows[0].Peer().GetAutomationControlType());
        Assert.Equal("已完成", WpfUi.Descendants(rows[0].Status).OfType<Icon>().Single().Peer().GetName());
        Assert.Equal(["验收报告", "10-12"], Texts(rows[0]).Where(text => text is "验收报告" or "10-12"));
        Assert.Contains("谈定价格与生效日期", Texts(rows[2]));
        var late = rows[1].Meta!.Children.OfType<Text>().Single(text => text.Text == "计划 10-20");
        Assert.Equal(TextTone.Danger, late.Tone);
        // 没完成的名称是次要色，不适用的划掉
        Assert.Equal(TextTone.Muted, ((Text)rows[2].LabelPart!).Tone);
        Assert.NotNull(((Text)rows[3].LabelPart!).TextDecorations);
    });

    [Fact]
    [Upstream("Checklist", "summary：已完成数 / 总数，不适用的不算")]
    public Task Summary_counts_without_skipped_items() => WpfUi.Run(() =>
    {
        var checklist = new Checklist { Items = Items(), Summary = true };
        WpfUi.Show(Host(checklist));

        Assert.Contains("已完成 1 / 3", Texts(checklist.SummaryPart!));
        var progress = checklist.SummaryPart!.Children.OfType<Progress>().Single();
        Assert.Equal(1, progress.Value);
        Assert.Equal(3, progress.Max);
    });

    [Fact]
    [Upstream("Checklist", "可以勾选：状态是 role=checkbox 的按钮，名称是这一项；不适用的不能勾")]
    public Task Checkable_status_is_a_checkbox_named_by_the_item() => WpfUi.Run(() =>
    {
        var items = Items();
        var checklist = new Checklist { Items = items };
        var changes = new List<(ChecklistItem, bool)>();
        checklist.CheckedChange += (_, e) => changes.Add((e.Item, e.Done));
        WpfUi.Show(Host(checklist));

        var boxes = checklist.Rows.Select(row => WpfUi.Descendants(row.Status).OfType<System.Windows.Controls.Button>().Single()).ToList();
        Assert.All(boxes, box => Assert.Equal(AutomationControlType.CheckBox, box.Peer().GetAutomationControlType()));
        Assert.Equal([ToggleState.On, ToggleState.Off, ToggleState.Off, ToggleState.Off], boxes.Select(box => Toggle(box).ToggleState));
        Assert.Equal("财务复核意见", boxes[1].Peer().GetName());
        Assert.Equal("已超时", boxes[1].Peer().GetHelpText());
        Assert.False(boxes[3].IsEnabled);
        Toggle(boxes[1]).Toggle();
        Assert.Equal((items[1], true), changes[^1]);
        Toggle(boxes[0]).Toggle();
        Assert.Equal((items[0], false), changes[^1]);
    });

    [Fact]
    [Upstream("Checklist", "inline 一行排开；icons 只有图标，整组一个说明")]
    public Task Inline_and_icons_layouts() => WpfUi.Run(() =>
    {
        var items = Items();
        var inline = new Checklist { Layout = ChecklistLayout.Inline, Items = items[..2] };
        var icons = new Checklist { Layout = ChecklistLayout.Icons, Items = items[..3] };
        WpfUi.Show(Host(inline, icons));

        Assert.Equal(2, inline.Rows.Count);
        var row = icons.IconsPart!;
        Assert.Equal(AutomationControlType.Image, row.Peer().GetAutomationControlType());
        Assert.Equal("验收报告已完成，财务复核意见已超时，合同签订记录未完成", row.Peer().GetName());
        Assert.Equal("验收报告已完成，财务复核意见已超时，合同签订记录未完成", row.ToolTip);
        Assert.Equal(3, row.Children.Count);
    });

    [Fact]
    public Task Replacing_items_moves_the_callers_controls() => WpfUi.Run(() =>
    {
        var action = new System.Windows.Controls.Button { Content = "去处理" };
        var checklist = new Checklist { Items = [new() { Label = "上线", Action = action }] };
        WpfUi.Show(Host(checklist));

        checklist.Items = [new() { Label = "上线", Status = ChecklistStatus.Done, Action = action }];
        Assert.Same(action, checklist.Rows[0].Action);
    });
}
