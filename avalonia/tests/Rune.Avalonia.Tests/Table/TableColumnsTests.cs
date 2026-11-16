namespace Rune.Avalonia.Tests;

/// <summary>列定义的纯函数（上游 table.test、column-features.test、column-window.test）：排序、列的类型、列宽、多列排序、表头筛选、列的位置、列虚拟化。</summary>
public class TableColumnsTests
{
    record Person(string Id, string Name, int? Age);

    static readonly Person[] People = [new("a", "张三", 30), new("b", "李四", null), new("c", "Ada", 25)];

    static readonly DataTableColumn[] PeopleColumns = [new("Name", "姓名") { Sortable = true }, new("Age", "年龄") { Align = TableAlign.End, Sortable = true }];

    [Fact]
    [Upstream("table", "升序、降序；空值总在最后；原数组不变")]
    public void Sorts_with_nulls_last()
    {
        int?[] Ages(SortDirection direction) => [.. TableColumns.SortRows(People, PeopleColumns, [new SortState("Age", direction)], "zh-CN").Cast<Person>().Select(person => person.Age)];
        Assert.Equal([25, 30, null], Ages(SortDirection.Ascending));
        Assert.Equal([30, 25, null], Ages(SortDirection.Descending));
        Assert.Equal(["a", "b", "c"], People.Select(person => person.Id));
    }

    record Amount(string V);

    [Fact]
    [Upstream("table", "数字类的列里十进制字符串按数值排（按文字比较时 2.5 会排在 2.10 前面）；文字列照常按文字")]
    public void Numeric_text_sorts_by_value()
    {
        Amount[] amounts = [new("2.5"), new("2.10"), new("-5"), new("-10")];
        string[] Sorted(DataTableColumn column) => [.. TableColumns.SortRows(amounts, [column], [new SortState("V", SortDirection.Ascending)], "zh-CN").Cast<Amount>().Select(row => row.V)];
        Assert.Equal(["-10", "-5", "2.10", "2.5"], Sorted(new DataTableColumn("V", "金额") { Type = ColumnType.Number }));
        Assert.NotEqual(["-10", "-5", "2.10", "2.5"], Sorted(new DataTableColumn("V", "版本")));
    }

    [Fact]
    [Upstream("table", "点表头：不排 → 升序 → 降序 → 不排；换一列从升序开始")]
    public void Next_sort_cycles()
    {
        Assert.Equal(new SortState("name", SortDirection.Ascending), TableColumns.NextSort(null, "name"));
        Assert.Equal(SortDirection.Descending, TableColumns.NextSort(new SortState("name", SortDirection.Ascending), "name")!.Direction);
        Assert.Null(TableColumns.NextSort(new SortState("name", SortDirection.Descending), "name"));
        Assert.Equal("age", TableColumns.NextSort(new SortState("name", SortDirection.Descending), "age")!.Key);
    }

    record Cell(object? V);

    [Fact]
    [Upstream("table", "写了数字格式或合计是 sum 的列是数字列：这一页全是空值时也是")]
    public void Format_or_sum_makes_a_number_column()
    {
        Cell[] empty = [new(null), new(null)];
        Assert.Equal(ColumnType.Number, TableColumns.TypeOf(new DataTableColumn("V", "v") { Format = new NumericFormat { MaximumFractionDigits = 2 } }, empty));
        Assert.Equal(ColumnType.Number, TableColumns.TypeOf(new DataTableColumn("V", "v") { Summary = ColumnSummary.Sum }, empty));
        Assert.Equal(ColumnType.Text, TableColumns.TypeOf(new DataTableColumn("V", "v"), empty));
    }

    [Fact]
    [Upstream("table", "没写 type 时看第一个非空值：数字类 → number，日期 → date，ISO 字符串与其余 → text")]
    public void Type_from_first_value()
    {
        ColumnType TypeOf(params object?[] values) => TableColumns.TypeOf(new DataTableColumn("V", ""), values.Select(value => (object)new Cell(value)));
        Assert.Equal(ColumnType.Number, TypeOf(null, 3));
        Assert.Equal(ColumnType.Number, TypeOf(1.5m));
        Assert.Equal(ColumnType.Number, TypeOf(10L));
        Assert.Equal(ColumnType.Date, TypeOf(DateTime.Now));
        Assert.Equal(ColumnType.Date, TypeOf(new DateOnly(2026, 10, 21)));
        Assert.Equal(ColumnType.Text, TypeOf("2026-10-21"));
        Assert.Equal(ColumnType.Text, TypeOf());
    }

    static double? Width(DataTableColumn column) => column.Width;

