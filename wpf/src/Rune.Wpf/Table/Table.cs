using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 静态表格（vue-tsx-pro 的 Table）：表格的各个部分各一个元素，内容直接写在 XAML 里（说明文字里的表格、属性表）。
/// <code>
/// &lt;rune:Table AutomationProperties.Name="属性"&gt;
///   &lt;rune:TableHead&gt;&lt;rune:TableRow&gt;&lt;rune:TableHeaderCell&gt;名称&lt;/rune:TableHeaderCell&gt;…&lt;/rune:TableRow&gt;&lt;/rune:TableHead&gt;
///   &lt;rune:TableBody&gt;&lt;rune:TableRow&gt;&lt;rune:TableCell&gt;size&lt;/rune:TableCell&gt;…&lt;/rune:TableRow&gt;&lt;/rune:TableBody&gt;
/// &lt;/rune:Table&gt;
/// </code>
/// 外观：<see cref="Density"/>、<see cref="Variant"/>（surface / ghost）、<see cref="Striped"/>。列宽按内容分配（同 CSS 的 table-layout: auto），
/// 放不下时单元格换行，再放不下时横向滚动；表头单元格不换行，<see cref="TableHeaderCell.ColumnWidth"/> 定宽。指针下的行 fillHover 底。
/// 行与单元格逻辑上属于各自的部分与行，可视上由表格里的网格排版、画行底色与分隔线。
/// </summary>
[ContentProperty(nameof(Sections))]
public class Table : FrameworkElement
{
    /// <summary>密度，默认 standard。</summary>
    public static readonly DependencyProperty DensityProperty = DependencyProperty.Register(nameof(Density), typeof(TableDensity), typeof(Table),
        new FrameworkPropertyMetadata(TableDensity.Standard, (d, _) => ((Table)d).Sync()));

