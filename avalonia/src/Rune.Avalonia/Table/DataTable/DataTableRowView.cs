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

/// <summary>DataTable 的一行是哪种。</summary>
internal enum DataTableRowKind
{
    Header,
    Body,
    Skeleton,
    Summary,
}

/// <summary>
/// DataTable 的一行：自己画底色（表头 fill；表体随样式，悬停 fillHover，已选 accentSoft，色调的浅色底，斑马纹）与下边线，排各格（左右 space.md、上下按密度）。
/// 固定列的格画上行的底色、挪到视口的边上。
/// </summary>
internal sealed class DataTableRowView : Control
{
    readonly DataTableSurface surface;
    readonly List<(Control Cell, DataTableColumn? Column)> cells = [];
    // 表头里可调整宽度的列的手柄：列的下标 → 手柄
    readonly Dictionary<int, ColumnResizer> resizers = [];
    // 分组表头（两行）：组标题与它跨的列（第一列的下标、几列）；与分组相邻的竖线在这些列的起始边；第一行的高度
    readonly List<(TableHeaderCell Cell, int First, int Count)> groups = [];
    readonly HashSet<int> grouped = [];
    readonly List<int> dividers = [];
    double groupRowHeight;
    Control? selector;
    // 详情列：表头是空的格（读屏名称"详情"），各行是展开按钮（没有详情的行空着）
    Control? detailCell;
    bool hovered;

    public DataTableRowView(DataTableSurface surface, DataTableRowKind kind, int index = -1)
    {
        this.surface = surface;
        Kind = kind;
        Index = index;
        Name = kind switch
        {
            DataTableRowKind.Header => DataTableParts.HeaderRow,
            DataTableRowKind.Summary => DataTableParts.SummaryRow,
            _ => DataTableParts.Row,
        };
        if (kind is DataTableRowKind.Header or DataTableRowKind.Summary or DataTableRowKind.Skeleton) Build(null, index);
        if (kind == DataTableRowKind.Body)
        {
            PointerEntered += (_, _) => SetHovered(true);
            PointerExited += (_, _) => SetHovered(false);
            PointerPressed += (_, e) =>
            {
                if (Row is { } row && CellAt(e.GetPosition(this).X) is { } hit && !e.Handled) Owner.CellPressed(hit.Cell, row, hit.Column);
            };
            DoubleTapped += (_, e) =>
            {
                if (Row is { } row && CellAt(e.GetPosition(this).X) is { } hit) Owner.CellDoubleTapped(row, hit.Column);
            };
        }
    }

    public DataTableRowKind Kind { get; }

    /// <summary>这一行的数据（表体）。</summary>
    public object? Row { get; private set; }

    /// <summary>整行的色调（RowTone）；没有时 null。</summary>
    internal StatusTone? Tone => Row is { } row ? Owner.RowTone?.Invoke(row) : null;

    /// <summary>在显示的行里的下标（表体）。</summary>
    public int Index { get; private set; } = -1;

    /// <summary>各格（测试用）：选择列（有时）在前，再是各数据列。</summary>
    internal IReadOnlyList<Control> Cells => [.. (selector is null ? [] : new[] { selector }), .. cells.Select(cell => cell.Cell)];

    /// <summary>这一行的选择框（测试用）。</summary>
    internal ChoiceBase? Choice => (selector as Panel)?.Children.OfType<ChoiceBase>().FirstOrDefault();

    DataTable Owner => surface.Owner;

    void Attach(Control child)
    {
        LogicalChildren.Add(child);
        VisualChildren.Add(child);
    }

    void Detach(Control child)
    {
        LogicalChildren.Remove(child);
        VisualChildren.Remove(child);
    }

    void SetHovered(bool value)
    {
        hovered = value;
        InvalidateVisual();
    }

    /// <summary>按现在的状态（编辑中的格、校验）重建这一行。</summary>
    public void Rebuild()
    {
        if (Row is { } row) Build(row, Index);
    }

