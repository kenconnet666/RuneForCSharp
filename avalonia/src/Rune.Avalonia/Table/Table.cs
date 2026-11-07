using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>
/// 静态表格（vue-tsx-pro 的 Table）：表格的各个部分各一个元素，内容直接写在 XAML 里（说明文字里的表格、属性表）。
/// <code>
/// &lt;rune:Table AutomationProperties.Name="属性"&gt;
///   &lt;rune:TableHead&gt;&lt;rune:TableRow&gt;&lt;rune:TableHeaderCell&gt;名称&lt;/rune:TableHeaderCell&gt;…&lt;/rune:TableRow&gt;&lt;/rune:TableHead&gt;
///   &lt;rune:TableBody&gt;&lt;rune:TableRow&gt;&lt;rune:TableCell&gt;size&lt;/rune:TableCell&gt;…&lt;/rune:TableRow&gt;&lt;/rune:TableBody&gt;
/// &lt;/rune:Table&gt;
/// </code>
/// 外观：<see cref="Density"/>（单元格上下 xs / sm / md，紧凑时字号 sm）、<see cref="Variant"/>（surface：border 边框、lg 圆角、surface 底；ghost：页面底色）、
/// <see cref="Striped"/>（斑马纹）。列宽按内容分配（同 CSS 的 table-layout: auto，放得下时按各列内容的比例分多出来的宽度，放不下时单元格换行，
/// 再放不下时横向滚动）；表头单元格不换行，<see cref="TableHeaderCell.ColumnWidth"/> 定宽。指针下的行 fillHover 底。
/// 表头 fill 底、textMuted、sm、中等字重，下边一道 border；表体的行标题中等字重；表尾的格加粗、上方一道 border。
/// </summary>
public class Table : Control
{
    /// <summary>密度，默认 standard。</summary>
    public static readonly StyledProperty<TableDensity> DensityProperty = AvaloniaProperty.Register<Table, TableDensity>(nameof(Density), TableDensity.Standard);

    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<TableVariant> VariantProperty = AvaloniaProperty.Register<Table, TableVariant>(nameof(Variant), TableVariant.Surface);

    /// <summary>斑马纹：表体的偶数行 fill 底。</summary>
    public static readonly StyledProperty<bool> StripedProperty = AvaloniaProperty.Register<Table, bool>(nameof(Striped));

    readonly Border root = new() { ClipToBounds = true };
    readonly ScrollArea scroll = new() { Axis = ScrollAreaAxis.X };
    readonly VisualStack stack = new();
    readonly TableGrid grid;
    TableCaption? caption;

    static Table() => AffectsMeasure<Table>(DensityProperty);

    /// <summary>新建：外框里是标题与可横向滚动的网格。</summary>
    public Table()
    {
        grid = new TableGrid(this);
        scroll.Content = grid;
        stack.Add(scroll);
        // 滚动区域可视上在竖排容器里，逻辑上挂在表格下：主题按逻辑树找
        LogicalChildren.Add(scroll);
        root.Child = stack;
        VisualChildren.Add(root);
        Sections.CollectionChanged += (_, e) =>
        {
            foreach (var item in e.OldItems?.OfType<Control>() ?? []) LogicalChildren.Remove(item);
            foreach (var item in e.NewItems?.OfType<Control>() ?? []) LogicalChildren.Add(item);
            Sync();
        };
    }

    /// <summary>表格的各个部分：TableCaption、TableHead、TableBody、TableFoot。</summary>
    [Content]
    public Controls Sections { get; } = [];

    /// <summary>密度。</summary>
    public TableDensity Density { get => GetValue(DensityProperty); set => SetValue(DensityProperty, value); }

    /// <summary>样式。</summary>
    public TableVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>斑马纹。</summary>
    public bool Striped { get => GetValue(StripedProperty); set => SetValue(StripedProperty, value); }

