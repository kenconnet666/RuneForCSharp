using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// ToggleGroup：单选写回 Value、必选时不能取消、Tab 停靠点是选中项；在必填的 Field 里默认必选；多选每项独立切换、整组不可用时都不能点；
/// 读屏里每项是切换按钮（选中是 On）。
/// </summary>
public class ToggleGroupTests
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

    static ToggleState State(UIElement element) => ((IToggleProvider)element.Peer().GetPattern(PatternInterface.Toggle)).ToggleState;

    [Fact]
    [Upstream("ToggleGroup", "单选：role")]
    public Task Single_selection_writes_back_and_required_keeps_the_last_item() => WpfUi.Run(() =>
    {
        var (list, grid) = (new ToggleItem { Value = "list", Content = "列表" }, new ToggleItem { Value = "grid", Content = "网格" });
        var group = new ToggleGroup { Value = "list", IsRequired = true, Children = { list, grid } };
        AutomationProperties.SetName(group, "视图");
        WpfUi.Show(Host(group));

        Assert.Equal("视图", group.Peer().GetName());
        Assert.Equal(AutomationControlType.Group, group.Peer().GetAutomationControlType());
        Assert.Equal([ToggleState.On, ToggleState.Off], new[] { list, grid }.Select(State));

        Invoke(grid);
        Assert.Equal("grid", group.Value);
        Invoke(grid);
        Assert.Equal("grid", group.Value); // 必选：再点已选项不取消
        Assert.True(grid.IsSelected);
        // 整组一个 Tab 停靠点：选中项
        Assert.Equal([false, true], new[] { list, grid }.Select(item => KeyboardNavigation.GetIsTabStop(item)));
    });

    [Fact]
    [Upstream("ToggleGroup", "在必填的 Field 里默认是 required")]
    public Task Groups_in_a_required_field_are_required_by_default() => WpfUi.Run(() =>
    {
        var kept = new ToggleItem { Value = "list", Content = "列表" };
        var inField = new ToggleGroup { Value = "list", Children = { kept } };
        var toggled = new ToggleItem { Value = "list", Content = "列表" };
        var alone = new ToggleGroup { Value = "list", Children = { toggled } };
        WpfUi.Show(Host(new Field { Label = "视图", IsRequired = true, Content = inField }, alone));

        Invoke(kept);
        Invoke(toggled);
        Assert.Equal("list", inField.Value);
        Assert.Null(alone.Value);
        Assert.Equal("视图", inField.Peer().GetName());
    });

    [Fact]
    [Upstream("ToggleGroup", "多选：每项独立切换；整组不可用时都不能点")]
    public Task Multiple_selection_toggles_each_item_and_a_disabled_group_blocks_all() => WpfUi.Run(() =>
    {
        var (bold, italic) = (new ToggleItem { Value = "bold", Icon = Lucide.LetterB }, new ToggleItem { Value = "italic", Icon = Lucide.Italic });
        var group = new ToggleGroup { SelectionMode = SelectionMode.Multiple, Values = [], Children = { bold, italic } };
        WpfUi.Show(Host(group));

        Invoke(bold);
        Invoke(italic);
        Assert.Equal(["bold", "italic"], group.Values);
        Invoke(bold);
        Assert.Equal(["italic"], group.Values);
        Assert.Equal(bold.ActualWidth, bold.ActualHeight); // 只有图标的项是方的

        group.IsEnabled = false;
        WpfUi.Flush();
        Assert.False(bold.IsEnabled);
    });
}
