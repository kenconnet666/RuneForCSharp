using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>透视表的一个维度（行或列）：取哪个字段、表头、显示与顺序。</summary>
/// <param name="Key">字段名（取同名属性）；写了 Value 时只作标识。</param>
/// <param name="Header">表头（行维度是这一列的表头；列维度显示在读屏的说明里）。</param>
public sealed record PivotDimension(string Key, object Header)
{
    /// <summary>取值；不写时取同名属性。</summary>
    public Func<object, object?>? Value { get; init; }

    /// <summary>显示的文字；不写时是值本身。</summary>
    public Func<string, string>? Format { get; init; }

    /// <summary>顺序：按这个排，没列出的排在后面；不写时按值排序。</summary>
    public IReadOnlyList<string>? Order { get; init; }
}

/// <summary>透视表聚合的数值。</summary>
/// <param name="Key">字段名（取同名属性）；写了 Value 时只作标识。</param>
/// <param name="Header">名称：没有列维度时是那一列的表头。</param>
public sealed record PivotValue(string Key, object Header)
{
    /// <summary>取值；不写时取同名属性。</summary>
    public Func<object, object?>? Value { get; init; }

    /// <summary>值的类型（同表格的列），默认 Number。</summary>
    public ColumnType? Type { get; init; }

    /// <summary>数字的格式（同表格的列）。</summary>
    public NumericFormat? Format { get; init; }
}

/// <summary>透视表的一行：行维度的值（每行的 DataContext）。</summary>
/// <param name="Key">各级维度值连起来的标识。</param>
/// <param name="Dimensions">各级行维度的值。</param>
public sealed record PivotRow(string Key, IReadOnlyList<string> Dimensions);

/// <summary>
/// 透视表（vue-tsx-pro 的 PivotTable）：明细按行维度（可以多级，相同的上级合并成一格）与列维度交叉汇总，每格是一个聚合值，带合计行与合计列；
/// <see cref="Heatmap"/> 时按数值给格子着色（热力图：一眼看出哪里多、哪里少）。建在 <see cref="Pivot"/> 与 ColumnTable 的建表上。
/// <code>
/// &lt;rune:PivotTable Data="{Binding Records}" Heatmap="True" AutomationProperties.Name="各区域、门店的季度销售额" /&gt;
/// table.Rows = [new("Region", "区域"), new("Supplier", "供应商")];
/// table.Columns = new("Quarter", "季度") { Order = ["Q1", "Q2", "Q3", "Q4"] };
/// table.Value = new("Amount", "销售额") { Type = ColumnType.Currency };
/// </code>
/// 数值的格式同表格的列（Type、Format）；<see cref="Aggregate"/> 是 Sum（默认）、Count、Avg、Min、Max。维度值按当前语言排序（中文按拼音，数字按大小）。
/// 热力图的颜色是强调色按比例的不透明度（6% 到 45%，再深时文字的对比度不够），合计不着色，读屏照常读数字。外观属性同 Table；每行的 DataContext 是 <see cref="PivotRow"/>，
/// 部件名同 ColumnTable（<see cref="ColumnTableParts"/>）。
/// </summary>
public class PivotTable : Table
{
    /// <summary>明细：实现了 INotifyCollectionChanged 的集合增删时跟着变。</summary>
    public static readonly StyledProperty<IEnumerable?> DataProperty = AvaloniaProperty.Register<PivotTable, IEnumerable?>(nameof(Data));

    /// <summary>行维度，可以多级（相同的上级合并成一格）。</summary>
    public static readonly StyledProperty<IReadOnlyList<PivotDimension>> RowsProperty = AvaloniaProperty.Register<PivotTable, IReadOnlyList<PivotDimension>>(nameof(Rows), []);

    /// <summary>列维度；不写时只有一列（按行汇总）。</summary>
    public static readonly StyledProperty<PivotDimension?> ColumnsProperty = AvaloniaProperty.Register<PivotTable, PivotDimension?>(nameof(Columns));

    /// <summary>聚合的数值；Count 时可以不写。</summary>
    public static readonly StyledProperty<PivotValue?> ValueProperty = AvaloniaProperty.Register<PivotTable, PivotValue?>(nameof(Value));

