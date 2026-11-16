using Avalonia.Headless;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>DataTable 的表头筛选、多列排序与列菜单（上游 column-features.test 里 DataTable 的部分）。</summary>
public class DataTableColumnFeaturesTests
{
    record Order(string Id, string Team, string Status, int Amount);

    static readonly StatusMap Statuses = new() { ["draft"] = new("草稿", StatusTone.Neutral), ["paid"] = new("已付款", StatusTone.Success) };

    static readonly Order[] Orders = [new("o1", "生产", "paid", 300), new("o2", "采购", "draft", 100), new("o3", "生产", "draft", 200), new("o4", "采购", "paid", 400)];

    static readonly DataTableColumn[] Columns =
    [
        new("Id", "编号") { Width = 80 },
        new("Team", "团队") { Sortable = true, Filter = ColumnFilter.Values },
        new("Status", "状态") { Statuses = Statuses, Filter = ColumnFilter.Values },
        new("Amount", "金额") { Sortable = true, Filter = ColumnFilter.Keyword },
    ];

    static (DataTable Table, Window Window) Render(DataTable table)
    {
        table.ItemsSource = Orders;
        table.Columns = Columns;
        table.GetKey = row => ((Order)row).Id;
        table.Width = 720;
        table.Height = 400;
        var window = HeadlessUi.Show(table);
        HeadlessUi.Flush(window);
        return (table, window);
    }

    static List<string> Ids(DataTable table) => [.. table.Surface.Realized.OrderBy(pair => pair.Key).Select(pair => ((Order)pair.Value.Row!).Id)];

    static List<TableHeaderCell> Headers(DataTable table) => [.. table.Surface.Header!.Cells.OfType<TableHeaderCell>()];

    static T Part<T>(DataTable table, Func<T, bool> match) where T : Control => table.Surface.Header!.GetLogicalDescendants().OfType<T>().First(match);

