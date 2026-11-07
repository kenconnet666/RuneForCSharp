using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 二维布局（vue-tsx-pro 的 Grid，CSS grid）：等宽的列，子元素按行自动放下；跨列、跨行用 <see cref="GridItem"/>。
/// <code>
/// &lt;rune:Grid Columns="3" Gap="md"&gt;…&lt;/rune:Grid&gt;                 &lt;!-- 固定列数，等宽 --&gt;
/// &lt;rune:Grid MinItemWidth="14em" Gap="md"&gt;…&lt;/rune:Grid&gt;         &lt;!-- 按宽度自动换列：多宽就放几列，不需要断点 --&gt;
/// &lt;rune:GridItem Span="2"&gt;…&lt;/rune:GridItem&gt;                     &lt;!-- 跨两列；full 占满一整行 --&gt;
/// </code>
/// 同时给 Columns 与 MinItemWidth 时以 MinItemWidth 为准；项目比放得下的列数少时，空列收起、各项拉宽（auto-fit）。
/// 跨列数不超过当前的列数（自动换列窄到放不下时跨当前的列数）。间距按 Grid 自己的字号算（em）。
/// WPF 自己有 Grid（行列定义）：在 XAML 里写 rune:Grid，C# 里同时引用两个命名空间时写全名。
/// </summary>
public class Grid : Panel
{
    /// <summary>列数（等宽列）。</summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int?), typeof(Grid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>每项的最小宽度（14em、9rem、200）：按宽度自动决定列数。</summary>
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(Length?), typeof(Grid), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>和 MinItemWidth 一起用：按项数取列数，最后一行最多空一格（4 项放得下 3 列时排 2 + 2，6 项放得下 4 列时排 3 + 3），一排卡片不落单。</summary>
    public static readonly DependencyProperty BalancedProperty = DependencyProperty.Register(
        nameof(Balanced), typeof(bool), typeof(Grid), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>按最小宽度分列时保留空列（auto-fill，项少时不拉宽；卡片外观的复选组、单选组）；默认收起（auto-fit）。</summary>
    internal static readonly DependencyProperty AutoFillProperty = DependencyProperty.Register(
        "AutoFill", typeof(bool), typeof(Grid), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>间距档位，对应主题的 space。</summary>
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(Space), typeof(Grid), new FrameworkPropertyMetadata(default(Space), OnGapChanged));

    /// <summary>行间距，盖过 Gap。</summary>
    public static readonly DependencyProperty RowGapProperty = DependencyProperty.Register(
        nameof(RowGap), typeof(Space), typeof(Grid), new FrameworkPropertyMetadata(default(Space), OnGapChanged));

    /// <summary>列间距，盖过 Gap。</summary>
    public static readonly DependencyProperty ColumnGapProperty = DependencyProperty.Register(
        nameof(ColumnGap), typeof(Space), typeof(Grid), new FrameworkPropertyMetadata(default(Space), OnGapChanged));

    /// <summary>单元格在块方向的对齐（align-items），默认 stretch。</summary>
    public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(
        nameof(Align), typeof(GridAlign), typeof(Grid), new FrameworkPropertyMetadata(default(GridAlign), FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>单元格在行内方向的对齐（justify-items），默认 stretch。</summary>
    public static readonly DependencyProperty JustifyProperty = DependencyProperty.Register(
        nameof(Justify), typeof(GridAlign), typeof(Grid), new FrameworkPropertyMetadata(default(GridAlign), FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>自动排布方向（grid-auto-flow），dense 回填空位。</summary>
    public static readonly DependencyProperty FlowProperty = DependencyProperty.Register(
        nameof(Flow), typeof(GridFlow), typeof(Grid), new FrameworkPropertyMetadata(default(GridFlow), FrameworkPropertyMetadataOptions.AffectsMeasure));

    static readonly DependencyProperty RowGapFactorProperty = DependencyProperty.Register(
        "RowGapFactor", typeof(double), typeof(Grid), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static readonly DependencyProperty ColumnGapFactorProperty = DependencyProperty.Register(
        "ColumnGapFactor", typeof(double), typeof(Grid), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    GridLayout.Cell[] cells = [];
    double[] rows = [];
    double columnWidth;

    static Grid() => TextElement.FontSizeProperty.OverrideMetadata(typeof(Grid), new FrameworkPropertyMetadata(
        SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>列数。</summary>
    public int? Columns { get => (int?)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>每项的最小宽度。</summary>
    public Length? MinItemWidth { get => (Length?)GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }

    /// <summary>均衡换列。</summary>
    public bool Balanced { get => (bool)GetValue(BalancedProperty); set => SetValue(BalancedProperty, value); }

    /// <summary>间距档位。</summary>
    public Space Gap { get => (Space)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>行间距。</summary>
    public Space RowGap { get => (Space)GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>列间距。</summary>
    public Space ColumnGap { get => (Space)GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <summary>块方向的对齐。</summary>
    public GridAlign Align { get => (GridAlign)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>行内方向的对齐。</summary>
    public GridAlign Justify { get => (GridAlign)GetValue(JustifyProperty); set => SetValue(JustifyProperty, value); }

    /// <summary>自动排布方向。</summary>
    public GridFlow Flow { get => (GridFlow)GetValue(FlowProperty); set => SetValue(FlowProperty, value); }

    /// <summary>现在实际有几列（量过之后）。</summary>
    internal int ColumnCount { get; private set; } = 1;

    double Em => TextElement.GetFontSize(this);

    (double Row, double Column) Gaps => ((double)GetValue(RowGapFactorProperty) * Em, (double)GetValue(ColumnGapFactorProperty) * Em);

    static void OnGapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (Grid)d;
        Reference(grid, RowGapFactorProperty, (grid.RowGap.Value is null ? grid.Gap : grid.RowGap).ResourceKey);
        Reference(grid, ColumnGapFactorProperty, (grid.ColumnGap.Value is null ? grid.Gap : grid.ColumnGap).ResourceKey);
    }

    static void Reference(Grid grid, DependencyProperty property, string? key)
    {
        if (key is null) grid.ClearValue(property);
        else grid.SetResourceReference(property, key);
    }

    List<UIElement> Shown() => [.. InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed)];

    static GridLayout.Item ItemOf(UIElement child) => child is GridItem item
        ? new GridLayout.Item(item.Span.Count ?? 0, Math.Max(1, item.RowSpan))
        : new GridLayout.Item(1, 1);

    double CellWidth(GridLayout.Cell cell) => cell.Span * columnWidth + (cell.Span - 1) * Gaps.Column;

    /// <summary>放好、量好：列数、每项的格子、列宽、行高。</summary>
    void Compute(List<UIElement> children, double width)
    {
        var gaps = Gaps;
        var items = children.Select(ItemOf).ToList();
        var columns = GridLayout.Columns(width, gaps.Column, Columns, MinItemWidth?.Pixels(Em), items.Count, Balanced);
        var columnFlow = Flow.Value is "column" or "column dense";
        var dense = Flow.Value is "row dense" or "column dense";
        cells = GridLayout.Place(items, columns, columnFlow, dense);
        // auto-fit：项目比放得下的列数少时，空列收起
        if (MinItemWidth is not null && !(bool)GetValue(AutoFillProperty) && !double.IsInfinity(width) && GridLayout.UsedColumns(cells) is var used && used < columns)
            cells = GridLayout.Place(items, columns = used, columnFlow, dense);
        ColumnCount = Math.Max(columns, GridLayout.UsedColumns(cells));
        if (double.IsInfinity(width))
            foreach (var child in children) child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var natural = children.Select((child, i) => (child.DesiredSize.Width - (cells[i].Span - 1) * gaps.Column) / cells[i].Span).DefaultIfEmpty().Max();
        columnWidth = GridLayout.ColumnWidth(width, gaps.Column, ColumnCount, natural);
        var heights = new double[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            children[i].Measure(new Size(CellWidth(cells[i]), double.PositiveInfinity));
            heights[i] = children[i].DesiredSize.Height;
        }
        rows = GridLayout.Rows(cells, heights, gaps.Row);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        Compute(Shown(), availableSize.Width);
        var gaps = Gaps;
        return new Size(ColumnCount * columnWidth + (ColumnCount - 1) * gaps.Column, rows.Sum() + Math.Max(0, rows.Length - 1) * gaps.Row);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Shown();
        Compute(children, finalSize.Width);
        var gaps = Gaps;
        for (var i = 0; i < children.Count; i++)
        {
            var cell = cells[i];
            var x = cell.Column * (columnWidth + gaps.Column);
            var y = rows.Take(cell.Row).Sum() + cell.Row * gaps.Row;
            var (width, height) = (CellWidth(cell), rows.Skip(cell.Row).Take(cell.RowSpan).Sum() + (cell.RowSpan - 1) * gaps.Row);
            var (dx, w) = GridLayout.Align(children[i].DesiredSize.Width, width, Justify);
            var (dy, h) = GridLayout.Align(children[i].DesiredSize.Height, height, Align);
            children[i].Arrange(new Rect(x + dx, y + dy, w, h));
        }
        return finalSize;
    }
}

/// <summary>grid 布局的列表（vue-tsx-pro 的 GridList，ul 去掉圆点）：卡片列表等，读屏里是列表；子元素用 <see cref="GridListItem"/>。</summary>
public class GridList : Grid
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    sealed class ListPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => nameof(GridList);
    }
}

/// <summary>网格里的一项（vue-tsx-pro 的 GridItem）：跨列、跨行。只是包一层，读屏看不到它。</summary>
public class GridItem : Decorator
{
    /// <summary>跨几列；full 占满一整行。按宽度自动换列窄到放不下时跨当前的列数。</summary>
    public static readonly DependencyProperty SpanProperty = DependencyProperty.Register(
        nameof(Span), typeof(GridSpan), typeof(GridItem), new FrameworkPropertyMetadata(default(GridSpan), FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>跨几行。</summary>
    public static readonly DependencyProperty RowSpanProperty = DependencyProperty.Register(
        nameof(RowSpan), typeof(int), typeof(GridItem), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>跨几列。</summary>
    public GridSpan Span { get => (GridSpan)GetValue(SpanProperty); set => SetValue(SpanProperty, value); }

    /// <summary>跨几行。</summary>
    public int RowSpan { get => (int)GetValue(RowSpanProperty); set => SetValue(RowSpanProperty, value); }
}

/// <summary>GridList 里的一项（vue-tsx-pro 的 GridListItem，li）：读屏里是列表项。</summary>
public class GridListItem : GridItem
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListItemPeer(this);

    sealed class ListItemPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(GridListItem);
    }
}

/// <summary>单元格内容在格子里的对齐（vue-tsx-pro 的 GridAligns）。</summary>
/// <param name="Value">取值（start、center、end、stretch）。</param>
[TypeConverter(typeof(VariantConverter<GridAlign>))]
public readonly record struct GridAlign(string Value) : IVariant<GridAlign>
{
    /// <summary>贴着格子的起始边。</summary>
    public static readonly GridAlign Start = new("start");

    /// <summary>居中。</summary>
    public static readonly GridAlign Center = new("center");

    /// <summary>贴着格子的结束边。</summary>
    public static readonly GridAlign End = new("end");

    /// <summary>拉伸填满格子（默认）。</summary>
    public static readonly GridAlign Stretch = new("stretch");

    /// <inheritdoc />
    public static IReadOnlyList<GridAlign> Known { get; } = [Start, Center, End, Stretch];

    /// <inheritdoc />
    public static GridAlign Parse(string value)
    {
        var align = new GridAlign(value);
        Variants.WarnIfUnknown(align);
        return align;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>自动排布方向（vue-tsx-pro 的 GridFlows，grid-auto-flow）。</summary>
/// <param name="Value">取值（row、column、row dense、column dense）。</param>
[TypeConverter(typeof(VariantConverter<GridFlow>))]
public readonly record struct GridFlow(string Value) : IVariant<GridFlow>
{
    /// <summary>逐行填充（默认）。</summary>
    public static readonly GridFlow Row = new("row");

    /// <summary>逐列填充。</summary>
    public static readonly GridFlow Column = new("column");

    /// <summary>逐行填充，并回填前面留下的空位（顺序可能变化）。</summary>
    public static readonly GridFlow RowDense = new("row dense");

    /// <summary>逐列填充，并回填空位。</summary>
    public static readonly GridFlow ColumnDense = new("column dense");

    /// <inheritdoc />
    public static IReadOnlyList<GridFlow> Known { get; } = [Row, Column, RowDense, ColumnDense];

    /// <inheritdoc />
    public static GridFlow Parse(string value)
    {
        var flow = new GridFlow(value);
        Variants.WarnIfUnknown(flow);
        return flow;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>跨几列（vue-tsx-pro 的 span：number | 'full'）。XAML 里写数字或 full；C# 里 int 能直接赋值。</summary>
/// <param name="Value">取值（数字或 full）。</param>
[TypeConverter(typeof(VariantConverter<GridSpan>))]
public readonly record struct GridSpan(string Value) : IVariant<GridSpan>
{
    /// <summary>占满一整行（从第一列到最后一列）。</summary>
    public static readonly GridSpan Full = new("full");

    /// <inheritdoc />
    public static IReadOnlyList<GridSpan> Known { get; } = [Full];

    /// <summary>跨的列数；full 时为 null；没写时 1。</summary>
    public int? Count => Value switch
    {
        null => 1,
        "full" => null,
        _ => int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? Math.Max(1, count) : 1,
    };

    /// <summary>列数。</summary>
    /// <param name="count">跨几列。</param>
    public static implicit operator GridSpan(int count) => new(count.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public static GridSpan Parse(string value)
    {
        var span = new GridSpan(value);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) Variants.WarnIfUnknown(span);
        return span;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// CSS 长度（vue-tsx-pro 里写成字符串的尺寸，如 Grid 的 minItemWidth）：像素数（200、200px）、em（按元素的字号）、
/// rem（按浏览器的根字号 16px）。
/// </summary>
/// <param name="Value">写法，如 14em。</param>
[TypeConverter(typeof(LengthConverter))]
public readonly record struct Length(string Value)
{
    /// <summary>换成设备无关像素。</summary>
    /// <param name="em">所在元素的字号。</param>
    public double Pixels(double em)
    {
        var text = Value.Trim();
        var (number, unit) = text.EndsWith("rem", StringComparison.Ordinal) ? (text[..^3], 16d)
            : text.EndsWith("em", StringComparison.Ordinal) ? (text[..^2], em)
            : text.EndsWith("px", StringComparison.Ordinal) ? (text[..^2], 1d)
            : (text, 1d);
        return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value * unit
            : throw new FormatException($"认不出长度 \"{Value}\"：写像素数（200、200px）、em 或 rem。");
    }

    /// <summary>像素数。</summary>
    /// <param name="pixels">设备无关像素。</param>
    public static implicit operator Length(double pixels) => new(pixels.ToString(CultureInfo.InvariantCulture));

    /// <summary>写法（同 XAML：14em、9rem、200）。</summary>
    /// <param name="value">写法。</param>
    public static implicit operator Length(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Length 与字符串互转（XAML、设计器）。</summary>
public sealed class LengthConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? new Length(text) : base.ConvertFrom(context, culture, value);
}