    /// <summary>样式，默认 surface。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(TableVariant), typeof(Table),
        new FrameworkPropertyMetadata(TableVariant.Surface, (d, _) => ((Table)d).Sync()));

    /// <summary>斑马纹：表体的偶数行 fill 底。</summary>
    public static readonly DependencyProperty StripedProperty = DependencyProperty.Register(nameof(Striped), typeof(bool), typeof(Table),
        new FrameworkPropertyMetadata(false, (d, _) => ((Table)d).Sync()));

    readonly Border root = new() { ClipToBounds = true };
    readonly VisualStack stack = new();
    readonly ScrollArea scroll = new() { Axis = ScrollAreaAxis.X };
    readonly TableGrid grid;
    TableCaption? caption;

    /// <summary>新建：外框里是标题与可横向滚动的网格。</summary>
    public Table()
    {
        grid = new TableGrid(this);
        scroll.Content = grid;
        stack.Add(scroll);
        root.Child = stack;
        AddVisualChild(root);
        AddLogicalChild(root);
        Sections.CollectionChanged += (_, e) =>
        {
            foreach (var item in e.OldItems?.OfType<FrameworkElement>() ?? []) RemoveLogicalChild(item);
            foreach (var item in e.NewItems?.OfType<FrameworkElement>() ?? []) AddLogicalChild(item);
            Sync();
        };
        Loaded += (_, _) => Sync();
    }

    /// <summary>表格的各个部分：TableCaption、TableHead、TableBody、TableFoot。</summary>
    public ObservableCollection<FrameworkElement> Sections { get; } = [];

    /// <summary>密度。</summary>
    public TableDensity Density { get => (TableDensity)GetValue(DensityProperty); set => SetValue(DensityProperty, value); }

    /// <summary>样式。</summary>
    public TableVariant Variant { get => (TableVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>斑马纹。</summary>
    public bool Striped { get => (bool)GetValue(StripedProperty); set => SetValue(StripedProperty, value); }

    /// <summary>所有行，按表头、表体、表尾的顺序（测试用）。</summary>
    internal IReadOnlyList<(TableSection Section, TableRow Row)> Rows =>
        [.. Sections.OfType<TableSection>().OrderBy(section => section.Order).SelectMany(section => section.Rows.OfType<TableRow>().Select(row => (section, row)))];

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => Sections.Cast<object>().Prepend(root).GetEnumerator();

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TextElement.FontSizeProperty) ApplyRoot();
    }

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <summary>行、单元格变了：重新收集网格里的格，标题放到网格上方，外观交给它们。</summary>
    internal void Sync()
    {
        var next = Sections.OfType<TableCaption>().FirstOrDefault();
        if (next != caption)
        {
            if (caption is not null) stack.Remove(caption);
            caption = next;
            if (caption is not null) stack.Insert(0, caption);
        }
        if (Density == TableDensity.Compact) SetResourceReference(TextElement.FontSizeProperty, "Rune.FontSize.Sm");
        else ClearValue(TextElement.FontSizeProperty);
        ApplyRoot();
        grid.Rebuild();
    }

    /// <summary>外框：surface 时 border 边框、lg 圆角、surface 底；ghost 时页面底色、没有边框。</summary>
    void ApplyRoot()
    {
        var ghost = Variant == TableVariant.Ghost;
        root.BorderThickness = ghost ? default : new Thickness(1);
        root.SetResourceReference(Border.BorderBrushProperty, "Rune.Brush.Border");
        root.SetResourceReference(Border.BackgroundProperty, ghost ? "Rune.Brush.Background" : "Rune.Brush.Surface");
        root.CornerRadius = ghost ? default : new CornerRadius((TryFindResource("Rune.Radius.Lg") as double? ?? 0.5) * TextElement.GetFontSize(this));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 表格宽 100%：横向滚动区域给内容的是无限宽，列宽按表格自己的宽度（减去外框）分配
        var width = availableSize.Width - root.BorderThickness.Left - root.BorderThickness.Right;
        if (!double.IsInfinity(width) && Math.Abs(width - grid.TableWidth) > 0.5)
        {
            grid.TableWidth = width;
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
    protected override AutomationPeer OnCreateAutomationPeer() => new TablePeer(this);

    sealed class TablePeer(Table owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

        protected override string GetClassNameCore() => nameof(Table);

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name
            : owner.Sections.OfType<TableCaption>().FirstOrDefault()?.Content?.ToString() ?? "";
    }

    /// <summary>只挂可视子元素的竖排容器：子元素的逻辑父元素在别处（表格的标题、滚动区域）。</summary>
    sealed class VisualStack : FrameworkElement
    {
        readonly List<UIElement> children = [];

        public void Add(UIElement child) => Insert(children.Count, child);

        public void Insert(int index, UIElement child)
        {
            children.Insert(index, child);
            AddVisualChild(child);
            InvalidateMeasure();
        }

        public void Remove(UIElement child)
        {
            if (!children.Remove(child)) return;
            RemoveVisualChild(child);
            InvalidateMeasure();
        }

        protected override int VisualChildrenCount => children.Count;

        protected override Visual GetVisualChild(int index) => children[index];

        protected override Size MeasureOverride(Size availableSize)
        {
            var (width, height) = (0d, 0d);
            foreach (var child in children)
            {
                child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
                width = Math.Max(width, child.DesiredSize.Width);
                height += child.DesiredSize.Height;
            }
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var y = 0d;
            foreach (var child in children)
            {
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height;
            }
            return finalSize;
        }
    }

    /// <summary>网格：所有单元格是它的可视子元素（逻辑上仍属于各自的行）。列宽按内容分配，行高按各格；行底色、分隔线、指针下的行在这里画。</summary>
    sealed class TableGrid(Table owner) : FrameworkElement
    {
        readonly List<(TableSection Section, TableRow Row, TableCell[] Cells)> rows = [];
        readonly List<TableCell> cells = [];
        double[] columns = [];
        double[] heights = [];
        int hovered = -1;

        /// <summary>表格的内容宽度（Table 量的时候交过来）。</summary>
        public double TableWidth { get; set; } = double.PositiveInfinity;

        public void Rebuild()
        {
            foreach (var cell in cells) RemoveVisualChild(cell);
            cells.Clear();
            rows.Clear();
            var bodyIndex = 0;
            foreach (var (section, row) in owner.Rows)
            {
                var items = row.Cells.OfType<TableCell>().ToArray();
                rows.Add((section, row, items));
                row.IsStriped = owner.Striped && section is TableBody && bodyIndex++ % 2 == 1;
                foreach (var cell in items)
                {
                    cell.Apply(section);
                    cells.Add(cell);
                    AddVisualChild(cell);
                }
            }
            InvalidateMeasure();
            InvalidateVisual();
        }

        protected override int VisualChildrenCount => cells.Count;

        protected override Visual GetVisualChild(int index) => cells[index];

        Thickness Padding(TableCell cell)
        {
            var em = TextElement.GetFontSize(cell);
            var block = owner.Density == TableDensity.Compact ? 0.25 : owner.Density == TableDensity.Comfortable ? 0.75 : 0.5;
            return new Thickness(0.75 * em, block * em, 0.75 * em, block * em);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var count = rows.Count == 0 ? 0 : rows.Max(row => row.Cells.Length);
            var max = new double[count];
            var min = new double[count];
            var fixedWidths = new double?[count];
            foreach (var (section, _, items) in rows)
                for (var i = 0; i < items.Length; i++)
                {
                    var cell = items[i];
                    var padding = Padding(cell).Left * 2;
                    cell.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    max[i] = Math.Max(max[i], cell.DesiredSize.Width + padding);
                    if (cell.Nowrap || (cell is TableHeaderCell && section is TableHead)) min[i] = Math.Max(min[i], cell.DesiredSize.Width + padding);
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
            var available = !double.IsInfinity(TableWidth) ? TableWidth : double.IsInfinity(availableSize.Width) ? max.Sum() : availableSize.Width;
            var (sumMax, sumMin) = (max.Sum(), min.Sum());
            columns = new double[count];
            for (var i = 0; i < count; i++)
                columns[i] = sumMax <= available ? max[i] + (sumMax > 0 ? (available - sumMax) * max[i] / sumMax : 0)
                    : sumMin <= available ? min[i] + (available - sumMin) * (max[i] - min[i]) / Math.Max(1, sumMax - sumMin)
                    : min[i];
            heights = new double[rows.Count];
            for (var r = 0; r < rows.Count; r++)
            {
                var items = rows[r].Cells;
                for (var i = 0; i < items.Length; i++)
                {
                    var padding = Padding(items[i]);
                    items[i].Measure(new Size(Math.Max(0, columns[i] - padding.Left - padding.Right), double.PositiveInfinity));
                    heights[r] = Math.Max(heights[r], items[i].DesiredSize.Height + padding.Top + padding.Bottom + 1);
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
                var items = rows[r].Cells;
                var top = rows[r].Section is TableFoot ? 1 : 0;
                for (var i = 0; i < items.Length; i++)
                {
                    var padding = Padding(items[i]);
                    items[i].Arrange(new Rect(x + padding.Left, y + padding.Top + top, Math.Max(0, columns[i] - padding.Left - padding.Right),
                        Math.Max(0, heights[r] - padding.Top - padding.Bottom - 1)));
                    x += columns[i];
                }
                y += heights[r];
            }
            return finalSize;
        }

        /// <summary>行底色（表头 fill；表体 surface 或页面底色，斑马纹 fill，指针下的行 fillHover）与分隔线（表头下 border，表体行下 borderMuted，表尾上 border）。</summary>
        protected override void OnRender(DrawingContext context)
        {
            var width = ActualWidth;
            var y = 0d;
            Brush? Brush(string key) => owner.TryFindResource(key) as Brush;
            var rowBrush = Brush(owner.Variant == TableVariant.Ghost ? "Rune.Brush.Background" : "Rune.Brush.Surface");
            // 行的上下边吸附到设备像素（同浏览器画表格）：行高带小数时分隔线不发虚
            var guidelines = new GuidelineSet();
            foreach (var height in heights)
            {
                guidelines.GuidelinesY.Add(y);
                guidelines.GuidelinesY.Add(y + height - 1);
                y += height;
            }
            guidelines.GuidelinesY.Add(y);
            context.PushGuidelineSet(guidelines);
            y = 0;
            for (var r = 0; r < rows.Count && r < heights.Length; r++)
            {
                var (section, row, _) = rows[r];
                var height = heights[r];
                var background = section is TableHead ? Brush("Rune.Brush.Fill")
                    : r == hovered && section is TableBody ? Brush("Rune.Brush.FillHover")
                    : row.IsStriped ? Brush("Rune.Brush.Fill") : rowBrush;
                if (background is not null) context.DrawRectangle(background, null, new Rect(0, y, width, height));
                if (section is TableFoot)
                {
                    if (Brush("Rune.Brush.Border") is { } line) context.DrawRectangle(line, null, new Rect(0, y, width, 1));
                }
                else if (Brush(section is TableHead ? "Rune.Brush.Border" : "Rune.Brush.BorderMuted") is { } line)
                    context.DrawRectangle(line, null, new Rect(0, y + height - 1, width, 1));
                y += height;
            }
            context.Pop();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var y = e.GetPosition(this).Y;
            var index = -1;
            var top = 0d;
            for (var r = 0; r < heights.Length; top += heights[r], r++)
                if (y >= top && y < top + heights[r]) index = r;
            if (index == hovered) return;
            hovered = index;
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = -1;
            InvalidateVisual();
        }

        // 只画背景：命中测试交给单元格，空白处也要能收到指针移动
        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) =>
            new Rect(RenderSize).Contains(hitTestParameters.HitPoint) ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null;
    }
}

/// <summary>表格标题（caption）：同时是表格的读屏名称；在表格上方，textMuted、sm，上下 space.sm、左右 space.md。</summary>
public class TableCaption : ContentControl
{
    static TableCaption()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TableCaption), new FrameworkPropertyMetadata(typeof(TableCaption)));
        EmSize.Register(typeof(TableCaption));
    }
}