    /// <summary>横坐标落在哪一格（含左右内边距）；落在选择列上时 null。</summary>
    (TableCell Cell, DataTableColumn Column)? CellAt(double x)
    {
        foreach (var (cell, column) in cells)
        {
            var padding = PaddingOf(cell);
            if (cell is TableCell table && column is not null && x >= cell.Bounds.X - padding.Left && x < cell.Bounds.Right + padding.Right) return (table, column);
        }
        return null;
    }

    /// <summary>某一列的格。</summary>
    internal TableCell? CellOf(string column) => cells.FirstOrDefault(cell => cell.Column?.Key == column).Cell as TableCell;

    /// <summary>网格时只有一格能 Tab 进入。</summary>
    public void SyncTabStops()
    {
        if (Row is not { } row || !Owner.GridMode) return;
        var key = Owner.KeyOf(row);
        foreach (var (cell, column) in cells)
            if (column is not null) KeyboardNavigation.SetIsTabStop(cell, Owner.IsActiveCell(key, column.Key));
    }

    /// <summary>表体的行换成另一行的数据（回收）。</summary>
    public void Bind(object row, int index)
    {
        if (Equals(Row, row) && Index == index) return;
        Row = row;
        Index = index;
        DataContext = row;
        Build(row, index);
    }

    /// <summary>建各格。</summary>
    void Build(object? row, int index)
    {
        foreach (var child in VisualChildren.OfType<Control>().ToList()) Detach(child);
        cells.Clear();
        resizers.Clear();
        groups.Clear();
        grouped.Clear();
        dividers.Clear();
        selector = null;
        detailCell = null;
        var owner = Owner;
        var lang = owner.Lang;
        if (surface.HasSelection)
        {
            selector = SelectCell(row, index, lang);
            Attach(selector);
        }
        if (surface.HasDetails)
        {
            detailCell = DetailCell(row, lang);
            Attach(detailCell);
        }
        var columns = owner.Shown;
        for (var position = 0; position < columns.Count; position++)
        {
            var column = columns[position];
            var align = ColumnTable.AlignOf(column, owner.Types, position);
            TableCell cell = Kind switch
            {
                DataTableRowKind.Header => HeaderCell(column, align, position),
                DataTableRowKind.Summary => new TableCell { Name = DataTableParts.SummaryCell, Align = align, Nowrap = true, Content = SummaryContent(column, position, lang) },
                DataTableRowKind.Skeleton => new TableCell { Name = DataTableParts.Cell, Content = new Skeleton { HorizontalAlignment = HorizontalAlignment.Left } },
                _ => new TableCell
                {
                    Name = DataTableParts.Cell, Align = align, Nowrap = !column.Wrap,
                    Content = column.Cell?.Invoke(row!, index) ?? ColumnTable.Display(column, owner.Types[position], TableColumns.ValueOf(column, row), lang),
                },
            };
            cell.ApplyRole(head: Kind == DataTableRowKind.Header, foot: Kind == DataTableRowKind.Summary);
            TextBlock.SetTextTrimming(cell, TextTrimming.CharacterEllipsis);
            if (Kind == DataTableRowKind.Body) owner.PrepareCell(cell, row!, column);
            if (column.Sticky is not null) cell.ZIndex = 1;
            cells.Add((cell, column));
            Attach(cell);
            if (Kind == DataTableRowKind.Header && (column.Resizable ?? owner.Resizable))
            {
                var key = column.Key;
                var at = position;
                var resizer = new ColumnResizer { Name = DataTableParts.Resizer, Label = TableColumns.Label(column), ZIndex = 2 };
                if (column.MinWidth is { } min) resizer.MinLength = min;
                resizer.Bind(() => surface.Widths.Count > at ? surface.Widths[at] : 0, width => owner.ResizeColumn(key, width));
                resizers[position] = resizer;
                Attach(resizer);
            }
        }
        if (Kind == DataTableRowKind.Header) BuildGroups();
        // 编辑中的行浮在别的行上面：校验的原因在格子下面，不被下一行盖住
        ZIndex = cells.Any(cell => cell.Cell.Classes.Contains("editing")) ? 1 : 0;
        SyncSelection();
        SyncDetail();
        InvalidateMeasure();
    }

