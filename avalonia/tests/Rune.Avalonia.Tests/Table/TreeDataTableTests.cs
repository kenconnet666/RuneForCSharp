using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>TreeDataTable：展开与懒加载、展开按钮的部件、勾选的父子联动、ManualSort、筛选后 key 不串、MultiSort、两万个节点的性能。</summary>
public class TreeDataTableTests
{
    record Person(string Id, string Name, int? Age, Person[]? Children = null);

    static readonly Person[] Tree =
    [
        new("dept", "研发部", null, [new("x", "小王", 28), new("y", "小李", 24)]),
        new("lazy", "市场部", null),
    ];

    static readonly DataTableColumn[] Columns = [new("Name", "姓名") { Sortable = true }, new("Age", "年龄") { Sortable = true }];

    static TreeDataTable New(IReadOnlyList<Person>? rows = null, IReadOnlyList<DataTableColumn>? columns = null) => new()
    {
        ItemsSource = rows ?? Tree, Columns = columns ?? Columns, GetKey = row => ((Person)row).Id, GetChildren = row => ((Person)row).Children,
        Width = 640, Height = 400,
    };

    static Window Render(TreeDataTable table)
    {
        var window = HeadlessUi.Show(table);
        HeadlessUi.Flush(window);
        return window;
    }

    static List<DataTableRowView> Rows(DataTable table) => [.. table.Surface.Realized.OrderBy(pair => pair.Key).Select(pair => pair.Value)];