/// <summary>表格的一个部分：表头、表体、表尾里放行。</summary>
[ContentProperty(nameof(Rows))]
public abstract class TableSection : FrameworkElement
{
    /// <summary>新建：行变了时通知所在的表格。</summary>
    protected TableSection() => Rows.CollectionChanged += (_, e) =>
    {
        foreach (var item in e.OldItems?.OfType<FrameworkElement>() ?? []) RemoveLogicalChild(item);
        foreach (var item in e.NewItems?.OfType<FrameworkElement>() ?? []) AddLogicalChild(item);
        (Parent as Table)?.Sync();
    };

    /// <summary>行。</summary>
    public ObservableCollection<FrameworkElement> Rows { get; } = [];

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => Rows.GetEnumerator();

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
[ContentProperty(nameof(Cells))]
public class TableRow : FrameworkElement
{
    /// <summary>新建：单元格变了时通知所在的表格。</summary>
    public TableRow() => Cells.CollectionChanged += (_, e) =>
    {
        foreach (var item in e.OldItems?.OfType<FrameworkElement>() ?? []) RemoveLogicalChild(item);
        foreach (var item in e.NewItems?.OfType<FrameworkElement>() ?? []) AddLogicalChild(item);
        ((Parent as FrameworkElement)?.Parent as Table)?.Sync();
    };

