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
/// 外层限了高度时 <see cref="StickyHeader"/> 让表头吸顶（<see cref="ScrollAxis"/> 写 Both 或 Y 才能竖着滚），单元格的 <see cref="TableCell.Sticky"/>
/// 把那一列固定在起始边（常是行标题），结束边一道 borderMuted 的竖线；固定的格用行的底色盖住滚到下面的内容。
/// </summary>
public class Table : Control
{
    /// <summary>密度，默认 standard。</summary>
    public static readonly StyledProperty<TableDensity> DensityProperty = AvaloniaProperty.Register<Table, TableDensity>(nameof(Density), TableDensity.Standard);

    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<TableVariant> VariantProperty = AvaloniaProperty.Register<Table, TableVariant>(nameof(Variant), TableVariant.Surface);

    /// <summary>斑马纹：表体的偶数行 fill 底。</summary>
    public static readonly StyledProperty<bool> StripedProperty = AvaloniaProperty.Register<Table, bool>(nameof(Striped));

    /// <summary>表头吸顶：表格自己竖着滚动时（限了高度、<see cref="ScrollAxis"/> 能竖着滚）表头一直在顶上。</summary>
    public static readonly StyledProperty<bool> StickyHeaderProperty = AvaloniaProperty.Register<Table, bool>(nameof(StickyHeader));

    /// <summary>表格自己的滚动方向（TS 的 propsRoot 的 axis），默认横向；限了高度的明细表写 Both。</summary>
    public static readonly StyledProperty<ScrollAreaAxis> ScrollAxisProperty = AvaloniaProperty.Register<Table, ScrollAreaAxis>(nameof(ScrollAxis), ScrollAreaAxis.X);

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
        // 固定的表头、列跟着滚动的位移走（网格就是滚动区域的内容，位移就是滚动的偏移）
        scroll.ScrollChanged += (_, _) => grid.ApplyStickyOffsets();
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

    /// <summary>表头吸顶。</summary>
    public bool StickyHeader { get => GetValue(StickyHeaderProperty); set => SetValue(StickyHeaderProperty, value); }

    /// <summary>表格自己的滚动方向。</summary>
    public ScrollAreaAxis ScrollAxis { get => GetValue(ScrollAxisProperty); set => SetValue(ScrollAxisProperty, value); }

    /// <summary>固定的格、表头变了：重新排版（单元格的 Sticky 变了时调用）。</summary>
    internal void InvalidateGrid() => grid.InvalidateMeasure();

    /// <summary>格的底色变了：重画行与格的底色。</summary>
    internal void InvalidateGridVisual() => grid.InvalidateRows();

