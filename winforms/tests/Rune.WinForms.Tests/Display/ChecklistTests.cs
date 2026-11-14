namespace Rune.WinForms.Tests;

/// <summary>ChecklistControl：各项的状态写给读屏、超时的日期用危险色、不适用的不算进汇总；可以勾选时状态是复选框；inline 一行排开；icons 只有图标，整组一个说明。</summary>
public class ChecklistTests
{
    static ChecklistItem[] Items() =>
    [
        new() { Label = "验收报告", Status = ChecklistStatus.Done, Date = "10-12", Evidence = new LinkControl { Text = "验证报告.pdf" } },
        new() { Label = "财务复核意见", Status = ChecklistStatus.Overdue, Owner = "财务部", Date = "计划 10-20" },
        new() { Label = "合同签订记录", Description = "谈定价格与生效日期", Owner = "采购部" },
        new() { Label = "海关备案", Status = ChecklistStatus.Skipped },
    ];

    static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(child => Descendants(child).Prepend(child));

    [Fact]
    [Upstream("Checklist", "一项一行：状态写给读屏")]
    public Task One_row_per_item_with_the_status_for_screen_readers() => WinFormsUi.Run(() =>
    {
        var checklist = new ChecklistControl { Items = Items() };
        using var form = WinFormsUi.Show(checklist);

        var rows = checklist.Rows;
        Assert.Equal([ChecklistStatus.Done, ChecklistStatus.Overdue, ChecklistStatus.Todo, ChecklistStatus.Skipped], rows.Select(row => row.Item.Status));
        Assert.Equal(AccessibleRole.ListItem, rows[0].AccessibilityObject.Role);
        Assert.Equal("已完成", rows[0].Status.AccessibleName);
        Assert.Equal(["验收报告", "10-12", "验证报告.pdf"], Descendants(rows[0]).Select(control => control.Text).Where(text => text is "验收报告" or "10-12" or "验证报告.pdf"));
        Assert.Contains(Descendants(rows[2]), control => control.Text == "谈定价格与生效日期");
        var late = rows[1].Meta!.Controls.OfType<TextControl>().Single(text => text.Text == "计划 10-20");
        Assert.Equal(TextTone.Danger, late.Tone);
        // 没完成的名称是次要色，不适用的划掉
        Assert.Equal(TextTone.Muted, ((TextControl)rows[2].LabelPart).Tone);
        Assert.True(((TextControl)rows[3].LabelPart).Element.Strikethrough);
    });

    [Fact]
    [Upstream("Checklist", "summary：已完成数 / 总数，不适用的不算")]
    public Task Summary_counts_without_skipped_items() => WinFormsUi.Run(() =>
    {
        var checklist = new ChecklistControl { Items = Items(), Summary = true };
        using var form = WinFormsUi.Show(checklist);

        Assert.Contains(checklist.SummaryPart!.Controls.Cast<Control>(), control => control.Text == "已完成 1 / 3");
        var progress = checklist.SummaryPart.Controls.OfType<ProgressControl>().Single();
        Assert.Equal(1, progress.Value);
        Assert.Equal(3, progress.Max);
    });

    [Fact]
    [Upstream("Checklist", "可以勾选：状态是 role=checkbox 的按钮，名称是这一项；不适用的不能勾")]
    public Task Checkable_status_is_a_checkbox_named_by_the_item() => WinFormsUi.Run(() =>
    {
        var items = Items();
        var checklist = new ChecklistControl { Items = items };
        var changes = new List<(ChecklistItem, bool)>();
        checklist.CheckedChange += (_, e) => changes.Add((e.Item, e.Done));
        using var form = WinFormsUi.Show(checklist);

        var boxes = checklist.Rows.Select(row => row.Status).ToList();
        Assert.All(boxes, box => Assert.Equal(AccessibleRole.CheckButton, box.AccessibilityObject.Role));
        Assert.Equal([true, false, false, false], boxes.Select(box => box.AccessibilityObject.State.HasFlag(AccessibleStates.Checked)));
        Assert.Equal("财务复核意见", boxes[1].AccessibilityObject.Name);
        Assert.Equal("已超时", boxes[1].AccessibilityObject.Description);
        Assert.False(boxes[3].Enabled);
        boxes[1].AccessibilityObject.DoDefaultAction();
        Assert.Equal((items[1], true), changes[^1]);
        boxes[0].AccessibilityObject.DoDefaultAction();
        Assert.Equal((items[0], false), changes[^1]);
    });

    [Fact]
    [Upstream("Checklist", "inline 一行排开；icons 只有图标，整组一个说明")]
    public Task Inline_and_icons_layouts() => WinFormsUi.Run(() =>
    {
        var items = Items();
        var inline = new ChecklistControl { ItemLayout = ChecklistLayout.Inline, Items = items[..2] };
        var icons = new ChecklistControl { ItemLayout = ChecklistLayout.Icons, Items = items[..3] };
        using var form = WinFormsUi.Show(inline, icons);

        Assert.Equal(2, inline.Rows.Count);
        var row = icons.IconsPart!;
        Assert.Equal(AccessibleRole.Graphic, row.AccessibilityObject.Role);
        Assert.Equal("验收报告已完成，财务复核意见已超时，合同签订记录未完成", row.AccessibilityObject.Name);
        Assert.Equal("验收报告已完成，财务复核意见已超时，合同签订记录未完成", icons.IconsTooltip);
        Assert.Equal(3, row.Controls.Count);
    });

    [Fact]
    public Task Replacing_items_moves_the_callers_controls() => WinFormsUi.Run(() =>
    {
        var action = new ButtonControl { Text = "去处理" };
        var checklist = new ChecklistControl { Items = [new() { Label = "上线", Action = action }] };
        using var form = WinFormsUi.Show(checklist);

        checklist.Items = [new() { Label = "上线", Status = ChecklistStatus.Done, Action = action }];
        Assert.Same(action, checklist.Rows[0].Action);
        Assert.False(action.IsDisposed);
    });
}