    /// <summary>单元格。</summary>
    public ObservableCollection<FrameworkElement> Cells { get; } = [];

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => Cells.GetEnumerator();

    /// <summary>斑马纹的浅色行（测试用）。</summary>
    internal bool IsStriped { get; set; }
}

/// <summary>单元格（td）：左右 space.md、上下按密度；对齐按 <see cref="Align"/>；默认可以换行，<see cref="Nowrap"/> 时一行。</summary>
public class TableCell : ContentControl
{
    /// <summary>对齐，默认 start；数字、金额用 end。</summary>
    public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(nameof(Align), typeof(TableAlign), typeof(TableCell),
        new FrameworkPropertyMetadata(TableAlign.Start, (d, _) => ((TableCell)d).ApplyAlign()));

    /// <summary>不换行（超出时省略号）。</summary>
    public static readonly DependencyProperty NowrapProperty = DependencyProperty.Register(nameof(Nowrap), typeof(bool), typeof(TableCell));

    static readonly DependencyPropertyKey SectionKindKey = DependencyProperty.RegisterReadOnly(nameof(SectionKind), typeof(string), typeof(TableCell), new PropertyMetadata("body"));

    /// <summary>所在的部分：head、body、foot（主题的触发器按它切换外观）。</summary>
    public static readonly DependencyProperty SectionKindProperty = SectionKindKey.DependencyProperty;

