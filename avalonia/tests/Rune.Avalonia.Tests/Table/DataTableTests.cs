using Avalonia.Headless;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>DataTable：列标题与排序、多选与全选的半选、单选、五万行全选的性能、虚拟滚动、加载与空状态、台账（状态、空值、合计）、色调与不能选的行、隐藏列、定位到行。</summary>
public class DataTableTests
{
    record Person(string Id, string Name, int? Age);

    static readonly Person[] People = [new("a", "张三", 30), new("b", "李四", null), new("c", "Ada", 25)];

    static readonly DataTableColumn[] Columns = [new("Name", "姓名") { Sortable = true }, new("Age", "年龄") { Align = TableAlign.End, Sortable = true }];

    static (DataTable Table, Window Window) Render(DataTable table, double height = 400)
    {
        table.Height = height;
        table.Width = 640;
        var window = HeadlessUi.Show(table);
        HeadlessUi.Flush(window);
        return (table, window);
    }

    static List<DataTableRowView> Rows(DataTable table) => [.. table.Surface.Realized.OrderBy(pair => pair.Key).Select(pair => pair.Value)];

    /// <summary>一格显示的文字：里面各段文字连起来。</summary>
    static string TextOf(Control cell) => string.Concat(cell.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text)) is { Length: > 0 } text
        ? text
        : (cell as ContentControl)?.Content as string ?? "";

    static List<string> Texts(DataTableRowView row) => [.. row.Cells.Where(cell => cell is TableCell).Select(TextOf)];

    static void Click(Control control) => control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>点选择框：同用户点击（经 Toggling，由表格写回 Selected）。</summary>
    static void Toggle(ChoiceBase choice) => choice.Toggle();

    [Fact]
    [Upstream("DataTable", "原生 table 语义：列标题 scope、aria-sort、aria-rowcount / aria-rowindex；点表头排序")]
    public Task Header_and_sorting() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable { ItemsSource = People, Columns = Columns, GetKey = row => ((Person)row).Id });
        var headers = table.Surface.Header!.Cells.OfType<TableHeaderCell>().ToList();
        Assert.Equal(("col", "none"), (headers[0].Scope, AutomationProperties.GetItemStatus(headers[0])));
        Click(table.Surface.Header.Cells.OfType<TableHeaderCell>().ElementAt(1).GetLogicalDescendants().OfType<ButtonBase>().Single());
        HeadlessUi.Flush(window);
        Assert.Equal(new SortState("Age", SortDirection.Ascending), table.Sort);
        Assert.Equal("ascending", AutomationProperties.GetItemStatus(table.Surface.Header!.Cells.OfType<TableHeaderCell>().ElementAt(1)));
        Assert.Equal(["Ada", "张三", "李四"], Rows(table).Select(row => Texts(row)[0]));
    });

    [Fact]
    [Upstream("DataTable", "多选：每行勾选框写回 selected，表头全选有部分选中状态")]
    public Task Multiple_selection_and_select_all() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable { ItemsSource = People, Columns = Columns, GetKey = row => ((Person)row).Id, SelectionMode = SelectionMode.Multiple, Selected = ["a"] });
        var all = (Checkbox)table.Surface.Header!.Choice!;
        Assert.Equal((false, true), (all.IsChecked, all.IsIndeterminate));
        Toggle(all);
        HeadlessUi.Flush(window);
        Assert.Equal(["a", "b", "c"], table.Selected);
        Toggle(Rows(table)[1].Choice!);
        HeadlessUi.Flush(window);
        Assert.Equal(["a", "c"], table.Selected);
        Assert.Equal(2, Rows(table).Count(row => row.Choice!.IsChecked));
    });

    [Fact]
    [Upstream("DataTable", "单选：每行是同一组的单选框（同一个 name），选中写回 selected；表头没有全选")]
    public Task Single_selection_uses_radios() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable { ItemsSource = People, Columns = Columns, GetKey = row => ((Person)row).Id, SelectionMode = SelectionMode.Single, Selected = ["a"] });
        Assert.Null(table.Surface.Header!.Choice);
        var radios = Rows(table).Select(row => row.Choice).ToList();
        Assert.All(radios, radio => Assert.IsType<Radio>(radio));
        Assert.Equal([true, false, false], radios.Select(radio => radio!.IsChecked));
        Assert.Equal("选择第 2 行", AutomationProperties.GetName(radios[1]!));
        Toggle(radios[2]!);
        HeadlessUi.Flush(window);
        Assert.Equal(["c"], table.Selected);
        Assert.Single(Rows(table), row => row.Choice!.IsChecked);
    });

    [Fact]
    [Upstream("DataTable", "性能：五万行全选、取消全选各在 1 秒内")]
    public Task Select_all_fifty_thousand_rows_fast() => HeadlessUi.Run(() =>
    {
        var many = Enumerable.Range(0, 50_000).Select(index => new Person(index.ToString(), $"第 {index} 行", index)).ToList();
        var (table, window) = Render(new DataTable { ItemsSource = many, Columns = Columns, GetKey = row => ((Person)row).Id, SelectionMode = SelectionMode.Multiple });
        var all = table.Surface.Header!.Choice!;
        foreach (var expected in new[] { 50_000, 0 })
        {
            var watch = Stopwatch.StartNew();
            Toggle(all);
            HeadlessUi.Flush(window);
            Assert.True(watch.ElapsedMilliseconds < 1000, $"{watch.ElapsedMilliseconds} ms");
            Assert.Equal(expected, table.Selected.Count);
        }
    });

    [Fact]
    [Upstream("DataTable", "虚拟滚动：一万行只渲染一部分，上下有占位行；没有数据时加载显示骨架、否则显示空状态")]
    public Task Virtualizes_and_shows_loading_or_empty() => HeadlessUi.Run(() =>
    {
        var many = Enumerable.Range(0, 10_000).Select(index => new Person(index.ToString(), $"第 {index} 行", index)).ToList();
        var (table, _) = Render(new DataTable { ItemsSource = many, Columns = Columns });
        var rendered = table.Surface.Realized.Count;
        Assert.InRange(rendered, 1, 59);
        Assert.True(table.Surface.Bounds.Height > 10_000 * table.Surface.RowHeight - 1);
        var (pending, _) = Render(new DataTable { ItemsSource = Array.Empty<Person>(), Columns = Columns, IsLoading = true });
        Assert.Equal("busy", pending.Peer().GetItemStatus());
        Assert.NotEmpty(pending.Surface.Skeletons);
        var (blank, _) = Render(new DataTable { ItemsSource = Array.Empty<Person>(), Columns = Columns });
        Assert.Equal("暂无数据", Assert.IsType<Empty>(blank.Surface.EmptyView).Title);
    });

    [Fact]
    [Upstream("DataTable", "刷新（已有数据时 loading）：表体换成骨架行，不再另加进度条；加载完数据行回来")]
    public Task Refresh_replaces_rows_with_skeletons() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable { ItemsSource = People, Columns = Columns, IsLoading = true });
        Assert.Empty(table.Surface.Realized);
        Assert.NotEmpty(table.Surface.Skeletons);
        table.IsLoading = false;
        HeadlessUi.Flush(window);
        Assert.Equal(People.Length, table.Surface.Realized.Count);
        Assert.Empty(table.Surface.Skeletons);
    });

    record Task_(string Id, string Status, decimal? Estimate, string? Agreed);

    static readonly Task_[] Tasks = [new("a", "verifying", 155353.2m, null), new("b", "closed", 109600m, "98000.10"), new("c", "archived", null, "0.20")];

    static readonly StatusMap Statuses = new() { ["verifying"] = new("验证中", StatusTone.Info), ["closed"] = new("关闭", StatusTone.Success) };

    static readonly DataTableColumn[] Ledger =
    [
        new("Id", "编码"),
        new("Status", "状态") { Statuses = Statuses },
        new("Estimate", "测算（万元）") { Format = new NumericFormat { Scale = 10_000, MaximumFractionDigits = 2 }, Summary = ColumnSummary.Sum },
        new("Agreed", "谈定（元）") { Type = ColumnType.Number, Empty = "待定价", Summary = ColumnSummary.Sum },
    ];

    [Fact]
    [Upstream("DataTable 的台账能力", "状态列显示 StatusBadge；空值显示 empty；合计行精确相加并按列的格式显示")]
    public Task Ledger_statuses_empty_and_summary() => HeadlessUi.Run(() =>
    {
        var (table, _) = Render(new DataTable { ItemsSource = Tasks, Columns = Ledger, GetKey = row => ((Task_)row).Id });
        var rows = Rows(table);
        Assert.Equal(["a", "验证中", "15.54", "待定价"], Texts(rows[0]));
        // 不认识的状态显示 key；没有测算值显示默认的 —
        Assert.Equal(["c", "archived", "—", "0.2"], Texts(rows[2]));
        Assert.IsType<StatusBadge>(((TableCell)rows[0].Cells[1]).Content);
        Assert.Equal(["合计", "", "26.5", "98,000.3"], table.Surface.Summary!.Cells.Select(TextOf));
    });

    [Fact]
    [Upstream("DataTable 的台账能力", "rowTone 给整行着色；isRowSelectable 不能勾选的行勾选框不可用，全选跳过它；hiddenColumns 隐藏列")]
    public Task Tones_unselectable_rows_and_hidden_columns() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable
        {
            ItemsSource = Tasks, Columns = Ledger, GetKey = row => ((Task_)row).Id, SelectionMode = SelectionMode.Multiple,
            IsRowSelectable = row => ((Task_)row).Status != "closed", RowTone = row => ((Task_)row).Estimate is null ? StatusTone.Danger : null, HiddenColumns = ["Agreed"],
        });
        var rows = Rows(table);
        Assert.Equal([null, null, StatusTone.Danger], rows.Select(row => row.Tone));
        Assert.Equal([true, false, true], rows.Select(row => row.Choice!.IsEnabled));
        Toggle(table.Surface.Header!.Choice!);
        HeadlessUi.Flush(window);
        Assert.Equal(["a", "c"], table.Selected);
        var all = (Checkbox)table.Surface.Header!.Choice!;
        Assert.Equal((true, false), (all.IsChecked, all.IsIndeterminate));
        Assert.Equal(["编码", "状态", "测算（万元）"], table.Surface.Header.Cells.OfType<TableHeaderCell>().Select(TextOf));
    });

    [Fact]
    [Upstream("DataTable 的 handle", "挂载后交出 scrollToRow：没渲染的行也能滚到；找不到的 key 返回 false、不滚动")]
    public Task Scroll_to_row() => HeadlessUi.Run(() =>
    {
        var many = Enumerable.Range(0, 1000).Select(index => new Person(index.ToString(), $"第 {index} 人", index)).ToList();
        var (table, window) = Render(new DataTable { ItemsSource = many, Columns = Columns, GetKey = row => ((Person)row).Id });
        Assert.False(table.ScrollToRow("nobody"));
        Assert.Equal(0, table.Scroller.Offset.Y);
        Assert.True(table.ScrollToRow("500"));
        HeadlessUi.Flush(window);
        Assert.True(table.Scroller.Offset.Y > 400);
        Assert.True(table.Surface.Realized.ContainsKey(500));
    });

    [Fact]
    public Task Resizer_writes_column_widths() => HeadlessUi.Run(() =>
    {
        // 有一列没写宽度：不按比例放大写了宽度的列
        DataTableColumn[] columns = [new("Name", "姓名") { Width = 160 }, new("Age", "年龄") { Width = 120, Resizable = false }, new("Id", "编号") { Resizable = false }];
        var (table, window) = Render(new DataTable { ItemsSource = People, Columns = columns, Resizable = true });
        var resizers = table.Surface.Header!.GetLogicalChildren().OfType<ColumnResizer>().ToList();
        // 列自己写了 Resizable = false 的没有手柄；手柄的名称取语言包
        var resizer = Assert.Single(resizers);
        Assert.Equal("调整“姓名”的列宽", AutomationProperties.GetName(resizer));
        Assert.Equal(160, resizer.Length);
        resizer.Press(global::Avalonia.Input.Key.Right);
        HeadlessUi.Flush(window);
        Assert.Equal(160 + resizer.Step, table.ColumnWidths!["Name"]);
        Assert.Equal(160 + resizer.Step, table.Surface.Widths[0]);
        // 不能窄过最小宽度（默认 64）
        resizer.Resize(10);
        HeadlessUi.Flush(window);
        Assert.Equal(TableColumns.ColumnMinWidth, table.ColumnWidths!["Name"]);
        // 调整宽度不重建表头：手柄还是那一个
        Assert.Same(resizer, table.Surface.Header!.GetLogicalChildren().OfType<ColumnResizer>().Single());
    });

    [Fact]
    [Upstream("DataTable 的详情行", "展开按钮带 aria-expanded 与 aria-controls；展开后下面多一行跨全部列的详情，行号随之顺延；hasDetail 为 false 的行没有按钮")]
    public Task Detail_rows_expand_below_their_row() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable
        {
            ItemsSource = People, Columns = Columns, GetKey = row => ((Person)row).Name, SelectionMode = SelectionMode.Multiple,
            RenderDetail = row => new TextBlock { Text = $"{((Person)row).Name} 的简历" }, HasDetail = row => ((Person)row).Name != "Ada",
        });
        List<IconButton> Toggles() => [.. Rows(table).Select(row => row.DetailToggle).OfType<IconButton>()];
        Assert.Equal(People.Length - 1, Toggles().Count);
        Assert.Equal("张三 的详情", AutomationProperties.GetName(Toggles()[0]));
        Assert.Equal(false, Toggles()[0].GetValue(Popover.TriggerExpandedProperty));
        Click(Toggles()[0]);
        HeadlessUi.Flush(window);
        Assert.Equal(["张三"], table.ExpandedRows);
        Assert.Equal(true, Toggles()[0].GetValue(Popover.TriggerExpandedProperty));
        var detail = Assert.Single(table.Surface.Details).Value;
        Assert.Equal("张三 的简历", ((TextBlock)detail.Content!).Text);
        // 详情在它的行下面、下一行上面
        var rows = Rows(table);
        Assert.True(detail.Bounds.Top >= rows[0].Bounds.Bottom - 0.5 && detail.Bounds.Bottom <= rows[1].Bounds.Top + 0.5);
        // 内容让过勾选列（44）与展开列（40），和第一个数据列对齐
        var text = (TextBlock)detail.Content!;
        Assert.True(text.TranslatePoint(default, detail)!.Value.X >= 84);
        table.ExpandedRows = [];
        HeadlessUi.Flush(window);
        Assert.Empty(table.Surface.Details);
    });

    [Fact]
    [Upstream("DataTable 的台账能力", "分组表头：两行，组标题跨组里的列，没分组的列与勾选列跨两行；行号从表头之后算起；隐藏列不算进组")]
    public Task Grouped_header_spans_its_columns() => HeadlessUi.Run(() =>
    {
        var (table, window) = Render(new DataTable
        {
            ItemsSource = Tasks, Columns = Ledger, GetKey = row => ((Task_)row).Id, SelectionMode = SelectionMode.Multiple,
            ColumnGroups = [new DataTableColumnGroup("money", "金额", ["Estimate", "Agreed"])],
        });
        var header = table.Surface.Header!;
        var (group, first, count) = Assert.Single(header.Groups);
        Assert.Equal(("金额", "colgroup", 2, 2), (group.Content as string, group.Scope, first, count));
        var cells = header.Cells.OfType<TableHeaderCell>().ToList();
        // 没分组的列跨两行（从表头顶上排起），组里的列在组标题下面
        Assert.True(cells[0].Bounds.Bottom > group.Bounds.Bottom);
        Assert.True(cells[2].Bounds.Top >= group.Bounds.Bottom);
        Assert.True(group.Bounds.Width > cells[2].Bounds.Width);

        table.HiddenColumns = ["Agreed"];
        HeadlessUi.Flush(window);
        Assert.Equal(1, Assert.Single(table.Surface.Header!.Groups).Count);
        // 组里的列全隐藏：回到一行表头
        table.HiddenColumns = ["Agreed", "Estimate"];
        HeadlessUi.Flush(window);
        Assert.Empty(table.Surface.Header!.Groups);
    });

    record Store(string Name, int[] Days);

    [Fact]
    [Upstream("VirtualColumns.browser", "只渲染看得见的列；滚到中间换一批；每行占的列数都等于总列数")]
    public Task Virtual_columns_build_only_visible_cells() => HeadlessUi.Run(() =>
    {
        var stores = Enumerable.Range(0, 60).Select(store => new Store($"店 {store + 1}", [.. Enumerable.Range(0, 120).Select(day => 40 + (store * 37 + day * 13) % 90)])).ToList();
        DataTableColumn[] columns =
        [
            new("Store", "门店") { Width = 120, Sticky = ColumnSticky.Start, Value = row => ((Store)row).Name },
            .. Enumerable.Range(0, 120).Select(day => new DataTableColumn($"d{day}", $"{day + 1} 日") { Width = 64, Align = TableAlign.End, Value = row => ((Store)row).Days[day] }),
            new("Total", "合计") { Width = 96, Sticky = ColumnSticky.End, Align = TableAlign.End, Value = row => ((Store)row).Days.Sum() },
        ];
        var table = new DataTable { ItemsSource = stores, Columns = columns, GetKey = row => ((Store)row).Name, Density = TableDensity.Compact, VirtualColumns = true, Width = 1000, Height = 400 };
        var window = HeadlessUi.Show(table);
        HeadlessUi.Flush(window);
        List<TableCell> Built(DataTableRowView row) => [.. row.Cells.OfType<TableCell>()];
        var first = table.Surface.Realized[0];
        Assert.Equal(122, first.Cells.Count);
        Assert.InRange(Built(first).Count, 2, 39);
        // 第一天那列（门店之后）建了
        Assert.IsType<TableCell>(first.CellOf("d0"));

        table.Scroller.Offset = new Vector(64 * 60, 0);
        HeadlessUi.Flush(window);
        first = table.Surface.Realized[0];
        Assert.Null(first.CellOf("d0"));
        Assert.NotNull(first.CellOf("d62"));
        // 两端的固定列总在
        var headers = table.Surface.Header!.Cells.OfType<TableHeaderCell>().Select(cell => cell.Content as string ?? "").ToList();
        Assert.Contains("门店", headers);
        Assert.Contains("合计", headers);
        Assert.InRange(Built(first).Count, 2, 39);
    });

    [Fact]
    [Upstream("DataTable", "滚动加载（有 onRangeChange）：已有数据时骨架接在末尾，已有的行留着")]
    public Task Range_listener_appends_skeletons_while_loading() => HeadlessUi.Run(() =>
    {
        var table = new DataTable { ItemsSource = People, Columns = Columns, IsLoading = true };
        VisibleRange? range = null;
        table.RangeChanged += (_, visible) => range = visible;
        var (_, window) = Render(table);
        Assert.Equal(People.Length, table.Surface.Realized.Count);
        Assert.Empty(table.Surface.Skeletons);
        Assert.True(table.Surface.Trailing.Count > 0);
        // 末尾的骨架在最后一行下面
        Assert.True(table.Surface.Trailing[0].Bounds.Top >= Rows(table)[^1].Bounds.Bottom - 0.5);
        HeadlessUi.Flush(window);
        Assert.Equal(new VisibleRange(0, People.Length - 1), range);
    });

    [Fact]
    public Task Active_row_and_row_click() => HeadlessUi.Run(() =>
    {
        var table = new DataTable { ItemsSource = People, Columns = Columns, GetKey = row => ((Person)row).Id, ActiveKey = "b" };
        object? clicked = null;
        table.RowClicked += (_, row) => clicked = row;
        var (_, window) = Render(table);
        Assert.Equal([null, "current", null], Rows(table).Select(AutomationProperties.GetItemStatus));
        var row = Rows(table)[2];
        var at = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, global::Avalonia.Input.MouseButton.Left);
        window.MouseUp(at, global::Avalonia.Input.MouseButton.Left);
        Assert.Same(People[2], clicked);
    });

    [Fact]
    public Task Scroll_to_row_can_center_it() => HeadlessUi.Run(() =>
    {
        var many = Enumerable.Range(0, 1000).Select(index => new Person(index.ToString(), $"第 {index} 人", index)).ToList();
        var (table, window) = Render(new DataTable { ItemsSource = many, Columns = Columns, GetKey = row => ((Person)row).Id });
        Assert.True(table.ScrollToRow("500", ScrollAlign.Center));
        HeadlessUi.Flush(window);
        var row = table.Surface.Realized[500];
        var top = row.TranslatePoint(default, table.Scroller)!.Value.Y;
        // 居中：行在视口（表头之下）的中间附近
        Assert.InRange(top, table.Scroller.Viewport.Height / 3, table.Scroller.Viewport.Height * 2 / 3);
    });

    record Event(int Id, string Message);

    [Fact]
    public Task Wrapping_rows_get_their_own_height() => HeadlessUi.Run(() =>
    {
        Event[] events = [new(1, "短"), new(2, string.Concat(Enumerable.Repeat("导出报表：2026 年 9 月销售明细（含退款与优惠券核销）", 3))), new(3, "短")];
        var (table, window) = Render(new DataTable { ItemsSource = events, Columns = [new("Id", "#") { Width = 64 }, new("Message", "事件") { Width = 200, Wrap = true }] });
        HeadlessUi.Flush(window);
        var rows = Rows(table);
        Assert.True(rows[1].Bounds.Height > rows[0].Bounds.Height * 1.5);
        // 下一行接在换行的行下面，不叠在一起
        Assert.True(rows[2].Bounds.Top >= rows[1].Bounds.Bottom - 0.5);
    });
}
