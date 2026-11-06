using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 二维布局（vue-tsx-pro 的 Grid，CSS grid）：等宽的列，子控件按 Controls 的顺序逐行放下。
/// <code>
/// var cards = new GridControl { MinItemWidth = "14em", Gap = Space.Md, Dock = DockStyle.Top };
/// cards.Controls.AddRange([card1, card2, card3]);
/// cards.SetSpan(card1, 2);   // 跨两列（设计器里是子控件的"grid1 上的 Span"）；也可以把子控件包进 GridItemControl
/// </code>
/// 同时给 Columns 与 MinItemWidth 时以 MinItemWidth 为准；项目比放得下的列数少时，空列收起、各项拉宽（auto-fit）。
/// 子控件的宽度都拉到格子宽；高度：自动大小的按格子宽取想要的高度、stretch 时拉到行高，关掉 AutoSize 的保留自己的高度。
/// 自己的大小：宽度由父容器给（停靠、锚定），高度跟着内容（AutoSize）。间距按 GridControl 的字体算（em）。
/// </summary>
[ProvideProperty("Span", typeof(Control))]
[ProvideProperty("RowSpan", typeof(Control))]
public class GridControl : Panel, IExtenderProvider
{
    readonly GridEngine engine;
    readonly Dictionary<Control, GridSpan> spans = [];
    readonly Dictionary<Control, int> rowSpans = [];