    /// <summary>聚合方式，默认 Sum。</summary>
    public static readonly StyledProperty<PivotAggregate> AggregateProperty = AvaloniaProperty.Register<PivotTable, PivotAggregate>(nameof(Aggregate));

    /// <summary>合计行与合计列，默认 true。</summary>
    public static readonly StyledProperty<bool> TotalsProperty = AvaloniaProperty.Register<PivotTable, bool>(nameof(Totals), true);

    /// <summary>按数值给格子着色（热力图）；合计不着色。</summary>
    public static readonly StyledProperty<bool> HeatmapProperty = AvaloniaProperty.Register<PivotTable, bool>(nameof(Heatmap));

    /// <summary>没有明细的格子显示的内容，默认"—"。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<PivotTable, object?>(nameof(Empty));

    /// <summary>合计行第一列的内容（同 ColumnTable），默认取语言包（合计）。</summary>
    public static readonly StyledProperty<object?> SummaryLabelProperty = AvaloniaProperty.Register<PivotTable, object?>(nameof(SummaryLabel));

    /// <summary>合并单元格（同 ColumnTable 的 GetCellSpan）：行是 <see cref="PivotRow"/>；不写时只合并相同的上级维度。</summary>
    public static readonly StyledProperty<Func<object, DataTableColumn, int, CellSpan?>?> GetCellSpanProperty =
        AvaloniaProperty.Register<PivotTable, Func<object, DataTableColumn, int, CellSpan?>?>(nameof(GetCellSpan));

    /// <summary>热力图最浅、最深的不透明度（%）：再深时文字的对比度不够。</summary>
    const int MinHeat = 6, MaxHeat = 45;

    INotifyCollectionChanged? observed;

    /// <summary>明细。</summary>
    public IEnumerable? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <summary>行维度。</summary>
    public IReadOnlyList<PivotDimension> Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    /// <summary>列维度。</summary>
    public PivotDimension? Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>聚合的数值。</summary>
    public PivotValue? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>聚合方式。</summary>
    public PivotAggregate Aggregate { get => GetValue(AggregateProperty); set => SetValue(AggregateProperty, value); }

    /// <summary>合计行与合计列。</summary>
    public bool Totals { get => GetValue(TotalsProperty); set => SetValue(TotalsProperty, value); }

    /// <summary>热力图。</summary>
    public bool Heatmap { get => GetValue(HeatmapProperty); set => SetValue(HeatmapProperty, value); }

    /// <summary>没有明细的格子显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>合计行第一列的内容。</summary>
    public object? SummaryLabel { get => GetValue(SummaryLabelProperty); set => SetValue(SummaryLabelProperty, value); }

    /// <summary>合并单元格。</summary>
    public Func<object, DataTableColumn, int, CellSpan?>? GetCellSpan { get => GetValue(GetCellSpanProperty); set => SetValue(GetCellSpanProperty, value); }