    /// <summary>所有行，按表头、表体、表尾的顺序（测试用）。</summary>
    internal IReadOnlyList<(TableSection Section, TableRow Row)> Rows =>
        [.. Sections.OfType<TableSection>().OrderBy(section => section.Order).SelectMany(section => section.Rows.OfType<TableRow>().Select(row => (section, row)))];

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DensityProperty || change.Property == VariantProperty || change.Property == StripedProperty) Sync();
        else if (change.Property == TextElement.FontSizeProperty) ApplyRoot();
    }

    /// <summary>行、单元格变了（加进来、去掉）：重新收集网格里的格，并把外观交给它们。</summary>
    internal void Sync()
    {
        // 标题逻辑上是表格的子元素，可视上放在网格上方（不能再进 ContentPresenter：一个元素只有一个逻辑父元素）
        var next = Sections.OfType<TableCaption>().FirstOrDefault();
        if (next != caption)
        {
            if (caption is not null) stack.Remove(caption);
            caption = next;
            if (caption is not null) stack.Insert(0, caption);
        }
        var compact = Density == TableDensity.Compact;
        if (compact) Bind(TextElement.FontSizeProperty, this.GetResourceObservable("Rune.FontSize.Sm"));
        else ClearValue(TextElement.FontSizeProperty);
        ApplyRoot();
        grid.Rebuild();
    }

    /// <summary>外框：surface 时 border 边框、lg 圆角、surface 底；ghost 时页面底色、没有边框。</summary>
    void ApplyRoot()
    {
        var ghost = Variant == TableVariant.Ghost;
        root.BorderThickness = ghost ? default : new Thickness(1);
        root.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Rune.Brush.Border"));
        root.Bind(Border.BackgroundProperty, this.GetResourceObservable(ghost ? "Rune.Brush.Background" : "Rune.Brush.Surface"));
        root.CornerRadius = ghost ? default : new CornerRadius(this.Resource("Rune.Radius.Lg", 0.5) * TextElement.GetFontSize(this));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 表格宽 100%：横向滚动区域给内容的是无限宽，列宽按表格自己的宽度（减去外框）分配
        var width = availableSize.Width - root.BorderThickness.Left - root.BorderThickness.Right;
        if (!double.IsInfinity(width) && Math.Abs(width - grid.Width_) > 0.5)
        {
            grid.Width_ = width;
            grid.InvalidateMeasure();
        }
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Sync();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TablePeer(this);

    sealed class TablePeer(Table owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name
            : owner.Sections.OfType<TableCaption>().FirstOrDefault()?.Content?.ToString();
    }

    /// <summary>
    /// 网格：所有单元格是它的可视子元素（逻辑上仍属于各自的行）。列宽按内容分配，行高按各格的高度；行底色、分隔线、指针下的行在这里画。
    /// </summary>
    sealed class TableGrid(Table owner) : Control
    {
        readonly List<(TableSection Section, TableRow Row, TableCell[] Cells)> rows = [];
        double[] columns = [];
        double[] heights = [];
        int hovered = -1;

        /// <summary>表格的内容宽度（Table 量的时候交过来）；不知道时按各列的内容宽。</summary>
        public double Width_ { get; set; } = double.PositiveInfinity;

        public void Rebuild()
        {
            foreach (var cell in rows.SelectMany(row => row.Cells)) VisualChildren.Remove(cell);
            rows.Clear();
            var bodyIndex = 0;
            foreach (var (section, row) in owner.Rows)
            {
                var cells = row.Cells.OfType<TableCell>().ToArray();
                rows.Add((section, row, cells));
                var striped = owner.Striped && section is TableBody && bodyIndex++ % 2 == 1;
                foreach (var cell in cells)
                {
                    cell.Apply(section, owner.Density);
                    VisualChildren.Add(cell);
                }
                row.IsStriped = striped;
            }
            InvalidateMeasure();
            InvalidateVisual();
        }

        Thickness Padding(TableCell cell)
        {
            var em = TextElement.GetFontSize(cell);
            var block = owner.Density == TableDensity.Compact ? 0.25 : owner.Density == TableDensity.Comfortable ? 0.75 : 0.5;
            return new Thickness(0.75 * em, block * em);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var count = rows.Count == 0 ? 0 : rows.Max(row => row.Cells.Length);
            var max = new double[count];
            var min = new double[count];
            var fixedWidths = new double?[count];
            foreach (var (_, _, cells) in rows)
                for (var i = 0; i < cells.Length; i++)
                {
                    var cell = cells[i];
                    var padding = Padding(cell).Left * 2;
                    cell.Measure(Size.Infinity);
                    max[i] = Math.Max(max[i], cell.DesiredSize.Width + padding);
                    if (cell.Nowrap || cell is TableHeaderCell { Section: TableHead }) min[i] = Math.Max(min[i], cell.DesiredSize.Width + padding);
                    else
                    {
                        cell.Measure(new Size(0, double.PositiveInfinity));
                        min[i] = Math.Max(min[i], cell.DesiredSize.Width + padding);
                    }
                    if (cell is TableHeaderCell { ColumnWidth: { } width }) fixedWidths[i] = width;
                }
            for (var i = 0; i < count; i++)
                if (fixedWidths[i] is { } width) max[i] = min[i] = width;
            // CSS 的 table-layout: auto（表格宽 100%）：放得下时按各列内容的比例分多出来的宽度；放不下时按最小宽度插值；再放不下时最小宽度、横向滚动
            var available = !double.IsInfinity(Width_) ? Width_ : double.IsInfinity(availableSize.Width) ? max.Sum() : availableSize.Width;
            var (sumMax, sumMin) = (max.Sum(), min.Sum());
            columns = new double[count];
            for (var i = 0; i < count; i++)
                columns[i] = sumMax <= available ? max[i] + (sumMax > 0 ? (available - sumMax) * max[i] / sumMax : 0)
                    : sumMin <= available ? min[i] + (available - sumMin) * (max[i] - min[i]) / Math.Max(1, sumMax - sumMin)
                    : min[i];
            heights = new double[rows.Count];
            for (var r = 0; r < rows.Count; r++)
            {
                var cells = rows[r].Cells;
                for (var i = 0; i < cells.Length; i++)
                {
                    var padding = Padding(cells[i]);
                    cells[i].Measure(new Size(Math.Max(0, columns[i] - padding.Left - padding.Right), double.PositiveInfinity));
                    heights[r] = Math.Max(heights[r], cells[i].DesiredSize.Height + padding.Top + padding.Bottom + 1);
                }
            }
            return new Size(columns.Sum(), heights.Sum());
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var y = 0d;
            for (var r = 0; r < rows.Count; r++)
            {
                var x = 0d;
                var cells = rows[r].Cells;
                var top = rows[r].Section is TableFoot ? 1 : 0;
                for (var i = 0; i < cells.Length; i++)
                {
                    var padding = Padding(cells[i]);
                    cells[i].Arrange(new Rect(x + padding.Left, y + padding.Top + top, Math.Max(0, columns[i] - padding.Left - padding.Right),
                        Math.Max(0, heights[r] - padding.Top - padding.Bottom - 1)));
                    x += columns[i];
                }
                y += heights[r];
            }
            return finalSize;
        }

        /// <summary>行底色（表头 fill；表体 surface 或页面底色，斑马纹 fill，指针下的行 fillHover）与分隔线（表头下 border，表体行下 borderMuted，表尾上 border）。</summary>
        public override void Render(DrawingContext context)
        {
            var width = Bounds.Width;
            var y = 0d;
            IBrush? Brush(string key) => owner.Resource<IBrush?>(key, null);
            var rowBrush = Brush(owner.Variant == TableVariant.Ghost ? "Rune.Brush.Background" : "Rune.Brush.Surface");
            for (var r = 0; r < rows.Count && r < heights.Length; r++)
            {
                var (section, row, _) = rows[r];
                var height = heights[r];
                var background = section is TableHead ? Brush("Rune.Brush.Fill")
                    : r == hovered && section is TableBody ? Brush("Rune.Brush.FillHover")
                    : row.IsStriped ? Brush("Rune.Brush.Fill") : rowBrush;
                if (background is not null) context.FillRectangle(background, new Rect(0, y, width, height));
                if (section is TableFoot)
                {
                    if (Brush("Rune.Brush.Border") is { } line) context.FillRectangle(line, new Rect(0, y, width, 1));
                }
                else if (Brush(section is TableHead ? "Rune.Brush.Border" : "Rune.Brush.BorderMuted") is { } line)
                    context.FillRectangle(line, new Rect(0, y + height - 1, width, 1));
                y += height;
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var y = e.GetPosition(this).Y;
            var index = -1;
            for (double top = 0, r = 0; r < heights.Length; top += heights[(int)r], r++)
                if (y >= top && y < top + heights[(int)r]) index = (int)r;
            if (index == hovered) return;
            hovered = index;
            InvalidateVisual();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            hovered = -1;
            InvalidateVisual();
        }
    }
}

/// <summary>只挂可视子元素的竖排容器：子元素的逻辑父元素在别处（表格的标题）。</summary>
sealed class VisualStack : Control
{
    public void Add(Control child) => VisualChildren.Add(child);

    public void Insert(int index, Control child) => VisualChildren.Insert(index, child);

    public void Remove(Control child) => VisualChildren.Remove(child);

    protected override Size MeasureOverride(Size availableSize)
    {
        var (width, height) = (0d, 0d);
        foreach (var child in VisualChildren.OfType<Control>())
        {
            child.Measure(availableSize.WithHeight(double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0d;
        foreach (var child in VisualChildren.OfType<Control>())
        {
            child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
            y += child.DesiredSize.Height;
        }
        return finalSize;
    }
}

/// <summary>表格标题（caption）：同时是表格的读屏名称；在表格上方，textMuted、sm，上下 space.sm、左右 space.md。</summary>
public class TableCaption : ContentControl
{
    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TableCaption);
}

/// <summary>表格的一个部分：表头、表体、表尾里放行。</summary>
public abstract class TableSection : Control
{
    /// <summary>新建：行变了时通知所在的表格。</summary>
    protected TableSection() => Rows.CollectionChanged += (_, e) =>
    {
        foreach (var item in e.OldItems?.OfType<Control>() ?? []) LogicalChildren.Remove(item);
        foreach (var item in e.NewItems?.OfType<Control>() ?? []) LogicalChildren.Add(item);
        (Parent as Table)?.Sync();
    };

    /// <summary>行。</summary>
    [Content]
    public Controls Rows { get; } = [];

    /// <summary>在表格里的先后：表头、表体、表尾。</summary>
    internal abstract int Order { get; }
}

/// <summary>表头（thead）：列标题。</summary>
public class TableHead : TableSection
{
    internal override int Order => 0;
}

/// <summary>表体（tbody）。</summary>
public class TableBody : TableSection
{
    internal override int Order => 1;
}

/// <summary>表尾（tfoot）：合计行。</summary>
public class TableFoot : TableSection
{
    internal override int Order => 2;
}

/// <summary>行（tr）：底色随表格的样式，斑马纹时表体隔行浅色底。</summary>
public class TableRow : Control
{
    /// <summary>新建：单元格变了时通知所在的表格。</summary>
    public TableRow() => Cells.CollectionChanged += (_, e) =>
    {
        foreach (var item in e.OldItems?.OfType<Control>() ?? []) LogicalChildren.Remove(item);
        foreach (var item in e.NewItems?.OfType<Control>() ?? []) LogicalChildren.Add(item);
        (Parent?.Parent as Table)?.Sync();
    };

    /// <summary>单元格。</summary>
    [Content]
    public Controls Cells { get; } = [];

    /// <summary>斑马纹的浅色行（测试用）。</summary>
    internal bool IsStriped { get; set; }
}

/// <summary>单元格（td）：左右 space.md、上下按密度；对齐按 <see cref="Align"/>；默认可以换行，<see cref="Nowrap"/> 时一行（放不下省略号）。</summary>
public class TableCell : ContentControl
{
    /// <summary>对齐，默认 start；数字、金额用 end。</summary>
    public static readonly StyledProperty<TableAlign> AlignProperty = AvaloniaProperty.Register<TableCell, TableAlign>(nameof(Align), TableAlign.Start);

    /// <summary>不换行（超出时省略号）。</summary>
    public static readonly StyledProperty<bool> NowrapProperty = AvaloniaProperty.Register<TableCell, bool>(nameof(Nowrap));

    /// <summary>对齐。</summary>
    public TableAlign Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>不换行。</summary>
    public bool Nowrap { get => GetValue(NowrapProperty); set => SetValue(NowrapProperty, value); }

    /// <summary>所在的部分（表头、表体、表尾）。</summary>
    internal TableSection? Section { get; private set; }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TableCell);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AlignProperty)
            HorizontalContentAlignment = Align == TableAlign.Center ? HorizontalAlignment.Center : Align == TableAlign.End ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        else if (change.Property == NowrapProperty) TextBlock.SetTextWrapping(this, Nowrap ? TextWrapping.NoWrap : TextWrapping.Wrap);
    }

    /// <summary>按所在的部分设外观（伪类 :head、:foot、:row-header）。</summary>
    internal void Apply(TableSection section, TableDensity density)
    {
        Section = section;
        PseudoClasses.Set(":head", section is TableHead);
        PseudoClasses.Set(":foot", section is TableFoot);
        PseudoClasses.Set(":row-header", this is TableHeaderCell && section is not TableHead);
        if (!IsSet(NowrapProperty)) TextBlock.SetTextWrapping(this, this is TableHeaderCell && section is TableHead ? TextWrapping.NoWrap : TextWrapping.Wrap);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CellPeer(this);

    sealed class CellPeer(TableCell owner) : ContentControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            owner is TableHeaderCell ? AutomationControlType.HeaderItem : AutomationControlType.DataItem;
    }
}

/// <summary>表头单元格（th）：表头里是列标题（fill 底、textMuted、sm、中等字重、不换行），表体与表尾里是行标题（中等字重）。</summary>
public class TableHeaderCell : TableCell
{
    /// <summary>列宽（像素）：日期、数字、操作这类按内容定宽的列；不写的列分剩下的（TS 的 width：控件自己有 Width）。</summary>
    public static readonly StyledProperty<double?> ColumnWidthProperty = AvaloniaProperty.Register<TableHeaderCell, double?>(nameof(ColumnWidth));

    /// <summary>表头单元格对应的方向（TS 的 scope）：表头里是列标题，表体与表尾里写 row 是行标题；不写时按所在的部分。</summary>
    public static readonly StyledProperty<string?> ScopeProperty = AvaloniaProperty.Register<TableHeaderCell, string?>(nameof(Scope));

    /// <summary>列宽（像素）。</summary>
    public double? ColumnWidth { get => GetValue(ColumnWidthProperty); set => SetValue(ColumnWidthProperty, value); }

    /// <summary>对应的方向：col（列标题）或 row（行标题）。</summary>
    public string? Scope { get => GetValue(ScopeProperty); set => SetValue(ScopeProperty, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TableHeaderCell);
}