    static TableCell()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TableCell), new FrameworkPropertyMetadata(typeof(TableCell)));
        EmSize.Register(typeof(TableCell));
    }

    /// <summary>对齐。</summary>
    public TableAlign Align { get => (TableAlign)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>不换行。</summary>
    public bool Nowrap { get => (bool)GetValue(NowrapProperty); set => SetValue(NowrapProperty, value); }

    /// <summary>所在的部分。</summary>
    public string SectionKind => (string)GetValue(SectionKindProperty);

    void ApplyAlign() => HorizontalContentAlignment = Align == TableAlign.Center ? HorizontalAlignment.Center : Align == TableAlign.End ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    /// <summary>按所在的部分设外观；表头的格不换行。</summary>
    internal void Apply(TableSection section)
    {
        SetValue(SectionKindKey, section is TableHead ? "head" : section is TableFoot ? "foot" : "body");
        var wrap = !Nowrap && !(this is TableHeaderCell && section is TableHead);
        Resources[typeof(TextBlock)] = wrap ? WrapStyle : NoWrapStyle;
    }

    static readonly Style WrapStyle = new(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap) } };

    static readonly Style NoWrapStyle = new(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis) } };

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CellPeer(this);

    sealed class CellPeer(TableCell owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner is TableHeaderCell ? AutomationControlType.HeaderItem : AutomationControlType.DataItem;

        protected override string GetClassNameCore() => owner.GetType().Name;

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Content as string ?? "";
    }
}

/// <summary>表头单元格（th）：表头里是列标题（fill 底、textMuted、sm、中等字重、不换行），表体与表尾里是行标题（中等字重）。</summary>
public class TableHeaderCell : TableCell
{
    /// <summary>列宽（像素）：日期、数字、操作这类按内容定宽的列；不写的列分剩下的（TS 的 width：元素自己有 Width）。</summary>
    public static readonly DependencyProperty ColumnWidthProperty = DependencyProperty.Register(nameof(ColumnWidth), typeof(double?), typeof(TableHeaderCell));

    /// <summary>对应的方向（TS 的 scope）：col（列标题）或 row（行标题）；不写时按所在的部分。</summary>
    public static readonly DependencyProperty ScopeProperty = DependencyProperty.Register(nameof(Scope), typeof(string), typeof(TableHeaderCell));

    static TableHeaderCell() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TableHeaderCell), new FrameworkPropertyMetadata(typeof(TableHeaderCell)));

    /// <summary>列宽（像素）。</summary>
    public double? ColumnWidth { get => (double?)GetValue(ColumnWidthProperty); set => SetValue(ColumnWidthProperty, value); }

    /// <summary>对应的方向。</summary>
    public string? Scope { get => (string?)GetValue(ScopeProperty); set => SetValue(ScopeProperty, value); }
}