    /// <summary>Table 的类型键：PivotTable 用 Table 的主题。</summary>
    protected override Type StyleKeyOverride => typeof(Table);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty)
        {
            if (observed is not null) observed.CollectionChanged -= OnDataChanged;
            observed = Data as INotifyCollectionChanged;
            if (observed is not null) observed.CollectionChanged += OnDataChanged;
            Rebuild();
        }
        else if (change.Property == RowsProperty || change.Property == ColumnsProperty || change.Property == ValueProperty || change.Property == AggregateProperty
                 || change.Property == TotalsProperty || change.Property == HeatmapProperty || change.Property == EmptyProperty || change.Property == SummaryLabelProperty
                 || change.Property == GetCellSpanProperty || change.Property == Provider.LangProperty) Rebuild();
    }

    void OnDataChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    /// <summary>维度、数值字段的取值：Value 函数，或同名属性（字典按键）。</summary>
    static Func<object, object?> FieldOf(string key, Func<object, object?>? value)
    {
        var column = new DataTableColumn(key, key) { Value = value };
        return row => TableColumns.ValueOf(column, row);
    }

    void Rebuild()
    {
        var lang = Provider.GetLang(this);
        var (dimensions, across, value, aggregate) = (Rows, Columns, Value, Aggregate);
        var result = Pivot.Of(Data?.Cast<object>() ?? [], new PivotOptions([.. dimensions.Select(dimension => FieldOf(dimension.Key, dimension.Value))])
        {
            Columns = across is null ? null : FieldOf(across.Key, across.Value),
            Value = value is null ? null : FieldOf(value.Key, value.Value),
            Aggregate = aggregate,
            RowOrder = [.. dimensions.Select(dimension => dimension.Order)],
            ColumnOrder = across?.Order,
            Compare = Pivot.Comparer(lang.Code),
        });
        var type = aggregate == PivotAggregate.Count ? ColumnType.Number : value?.Type ?? ColumnType.Number;
        var format = aggregate == PivotAggregate.Count ? null : value?.Format;
        var zone = TimeText.Zone(lang.TimeZone);
        object? Show(decimal? number) => number is { } n ? TableColumns.FormatCell(n, type, format, lang.Code, lang.Currency, zone) : Empty ?? lang.EmptyValue;
        IReadOnlyList<string> DimensionsOf(object row) => ((PivotRow)row).Dimensions;

        var columns = new List<DataTableColumn>();
        for (var level = 0; level < dimensions.Count; level++)
        {
            var (dimension, at) = (dimensions[level], level);
            columns.Add(new DataTableColumn($"row:{at}", dimension.Header)
            {
                Value = row => DimensionsOf(row)[at],
                Cell = (row, _) => DimensionsOf(row)[at] is var raw && dimension.Format is { } show ? show(raw) : raw is "" ? lang.EmptyValue : raw,
            });
        }
        foreach (var key in result.ColumnKeys)
            columns.Add(new DataTableColumn($"column:{key}", across is null ? value?.Header ?? lang.TableSummary : across.Format is { } show ? show(key) : key is "" ? lang.EmptyValue : key)
            {
                Align = TableAlign.End,
                Value = row => result.Cell(DimensionsOf(row), key),
                Cell = (row, _) => Show(result.Cell(DimensionsOf(row), key)),
                Summary = Totals ? ColumnSummary.Of(_ => Show(result.ColumnTotal(key))) : null,
            });
        if (Totals && across is not null)
            columns.Add(new DataTableColumn("total", lang.TableSummary)
            {
                Align = TableAlign.End,
                Cell = (row, _) => Show(result.RowTotal(DimensionsOf(row))),
                Summary = ColumnSummary.Of(_ => Show(result.GrandTotal)),
            });

        ColumnTable.Fill(this, new ColumnTableSource([.. result.RowKeys.Select(keys => new PivotRow(string.Join('\0', keys), keys))], columns, lang)
        {
            MergeRepeated = [.. Enumerable.Range(0, Math.Max(0, dimensions.Count - 1)).Select(level => $"row:{level}")],
            GetCellSpan = GetCellSpan,
            SummaryLabel = SummaryLabel,
            PrepareCell = (cell, row, column) =>
            {
                if (column.Key == "total") cell.Bind(TemplatedControl.FontWeightProperty, cell.GetResourceObservable(ThemeKeys.FontWeight.Semibold), BindingPriority.Template);
                else if (column.Key.StartsWith("column:", StringComparison.Ordinal) && Heat(result, result.Cell(DimensionsOf(row), column.Key[7..])) is { } percent)
                    cell.Bind(TemplatedControl.BackgroundProperty, cell.GetResourceObservable(ThemeKeys.Brush.Accent,
                        brush => brush is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, percent / 100d) : null), BindingPriority.Template);
            },
        });
    }

    /// <summary>热力图的不透明度（%）：按这一格在各格里最小到最大之间的位置；关着、空格子时 null。</summary>
    int? Heat(PivotResult result, decimal? number)
    {
        if (!Heatmap || number is not { } n || result.Extent is not var (low, high)) return null;
        var ratio = high == low ? 1 : (double)((n - low) / (high - low));
        return (int)Math.Round(MinHeat + ratio * (MaxHeat - MinHeat), MidpointRounding.AwayFromZero);
    }
}