    /// <summary>新建：高度跟着内容。</summary>
    public GridControl()
    {
        engine = new GridEngine(this);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    /// <summary>列数（等宽列）。</summary>
    [Category("Layout")]
    [Description("列数（等宽列）；与 MinItemWidth 同时给时以 MinItemWidth 为准。")]
    [DefaultValue(null)]
    public int? Columns { get; set { field = value; PerformLayout(); } }

    /// <summary>每项的最小宽度（14em、9rem、200）：按宽度自动决定列数。</summary>
    [Category("Layout")]
    [Description("每项的最小宽度（14em、9rem、200）：按宽度自动决定列数。")]
    [DefaultValue(null)]
    public Length? MinItemWidth { get; set { field = value; PerformLayout(); } }

    /// <summary>间距档位，对应主题的 space。</summary>
    [Category("Layout")]
    [Description("间距档位（none、xs、sm、md、lg、xl）。")]
    public Space Gap { get; set { field = value; PerformLayout(); } }

    /// <summary>行间距，盖过 Gap。</summary>
    [Category("Layout")]
    [Description("行间距，盖过 Gap。")]
    public Space RowGap { get; set { field = value; PerformLayout(); } }

    /// <summary>列间距，盖过 Gap。</summary>
    [Category("Layout")]
    [Description("列间距，盖过 Gap。")]
    public Space ColumnGap { get; set { field = value; PerformLayout(); } }

    /// <summary>单元格在块方向的对齐（align-items），默认 stretch。</summary>
    [Category("Layout")]
    [Description("单元格在竖直方向的对齐：start、center、end、stretch（默认）。")]
    public GridAlign Align { get; set { field = value; PerformLayout(); } }

    /// <summary>单元格在行内方向的对齐（justify-items），默认 stretch。</summary>
    [Category("Layout")]
    [Description("单元格在水平方向的对齐：start、center、end、stretch（默认）。")]
    public GridAlign Justify { get; set { field = value; PerformLayout(); } }

    /// <summary>自动排布方向（grid-auto-flow），dense 回填空位。</summary>
    [Category("Layout")]
    [Description("自动排布方向：row（默认）、column、row dense、column dense。")]
    public GridFlow Flow { get; set { field = value; PerformLayout(); } }

    /// <summary>现在实际有几列（排过之后）。</summary>
    internal int ColumnCount { get; private set; } = 1;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    bool ShouldSerializeGap() => Gap.Value is not null;

    void ResetGap() => Gap = default;

    bool ShouldSerializeRowGap() => RowGap.Value is not null;

    void ResetRowGap() => RowGap = default;

    bool ShouldSerializeColumnGap() => ColumnGap.Value is not null;

    void ResetColumnGap() => ColumnGap = default;

    bool ShouldSerializeAlign() => Align.Value is not null;

    void ResetAlign() => Align = default;

    bool ShouldSerializeJustify() => Justify.Value is not null;

    void ResetJustify() => Justify = default;

    bool ShouldSerializeFlow() => Flow.Value is not null;

    void ResetFlow() => Flow = default;

    /// <summary>直接的子控件都能设跨度。</summary>
    bool IExtenderProvider.CanExtend(object extendee) => extendee is Control control && control.Parent == this;

    /// <summary>子控件跨几列；没写时取 GridItemControl 的 Span，都没有时 1。</summary>
    /// <param name="control">子控件。</param>
    [DisplayName("Span")]
    [Category("Layout")]
    [Description("跨几列；full 占满一整行。")]
    public GridSpan GetSpan(Control control) => spans.TryGetValue(control, out var span) ? span : (control as GridItemControl)?.Span ?? default;

    /// <summary>设子控件跨几列。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">列数或 full；default 时不再盖过。</param>
    public void SetSpan(Control control, GridSpan value)
    {
        if (value.Value is null) spans.Remove(control);
        else spans[control] = value;
        PerformLayout();
    }

    /// <summary>子控件跨几行；没写时取 GridItemControl 的 RowSpan，都没有时 1。</summary>
    /// <param name="control">子控件。</param>
    [DisplayName("RowSpan")]
    [Category("Layout")]
    [Description("跨几行。")]
    [DefaultValue(1)]
    public int GetRowSpan(Control control) => rowSpans.TryGetValue(control, out var rows) ? rows : (control as GridItemControl)?.RowSpan ?? 1;

    /// <summary>设子控件跨几行。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">行数。</param>
    public void SetRowSpan(Control control, int value)
    {
        rowSpans[control] = Math.Max(1, value);
        PerformLayout();
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        spans.Remove(e.Control!);
        rowSpans.Remove(e.Control!);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PerformLayout();
    }

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        PerformLayout();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => engine.Measure(proposedSize);

    /// <summary>grid 排版：交给 <see cref="GridLayout"/> 放、算行高，再摆（从右到左时镜像）。</summary>
    sealed class GridEngine(GridControl owner) : LayoutEngine
    {
        double Em => owner.Font.SizeInPoints * owner.DeviceDpi / 72f;

        (double Row, double Column) Gaps =>
            (Math.Round(((owner.RowGap.Value is null ? owner.Gap : owner.RowGap).Factor ?? 0) * Em),
             Math.Round(((owner.ColumnGap.Value is null ? owner.Gap : owner.ColumnGap).Factor ?? 0) * Em));

        List<Control> Shown() => [.. owner.Controls.Cast<Control>().Where(child => child.Visible || !owner.Visible)];

        static int HeightAt(Control child, int width) => child.AutoSize ? child.GetPreferredSize(new Size(Math.Max(1, width), 0)).Height : child.Height;

        (GridLayout.Cell[] Cells, double ColumnWidth, double[] Rows) Compute(List<Control> children, double width)
        {
            var gaps = Gaps;
            var items = children.Select(child => new GridLayout.Item(owner.GetSpan(child).Count ?? 0, owner.GetRowSpan(child))).ToList();
            var min = owner.MinItemWidth?.Pixels(Em) * owner.DeviceDpi / 96;
            var columns = GridLayout.Columns(width, gaps.Column, owner.Columns, min, items.Count);
            var columnFlow = owner.Flow.Value is "column" or "column dense";
            var dense = owner.Flow.Value is "row dense" or "column dense";
            var cells = GridLayout.Place(items, columns, columnFlow, dense);
            // auto-fit：项目比放得下的列数少时，空列收起
            if (min is not null && !double.IsInfinity(width) && GridLayout.UsedColumns(cells) is var used && used < columns)
                cells = GridLayout.Place(items, columns = used, columnFlow, dense);
            owner.ColumnCount = Math.Max(columns, GridLayout.UsedColumns(cells));
            var natural = children.Select((child, i) => ((child.AutoSize ? child.GetPreferredSize(Size.Empty).Width : child.Width) - (cells[i].Span - 1) * gaps.Column) / cells[i].Span).DefaultIfEmpty().Max();
            var columnWidth = GridLayout.ColumnWidth(width, gaps.Column, owner.ColumnCount, natural);
            var heights = children.Select((child, i) => (double)HeightAt(child, (int)Math.Round(cells[i].Span * columnWidth + (cells[i].Span - 1) * gaps.Column))).ToList();
            return (cells, columnWidth, GridLayout.Rows(cells, heights, gaps.Row));
        }

        /// <summary>想要的大小：宽度是布局给的（不限时按内容），高度是各行加起来。</summary>
        public Size Measure(Size proposed)
        {
            var limit = proposed.Width is > 1 and < int.MaxValue ? proposed.Width : owner.Width > 0 && owner.Dock is DockStyle.Top or DockStyle.Bottom or DockStyle.Fill ? owner.Width : int.MaxValue;
            if (owner.MaximumSize.Width > 0) limit = Math.Min(limit, owner.MaximumSize.Width);
            var padding = owner.Padding;
            var width = limit == int.MaxValue ? double.PositiveInfinity : limit - padding.Horizontal;
            var (_, columnWidth, rows) = Compute(Shown(), width);
            var gaps = Gaps;
            var used = owner.ColumnCount * columnWidth + (owner.ColumnCount - 1) * gaps.Column;
            return new Size((int)Math.Ceiling(used), (int)Math.Ceiling(rows.Sum() + Math.Max(0, rows.Length - 1) * gaps.Row)) + padding.Size;
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            var area = owner.DisplayRectangle;
            var children = Shown();
            var (cells, columnWidth, rows) = Compute(children, area.Width);
            var gaps = Gaps;
            var mirror = owner.RightToLeft == RightToLeft.Yes;
            for (var i = 0; i < children.Count; i++)
            {
                var cell = cells[i];
                var child = children[i];
                var x = cell.Column * (columnWidth + gaps.Column);
                var y = rows.Take(cell.Row).Sum() + cell.Row * gaps.Row;
                var width = cell.Span * columnWidth + (cell.Span - 1) * gaps.Column;
                var height = rows.Skip(cell.Row).Take(cell.RowSpan).Sum() + (cell.RowSpan - 1) * gaps.Row;
                var natural = child.AutoSize ? child.GetPreferredSize(Size.Empty).Width : width;
                var (dx, w) = GridLayout.Align(natural, width, owner.Justify);
                var own = HeightAt(child, (int)Math.Round(w));
                var (dy, h) = GridLayout.Align(own, height, child.AutoSize ? owner.Align : owner.Align.Value is null or "stretch" ? GridAlign.Start : owner.Align);
                var left = mirror ? area.Width - (x + dx) - w : x + dx;
                child.SetBounds(area.X + (int)Math.Round(left), area.Y + (int)Math.Round(y + dy), (int)Math.Round(w), (int)Math.Round(h));
            }
            return owner.AutoSize;
        }
    }
}

/// <summary>grid 布局的列表（vue-tsx-pro 的 GridList，ul 去掉圆点）：卡片列表等，读屏里是列表；子控件用 <see cref="GridListItemControl"/>。</summary>
public class GridListControl : GridControl
{
    /// <summary>新建：读屏里是列表。</summary>
    public GridListControl() => AccessibleRole = AccessibleRole.List;
}

/// <summary>
/// 网格里的一项（vue-tsx-pro 的 GridItem）：包一层，带上跨度（也可以直接用 GridControl 的 SetSpan、SetRowSpan）。
/// 里面的子控件竖着排、撑满宽度，大小跟着内容。
/// </summary>
public class GridItemControl : Panel
{
    readonly VerticalStack layout = new(() => 0);