    /// <summary>所有行，按表头、表体、表尾的顺序（测试用）。</summary>
    internal IReadOnlyList<(TableSection Section, TableRow Row)> SectionRows =>
        [.. Sections.OfType<TableSection>().OrderBy(section => section.Order).SelectMany(section => section.Rows.OfType<TableRow>().Select(row => (section, row)))];

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DensityProperty || change.Property == VariantProperty || change.Property == StripedProperty) Sync();
        else if (change.Property == StickyHeaderProperty) grid.InvalidateArrange();
        else if (change.Property == ScrollAxisProperty) scroll.Axis = ScrollAxis;
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
        if (compact) Bind(TextElement.FontSizeProperty, this.GetResourceObservable(ThemeKeys.FontSize.Sm));
        else ClearValue(TextElement.FontSizeProperty);
        ApplyRoot();
        grid.Rebuild();
    }

    /// <summary>外框：surface 时 border 边框、lg 圆角、surface 底；ghost 时页面底色、没有边框。</summary>
    void ApplyRoot()
    {
        var ghost = Variant == TableVariant.Ghost;
        root.BorderThickness = ghost ? default : new Thickness(1);
        root.Bind(Border.BorderBrushProperty, this.GetResourceObservable(ThemeKeys.Brush.Border));
        root.Bind(Border.BackgroundProperty, this.GetResourceObservable(ghost ? ThemeKeys.Brush.Background : ThemeKeys.Brush.Surface));
        root.CornerRadius = ghost ? default : new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Lg) * TextElement.GetFontSize(this));
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
    sealed class TableGrid : Control
    {
        readonly Table owner;
        readonly List<(TableSection Section, TableRow Row, TableCell[] Cells)> rows = [];
        // 各格占的位置（同 HTML 表格：跨行的格占住下面几行的那几列，后面的格往后排）
        readonly List<(TableCell Cell, Slot Slot)> placed = [];
        // 固定的表头、固定的列、两者相交的角各一层，画行的底色盖住滚到下面的格（CSS 里 position: sticky 的格继承行的底色）
        readonly Backdrop headBackdrop, sideBackdrop, cornerBackdrop;
        double[] columns = [];
        double[] heights = [];
        bool[] stickyColumns = [];
        int hovered = -1;
        double shiftY;

        public TableGrid(Table owner)
        {
            this.owner = owner;
            headBackdrop = new Backdrop(this, head: true, edge: false) { ZIndex = 3 };
            sideBackdrop = new Backdrop(this, head: false, edge: true) { ZIndex = 1 };
            cornerBackdrop = new Backdrop(this, head: true, edge: true) { ZIndex = 5 };
            VisualChildren.AddRange([headBackdrop, sideBackdrop, cornerBackdrop]);
        }

        /// <summary>表格的内容宽度（Table 量的时候交过来）；不知道时按各列的内容宽。</summary>
        public double Width_ { get; set; } = double.PositiveInfinity;

        /// <summary>表头的高度（表头各行之和）。</summary>
        double HeadHeight => rows.Select((row, index) => row.Section is TableHead && index < heights.Length ? heights[index] : 0).Sum();

        /// <summary>固定的列：起始的 x 与总宽（有格写了 Sticky 的列）。</summary>
        (double Left, double Width) StickySpan
        {
            get
            {
                var (left, width, x) = (double.NaN, 0d, 0d);
                for (var i = 0; i < stickyColumns.Length && i < columns.Length; i++)
                {
                    if (stickyColumns[i])
                    {
                        if (double.IsNaN(left)) left = x;
                        width += columns[i];
                    }
                    x += columns[i];
                }
                return (double.IsNaN(left) ? 0 : left, width);
            }
        }

        public void Rebuild()
        {
            foreach (var cell in rows.SelectMany(row => row.Cells)) VisualChildren.Remove(cell);
            rows.Clear();
            var bodyIndex = 0;
            foreach (var (section, row) in owner.SectionRows)
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
            InvalidateRows();
        }

        /// <summary>单元格的内边距：左右 space.md、上下按密度，按格自己的字号。</summary>
        Thickness Padding(TableCell cell)
        {
            var em = TextElement.GetFontSize(cell);
            var block = owner.Density == TableDensity.Compact ? 0.25 : owner.Density == TableDensity.Comfortable ? 0.75 : 0.5;
            return new Thickness(0.75 * em, block * em);
        }

        /// <summary>一格占的位置：第几行、第几列起，跨几行、几列。</summary>
        readonly record struct Slot(int Row, int Column, int RowSpan, int ColumnSpan);

        /// <summary>按 HTML 表格的规则排各格：每格放在本行第一个没被上面跨行的格占住的列；跨行不超出所在的部分（表头、表体、表尾）。</summary>
        void Place()
        {
            placed.Clear();
            var sectionEnd = new int[rows.Count];
            for (var r = rows.Count - 1; r >= 0; r--)
                sectionEnd[r] = r + 1 < rows.Count && rows[r + 1].Section == rows[r].Section ? sectionEnd[r + 1] : r;
            var occupied = new HashSet<(int Row, int Column)>();
            for (var r = 0; r < rows.Count; r++)
            {
                var column = 0;
                foreach (var cell in rows[r].Cells)
                {
                    while (occupied.Contains((r, column))) column++;
                    var slot = new Slot(r, column, Math.Clamp(cell.RowSpan, 1, sectionEnd[r] - r + 1), Math.Max(1, cell.ColumnSpan));
                    for (var dr = 0; dr < slot.RowSpan; dr++)
                        for (var dc = 0; dc < slot.ColumnSpan; dc++) occupied.Add((r + dr, column + dc));
                    placed.Add((cell, slot));
                    column += slot.ColumnSpan;
                }
            }
        }

        /// <summary>跨几列（或几行）的格要的尺寸比那几列（行）合起来的大时，差的平分给它们（HTML 表格分得更细，静态表格够用）。</summary>
        static void Spread(double[] sizes, int from, int span, double need)
        {
            var have = Sum(sizes, from, span);
            if (need <= have) return;
            for (var i = from; i < from + span; i++) sizes[i] += (need - have) / span;
        }

        /// <summary>从 from 起 span 个尺寸的和（列宽、行高）。</summary>
        static double Sum(double[] sizes, int from, int span)
        {
            var total = 0d;
            for (var i = from; i < from + span && i < sizes.Length; i++) total += sizes[i];
            return total;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Place();
            var count = placed.Count == 0 ? 0 : placed.Max(item => item.Slot.Column + item.Slot.ColumnSpan);
            var max = new double[count];
            var min = new double[count];
            var fixedWidths = new double?[count];
            stickyColumns = new bool[count];
            var spanning = new List<(Slot Slot, double Min, double Max)>();
            foreach (var (cell, slot) in placed)
            {
                var padding = Padding(cell).Left * 2;
                cell.Measure(Size.Infinity);
                var cellMax = cell.DesiredSize.Width + padding;
                var cellMin = cellMax;
                if (!cell.Nowrap && cell is not TableHeaderCell { Section: TableHead })
                {
                    cell.Measure(new Size(0, double.PositiveInfinity));
                    cellMin = cell.DesiredSize.Width + padding;
                }
                for (var i = slot.Column; i < slot.Column + slot.ColumnSpan; i++) stickyColumns[i] |= cell.Sticky;
                if (slot.ColumnSpan > 1)
                {
                    spanning.Add((slot, cellMin, cellMax));
                    continue;
                }
                max[slot.Column] = Math.Max(max[slot.Column], cellMax);
                min[slot.Column] = Math.Max(min[slot.Column], cellMin);
                if (cell is TableHeaderCell { ColumnWidth: { } width }) fixedWidths[slot.Column] = width;
            }
            foreach (var (slot, cellMin, cellMax) in spanning)
            {
                Spread(max, slot.Column, slot.ColumnSpan, cellMax);
                Spread(min, slot.Column, slot.ColumnSpan, cellMin);
            }
            for (var i = 0; i < count; i++)
                if (fixedWidths[i] is { } width) max[i] = min[i] = width;
            // CSS 的 table-layout: auto（表格宽 100%）：放得下时按各列内容的比例分多出来的宽度（有不定宽的列时只分给它们，定宽的列不变宽）；
            // 放不下时按最小宽度插值；再放不下时最小宽度、横向滚动
            var available = !double.IsInfinity(Width_) ? Width_ : double.IsInfinity(availableSize.Width) ? max.Sum() : availableSize.Width;
            var (sumMax, sumMin) = (max.Sum(), min.Sum());
            var growing = fixedWidths.Any(width => width is null) ? Enumerable.Range(0, count).Where(i => fixedWidths[i] is null).Sum(i => max[i]) : sumMax;
            columns = new double[count];
            for (var i = 0; i < count; i++)
            {
                var grows = fixedWidths[i] is null || growing == sumMax;
                columns[i] = sumMax <= available ? max[i] + (grows && growing > 0 ? (available - sumMax) * max[i] / growing : 0)
                    : sumMin <= available ? min[i] + (available - sumMin) * (max[i] - min[i]) / Math.Max(1, sumMax - sumMin)
                    : min[i];
            }
            // 行高：先按只占一行的格，跨行的格放不下时把差的加到它的最后一行（同浏览器）；加的 1 像素是格的分隔线
            heights = new double[rows.Count];
            foreach (var (cell, slot) in placed.OrderBy(item => item.Slot.RowSpan))
            {
                // 内边距不交给格自己：布局取整会把 6.125 的内边距取成 6，行高就比上游（内容加精确的内边距）矮
                var padding = Padding(cell);
                cell.Measure(new Size(Math.Max(0, Sum(columns, slot.Column, slot.ColumnSpan) - padding.Left - padding.Right), double.PositiveInfinity));
                var need = cell.DesiredSize.Height + padding.Top + padding.Bottom + 1;
                var have = Sum(heights, slot.Row, slot.RowSpan);
                if (need > have) heights[slot.Row + slot.RowSpan - 1] += need - have;
            }
            foreach (var backdrop in new[] { headBackdrop, sideBackdrop, cornerBackdrop }) backdrop.Measure(Size.Infinity);
            return new Size(columns.Sum(), heights.Sum());
        }

        /// <summary>一格占的矩形（含内边距，网格的坐标）。</summary>
        Rect SlotRect(Slot slot) =>
            new(Sum(columns, 0, slot.Column), Sum(heights, 0, slot.Row), Sum(columns, slot.Column, slot.ColumnSpan), Sum(heights, slot.Row, slot.RowSpan));

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var (cell, slot) in placed)
            {
                // 格排在内边距里；分隔线占格的下边 1 像素（表尾是上边）。裁剪框是含内边距的整格（同 CSS 的 td）：内容能伸进内边距（迷你图最后的点），不伸进旁边的格
                var padding = Padding(cell);
                var rect = SlotRect(slot);
                var top = rows[slot.Row].Section is TableFoot ? 1 : 0;
                var content = new Rect(rect.X + padding.Left, rect.Y + padding.Top + top, Math.Max(0, rect.Width - padding.Left - padding.Right),
                    Math.Max(0, rect.Height - padding.Top - padding.Bottom - 1));
                cell.Arrange(content);
                cell.Clip = new RectangleGeometry(new Rect(-padding.Left, -padding.Top, rect.Width, Math.Max(0, rect.Height - 1)));
            }
            var head = HeadHeight;
            var (left, width) = StickySpan;
            headBackdrop.Arrange(new Rect(0, 0, finalSize.Width, head));
            sideBackdrop.Arrange(new Rect(left, head, width, Math.Max(0, finalSize.Height - head)));
            cornerBackdrop.Arrange(new Rect(left, 0, width, head));
            ApplyStickyOffsets();
            return finalSize;
        }

        /// <summary>
        /// 固定的表头往下移滚动的竖向偏移、固定的列往右移横向偏移（不超出表格）；叠放次序：表体、固定列、表头、表头里的固定格。
        /// 用变换不重新排版：滚动时只改位移。
        /// </summary>
        public void ApplyStickyOffsets()
        {
            var scroll = owner.scroll;
            var head = HeadHeight;
            var (_, width) = StickySpan;
            var sticky = width > 0;
            var dy = shiftY = owner.StickyHeader && head > 0 ? Math.Clamp(scroll.Offset.Y, 0, Math.Max(0, Bounds.Height - head)) : 0;
            var dx = sticky ? Math.Clamp(scroll.Offset.X, 0, Math.Max(0, Bounds.Width - width)) : 0;
            headBackdrop.IsVisible = owner.StickyHeader && head > 0;
            sideBackdrop.IsVisible = sticky;
            cornerBackdrop.IsVisible = sticky && head > 0;
            Shift(headBackdrop, 0, dy);
            Shift(sideBackdrop, dx, 0);
            Shift(cornerBackdrop, dx, dy);
            foreach (var (cell, slot) in placed)
            {
                var (isHead, isSticky) = (rows[slot.Row].Section is TableHead, slot.Column < stickyColumns.Length && stickyColumns[slot.Column]);
                cell.ZIndex = (isHead ? 4 : 0) + (isSticky ? 2 : 0);
                Shift(cell, isSticky ? dx : 0, isHead ? dy : 0);
            }

            static void Shift(Visual visual, double x, double y) =>
                visual.RenderTransform = x == 0 && y == 0 ? null : new TranslateTransform(x, y);
        }

        public void InvalidateRows()
        {
            InvalidateVisual();
            foreach (var backdrop in new[] { headBackdrop, sideBackdrop, cornerBackdrop }) backdrop.InvalidateVisual();
        }

        public override void Render(DrawingContext context) => DrawRows(context, new Rect(Bounds.Size), _ => true, edge: false);

        /// <summary>
        /// 行底色（表头 fill；表体 surface 或页面底色，斑马纹 fill，指针下的行 fillHover）与各格的分隔线（同 CSS 画在格上：表头格下 border，表体格下 borderMuted，
        /// 表尾格上 border；跨行的格用自己那一行的底色，跨过的行线在它那段断开；格写了 Background 时盖在行的底色上、画满整格），只画 <paramref name="area"/>（网格的坐标）里 <paramref name="include"/> 的行；
        /// <paramref name="edge"/> 时在结束边画固定列的竖线。
        /// </summary>
        void DrawRows(DrawingContext context, Rect area, Func<TableSection, bool> include, bool edge)
        {
            IBrush? Brush(string key) => owner.Resource<IBrush?>(key);
            var rowBrush = Brush(owner.Variant == TableVariant.Ghost ? ThemeKeys.Brush.Background : ThemeKeys.Brush.Surface);
            IBrush? Background(int r) => rows[r].Section is TableHead ? Brush(ThemeKeys.Brush.Fill)
                : r == hovered && rows[r].Section is TableBody ? Brush(ThemeKeys.Brush.FillHover)
                : rows[r].Row.IsStriped ? Brush(ThemeKeys.Brush.Fill) : rowBrush;
            // 行的上下边吸附到设备像素（同浏览器画表格）：行高带小数时分隔线不发虚。网格自己可能不在整像素上，按到根的偏移算
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            var offset = VisualRoot is Visual root ? this.TranslatePoint(default, root)?.Y ?? 0 : 0;
            double Snap(double value) => Math.Floor((value + offset) * scale + 0.5) / scale - offset; // 不用 Math.Round：.5 时银行家舍入让一条线的两边差出 2 像素
            Rect Band(double left, double width, double top, double bottom) => new(left, Snap(top), width, Snap(bottom) - Snap(top));
            // 固定的那几层只画自己的区域（格线按格的位置画，不能画到层外面）；网格本身不裁：高度带小数时会裁掉吸附到整像素的最后一条线
            using var clip = area == new Rect(Bounds.Size) ? default : context.PushClip(new Rect(area.Size));
            using var shift = context.PushTransform(Matrix.CreateTranslation(-area.X, -area.Y));
            var y = 0d;
            for (var r = 0; r < rows.Count && r < heights.Length; r++)
            {
                var bottom = y + heights[r];
                if (include(rows[r].Section))
                {
                    if (Background(r) is { } background) context.FillRectangle(background, Band(area.X, area.Width, y, bottom));
                    if (edge && Brush(ThemeKeys.Brush.BorderMuted) is { } side) context.FillRectangle(side, new Rect(area.Right - 1, Snap(y), 1, Snap(bottom) - Snap(y)));
                }
                y = bottom;
            }
            foreach (var (cell, slot) in placed)
            {
                var section = rows[slot.Row].Section;
                if (!include(section) || slot.Row + slot.RowSpan > heights.Length || slot.Column + slot.ColumnSpan > columns.Length) continue;
                var rect = SlotRect(slot);
                if (slot.RowSpan > 1 && Background(slot.Row) is { } background) context.FillRectangle(background, Band(rect.X, rect.Width, rect.Y, rect.Bottom));
                if (cell.Background is { } own) context.FillRectangle(own, Band(rect.X, rect.Width, rect.Y, rect.Bottom));
                if (section is TableFoot)
                {
                    if (Brush(ThemeKeys.Brush.Border) is { } line) context.FillRectangle(line, Band(rect.X, rect.Width, rect.Y, rect.Y + 1));
                }
                else if (Brush(section is TableHead ? ThemeKeys.Brush.Border : ThemeKeys.Brush.BorderMuted) is { } line)
                    context.FillRectangle(line, Band(rect.X, rect.Width, rect.Bottom - 1, rect.Bottom));
            }
        }

        /// <summary>指针所在的行；吸顶的表头盖着的地方算表头（不高亮下面的表体行）。</summary>
        int RowAt(double y)
        {
            if (shiftY > 0 && y >= shiftY && y < shiftY + HeadHeight) return -1;
            var top = 0d;
            for (var r = 0; r < heights.Length; top += heights[r], r++)
                if (y >= top && y < top + heights[r]) return r;
            return -1;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var index = RowAt(e.GetPosition(this).Y);
            if (index == hovered) return;
            hovered = index;
            InvalidateRows();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            hovered = -1;
            InvalidateRows();
        }

        /// <summary>固定的表头、列下面的一层：按自己在网格里的位置画对应的行；固定列的那几层在结束边画竖线。不参与命中测试。</summary>
        sealed class Backdrop : Control
        {
            readonly TableGrid grid;
            readonly bool head, edge;

            public Backdrop(TableGrid grid, bool head, bool edge)
            {
                (this.grid, this.head, this.edge) = (grid, head, edge);
                IsHitTestVisible = false;
            }

            public override void Render(DrawingContext context) => grid.DrawRows(context, Bounds, section => head == section is TableHead, edge);
        }
    }
}

