namespace Rune.WinForms.Tests;

/// <summary>
/// ToggleGroupControl：单选写回 Value、必选时不能取消、Tab 停靠点是选中项；在必填的 FieldControl 里默认必选；多选每项独立切换、整组不可用时都不能点；
/// 读屏里每项是按下或弹起的按钮。
/// </summary>
public class ToggleGroupTests
{
    [Fact]
    [Upstream("ToggleGroup", "单选：role")]
    public Task Single_selection_writes_back_and_required_keeps_the_last_item() => WinFormsUi.Run(() =>
    {
        var (list, grid) = (new ToggleItemControl { Value = "list", Text = "列表" }, new ToggleItemControl { Value = "grid", Text = "网格" });
        var group = new ToggleGroupControl { Value = "list", IsRequired = true, AccessibleName = "视图" };
        group.Controls.AddRange([list, grid]);
        using var form = WinFormsUi.Show(group);

        Assert.Equal("视图", group.AccessibilityObject.Name);
        Assert.Equal(AccessibleRole.Grouping, group.AccessibilityObject.Role);
        Assert.Equal([true, false], new[] { list, grid }.Select(item => item.AccessibilityObject.State.HasFlag(AccessibleStates.Pressed)));

        grid.AccessibilityObject.DoDefaultAction();
        Assert.Equal("grid", group.Value);
        grid.AccessibilityObject.DoDefaultAction();
        Assert.Equal("grid", group.Value); // 必选：再点已选项不取消
        Assert.True(grid.IsSelected);
        // 整组一个 Tab 停靠点：选中项
        Assert.Equal([false, true], new[] { list, grid }.Select(item => item.TabStop));
    });

    [Fact]
    [Upstream("ToggleGroup", "在必填的 Field 里默认是 required")]
    public Task Groups_in_a_required_field_are_required_by_default() => WinFormsUi.Run(() =>
    {
        var kept = new ToggleItemControl { Value = "list", Text = "列表" };
        var inField = new ToggleGroupControl { Value = "list" };
        inField.Controls.Add(kept);
        var field = new FieldControl { Label = "视图", IsRequired = true, Content = inField };
        var toggled = new ToggleItemControl { Value = "list", Text = "列表" };
        var alone = new ToggleGroupControl { Value = "list", Top = 100 };
        alone.Controls.Add(toggled);
        using var form = WinFormsUi.Show(field, alone);

        kept.PerformClick();
        toggled.PerformClick();
        Assert.Equal("list", inField.Value);
        Assert.Null(alone.Value);
        Assert.Equal("视图", inField.AccessibilityObject.Name);
    });

    [Fact]
    [Upstream("ToggleGroup", "多选：每项独立切换；整组不可用时都不能点")]
    public Task Multiple_selection_toggles_each_item_and_a_disabled_group_blocks_all() => WinFormsUi.Run(() =>
    {
        var (bold, italic) = (new ToggleItemControl { Value = "bold", Icon = Lucide.LetterB, AccessibleName = "加粗" }, new ToggleItemControl { Value = "italic", Icon = Lucide.Italic, AccessibleName = "斜体" });
        var group = new ToggleGroupControl { SelectionMode = SelectionMode.Multiple, Values = [] };
        group.Controls.AddRange([bold, italic]);
        using var form = WinFormsUi.Show(group);

        bold.PerformClick();
        italic.PerformClick();
        Assert.Equal(["bold", "italic"], group.Values);
        bold.PerformClick();
        Assert.Equal(["italic"], group.Values);
        Assert.Equal(bold.Width, bold.Height); // 只有图标的项是方的

        group.Enabled = false;
        bold.PerformClick();
        Assert.Equal(["italic"], group.Values);
        Assert.False(bold.Enabled);
    });
}
