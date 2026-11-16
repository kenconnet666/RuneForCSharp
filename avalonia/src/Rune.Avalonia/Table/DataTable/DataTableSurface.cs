using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// DataTable 滚动区域里的内容（上游 DataTableView 的 table）：只建看得见的行。表头按纵向偏移挪到视口顶上（吸顶），合计行挪到视口底下（吸底），
/// 固定列的格按横向偏移挪回边上。行高按第一行量出的高度；展开的详情是一行另外的高度（量过之后记下，没量过时按估计），各行的位置按高度累加。
/// </summary>
internal sealed class DataTableSurface : Control
{
    const int Overscan = 8;
    readonly DataTable owner;
    DataTableRowView? header;
    DataTableRowView? summary;
    Control? emptyView;
    readonly Dictionary<int, DataTableRowView> realized = [];
    readonly List<DataTableRowView> pool = [];
    readonly List<DataTableRowView> skeletons = [];
    // 展开的详情：行的下标 → 详情行；量过的高度按行的 key 记
    readonly Dictionary<int, DataTableDetailView> details = [];
    readonly Dictionary<object, double> detailHeights = [];
    // 各行的上边（不含表头），有详情时详情的上边；总高度
    double[] rowTops = [];
    readonly Dictionary<int, double> detailTops = [];
    double bodyHeight;
    double rowHeight;
    double[] widths = [];

    public DataTableSurface(DataTable owner) => this.owner = owner;

    /// <summary>表头（测试用）。</summary>
    internal DataTableRowView? Header => header;

    /// <summary>建好的行（测试用）：下标 → 行。</summary>
    internal IReadOnlyDictionary<int, DataTableRowView> Realized => realized;

    /// <summary>骨架行（测试用）。</summary>
    internal IReadOnlyList<DataTableRowView> Skeletons => skeletons;

    /// <summary>空状态（测试用）。</summary>
    internal Control? EmptyView => emptyView;

    /// <summary>合计行（测试用）。</summary>
    internal DataTableRowView? Summary => summary;

    internal double RowHeight => rowHeight;

    internal bool HasSelection => owner.SelectionMode is not null;

    internal bool HasDetails => owner.RenderDetail is not null;

    /// <summary>数据列前面的宽度：勾选列、详情列。</summary>
    internal double Leading => (HasSelection ? DataTable.SelectWidth : 0) + (HasDetails ? DataTable.DetailWidth : 0);

    /// <summary>展开的详情行（测试用）：行的下标 → 详情行。</summary>
    internal IReadOnlyDictionary<int, DataTableDetailView> Details => details;

    internal IReadOnlyList<double> Widths => widths;

    internal DataTable Owner => owner;

    Vector Offset => owner.Scroller.Offset;

    Size Viewport => owner.Scroller.Viewport;

