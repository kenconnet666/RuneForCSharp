using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>ColumnSettings：按当前顺序列出全部列，勾掉的写进 Hidden；只剩一列可见时不能再隐藏；“重置”清空隐藏与顺序；排序写回 Order。</summary>
public class ColumnSettingsTests
{
    static readonly DataTableColumn[] Columns = [new("Id", "单号"), new("Customer", "客户"), new("Amount", "金额")];

    static List<Checkbox> Boxes(ColumnSettings settings) =>
        [.. ((Control)settings.Popover.Content!).GetVisualDescendants().OfType<Checkbox>()];

    [Fact]
    [Upstream("ColumnSettings", "按顺序列出，勾掉写回 _hidden；最后一列不能隐藏；重置清空")]
    public Task Lists_in_order_hides_and_resets() => HeadlessUi.Run(() =>
    {
        var settings = new ColumnSettings { Columns = Columns, Order = ["Amount", "Id", "Customer"] };
        var reset = 0;
        settings.ResetRequested += (_, _) => reset++;
        var window = HeadlessUi.Show(new StackPanel { Width = 400, Children = { settings } });
        settings.Popover.IsOpen = true;
        HeadlessUi.Flush(window);

        Assert.Equal("列设置", AutomationProperties.GetName(settings.GetLogicalDescendants().OfType<IconButton>().Single()));
        Assert.Equal(["金额", "单号", "客户"], Boxes(settings).Select(box => box.Content as string));
        Boxes(settings)[0].IsChecked = false;
        Boxes(settings)[1].IsChecked = false;
        HeadlessUi.Flush(window);
        Assert.Equal(["Amount", "Id"], settings.Hidden);
        // 只剩“客户”可见：它的勾选框不可用
        Assert.Equal([true, true, false], Boxes(settings).Select(box => box.IsEnabled));

        var resetButton = ((Control)settings.Popover.Content!).GetVisualDescendants().OfType<Button>().Single(button => button.Name == ColumnSettingsParts.Reset);
        Assert.True(resetButton.IsEnabled);
        resetButton.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        HeadlessUi.Flush(window);
        Assert.Equal((0, 0, 1), (settings.Hidden.Count, settings.Order.Count, reset));
        Assert.Equal(["单号", "客户", "金额"], Boxes(settings).Select(box => box.Content as string));
        Assert.False(resetButton.IsEnabled);
    });

    [Fact]
    public void Arrange_follows_the_order_and_appends_the_rest()
    {
        Assert.Equal(["Amount", "Id", "Customer"], TableColumns.Arrange(Columns, ["Amount", "Gone"]).Select(column => column.Key));
        Assert.Equal(["Id", "Customer", "Amount"], TableColumns.Arrange(Columns, null).Select(column => column.Key));
    }
}