/// <summary>只挂可视子元素的竖排容器：子元素的逻辑父元素在别处（表格的标题）。</summary>
sealed class VisualStack : Control
{
    public void Add(Control child) => VisualChildren.Add(child);

    public void Insert(int index, Control child) => VisualChildren.Insert(index, child);

    public void Remove(Control child) => VisualChildren.Remove(child);

    /// <summary>前面的（标题）按内容高，最后一个（滚动区域）拿剩下的高度：表格限了高时竖着滚的是网格，标题不动。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var (width, height) = (0d, 0d);
        var children = VisualChildren.OfType<Control>().ToList();
        for (var i = 0; i < children.Count; i++)
        {
            var last = i == children.Count - 1;
            children[i].Measure(availableSize.WithHeight(last ? Math.Max(0, availableSize.Height - height) : double.PositiveInfinity));
            width = Math.Max(width, children[i].DesiredSize.Width);
            height += children[i].DesiredSize.Height;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0d;
        var children = VisualChildren.OfType<Control>().ToList();
        for (var i = 0; i < children.Count; i++)
        {
            var height = i == children.Count - 1 ? Math.Max(0, finalSize.Height - y) : children[i].DesiredSize.Height;
            children[i].Arrange(new Rect(0, y, finalSize.Width, height));
            y += height;
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

/// <summary>单元格（td）：左右 space.md、上下按密度；对齐按 <see cref="Align"/>；默认可以换行，<see cref="Nowrap"/> 时一行（放不下省略号）。Background 由表格画满整格（含内边距），盖在行的底色上。</summary>
public class TableCell : ContentControl
{
    /// <summary>对齐，默认 start；数字、金额用 end。</summary>
    public static readonly StyledProperty<TableAlign> AlignProperty = AvaloniaProperty.Register<TableCell, TableAlign>(nameof(Align), TableAlign.Start);

    /// <summary>不换行（超出时省略号）。</summary>
    public static readonly StyledProperty<bool> NowrapProperty = AvaloniaProperty.Register<TableCell, bool>(nameof(Nowrap));

    /// <summary>固定在起始边（第一列，常是行标题）：表格横向滚动时一直看得到；一列里有格写了就整列固定。</summary>
    public static readonly StyledProperty<bool> StickyProperty = AvaloniaProperty.Register<TableCell, bool>(nameof(Sticky));

    /// <summary>往下合并几行（TS 的 rowspan），默认 1；不超出所在的部分（表头、表体、表尾）。</summary>
    public static readonly StyledProperty<int> RowSpanProperty = AvaloniaProperty.Register<TableCell, int>(nameof(RowSpan), 1);

    /// <summary>往后合并几列（TS 的 colspan），默认 1。</summary>
    public static readonly StyledProperty<int> ColumnSpanProperty = AvaloniaProperty.Register<TableCell, int>(nameof(ColumnSpan), 1);

    /// <summary>对齐。</summary>
    public TableAlign Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>不换行。</summary>
    public bool Nowrap { get => GetValue(NowrapProperty); set => SetValue(NowrapProperty, value); }

    /// <summary>固定在起始边。</summary>
    public bool Sticky { get => GetValue(StickyProperty); set => SetValue(StickyProperty, value); }

    /// <summary>往下合并几行。</summary>
    public int RowSpan { get => GetValue(RowSpanProperty); set => SetValue(RowSpanProperty, value); }

    /// <summary>往后合并几列。</summary>
    public int ColumnSpan { get => GetValue(ColumnSpanProperty); set => SetValue(ColumnSpanProperty, value); }

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
        else if (change.Property == StickyProperty || change.Property == RowSpanProperty || change.Property == ColumnSpanProperty)
            this.FindLogicalAncestorOfType<Table>()?.InvalidateGrid();
        else if (change.Property == BackgroundProperty) this.FindLogicalAncestorOfType<Table>()?.InvalidateGridVisual();
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