    [Fact]
    [Upstream("table", "没写宽度的列最小宽度都一样：平分就对，不写宽度")]
    public void Flex_width_is_even_split_for_equal_minimums()
    {
        DataTableColumn[] list = [new("a", "A"), new("b", "B"), new("c", "C") { Width = 80 }];
        Assert.Null(TableColumns.FlexColumnWidth(list[0], list, Width, 0, 1000));
    }

    [Fact]
    [Upstream("table", "最小宽度不一样：自己的最小宽度加上平分多出来的部分，放不下时保住最小宽度")]
    public void Flex_width_adds_shared_extra_to_own_minimum()
    {
        DataTableColumn[] list = [new("a", "A") { MinWidth = 200 }, new("b", "B"), new("c", "C") { Width = 80 }];
        // 表格最小 40 + 200 + 120 + 80 = 440：外层 640 时多出来 200，两列各分 100
        Assert.Equal(300, TableColumns.FlexColumnWidth(list[0], list, Width, 40, 640));
        Assert.Equal(220, TableColumns.FlexColumnWidth(list[1], list, Width, 40, 640));
        // 放不下：保住最小宽度
        Assert.Equal(200, TableColumns.FlexColumnWidth(list[0], list, Width, 40, 300));
    }

    [Fact]
    [Upstream("table", "写了宽度的列按宽度，没写宽度的按 minWidth，都没写的按 COLUMN_FLEX_MIN_WIDTH；加上前面的控制列")]
    public void Table_min_width()
    {
        DataTableColumn[] list = [new("code", "编码") { Width = 140 }, new("name", "名称") { MinWidth = 200 }, new("note", "备注")];
        Assert.Equal(84 + 140 + 200 + TableColumns.ColumnFlexMinWidth, TableColumns.TableMinWidth(list, Width, 84));
    }

    [Fact]
    public void Sticky_offsets_accumulate_from_each_edge()
    {
        DataTableColumn[] list = [new("id", "编号") { Width = 80, Sticky = ColumnSticky.Start }, new("name", "名称") { Sticky = ColumnSticky.Start }, new("note", "备注"), new("ops", "操作") { Width = 96, Sticky = ColumnSticky.End }];
        var offsets = TableColumns.StickyOffsetsOf(list, Width, 40);
        Assert.Equal(40, offsets.Start["id"]);
        Assert.Equal(120, offsets.Start["name"]);
        Assert.Equal("name", offsets.StartEdge);
        Assert.Equal(0, offsets.End["ops"]);
        Assert.Equal("ops", offsets.EndEdge);
    }

    record Order(string Id, string Team, string Status, int Amount);

    static readonly StatusMap Statuses = new() { ["draft"] = new("草稿", StatusTone.Neutral), ["paid"] = new("已付款", StatusTone.Success) };

    static readonly Order[] Orders = [new("o1", "生产", "paid", 300), new("o2", "采购", "draft", 100), new("o3", "生产", "draft", 200), new("o4", "采购", "paid", 400)];

    static readonly DataTableColumn[] OrderColumns =
    [
        new("Id", "编号") { Width = 80 },
        new("Team", "团队") { Sortable = true, Filter = ColumnFilter.Values },
        new("Status", "状态") { Statuses = Statuses, Filter = ColumnFilter.Values },
        new("Amount", "金额") { Sortable = true, Filter = ColumnFilter.Keyword },
    ];

    static SortState Asc(string key) => new(key, SortDirection.Ascending);

    [Fact]
    [Upstream("column-features", "nextSorts：不追加时只按这一列；追加时加在后面、再点切换方向、降序时去掉")]
    public void Next_sorts()
    {
        Assert.Equal([Asc("amount")], TableColumns.NextSorts([Asc("team")], "amount", false));
        Assert.Equal([Asc("team"), Asc("amount")], TableColumns.NextSorts([Asc("team")], "amount", true));
        Assert.Equal([Asc("team"), new SortState("amount", SortDirection.Descending)], TableColumns.NextSorts([Asc("team"), Asc("amount")], "amount", true));
        Assert.Equal([Asc("team")], TableColumns.NextSorts([Asc("team"), new SortState("amount", SortDirection.Descending)], "amount", true));
    }

    [Fact]
    [Upstream("column-features", "formatSorts / parseSorts：-amount,team 是两列，减号是倒序；空的项跳过")]
    public void Format_and_parse_sorts()
    {
        SortState[] sorts = [new("amount", SortDirection.Descending), Asc("team")];
        Assert.Equal("-amount,team", TableColumns.FormatSorts(sorts));
        Assert.Equal("", TableColumns.FormatSorts(null));
        Assert.Equal(sorts, TableColumns.ParseSorts("-amount, team,,-"));
        Assert.Empty(TableColumns.ParseSorts(""));
    }