    void Add(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    void Remove(Control child)
    {
        LogicalChildren.Remove(child);
        VisualChildren.Remove(child);
    }

    /// <summary>列、数据、外观变了：表头、合计、骨架、空状态与建好的行都重建。</summary>
    internal void Rebuild()
    {
        if (header is not null) Remove(header);
        header = new DataTableRowView(this, DataTableRowKind.Header) { ZIndex = 2 };
        Add(header);
        RebuildBody();
    }

    /// <summary>
    /// 只重建表体（行、骨架、空状态）与合计：表头筛选变了时用。表头不动——重建会换掉筛选按钮，正在勾选的弹出层就关了（上游是就地更新）；
    /// 筛选按钮的选中值就地改。
    /// </summary>
    internal void RebuildBody()
    {
        foreach (var child in VisualChildren.OfType<Control>().Where(child => child != header).ToList()) Remove(child);
        realized.Clear();
        pool.Clear();
        skeletons.Clear();
        details.Clear();
        emptyView = null;
        header?.SyncFilters();
        summary = owner.Shown.Any(column => column.Summary is not null) ? new DataTableRowView(this, DataTableRowKind.Summary) { ZIndex = 2 } : null;
        if (summary is not null) Add(summary);
        if (owner.IsLoading)
            for (var i = 0; i < 6; i++)
            {
                var skeleton = new DataTableRowView(this, DataTableRowKind.Skeleton, i);
                skeletons.Add(skeleton);
                Add(skeleton);
            }
        else if (owner.View.Count == 0)
        {
            emptyView = owner.Empty as Control ?? new Empty { Name = DataTableParts.Empty, Size = TextSize.Sm, Title = owner.Empty as string ?? owner.Lang.TableEmpty };
            Add(emptyView);
        }
        rowHeight = 0;
        InvalidateMeasure();
    }

    /// <summary>列宽变了：各行重新量、重新排。</summary>
    internal void Relayout()
    {
        InvalidateMeasure();
        foreach (var row in VisualChildren.OfType<DataTableRowView>())
        {
            row.InvalidateMeasure();
            row.InvalidateArrange();
        }
    }

    /// <summary>重建 key 对应的那一行（开始、结束编辑）；它没建时不用管。</summary>
    internal void RebuildRow(object key)
    {
        foreach (var row in realized.Values)
            if (row.Row is { } data && Equals(owner.KeyOf(data), key)) row.Rebuild();
    }

    /// <summary>能 Tab 进入的格变了：各行的格跟着改 Tab 停靠点。</summary>
    internal void SyncTabStops()
    {
        foreach (var row in realized.Values) row.SyncTabStops();
    }

    /// <summary>聚焦 key 那一行的某一格（滚到看得见、重建完再聚焦）。</summary>
    internal void FocusCell(object key, string column)
    {
        var index = owner.View.ToList().FindIndex(row => Equals(owner.KeyOf(row), key));
        if (index >= 0) FocusCell(index, column);
    }

    /// <summary>聚焦第 index 行的某一格：不在视口里时先滚过去（停在吸顶表头之下、吸底合计之上）。</summary>
    internal void FocusCell(int index, string column)
    {
        if (rowHeight > 0 && index < rowTops.Length)
        {
            var top = rowTops[index];
            var visible = Math.Max(rowHeight, Viewport.Height - HeaderHeight - SummaryHeight);
            if (top < Offset.Y) owner.Scroller.Offset = new Vector(Offset.X, top);
            else if (top + rowHeight > Offset.Y + visible) owner.Scroller.Offset = new Vector(Offset.X, top + rowHeight - visible);
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (realized.TryGetValue(index, out var row)) row.CellOf(column)?.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>展开的行变了：各行的展开按钮跟着改；详情行在下一次量的时候建或收。</summary>
    internal void SyncDetails()
    {
        foreach (var row in realized.Values) row.SyncDetail();
        InvalidateMeasure();
    }

    /// <summary>选择、色调变了：建好的行与表头的全选框跟着改，不重建。</summary>
    internal void SyncRows()
    {
        header?.SyncSelection();
        foreach (var row in realized.Values) row.SyncSelection();
    }

    /// <summary>
    /// 滚动或视口变了：可见的行可能变了（重新量），固定列的格要按新的横向偏移挪（行的位置没变时平台不会重排它，要自己通知）。
    /// </summary>
    internal void Scrolled()
    {
        InvalidateMeasure();
        header?.InvalidateArrange();
        summary?.InvalidateArrange();
        foreach (var row in realized.Values) row.InvalidateArrange();
        foreach (var detail in details.Values) detail.InvalidateArrange();
    }

    internal void ScrollTo(int index)
    {
        if (rowHeight <= 0) owner.UpdateLayout();
        owner.Scroller.Offset = new Vector(Offset.X, Math.Max(0, index < rowTops.Length ? rowTops[index] : index * rowHeight));
    }

    /// <summary>各行（与展开的详情）的位置：按行高与详情的高度（量过的，没量过时按估计）累加。</summary>
    void Layout(IReadOnlyList<object> rows)
    {
        var expanded = HasDetails ? owner.ExpandedRows.ToHashSet() : [];
        rowTops = new double[rows.Count];
        detailTops.Clear();
        var y = 0.0;
        for (var index = 0; index < rows.Count; index++)
        {
            rowTops[index] = y;
            y += rowHeight;
            if (expanded.Count == 0 || !expanded.Contains(owner.KeyOf(rows[index])) || !owner.RowHasDetail(rows[index])) continue;
            detailTops[index] = y;
            y += detailHeights.TryGetValue(owner.KeyOf(rows[index]), out var measured) ? measured : DataTable.DetailHeight;
        }
        bodyHeight = y;
    }

    /// <summary>表格内容里纵坐标 y（含表头）落在第几行；没有行时 -1。</summary>
    internal int RowIndexAt(double y) => rowTops.Length == 0 ? -1 : RowAt(Math.Max(0, y - HeaderHeight));

    /// <summary>拖着的行变了：那一行半透明、浮在别的行上面。</summary>
    internal void SyncDragging()
    {
        var dragging = owner.DraggingKey;
        foreach (var row in realized.Values)
        {
            var active = dragging is not null && row.Row is { } data && Convert.ToString(owner.KeyOf(data), System.Globalization.CultureInfo.InvariantCulture) == dragging;
            row.Opacity = active ? 0.6 : 1;
            row.ZIndex = active ? 1 : 0;
        }
    }

    /// <summary>聚焦 key 那一行的拖动把手（重排后行重建了）。</summary>
    internal void FocusHandle(string key)
    {
        foreach (var row in realized.Values)
            if (row.Row is { } data && Convert.ToString(owner.KeyOf(data), System.Globalization.CultureInfo.InvariantCulture) == key)
                row.GetLogicalDescendants().OfType<ButtonBase>().FirstOrDefault(button => button.Name == DataTableParts.RowHandle)?.Focus(NavigationMethod.Tab);
    }

    /// <summary>上边在 offset 之后的第一行的下标（二分查找）。</summary>
    int RowAt(double offset)
    {
        int low = 0, high = rowTops.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (rowTops[middle] <= offset) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    /// <summary>各列的宽度：调整过的、列定义的；其余平分剩下的（最小宽度不一样时按 FlexColumnWidth），放不下最小宽度时横向滚动。</summary>
    void ComputeWidths(double viewport)
    {
        var columns = owner.Shown;
        double? WidthOf(DataTableColumn column) => owner.ColumnWidths?.TryGetValue(column.Key, out var own) == true ? own : column.Width;
        var total = Math.Max(viewport, TableColumns.TableMinWidth(columns, WidthOf, Leading));
        var flexible = columns.Where(column => WidthOf(column) is null).ToList();
        var rest = Math.Max(0, total - Leading - columns.Sum(column => WidthOf(column) ?? 0));
        widths = [.. columns.Select(column => WidthOf(column) ?? TableColumns.FlexColumnWidth(column, columns, WidthOf, Leading, viewport) ?? (flexible.Count > 0 ? rest / flexible.Count : 0))];
        // 各列都写了宽度、合起来比表格窄：按比例分掉多出来的（同浏览器 table-layout: fixed 的做法）
        var sum = widths.Sum();
        if (flexible.Count == 0 && sum > 0 && Leading + sum < viewport) widths = [.. widths.Select(width => width * (viewport - Leading) / sum)];
    }

    internal double TableWidth => Leading + widths.Sum();

    double HeaderHeight => header?.DesiredSize.Height ?? 0;

    double SummaryHeight => summary?.DesiredSize.Height ?? 0;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var viewport = Viewport.Width > 0 ? Viewport.Width : double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        ComputeWidths(viewport);
        var width = TableWidth;
        var infinite = new Size(width, double.PositiveInfinity);
        header?.Measure(infinite);
        summary?.Measure(infinite);
        var rows = owner.View;
        var body = 0.0;
        if (skeletons.Count > 0)
        {
            foreach (var skeleton in skeletons) skeleton.Measure(infinite);
            body = skeletons.Sum(skeleton => skeleton.DesiredSize.Height);
        }
        else if (emptyView is not null)
        {
            emptyView.Measure(new Size(Math.Max(0, (viewport > 0 ? viewport : width) - 2 * 16), double.PositiveInfinity));
            body = emptyView.DesiredSize.Height + 2 * 16;
        }
        else if (rows.Count > 0)
        {
            if (rowHeight <= 0)
            {
                var probe = Realize(0);
                probe.Measure(infinite);
                rowHeight = Math.Max(1, probe.DesiredSize.Height);
            }
            var viewportHeight = Viewport.Height > 0 ? Viewport.Height : 32 * TextElement.GetFontSize(this);
            var top = Math.Max(0, Offset.Y - HeaderHeight);
            Layout(rows);
            var first = Math.Max(0, RowAt(top) - Overscan);
            var last = Math.Min(rows.Count - 1, RowAt(top + viewportHeight) + Overscan);
            foreach (var index in realized.Keys.Where(index => index < first || index > last).ToList())
            {
                var view = realized[index];
                realized.Remove(index);
                view.IsVisible = false;
                pool.Add(view);
            }
            for (var index = first; index <= last; index++) Realize(index).Measure(infinite);
            // 详情：看得见的展开的行建（或留着）详情行，量出高度记下；量出来与估计不同时重算位置
            foreach (var index in details.Keys.Where(index => index < first || index > last || !detailTops.ContainsKey(index)).ToList())
            {
                Remove(details[index]);
                details.Remove(index);
            }
            var changed = false;
            for (var index = first; index <= last; index++)
            {
                if (!detailTops.ContainsKey(index)) continue;
                if (!details.TryGetValue(index, out var detail))
                {
                    detail = new DataTableDetailView(this, rows[index]);
                    details[index] = detail;
                    Add(detail);
                }
                detail.Measure(new Size(ViewportWidth > 0 ? ViewportWidth : width, double.PositiveInfinity));
                var key = owner.KeyOf(rows[index]);
                if (!detailHeights.TryGetValue(key, out var known) || Math.Abs(known - detail.DesiredSize.Height) > 0.5)
                {
                    detailHeights[key] = detail.DesiredSize.Height;
                    changed = true;
                }
            }
            if (changed) Layout(rows);
            body = bodyHeight;
        }
        return new Size(width, HeaderHeight + body + SummaryHeight);
    }

    /// <summary>建（或从池里拿）第 index 行。</summary>
    DataTableRowView Realize(int index)
    {
        if (realized.TryGetValue(index, out var existing)) return existing;
        DataTableRowView view;
        if (pool.Count > 0)
        {
            view = pool[^1];
            pool.RemoveAt(pool.Count - 1);
            view.IsVisible = true;
        }
        else
        {
            view = new DataTableRowView(this, DataTableRowKind.Body);
            Add(view);
        }
        view.Bind(owner.View[index], index);
        realized[index] = view;
        return view;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = Math.Max(finalSize.Width, TableWidth);
        var headerHeight = HeaderHeight;
        header?.Arrange(new Rect(0, Offset.Y, width, headerHeight));
        var y = headerHeight;
        if (skeletons.Count > 0)
            foreach (var skeleton in skeletons)
            {
                skeleton.Arrange(new Rect(0, y, width, skeleton.DesiredSize.Height));
                y += skeleton.DesiredSize.Height;
            }
        else if (emptyView is not null)
        {
            // 空状态贴着视口居中（横向滚动时不跟着滚走）
            var visible = Viewport.Width > 0 ? Viewport.Width : width;
            emptyView.Arrange(new Rect(Offset.X + 16, y + 16, Math.Max(0, visible - 32), emptyView.DesiredSize.Height));
            y += emptyView.DesiredSize.Height + 32;
        }
        else
        {
            foreach (var (index, view) in realized) view.Arrange(new Rect(0, headerHeight + (index < rowTops.Length ? rowTops[index] : index * rowHeight), width, rowHeight));
            // 详情贴着可见区域的起始边、宽度不超过可见区域（横向滚动时不跟着滚走）
            var visibleWidth = ViewportWidth > 0 ? ViewportWidth : width;
            foreach (var (index, detail) in details)
                if (detailTops.TryGetValue(index, out var detailTop)) detail.Arrange(new Rect(Offset.X, headerHeight + detailTop, visibleWidth, detail.DesiredSize.Height));
            y += bodyHeight;
        }
        if (summary is not null)
        {
            var viewportBottom = Offset.Y + (Viewport.Height > 0 ? Viewport.Height : finalSize.Height);
            summary.Arrange(new Rect(0, Math.Min(y, viewportBottom - SummaryHeight), width, SummaryHeight));
        }
        foreach (var view in pool) view.Arrange(default);
        return finalSize;
    }

    /// <summary>横向偏移（固定列的格按它挪回边上）。</summary>
    internal double OffsetX => Offset.X;

    /// <summary>视口宽度。</summary>
    internal double ViewportWidth => Viewport.Width > 0 ? Viewport.Width : Bounds.Width;
}

/// <summary>
/// 展开的详情那一行（上游 detailRow / detailContent）：fill 底、下边线，内容让过勾选列与展开列、和第一个数据列对齐，四周 space.md；
/// 宽度是可见区域，横向滚动时贴着可见区域的起始边。读屏念出是哪一行的详情。
/// </summary>
internal sealed class DataTableDetailView : Control
{
    readonly DataTableSurface surface;
    readonly ContentControl content = new();

    public DataTableDetailView(DataTableSurface surface, object row)
    {
        this.surface = surface;
        Name = DataTableParts.DetailRow;
        Row = row;
        DataContext = row;
        content.Content = surface.Owner.RenderDetail?.Invoke(row);
        AutomationProperties.SetName(this, surface.Owner.Lang.TableRowDetails(DataTableRowView.LabelOf(surface.Owner, row)));
        LogicalChildren.Add(content);
        VisualChildren.Add(content);
    }

    /// <summary>这一行的数据。</summary>
    internal object Row { get; }

    /// <summary>详情的内容（测试用）。</summary>
    internal object? Content => content.Content;

    Thickness Padding
    {
        get
        {
            var md = this.Resource<double>(ThemeKeys.Space.Md) * TextElement.GetFontSize(this);
            return new Thickness(surface.Leading + md, md, md, md);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        content.Measure(availableSize.Deflate(padding));
        return new Size(availableSize.Width, content.DesiredSize.Height + padding.Top + padding.Bottom + 1);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        content.Arrange(new Rect(new Point(padding.Left, padding.Top), new Size(Math.Max(0, finalSize.Width - padding.Left - padding.Right), content.DesiredSize.Height)));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (this.Resource<IBrush?>(ThemeKeys.Brush.Fill) is { } fill) context.FillRectangle(fill, bounds);
        if (this.Resource<IBrush?>(ThemeKeys.Brush.BorderMuted) is { } line) context.FillRectangle(line, new Rect(0, bounds.Height - 1, bounds.Width, 1));
    }
}