    /// <summary>每一行第一格的文字。</summary>
    static List<string> Names(DataTable table) =>
        [.. Rows(table).Select(row => string.Concat(row.Cells.OfType<TableCell>().First().GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text)))];

    static List<string> Keys(DataTable table) => [.. Rows(table).Select(row => ((Person)row.Row!).Id)];

    static void Click(Control control) => control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    [Upstream("TreeDataTable", "展开按钮带 aria-expanded，展开后显示子项（同级按排序）；懒加载时调用 loadChildren")]
    public Task Toggle_expands_sorted_children_and_lazy_loads() => HeadlessUi.Run(() =>
    {
        var loaded = new List<string>();
        var table = New();
        table.HasChildren = row => ((Person)row).Id is "dept" or "lazy";
        table.LoadChildren = row =>
        {
            loaded.Add(((Person)row).Id);
            return null;
        };
        table.Sort = new SortState("Age", SortDirection.Ascending);
        var window = Render(table);
        Assert.Equal(["研发部", "市场部"], Names(table));
        var toggle = table.ToggleOf("dept")!;
        Assert.Equal("研发部 的子项", AutomationProperties.GetName(toggle));
        Assert.False(toggle.GetValue(Popover.TriggerExpandedProperty));
        Click(toggle);
        HeadlessUi.Flush(window);
        Assert.Equal(["dept"], table.Expanded);
        Assert.True(table.ToggleOf("dept")!.GetValue(Popover.TriggerExpandedProperty));
        Assert.Equal(["研发部", "小李", "小王", "市场部"], Names(table));
        Click(table.ToggleOf("lazy")!);
        HeadlessUi.Flush(window);
        Assert.Equal(["lazy"], loaded);
    });

    [Fact]
    [Upstream("TreeDataTable", "propsToggle 落到展开按钮上；propsIcon 与箭头的转向合并，aria-expanded 与点击仍由组件管理")]
    public Task Toggle_is_a_styleable_part_with_managed_state() => HeadlessUi.Run(() =>
    {
        var table = New();
        // 部件名对应 propsToggle：样式按名字改外观，展开状态与转向仍由表格管
        table.Styles.Add(new Style(x => x.OfType<IconButton>().Name(TreeDataTableParts.Toggle))
        {
            Setters = { new Setter(IconButton.ToneProperty, StatusTone.Info) },
        });
        var window = Render(table);
        var toggle = table.ToggleOf("dept")!;
        Assert.Equal(StatusTone.Info, toggle.Tone);
        Assert.Null(toggle.RenderTransform);
        Click(toggle);
        HeadlessUi.Flush(window);
        Assert.Equal(["dept"], table.Expanded);
        Assert.True(table.ToggleOf("dept")!.GetValue(Popover.TriggerExpandedProperty));
        Assert.NotNull(table.ToggleOf("dept")!.RenderTransform);
        // 叶子没有按钮，有同样宽的占位
        Assert.Null(table.ToggleOf("x"));
        Assert.Contains(Rows(table)[1].GetLogicalDescendants().OfType<Control>(), control => control.Name == TreeDataTableParts.Placeholder);
    });

    [Fact]
    [Upstream("TreeDataTable", "多选默认父子联动：勾父行连带子孙，只勾一个子行时父行半选；checkStrictly 时各自勾选")]
    public Task Multiple_selection_links_parents_and_children() => HeadlessUi.Run(() =>
    {
        var linked = New();
        (linked.Expanded, linked.SelectionMode) = (["dept"], SelectionMode.Multiple);
        var window = Render(linked);
        // 行的顺序：研发部、小王、小李、市场部（不排序时按数据）
        Rows(linked)[0].Choice!.Toggle();
        HeadlessUi.Flush(window);
        Assert.Equal(["dept", "x", "y"], linked.Selected.Cast<string>().Order());
        Rows(linked)[1].Choice!.Toggle();
        HeadlessUi.Flush(window);
        Assert.Equal(["y"], linked.Selected.Cast<string>());
        Assert.True(((Checkbox)Rows(linked)[0].Choice!).IsIndeterminate);
        Assert.False(Rows(linked)[0].Choice!.IsChecked);

        var strict = New();
        (strict.Expanded, strict.SelectionMode, strict.CheckStrictly) = (["dept"], SelectionMode.Multiple, true);
        window = Render(strict);
        Rows(strict)[0].Choice!.Toggle();
        HeadlessUi.Flush(window);
        Assert.Equal(["dept"], strict.Selected.Cast<string>());
        Assert.False(Rows(strict)[1].Choice!.IsChecked);
    });

    [Fact]
    [Upstream("TreeDataTable", "manualSort：同级之间不在本地排序，只写回 sort")]
    public Task Manual_sort_keeps_data_order() => HeadlessUi.Run(() =>
    {
        var table = New();
        (table.Expanded, table.ManualSort, table.Sort) = (["dept"], true, new SortState("Age", SortDirection.Ascending));
        Render(table);
        Assert.Equal(["研发部", "小王", "小李", "市场部"], Names(table));
    });

    [Fact]
    [Upstream("TreeDataTable", "表头筛选掉几行后，留下的行仍是自己的 key（展开按钮、勾选不串行）")]
    public Task Filtered_rows_keep_their_own_keys() => HeadlessUi.Run(() =>
    {
        var table = New(columns: [Columns[0] with { Filter = ColumnFilter.Values }, Columns[1]]);
        table.Expanded = ["dept"];
        table.Filters = new Dictionary<string, IReadOnlyList<string>> { ["Name"] = ["小李", "市场部"] };
        Render(table);
        Assert.Equal(["y", "lazy"], Keys(table));
        Assert.Equal(["小李", "市场部"], Names(table));
    });

    [Fact]
    [Upstream("TreeDataTable", "multiSort：同级之间按 sorts 里的几列依次排")]
    public Task Multi_sort_orders_each_level() => HeadlessUi.Run(() =>
    {
        Person[] flat = [new("a", "乙", 30), new("b", "甲", 30), new("c", "丙", 20)];
        var table = New(flat);
        (table.MultiSort, table.Sorts) = (true, [new SortState("Age", SortDirection.Ascending), new SortState("Name", SortDirection.Descending)]);
        Render(table);
        var keys = Keys(table);
        Assert.Equal("c", keys[0]);
        // 年龄相同的两行按姓名降序
        var byName = TableColumns.SortRows(flat[..2], Columns, [new SortState("Name", SortDirection.Descending)], "zh-CN").Select(row => ((Person)row).Id);
        Assert.Equal(byName, keys[1..]);
    });

    [Fact]
    [Upstream("TreeDataTable", "性能：两万个展开的节点挂载在 1 秒内")]
    public Task Twenty_thousand_expanded_nodes_mount_quickly() => HeadlessUi.Run(() =>
    {
        var parents = Enumerable.Range(0, 20_000).Select(index => new Person($"p{index}", $"部门 {index}", null, [new($"c{index}", $"成员 {index}", index)])).ToList();
        var watch = Stopwatch.StartNew();
        var table = New(parents);
        table.Expanded = [.. parents.Select(parent => parent.Id)];
        Render(table);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"{watch.ElapsedMilliseconds} ms");
        Assert.Equal(40_000, table.View.Count);
    });

    [Fact]
    public Task Lazy_children_show_loading_then_appear() => HeadlessUi.Run(() =>
    {
        var children = new Dictionary<string, Person[]>();
        var pending = new TaskCompletionSource();
        var table = New();
        table.GetChildren = row => ((Person)row).Children ?? children.GetValueOrDefault(((Person)row).Id);
        table.HasChildren = row => ((Person)row).Id is "dept" or "lazy";
        table.LoadChildren = row => pending.Task;
        var window = Render(table);
        Click(table.ToggleOf("lazy")!);
        HeadlessUi.Flush(window);
        Assert.True(table.ToggleOf("lazy")!.IsLoading);
        children["lazy"] = [new("brand", "品牌组", 7)];
        pending.SetResult();
        HeadlessUi.Flush(window);
        Assert.False(table.ToggleOf("lazy")!.IsLoading);
        Assert.Equal(["研发部", "市场部", "品牌组"], Names(table));
    });
}