    [Fact]
    [Upstream("column-features", "sortRows 多列：前一列相等时比下一列")]
    public void Sorts_by_several_columns()
    {
        var sorted = TableColumns.SortRows(Orders, OrderColumns, [Asc("Team"), new SortState("Amount", SortDirection.Descending)], "zh-CN");
        // 采购（cǎi）在生产（shēng）前；同一团队里金额从大到小
        Assert.Equal(["o4", "o2", "o1", "o3"], sorted.Cast<Order>().Select(order => order.Id));
    }

    [Fact]
    [Upstream("column-features", "filterOptions 列出出现过的值（状态按阶段顺序、用状态名）；filterRows 按选中的值与关键字筛")]
    public void Filter_options_and_rows()
    {
        Assert.Equal(["采购", "生产"], TableColumns.FilterOptions(OrderColumns[1], Orders, "zh-CN").Select(option => option.Value));
        Assert.Equal([new ColumnFilterOption("draft", "草稿"), new ColumnFilterOption("paid", "已付款")], TableColumns.FilterOptions(OrderColumns[2], Orders, "zh-CN"));
        var filters = new Dictionary<string, IReadOnlyList<string>> { ["Team"] = ["生产"], ["Amount"] = ["0"], ["Status"] = [] };
        Assert.Equal(["o1", "o3"], TableColumns.FilterRows(Orders, OrderColumns, filters, "zh-CN").Cast<Order>().Select(order => order.Id));
    }

    [Fact]
    [Upstream("column-features", "arrangeColumns：按 order 排，没列到的在后面；pins 覆盖固定并挪到边上")]
    public void Arrange_with_pins()
    {
        var pins = new Dictionary<string, ColumnSticky?> { ["Amount"] = ColumnSticky.Start };
        Assert.Equal(["Amount", "Status", "Team", "Id"], TableColumns.Arrange(OrderColumns, ["Status", "Team"], pins).Select(column => column.Key));
    }

    static DataTableColumn[] Columns(int count, Dictionary<int, ColumnSticky>? sticky = null) =>
        [.. Enumerable.Range(0, count).Select(index => new DataTableColumn($"c{index}", $"{index}") { Sticky = sticky is not null && sticky.TryGetValue(index, out var side) ? side : null })];

    static double Hundred(DataTableColumn column) => 100;

    [Fact]
    [Upstream("column-window", "开头：只留前面看得见的列与 overscan")]
    public void Skips_at_the_start() =>
        Assert.Equal([new ColumnSkip(6, 50)], TableColumns.ColumnSkips(Columns(50), Hundred, 0, 400, overscan: 2));

    [Fact]
    [Upstream("column-window", "滚到中间：前后各跳过一段；leading（勾选列）算在前面")]
    public void Skips_in_the_middle() =>
        Assert.Equal([new ColumnSkip(0, 19), new ColumnSkip(25, 50)], TableColumns.ColumnSkips(Columns(50), Hundred, 2040, 400, leading: 40, overscan: 1));

    [Fact]
    [Upstream("column-window", "两端的固定列总在；中间夹着固定列时不虚拟化")]
    public void Sticky_columns_stay()
    {
        Assert.Equal([new ColumnSkip(3, 29)], TableColumns.ColumnSkips(Columns(30, new() { [0] = ColumnSticky.Start, [29] = ColumnSticky.End }), Hundred, 0, 300, overscan: 0));
        Assert.Empty(TableColumns.ColumnSkips(Columns(30, new() { [10] = ColumnSticky.Start }), Hundred, 0, 300));
    }

    [Fact]
    [Upstream("column-window", "滚到最后：前面跳过，后面不剩")]
    public void Skips_at_the_end() =>
        Assert.Equal([new ColumnSkip(0, 46)], TableColumns.ColumnSkips(Columns(50), Hundred, 4600, 400, overscan: 0));

    [Fact]
    [Upstream("table", "组里的列被别的列隔开时另起一格；不在任何组里的列单独一格")]
    public void Header_layout_splits_separated_groups()
    {
        DataTableColumn[] columns = [.. new[] { "a", "b", "x", "c" }.Select(key => new DataTableColumn(key, key))];
        var cells = TableColumns.HeaderLayout(columns, [new DataTableColumnGroup("g", "G", ["a", "b", "c"])]);
        Assert.Equal(["g[a,b]", "x", "g[c]"], cells.Select(cell => cell.Group is { } group ? $"{group.Key}[{string.Join(",", cell.Columns.Select(column => column.Key))}]" : cell.Columns[0].Key));
    }
}