    static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    static void Click(Control control) => control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    [Upstream("DataTable 的表头筛选、多列排序与列菜单", "筛选按钮：勾选值写回 filters，只留下匹配的行；按钮名称说明已筛选")]
    public Task Filter_values_write_back_and_keep_matching_rows() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable());
        var filter = Part<ColumnFilterButton>(table, button => button.Label == "状态");
        Assert.Equal("筛选“状态”", AutomationProperties.GetName(filter.Trigger));
        filter.Boxes.Single(box => ((ColumnFilterOption)box.Tag!).Value == "paid").Toggle();
        HeadlessUi.Flush(window);
        Assert.Equal(["paid"], table.Filters!["Status"]);
        Assert.Equal(["o1", "o4"], Ids(table));
        Assert.Equal("筛选“状态”（已筛选）", AutomationProperties.GetName(Part<ColumnFilterButton>(table, button => button.Label == "状态").Trigger));
    });

    [Fact]
    [Upstream("DataTable 的表头筛选、多列排序与列菜单", "关键字筛选：停手一会儿才生效，回车立即生效，清空立即生效")]
    public Task Keyword_filter_applies_on_enter_and_clear() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable());
        var filter = Part<ColumnFilterButton>(table, button => button.Label == "金额");
        Click(filter.Trigger);
        HeadlessUi.Flush(window);
        var input = filter.Keyword;
        input.Focus();
        input.Text = "1";
        // 还在打字：不生效
        Assert.Null(table.Filters);
        input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = input });
        HeadlessUi.Flush(window);
        Assert.Equal(["1"], table.Filters!["Amount"]);
        // 表头没有重建：还是那个筛选按钮（弹出层开着、草稿还在）
        Assert.Same(filter, Part<ColumnFilterButton>(table, button => button.Label == "金额"));
        Assert.True(filter.IsAttachedToVisualTree());
        input.Text = "";
        HeadlessUi.Flush(window);
        Assert.Empty(table.Filters!["Amount"]);
    });

    [Fact]
    [Upstream("DataTable 的表头筛选、多列排序与列菜单", "multiSort：点是只按这一列，Shift 点追加；第一排序列带 aria-sort，其余的写给读屏")]
    public Task Multi_sort_with_shift() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable { MultiSort = true });
        ButtonBase SortButton(string text) => Part<ButtonBase>(table, button => button.Name == DataTableParts.SortButton && button.GetLogicalDescendants().OfType<TextBlock>().First().Text == text);
        Click(SortButton("团队"));
        HeadlessUi.Flush(window);
        var amount = SortButton("金额");
        amount.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, KeyModifiers = KeyModifiers.Shift, Source = amount });
        Click(amount);
        HeadlessUi.Flush(window);
        Assert.Equal([new SortState("Team", SortDirection.Ascending), new SortState("Amount", SortDirection.Ascending)], table.Sorts);
        Assert.Equal(["o2", "o4", "o3", "o1"], Ids(table));
        Assert.Equal([null, "ascending", null, "none"], Headers(table).Select(AutomationProperties.GetItemStatus));
        Assert.Contains("第 2 排序，升序", AutomationProperties.GetHelpText(Headers(table)[3]));
    });

    [Fact]
    [Upstream("DataTable 的表头筛选、多列排序与列菜单", "列菜单：隐藏写回 hiddenColumns；固定写回 columnPins 并记下没写宽度的列的宽度；向后移写回 columnOrder")]
    public Task Column_menu_moves_pins_and_hides() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable { ColumnMenu = true });
        void Act(string column, string item)
        {
            var menu = Part<ColumnMenuButton>(table, button => button.Label == column);
            Assert.Equal($"“{column}”列的操作", AutomationProperties.GetName(menu.Trigger));
            Click(menu.Items.Single(entry => entry.Content as string == item));
            HeadlessUi.Flush(window);
        }
        Act("团队", "向后移");
        Assert.Equal(["Id", "Status", "Team", "Amount"], table.ColumnOrder);
        Act("状态", "固定在开头");
        Assert.Equal(ColumnSticky.Start, table.ColumnPins!["Status"]);
        Assert.Equal(["Status"], table.ColumnWidths!.Keys);
        Act("金额", "隐藏这一列");
        Assert.Equal(["Amount"], table.HiddenColumns);
        Assert.Equal(["状态", "编号", "团队"], Headers(table).Select(cell => TableColumns.Label(table.Shown[Headers(table).IndexOf(cell)])));
    });

    static ButtonBase? Handle(DataTable table, string id) =>
        table.Surface.Realized.Values.SelectMany(row => row.GetLogicalDescendants().OfType<ButtonBase>()).FirstOrDefault(button => button.Name == DataTableParts.RowHandle && (string?)button.Tag == id);

    [Fact]
    [Upstream("DataTable 的行拖动", "把手在第一列前面：空格拿起、↓ 移动、空格放下，onReorder 得到新顺序；排序时没有把手")]
    public Task Keyboard_reorders_rows() => HeadlessUi.Run(() =>
    {
        var table = new DataTable();
        IReadOnlyList<object> rows = Orders;
        table.Reordered += (_, next) => table.ItemsSource = rows = next;
        var (_, window) = Render(table);
        var handle = Handle(table, "o1")!;
        Assert.Equal("移动 o1", AutomationProperties.GetName(handle));
        Assert.Contains("按空格拿起", AutomationProperties.GetHelpText(handle));
        handle.Focus();
        Press(handle, Key.Space);
        HeadlessUi.Flush(window);
        Press(Handle(table, "o1")!, Key.Down);
        HeadlessUi.Flush(window);
        Press(Handle(table, "o1")!, Key.Space);
        HeadlessUi.Flush(window);
        Assert.Equal(["o2", "o1", "o3", "o4"], rows.Cast<Order>().Select(order => order.Id));
        // 拖动后焦点回到那一行的把手上
        Assert.True(Handle(table, "o1")!.IsFocused);

        table.Sort = new SortState("Amount", SortDirection.Ascending);
        HeadlessUi.Flush(window);
        Assert.Null(Handle(table, "o1"));
    });

    [Fact]
    public Task Pointer_drag_reorders_rows() => HeadlessUi.Run(() =>
    {
        var table = new DataTable();
        IReadOnlyList<object> rows = Orders;
        table.Reordered += (_, next) => table.ItemsSource = rows = next;
        var (_, window) = Render(table);
        var handle = Handle(table, "o1")!;
        var from = handle.TranslatePoint(new Point(handle.Bounds.Width / 2, handle.Bounds.Height / 2), window)!.Value;
        var third = table.Surface.Realized[2];
        var to = third.TranslatePoint(new Point(10, third.Bounds.Height / 2), window)!.Value;
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to);
        window.MouseUp(to, MouseButton.Left);
        HeadlessUi.Flush(window);
        Assert.Equal(["o2", "o3", "o1", "o4"], rows.Cast<Order>().Select(order => order.Id));
    });
}