    /// <summary>新建：大小跟着内容。</summary>
    public GridItemControl()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    /// <summary>跨几列；full 占满一整行。按宽度自动换列窄到放不下时跨当前的列数。</summary>
    [Category("Layout")]
    [Description("跨几列；full 占满一整行。")]
    public GridSpan Span { get; set { field = value; Parent?.PerformLayout(this, nameof(Span)); } }

    /// <summary>跨几行。</summary>
    [Category("Layout")]
    [Description("跨几行。")]
    [DefaultValue(1)]
    public int RowSpan { get; set { field = Math.Max(1, value); Parent?.PerformLayout(this, nameof(RowSpan)); } } = 1;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => layout;

    bool ShouldSerializeSpan() => Span.Value is not null;

    void ResetSpan() => Span = default;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => layout.Measure(this, proposedSize);
}

/// <summary>GridList 里的一项（vue-tsx-pro 的 GridListItem，li）：读屏里是列表项。</summary>
public class GridListItemControl : GridItemControl
{
    /// <summary>新建：读屏里是列表项。</summary>
    public GridListItemControl() => AccessibleRole = AccessibleRole.ListItem;
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

/// <summary>跨几列（vue-tsx-pro 的 span：number | 'full'）。属性窗口里写数字或 full；C# 里 int 能直接赋值。</summary>
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
/// CSS 长度（vue-tsx-pro 里写成字符串的尺寸，如 Grid 的 minItemWidth）：像素数（200、200px，设备无关像素）、em（按控件的字体）、
/// rem（按浏览器的根字号 16px）。
/// </summary>
/// <param name="Value">写法，如 14em。</param>
[TypeConverter(typeof(LengthConverter))]
public readonly record struct Length(string Value)
{
    /// <summary>换成设备无关像素。</summary>
    /// <param name="em">所在控件的字号（设备无关像素）。</param>
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

    /// <summary>写法（14em、9rem、200）。</summary>
    /// <param name="value">写法。</param>
    public static implicit operator Length(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Length 与字符串互转（属性窗口）。</summary>
public sealed class LengthConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? new Length(text) : base.ConvertFrom(context, culture, value);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        value is Length length && destinationType == typeof(string) ? length.Value : base.ConvertTo(context, culture, value, destinationType);
}