    /// <summary>
    /// 分组表头（上游 renderHeaderRows）：组标题在第一行、跨组里的列，居中；没分组的列与勾选列、详情列跨两行。
    /// 与分组相邻处一条竖线：分组的起始边、分组后面那一列的起始边（第一格除外）。
    /// </summary>
    void BuildGroups()
    {
        var layout = Owner.HeaderCells;
        var shown = Owner.Shown.ToList();
        for (var position = 0; position < layout.Count; position++)
        {
            var entry = layout[position];
            var first = shown.IndexOf(entry.Columns[0]);
            if (position > 0 && (entry.Group is not null || layout[position - 1].Group is not null)) dividers.Add(first);
            if (entry.Group is not { } group) continue;
            var cell = new TableHeaderCell { Name = DataTableParts.GroupHeaderCell, Align = TableAlign.Center, Scope = "colgroup", Content = group.Header };
            cell.ApplyRole(head: true, foot: false);
            TextBlock.SetTextTrimming(cell, TextTrimming.CharacterEllipsis);
            groups.Add((cell, first, entry.Columns.Count));
            for (var i = first; i < first + entry.Columns.Count; i++) grouped.Add(i);
            Attach(cell);
        }
    }

    /// <summary>分组表头的组标题（测试用）：标题、第一列的下标、跨几列。</summary>
    internal IReadOnlyList<(TableHeaderCell Cell, int First, int Count)> Groups => groups;

    TableHeaderCell HeaderCell(DataTableColumn column, TableAlign align, int position)
    {
        var owner = Owner;
        var cell = new TableHeaderCell { Name = DataTableParts.HeaderCell, Align = align, Scope = "col" };
        var main = column.Sortable ? SortButton(cell, column, align) : column.Header;
        var filter = column.Filter is { } spec ? FilterButton(column, spec) : null;
        var menu = owner.ColumnMenu ? MenuButton(column, position) : null;
        if (filter is null && menu is null)
        {
            cell.Content = main;
            return cell;
        }
        // 有筛选、列菜单按钮的列标题：文字（或排序按钮）占满剩下的宽度，按钮在结尾
        var content = new Flex { Align = FlexAlign.Center, Gap = Space.Xs };
        var head = main as Control ?? new TextBlock { Text = main as string, TextTrimming = TextTrimming.CharacterEllipsis };
        if (align == TableAlign.End) head.HorizontalAlignment = HorizontalAlignment.Right;
        Flex.SetGrow(head, true);
        content.Children.Add(head);
        if (filter is not null) content.Children.Add(filter);
        if (menu is not null) content.Children.Add(menu);
        cell.Content = content;
        cell.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        return cell;
    }

    ColumnFilterButton FilterButton(DataTableColumn column, ColumnFilter spec)
    {
        var owner = Owner;
        var button = new ColumnFilterButton
        {
            Name = DataTableParts.FilterButton, Label = TableColumns.Label(column), IsKeyword = spec.IsKeyword, Tag = column.Key,
            Options = owner.FilterOptionsOf.TryGetValue(column.Key, out var options) ? options : [],
            Values = owner.Filters?.TryGetValue(column.Key, out var values) == true ? values : [],
        };
        button.ValuesChanged += (_, next) => owner.SetFilter(column.Key, next);
        return button;
    }

    ColumnMenuButton MenuButton(DataTableColumn column, int position)
    {
        var owner = Owner;
        var shown = owner.Shown;
        var menu = new ColumnMenuButton
        {
            Name = DataTableParts.ColumnMenuButton, Label = TableColumns.Label(column), Sortable = column.Sortable,
            Sorted = owner.ActiveSorts.FirstOrDefault(item => item.Key == column.Key)?.Direction, Pinned = column.Sticky,
            CanMoveBackward = position > 0, CanMoveForward = position < shown.Count - 1, CanHide = shown.Count > 1,
        };
        menu.ActionRequested += (_, action) => owner.ColumnAction(column.Key, action, surface.Widths.Count > position ? surface.Widths[position] : 0);
        return menu;
    }

