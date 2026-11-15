using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Rune.Avalonia;

/// <summary>
/// 按列定义渲染的静态表格（vue-tsx-pro 的 ColumnTable）：列定义同 DataTable（<see cref="DataTableColumn"/>：Type、Format、Statuses、Empty、Summary、Align、Cell），
/// 渲染成普通的 <see cref="Table"/>（列宽按内容、单元格可以换行），可以合并单元格。打印、导出预览、详情页里的明细表用它；要排序、勾选、筛选、十万行的用 DataTable。
/// <code>
/// table.ItemsSource = quotes;
/// table.Columns = [new("Supplier", "供应商"), new("Price", "单价") { Type = ColumnType.Currency, Summary = ColumnSummary.Sum }];
/// table.MergeRepeated = ["Supplier"];
/// </code>
/// 合并单元格：<see cref="MergeRepeated"/> 写列的 Key，这几列里上下相邻、值相同的格合成一格（后一列只在前一列的同一格里合并，层层分组）；
/// 更灵活的用 <see cref="GetCellSpan"/>，返回 <see cref="CellSpan"/>，被合并掉的格返回 0（同 Ant Design 的 onCell）。合并的格垂直居中。
/// 有一列写了 Summary 时底部多一行合计（第一列没写时是 <see cref="SummaryLabel"/>，默认"合计"）；没有行时表体里一行占满的 <see cref="Empty"/>（默认"暂无数据"）。
/// 每行的 DataContext 是那一行的数据。部件：<see cref="ColumnTableParts"/>。外观属性同 Table。
/// </summary>
public class ColumnTable : Table
{
    /// <summary>数据（TS 的 rows；Table 自己有 Rows）：实现了 INotifyCollectionChanged 的集合增删时跟着变。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<ColumnTable, IEnumerable?>(nameof(ItemsSource));

    /// <summary>列定义（同 DataTable；排序、筛选、固定、调整宽度在这里不起作用）。</summary>
    public static readonly StyledProperty<IReadOnlyList<DataTableColumn>> ColumnsProperty =
        AvaloniaProperty.Register<ColumnTable, IReadOnlyList<DataTableColumn>>(nameof(Columns), []);

    /// <summary>这几列里上下相邻、值相同的格合成一格；后一列只在前一列的同一格里合并。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> MergeRepeatedProperty = AvaloniaProperty.Register<ColumnTable, IReadOnlyList<string>?>(nameof(MergeRepeated));

    /// <summary>合并单元格（TS 的 cellSpan）：参数是行、列、下标，返回这一格合并几行、几列，被合并掉的返回 RowSpan 或 ColumnSpan 为 0；null 时照 MergeRepeated。</summary>
    public static readonly StyledProperty<Func<object, DataTableColumn, int, CellSpan?>?> GetCellSpanProperty =
        AvaloniaProperty.Register<ColumnTable, Func<object, DataTableColumn, int, CellSpan?>?>(nameof(GetCellSpan));

    /// <summary>合计行第一列的内容（第一列自己没写 Summary 时），默认取语言包（合计）。</summary>
    public static readonly StyledProperty<object?> SummaryLabelProperty = AvaloniaProperty.Register<ColumnTable, object?>(nameof(SummaryLabel));

    /// <summary>没有行时表体里显示的内容（占满一行），默认取语言包（暂无数据）；列定义里的 Empty 是单个格子没有值时的占位。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<ColumnTable, object?>(nameof(Empty));

    INotifyCollectionChanged? observed;

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>列定义。</summary>
    public IReadOnlyList<DataTableColumn> Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>相同值合并的列。</summary>
    public IReadOnlyList<string>? MergeRepeated { get => GetValue(MergeRepeatedProperty); set => SetValue(MergeRepeatedProperty, value); }

    /// <summary>合并单元格。</summary>
    public Func<object, DataTableColumn, int, CellSpan?>? GetCellSpan { get => GetValue(GetCellSpanProperty); set => SetValue(GetCellSpanProperty, value); }

    /// <summary>合计行第一列的内容。</summary>
    public object? SummaryLabel { get => GetValue(SummaryLabelProperty); set => SetValue(SummaryLabelProperty, value); }