    /// <summary>可排序列标题里的按钮：文字、方向箭头（排序中是强调色）、多列排序时的次序。</summary>
    ButtonBase SortButton(TableHeaderCell cell, DataTableColumn column, TableAlign align)
    {
        var owner = Owner;
        var sorts = owner.ActiveSorts;
        var priority = sorts.ToList().FindIndex(item => item.Key == column.Key);
        var sort = priority >= 0 ? sorts[priority].Direction : (SortDirection?)null;
        var icon = new Icon
        {
            Name = DataTableParts.SortIcon,
            Data = sort switch { SortDirection.Ascending => Lucide.ArrowUp, SortDirection.Descending => Lucide.ArrowDown, _ => Lucide.ArrowUpDown },
        };
        if (sort is not null) icon[!Icon.ForegroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.AccentText).ToBinding();
        var button = new ButtonBase
        {
            Name = DataTableParts.SortButton,
            Content = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new TextBlock { Text = column.Header as string, TextTrimming = TextTrimming.CharacterEllipsis }, icon } },
            HorizontalAlignment = align == TableAlign.End ? HorizontalAlignment.Right : align == TableAlign.Center ? HorizontalAlignment.Center : HorizontalAlignment.Left,
        };
        var parts = (Flex)button.Content;
        if (column.Header is not string) parts.Children[0] = new ContentControl { Content = column.Header };
        var lang = owner.Lang;
        if (sorts.Count > 1 && priority >= 0)
        {
            // 多列排序时的次序（1、2…）：小号数字跟在箭头后面；读屏另外念出"第几排序"
            var number = new TextBlock { Name = DataTableParts.SortPriority, Text = (priority + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = 0.75 * Em };
            number[!TextBlock.ForegroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.AccentText).ToBinding();
            parts.Children.Add(number);
            if (priority > 0) AutomationProperties.SetHelpText(cell, lang.TableSortPriority(priority + 1, sort == SortDirection.Ascending ? lang.TableSortAscending : lang.TableSortDescending));
        }
        // 按住 Shift 点是追加一列（多列排序）：点击事件不带修饰键，按下时记住
        var additive = false;
        button.AddHandler(PointerPressedEvent, (_, e) => additive = e.KeyModifiers.HasFlag(KeyModifiers.Shift), RoutingStrategies.Tunnel, handledEventsToo: true);
        button.AddHandler(KeyDownEvent, (_, e) => additive = e.KeyModifiers.HasFlag(KeyModifiers.Shift), RoutingStrategies.Tunnel, handledEventsToo: true);
        button.Click += (_, _) =>
        {
            owner.SortClicked(column.Key, additive);
            additive = false;
        };
        // 上游 th 的 aria-sort（只在第一排序列上）：读屏念出排序的方向
        AutomationProperties.SetItemStatus(cell, priority == 0 ? sort == SortDirection.Ascending ? "ascending" : "descending" : "none");
        return button;
    }

    object? SummaryContent(DataTableColumn column, int position, Lang lang) => column.Summary switch
    {
        { Compute: { } compute } => compute(Owner.View),
        { } => TableColumns.Sum(column, Owner.View) is { } total ? ColumnTable.Display(column, Owner.Types[position], total, lang) : column.Empty ?? lang.EmptyValue,
        null when position == 0 => Owner.SummaryLabel ?? lang.TableSummary,
        _ => null,
    };

    /// <summary>选择列的格：表头是全选框（多选时），表体是勾选框或单选框，骨架行是空的。</summary>
    Control SelectCell(object? row, int index, Lang lang)
    {
        var host = new Panel { Name = Kind == DataTableRowKind.Header ? DataTableParts.SelectHeaderCell : DataTableParts.SelectCell };
        var owner = Owner;
        ChoiceBase? choice = null;
        if (Kind == DataTableRowKind.Header && owner.SelectionMode == SelectionMode.Multiple)
        {
            choice = new Checkbox { Name = DataTableParts.SelectAll };
            AutomationProperties.SetName(choice, lang.TableSelectAll);
            choice.Toggling += (_, e) =>
            {
                e.Cancel = true;
                owner.ToggleAll();
            };
        }
        else if (Kind == DataTableRowKind.Body)
        {
            choice = owner.SelectionMode == SelectionMode.Multiple ? new Checkbox { Name = DataTableParts.SelectCheckbox } : new Radio { Name = DataTableParts.SelectRadio };
            AutomationProperties.SetName(choice, lang.TableSelectRow(index + 1));
            choice.Toggling += (_, e) =>
            {
                e.Cancel = true;
                if (Row is { } current) owner.ToggleRow(current);
            };
        }
        if (choice is not null)
        {
            choice.HorizontalAlignment = HorizontalAlignment.Center;
            choice.VerticalAlignment = VerticalAlignment.Top;
            host.Children.Add(choice);
        }
        return host;
    }

    /// <summary>表头筛选按钮的选中值跟上表格的 Filters。</summary>
    public void SyncFilters()
    {
        var filters = Owner.Filters;
        foreach (var button in this.GetLogicalDescendants().OfType<ColumnFilterButton>())
        {
            var key = (string)button.Tag!;
            button.Values = filters?.TryGetValue(key, out var values) == true ? values : [];
        }
    }

    /// <summary>一行的名称（展开按钮、详情的读屏名称）：第一个数据列的文字。</summary>
    internal static string LabelOf(DataTable owner, object row) =>
        owner.Shown.Count > 0 ? Convert.ToString(TableColumns.ValueOf(owner.Shown[0], row), System.Globalization.CultureInfo.CurrentCulture) ?? "" : "";

    /// <summary>详情列的格：表头是空的（读屏名称"详情"），有详情的行是展开按钮（ChevronRight，展开时转 90°）。</summary>
    Control DetailCell(object? row, Lang lang)
    {
        var host = new Panel { Name = Kind == DataTableRowKind.Header ? DataTableParts.DetailHeaderCell : null };
        if (Kind == DataTableRowKind.Header) AutomationProperties.SetName(host, lang.TableDetails);
        if (Kind != DataTableRowKind.Body || row is null || !Owner.RowHasDetail(row)) return host;
        var toggle = new IconButton { Name = DataTableParts.DetailToggle, Icon = Lucide.ChevronRight, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Size = TextSize.Sm, HorizontalAlignment = HorizontalAlignment.Center };
        AutomationProperties.SetName(toggle, lang.TableRowDetails(LabelOf(Owner, row)));
        toggle.Click += (_, _) =>
        {
            if (Row is { } current) Owner.ToggleDetail(current);
        };
        host.Children.Add(toggle);
        return host;
    }

    /// <summary>展开按钮（测试用）；没有详情的行为 null。</summary>
    internal IconButton? DetailToggle => (detailCell as Panel)?.Children.OfType<IconButton>().FirstOrDefault();

    /// <summary>展开按钮的展开状态与箭头的转向。</summary>
    public void SyncDetail()
    {
        if (DetailToggle is not { } toggle || Row is not { } row) return;
        var expanded = Owner.ExpandedRows.Contains(Owner.KeyOf(row));
        toggle.SetValue(Popover.TriggerExpandedProperty, expanded);
        toggle.RenderTransform = expanded ? new RotateTransform(FlowDirection == FlowDirection.RightToLeft ? -90 : 90) : null;
    }

    /// <summary>选择框的状态与这一行是否已选（整行底色）。</summary>
    public void SyncSelection()
    {
        var owner = Owner;
        if (Choice is Checkbox { Name: DataTableParts.SelectAll } all)
        {
            var (every, some) = owner.SelectionState();
            all.IsChecked = every;
            all.IsIndeterminate = some;
        }
        else if (Choice is { } choice && Row is { } row)
        {
            choice.IsChecked = owner.IsSelected(row);
            choice.IsEnabled = owner.CanSelect(row);
        }
        InvalidateVisual();
    }

    double Em => TextElement.GetFontSize(this);

    /// <summary>格的内边距：左右 space.md、上下按密度（同静态表格）。</summary>
    Thickness PaddingOf(Control cell)
    {
        var em = TextElement.GetFontSize(cell);
        var density = Owner.Density;
        var block = density == TableDensity.Compact ? 0.25 : density == TableDensity.Comfortable ? 0.75 : 0.5;
        // 编辑中的格：编辑器贴满格子，上下内边距收掉
        return new Thickness(0.75 * em, cell.Classes.Contains("editing") ? 0 : block * em);
    }

    /// <summary>
    /// 格里内容的高度：一行文字按行高（1.5em，不取整）。量出来的高度按像素向上取整，紧凑时 18.375 算成 19，行行累加会比上游高出一截（上游的行高是小数）。
    /// </summary>
    static double ContentHeight(Control cell)
    {
        var lineHeight = TextBlock.GetLineHeight(cell);
        return cell is TableCell { Content: string } && !double.IsNaN(lineHeight) && cell.DesiredSize.Height <= Math.Ceiling(lineHeight) ? lineHeight : cell.DesiredSize.Height;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var widths = surface.Widths;
        var height = 0.0;
        // 选择框不撑高这一行（上游 choiceInCell：24px 的点击区域在行里叠放），有选择列的行与纯文字的行一样高
        selector?.Measure(new Size(DataTable.SelectWidth, double.PositiveInfinity));
        detailCell?.Measure(new Size(DataTable.DetailWidth, double.PositiveInfinity));
        foreach (var resizer in resizers.Values) resizer.Measure(Size.Infinity);
        for (var i = 0; i < cells.Count && i < widths.Count; i++)
        {
            var padding = PaddingOf(cells[i].Cell);
            // 骨架：高 1em，宽度按行与列错开（90%、75%、60%、45%，同上游）
            if (cells[i].Cell is TableCell { Content: Skeleton bar })
                (bar.Width, bar.Height) = (Math.Max(0, widths[i] - padding.Left - padding.Right) * (90 - (Index + i) % 4 * 15) / 100, TextElement.GetFontSize(cells[i].Cell));
            cells[i].Cell.Measure(new Size(Math.Max(0, widths[i] - padding.Left - padding.Right), double.PositiveInfinity));
            if (!grouped.Contains(i)) height = Math.Max(height, ContentHeight(cells[i].Cell) + padding.Top + padding.Bottom);
        }
        if (groups.Count > 0)
        {
            // 两行表头：第一行是组标题，组里的列在第二行；没分组的列跨两行
            groupRowHeight = 0;
            foreach (var (cell, first, count) in groups)
            {
                var padding = PaddingOf(cell);
                cell.Measure(new Size(Math.Max(0, widths.Skip(first).Take(count).Sum() - padding.Left - padding.Right), double.PositiveInfinity));
                groupRowHeight = Math.Max(groupRowHeight, ContentHeight(cell) + padding.Top + padding.Bottom);
            }
            var second = grouped.Where(i => i < cells.Count).Select(i => ContentHeight(cells[i].Cell) + PaddingOf(cells[i].Cell).Top + PaddingOf(cells[i].Cell).Bottom).DefaultIfEmpty(0).Max();
            height = Math.Max(height, groupRowHeight + second);
        }
        return new Size(surface.TableWidth, height + 1);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = surface.Widths;
        var offsets = TableColumns.StickyOffsetsOf(Owner.Shown, column => widths[IndexOf(column)], surface.Leading);
        var height = finalSize.Height - 1;
        var x = 0.0;
        if (selector is not null)
        {
            // 选择框的第一行（1.5em，框画在它的正中）对着这一行的正中；24px 的点击区域多出的部分在下面，不撑高这一行
            var top = (height - 1.5 * TextElement.GetFontSize(selector)) / 2;
            selector.Arrange(new Rect(0, top, DataTable.SelectWidth, Math.Max(selector.DesiredSize.Height, height - top)));
            x = DataTable.SelectWidth;
        }
        if (detailCell is not null)
        {
            // 展开按钮同选择框：上下居中，多出的点击区域叠在行里
            var top = (height - detailCell.DesiredSize.Height) / 2;
            detailCell.Arrange(new Rect(x, top, DataTable.DetailWidth, detailCell.DesiredSize.Height));
            x += DataTable.DetailWidth;
        }
        var scroll = surface.OffsetX;
        var view = surface.ViewportWidth;
        for (var i = 0; i < cells.Count && i < widths.Count; i++)
        {
            var (cell, column) = cells[i];
            var left = x;
            // 固定列：挪到视口的边上（起始边不早于偏移，结束边不晚于视口的右边）
            if (column?.Sticky == ColumnSticky.Start && offsets.Start.TryGetValue(column.Key, out var start)) left = Math.Max(x, scroll + start);
            else if (column?.Sticky == ColumnSticky.End && offsets.End.TryGetValue(column.Key, out var end)) left = Math.Min(x, scroll + view - end - widths[i]);
            var padding = PaddingOf(cell);
            // 组里的列在第二行
            var top = grouped.Contains(i) ? groupRowHeight : 0;
            cell.Arrange(new Rect(left + padding.Left, top + padding.Top, Math.Max(0, widths[i] - padding.Left - padding.Right), Math.Max(0, height - top - padding.Top - padding.Bottom)));
            // 手柄贴着这一列的结束边（同上游 th 里的 ResizeHandle）
            if (resizers.TryGetValue(i, out var resizer))
                resizer.Arrange(new Rect(left + widths[i] - resizer.DesiredSize.Width, 0, resizer.DesiredSize.Width, finalSize.Height));
            x += widths[i];
        }
        foreach (var (cell, first, count) in groups)
        {
            var padding = PaddingOf(cell);
            var start = surface.Leading + widths.Take(first).Sum();
            cell.Arrange(new Rect(start + padding.Left, padding.Top, Math.Max(0, widths.Skip(first).Take(count).Sum() - padding.Left - padding.Right), Math.Max(0, groupRowHeight - padding.Top - padding.Bottom)));
        }
        return finalSize;
    }

    int IndexOf(DataTableColumn column)
    {
        for (var i = 0; i < Owner.Shown.Count; i++)
            if (ReferenceEquals(Owner.Shown[i], column)) return i;
        return 0;
    }

    /// <summary>行的底色：表头、合计 fill；已选 accentSoft；色调的浅色底；悬停 fillHover；斑马纹隔行 fill；其余随表格的样式。</summary>
    IBrush? RowBrush()
    {
        var owner = Owner;
        string key;
        if (Kind is DataTableRowKind.Header or DataTableRowKind.Summary) key = ThemeKeys.Brush.Fill;
        else if (Row is { } row && owner.IsSelected(row)) key = hovered ? ThemeKeys.Brush.AccentSoftHover : ThemeKeys.Brush.AccentSoft;
        else if (Row is { } toned && owner.RowTone?.Invoke(toned) is { } tone && ToneBrush(tone) is { } soft) key = soft;
        else if (hovered) key = ThemeKeys.Brush.FillHover;
        else if (owner.Striped && Index % 2 == 1) key = ThemeKeys.Brush.Fill;
        else key = owner.Variant == TableVariant.Ghost ? ThemeKeys.Brush.Background : ThemeKeys.Brush.Surface;
        return this.Resource<IBrush?>(key);

        string? ToneBrush(StatusTone tone) => tone == StatusTone.Info ? ThemeKeys.Brush.InfoSoft
            : tone == StatusTone.Success ? ThemeKeys.Brush.SuccessSoft
            : tone == StatusTone.Warning ? ThemeKeys.Brush.WarningSoft
            : tone == StatusTone.Danger ? ThemeKeys.Brush.DangerSoft : null;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var edges = TableColumns.StickyOffsetsOf(Owner.Shown, column => surface.Widths[IndexOf(column)], surface.Leading);
        if (RowBrush() is { } fill)
        {
            context.FillRectangle(fill, bounds);
            // 固定列的格盖住滚过去的内容：在它们的位置再铺一层底色（格本身透明）
            foreach (var (cell, column) in cells)
                if (column?.Sticky is not null)
                {
                    var padding = PaddingOf(cell);
                    context.FillRectangle(fill, new Rect(cell.Bounds.X - padding.Left, 0, cell.Bounds.Width + padding.Left + padding.Right, bounds.Height));
                }
        }
        // 固定列与滚动的列之间一条竖线：横向滚动时看得出哪几列是固定的
        if (this.Resource<IBrush?>(ThemeKeys.Brush.BorderMuted) is { } divider)
            foreach (var (cell, column) in cells)
            {
                if (column is null) continue;
                var padding = PaddingOf(cell);
                if (column.Key == edges.StartEdge) context.FillRectangle(divider, new Rect(cell.Bounds.Right + padding.Right - 1, 0, 1, bounds.Height));
                else if (column.Key == edges.EndEdge) context.FillRectangle(divider, new Rect(cell.Bounds.X - padding.Left, 0, 1, bounds.Height));
            }
        // 分组表头：组标题下边一条淡线（与下面一行的列标题仍是一个表头），与分组相邻处一条竖线
        if (groups.Count > 0 && this.Resource<IBrush?>(ThemeKeys.Brush.BorderMuted) is { } muted)
        {
            foreach (var (cell, _, _) in groups)
            {
                var padding = PaddingOf(cell);
                context.FillRectangle(muted, new Rect(cell.Bounds.X - padding.Left, groupRowHeight - 1, cell.Bounds.Width + padding.Left + padding.Right, 1));
            }
            foreach (var column in dividers.Where(column => column < cells.Count))
            {
                var cell = cells[column].Cell;
                context.FillRectangle(muted, new Rect(cell.Bounds.X - PaddingOf(cell).Left, 0, 1, bounds.Height));
            }
        }
        // 单元格导航：聚焦的格一圈 sm 宽的焦点环；校验没过的格一圈 xs 宽的 danger 内描边（聚焦时仍是焦点环）
        foreach (var (cell, _) in cells)
        {
            var focused = cell.IsFocused;
            if (!focused && !cell.Classes.Contains("invalid")) continue;
            var padding = PaddingOf(cell);
            var area = new Rect(cell.Bounds.X - padding.Left, 0, cell.Bounds.Width + padding.Left + padding.Right, bounds.Height - 1);
            var width = this.Resource<double>(focused ? ThemeKeys.BorderWidth.Sm : ThemeKeys.BorderWidth.Xs);
            if (this.Resource<IBrush?>(focused ? ThemeKeys.Brush.FocusRing : ThemeKeys.Brush.Danger) is { } ring)
                context.DrawRectangle(new Pen(ring, width), area.Deflate(width / 2));
        }
        // 下边线：表头 border，表体 borderMuted；合计行的上边线是 border
        var line = Kind == DataTableRowKind.Header || Kind == DataTableRowKind.Summary ? ThemeKeys.Brush.Border : ThemeKeys.Brush.BorderMuted;
        if (this.Resource<IBrush?>(line) is { } stroke)
        {
            if (Kind == DataTableRowKind.Summary) context.FillRectangle(stroke, new Rect(0, 0, bounds.Width, 1));
            else context.FillRectangle(stroke, new Rect(0, bounds.Height - 1, bounds.Width, 1));
        }
    }
}