    /// <summary>没有行时的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>Table 的类型键：ColumnTable 用 Table 的主题。</summary>
    protected override Type StyleKeyOverride => typeof(Table);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            if (observed is not null) observed.CollectionChanged -= OnItemsChanged;
            observed = ItemsSource as INotifyCollectionChanged;
            if (observed is not null) observed.CollectionChanged += OnItemsChanged;
            Rebuild();
        }
        else if (change.Property == ColumnsProperty || change.Property == MergeRepeatedProperty || change.Property == GetCellSpanProperty
                 || change.Property == SummaryLabelProperty || change.Property == EmptyProperty || change.Property == Provider.LangProperty) Rebuild();
    }

    void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    void Rebuild()
    {
        var lang = Provider.GetLang(this);
        var rows = ItemsSource?.Cast<object>().ToList() ?? [];
        var columns = Columns;
        var types = columns.Select(column => TableColumns.TypeOf(column, rows)).ToArray();
        var merged = MergeRepeated is { Count: > 0 } keys ? TableColumns.RepeatedRowSpans(rows, columns, keys) : null;

        var head = new TableRow();
        for (var position = 0; position < columns.Count; position++)
            head.Cells.Add(new TableHeaderCell { Name = ColumnTableParts.HeaderCell, Align = AlignOf(columns[position], types, position), ColumnWidth = columns[position].Width, Content = columns[position].Header });
        var body = new TableBody();
        if (rows.Count == 0)
        {
            var empty = new TableCell { Name = ColumnTableParts.Cell, ColumnSpan = Math.Max(1, columns.Count), Align = TableAlign.Center, Content = Empty ?? lang.TableEmpty };
            empty.Bind(TemplatedControl.ForegroundProperty, empty.GetResourceObservable(ThemeKeys.Brush.TextMuted), BindingPriority.Template);
            body.Rows.Add(new TableRow { Name = ColumnTableParts.Row, Cells = { empty } });
        }
        for (var index = 0; index < rows.Count; index++)
        {
            var row = new TableRow { Name = ColumnTableParts.Row, DataContext = rows[index] };
            for (var position = 0; position < columns.Count; position++)
            {
                var column = columns[position];
                var span = SpanOf(rows[index], column, index, merged);
                if (span is { RowSpan: 0 } or { ColumnSpan: 0 }) continue;
                row.Cells.Add(new TableCell
                {
                    Name = ColumnTableParts.Cell,
                    Align = AlignOf(column, types, position),
                    Nowrap = !column.Wrap && column.Width is not null,
                    RowSpan = span?.RowSpan ?? 1,
                    ColumnSpan = span?.ColumnSpan ?? 1,
                    Content = column.Cell?.Invoke(rows[index], index) ?? Display(column, types[position], TableColumns.ValueOf(column, rows[index]), lang),
                });
            }
            body.Rows.Add(row);
        }
        Sections.Clear();
        Sections.Add(new TableHead { Rows = { head } });
        Sections.Add(body);
        if (columns.Any(column => column.Summary is not null)) Sections.Add(new TableFoot { Rows = { SummaryRow(rows, columns, types, lang) } });
    }

    /// <summary>一格的合并：GetCellSpan 优先，其次 MergeRepeated。</summary>
    CellSpan? SpanOf(object row, DataTableColumn column, int index, IReadOnlyDictionary<string, int[]>? merged)
    {
        if (GetCellSpan?.Invoke(row, column, index) is { } own) return own;
        return merged?.TryGetValue(column.Key, out var spans) == true && spans[index] != 1 ? new CellSpan(spans[index]) : null;
    }

    TableRow SummaryRow(IReadOnlyList<object> rows, IReadOnlyList<DataTableColumn> columns, ColumnType[] types, Lang lang)
    {
        var row = new TableRow();
        for (var position = 0; position < columns.Count; position++)
        {
            var column = columns[position];
            object? content = column.Summary switch
            {
                { Compute: { } compute } => compute(rows),
                { } => TableColumns.Sum(column, rows) is { } total ? Display(column, types[position], total, lang) : column.Empty ?? lang.EmptyValue,
                null when position == 0 => SummaryLabel ?? lang.TableSummary,
                _ => null,
            };
            row.Cells.Add(new TableCell { Name = ColumnTableParts.SummaryCell, Align = AlignOf(column, types, position), Content = content });
        }
        return row;
    }

    /// <summary>列的对齐：写了的，状态列起始边，其余按值类型。</summary>
    static TableAlign AlignOf(DataTableColumn column, ColumnType[] types, int position) =>
        column.Align ?? (column.Statuses is not null ? TableAlign.Start : TableColumns.AlignOf(types[position]));

    /// <summary>一个值的显示（TS 的 useColumnDisplay）：空值显示列的 Empty（默认"—"），状态显示成 StatusBadge，其余按类型与 Format 格式化。</summary>
    static object? Display(DataTableColumn column, ColumnType type, object? value, Lang lang)
    {
        if (TableColumns.IsEmpty(value)) return column.Empty ?? lang.EmptyValue;
        if (column.Statuses is { } statuses) return new StatusBadge { Statuses = statuses, Value = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) };
        return TableColumns.FormatCell(value, type, column.Format, lang.Code, lang.Currency, TimeText.Zone(lang.TimeZone));
    }
}

/// <summary>ColumnTable 的部件名（TS 的 propsHeaderCell、propsRow、propsCell、propsSummaryCell）：样式写 <c>rune|TableCell#PART_Cell</c>，按行的数据写样式用行的 DataContext。</summary>
public static class ColumnTableParts
{
    /// <summary>列标题（th）。</summary>
    public const string HeaderCell = "PART_HeaderCell";

    /// <summary>表体的每一行（tr）。</summary>
    public const string Row = "PART_Row";

    /// <summary>表体的每个单元格（td）。</summary>
    public const string Cell = "PART_Cell";

    /// <summary>合计行的每个单元格。</summary>
    public const string SummaryCell = "PART_SummaryCell";
}
