using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>看板的一列（vue-tsx-pro 的 KanbanColumn）。</summary>
/// <param name="Key">列的标识：GetColumn 返回它。</param>
/// <param name="Label">列名。</param>
/// <param name="Tone">列名旁计数的色调（超时、异常这类要注意的列）；不写时中性色。</param>
public sealed record KanbanColumn(string Key, string Label, StatusTone? Tone = null);

/// <summary>
/// 看板（vue-tsx-pro 的 KanbanBoard）：一列一个状态（或阶段、负责人），卡片在列之间移动。任务看板、招聘流程、工单。
/// <code>
/// var board = new KanbanBoard { Columns = columns, ItemsSource = tickets, GetKey = …, GetColumn = …, GetLabel = …, CanMove = (ticket, to) =&gt; … };
/// board.Moved += (_, e) =&gt; OpenProcessDialog(e.Item, e.To);
/// </code>
/// <list type="bullet">
/// <item>数据由使用方控制：看板不自己挪卡片，只在放下时报告 <see cref="Moved"/>（使用方改了数据、调 <see cref="Refresh"/> 或换 ItemsSource，卡片才换列；ObservableCollection 的增删自动跟上）。</item>
/// <item><see cref="CanMove"/> 返回不能移动的原因（null 是可以）：拖动时经过的列按它高亮（能放的是强调色，不能放的标红并写出原因），落点处画一条插入线；
/// 放在不能放的列上时卡片弹回、读屏播报原因，并报告 <see cref="Rejected"/>。订阅了 <see cref="Reordered"/> 时列内也能排序（ItemsSource 就地改，同 Sortable）。</item>
/// <item>指针：在卡片上按下拖动（挪开 3 以上才开始；点在卡片里的按钮、输入框上不算），影子卡跟着指针，原位留着变淡的占位，拖到边缘时自动滚动。</item>
/// <item>键盘：每张卡片有把手按钮，空格 / Enter 拿起，← / → 换列（从右到左时对调），↑ / ↓ 调整位置，空格 / Enter 放下，Esc 取消；每一步都有读屏播报。</item>
/// </list>
/// 列多时整块横向滚动，每列的卡片多时列自己竖向滚动。
/// </summary>
public class KanbanBoard : CompositeControl
{
    /// <summary>全部卡片的数据。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(KanbanBoard),
        new PropertyMetadata(null, (sender, e) => ((KanbanBoard)sender).Observe(e.NewValue as IEnumerable)));

    /// <summary>各列：从起始边排。</summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(IReadOnlyList<KanbanColumn>), typeof(KanbanBoard),
        new PropertyMetadata(Array.Empty<KanbanColumn>(), OnSync));

    /// <summary>卡片的标识（必填）。</summary>
    public static readonly DependencyProperty GetKeyProperty = DependencyProperty.Register(nameof(GetKey), typeof(Func<object, string>), typeof(KanbanBoard),
        new PropertyMetadata(null, (sender, _) => ((KanbanBoard)sender).Rebuild()));

    /// <summary>卡片在哪一列（必填）。</summary>
    public static readonly DependencyProperty GetColumnProperty = DependencyProperty.Register(nameof(GetColumn), typeof(Func<object, string>), typeof(KanbanBoard), new PropertyMetadata(null, OnSync));

    /// <summary>卡片的名称：把手的名称、读屏播报；不写时是 key。</summary>
    public static readonly DependencyProperty GetLabelProperty = DependencyProperty.Register(nameof(GetLabel), typeof(Func<object, string>), typeof(KanbanBoard), new PropertyMetadata(null, OnSync));

    /// <summary>能不能移到这一列：返回不能的原因，null 是可以。</summary>
    public static readonly DependencyProperty CanMoveProperty = DependencyProperty.Register(nameof(CanMove), typeof(Func<object, string, string?>), typeof(KanbanBoard));

    /// <summary>每列至少多宽：放得下时各列平分整行，放不下时横向滚动；不写时 16em。</summary>
    public static readonly DependencyProperty ColumnWidthProperty = DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(KanbanBoard), new PropertyMetadata(double.NaN, OnSync));

    /// <summary>每列的最大高度：卡片多时列里竖向滚动；不写时窗口高度的 70%。</summary>
    public static readonly DependencyProperty ColumnMaxHeightProperty = DependencyProperty.Register(nameof(ColumnMaxHeight), typeof(double), typeof(KanbanBoard), new PropertyMetadata(double.NaN, OnSync));

    /// <summary>一列没有卡片时显示的内容；不写时是“没有”。</summary>
    public static readonly DependencyProperty EmptyProperty = DependencyProperty.Register(nameof(Empty), typeof(object), typeof(KanbanBoard), new PropertyMetadata(null, OnSync));

    /// <summary>卡片内容的数据模板（TS 的 children）。</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(KanbanBoard),
        new PropertyMetadata(null, (sender, _) => ((KanbanBoard)sender).Rebuild()));

    readonly System.Windows.Controls.Grid root = new();
    readonly ScrollArea scroll = new() { Axis = ScrollAreaAxis.X, Focusable = false, IsViewportFocusable = false };
    readonly Flex board = new() { Align = FlexAlign.Start, Gap = Space.Md };
    readonly Canvas overlay = new() { IsHitTestVisible = false };
    readonly LiveRegion live = new();
    readonly Dictionary<string, KanbanColumnView> columns = [];
    // 按 key 留着每张卡片：换列、重排时挪动它们而不是重建，键盘焦点留在被移动的把手上
    readonly Dictionary<string, KanbanCard> cards = [];
    readonly SortableModel<object> model;
    INotifyCollectionChanged? observed;
    EventHandler<SortableReorderedEventArgs>? reordered;
    // 换列、重排时挪动卡片会让把手失焦：这期间的失焦不算放下
    bool reordering;
    // 按下时：哪张卡片、指针的位置（相对自己）、指针在卡片里的位置、卡片的宽度
    (string Key, Point Start, Vector Grip, double Width)? press;
    KanbanCard? ghost;

    /// <summary>新建：横向滚动的一排列。</summary>
    public KanbanBoard()
    {
        model = new SortableModel<object>(() => Items, KeyOf, () => Provider.GetLang(this))
        {
            GetLabel = LabelOf,
            Groups = new SortableGroups<object>
            {
                Keys = () => [.. Columns.Select(column => column.Key)],
                Of = item => GetColumn?.Invoke(item) ?? "",
                Label = key => Columns.FirstOrDefault(column => column.Key == key)?.Label ?? key,
                CanMove = (item, to) => CanMove?.Invoke(item, to),
                Moved = (item, to, index) => Moved?.Invoke(this, new KanbanMovedEventArgs(item, to, index)),
                Rejected = (item, to, reason) => Rejected?.Invoke(this, new KanbanRejectedEventArgs(item, to, reason)),
            },
            Reordered = Apply,
        };
        model.Changed += () =>
        {
            live.Announce(model.Announcement);
            Sync();
        };
        scroll.Content = board;
        AutomationProperties.SetLiveSetting(live, AutomationLiveSetting.Assertive);
        root.Children.Add(scroll);
        root.Children.Add(overlay);
        root.Children.Add(live);
        Root = root;
        Loaded += (_, _) => Sync();
    }

    /// <summary>全部卡片的数据。</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>各列。</summary>
    public IReadOnlyList<KanbanColumn> Columns { get => (IReadOnlyList<KanbanColumn>)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>卡片的标识。</summary>
    public Func<object, string>? GetKey { get => (Func<object, string>?)GetValue(GetKeyProperty); set => SetValue(GetKeyProperty, value); }

    /// <summary>卡片在哪一列。</summary>
    public Func<object, string>? GetColumn { get => (Func<object, string>?)GetValue(GetColumnProperty); set => SetValue(GetColumnProperty, value); }

    /// <summary>卡片的名称。</summary>
    public Func<object, string>? GetLabel { get => (Func<object, string>?)GetValue(GetLabelProperty); set => SetValue(GetLabelProperty, value); }

    /// <summary>能不能移到这一列。</summary>
    public Func<object, string, string?>? CanMove { get => (Func<object, string, string?>?)GetValue(CanMoveProperty); set => SetValue(CanMoveProperty, value); }

    /// <summary>每列至少多宽。</summary>
    public double ColumnWidth { get => (double)GetValue(ColumnWidthProperty); set => SetValue(ColumnWidthProperty, value); }

    /// <summary>每列的最大高度。</summary>
    public double ColumnMaxHeight { get => (double)GetValue(ColumnMaxHeightProperty); set => SetValue(ColumnMaxHeightProperty, value); }

    /// <summary>空列显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>卡片内容的数据模板。</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>放到了可以放的列：使用方处理（打开处理的对话框、调接口），改了数据卡片才换列。</summary>
    public event EventHandler<KanbanMovedEventArgs>? Moved;

    /// <summary>放到了不能放的列：卡片已经弹回，可以在这里提示原因。</summary>
    public event EventHandler<KanbanRejectedEventArgs>? Rejected;

    /// <summary>列内排序了（ItemsSource 已经就地改好）：订阅了才能在列内排序。</summary>
    public event EventHandler<SortableReorderedEventArgs>? Reordered
    {
        add => reordered += value;
        remove => reordered -= value;
    }

    /// <summary>各列（测试用）。</summary>
    internal IReadOnlyDictionary<string, KanbanColumnView> ColumnViews => columns;

    /// <summary>卡片（测试用）。</summary>
    internal IReadOnlyDictionary<string, KanbanCard> Cards => cards;

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced => live.Text;

    /// <summary>正被拿起的卡片（测试用）。</summary>
    internal string? Active => model.Active;

    /// <summary>跟着指针的影子卡（测试用）。</summary>
    internal KanbanCard? Ghost => ghost;

    IReadOnlyList<object> Items => ItemsSource?.Cast<object>().ToList() ?? [];

    string KeyOf(object item) => GetKey?.Invoke(item) ?? item.ToString() ?? "";

    string LabelOf(object item) => GetLabel?.Invoke(item) ?? KeyOf(item);

    /// <summary>数据在原处改了（卡片换了列）：重新分到各列。</summary>
    public void Refresh() => Sync();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    static void OnSync(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((KanbanBoard)sender).Sync();

    void Observe(IEnumerable? items)
    {
        if (observed is not null) observed.CollectionChanged -= OnCollectionChanged;
        observed = items as INotifyCollectionChanged;
        if (observed is not null) observed.CollectionChanged += OnCollectionChanged;
        Sync();
    }

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    void Rebuild()
    {
        cards.Clear();
        foreach (var column in columns.Values) column.List.Children.Clear();
        Sync();
    }

    /// <summary>
    /// 按数据排出各列与卡片：列头是列名与卡片数（有卡片时按列的色调），拖到了这一列（不是来的那一列）时高亮（能放强调色、不能放标红并写出原因），
    /// 落点处是插入线；空列是“没有”。卡片按 key 留着，只在顺序真的变了时挪动。
    /// </summary>
    void Sync()
    {
        if (model is null) return;
        var lang = Provider.GetLang(this);
        var messages = PatternsMessages.For(lang);
        var em = FontSize;
        var items = Items;
        var active = model.Active;
        var target = model.Target;
        var from = active is null ? null : model.GroupOf(active);
        var keys = Columns.Select(column => column.Key).ToHashSet();
        foreach (var key in columns.Keys.Where(key => !keys.Contains(key)).ToList()) columns.Remove(key);
        var focused = Keyboard.FocusedElement as IInputElement;
        reordering = true;
        try
        {
            var views = new List<UIElement>();
            var seen = new HashSet<string>();
            foreach (var column in Columns)
            {
                if (!columns.TryGetValue(column.Key, out var view)) columns[column.Key] = view = new KanbanColumnView();
                var inColumn = items.Where(item => GetColumn?.Invoke(item) == column.Key).ToList();
                var over = target is not null && from != column.Key && target.Group == column.Key;
                var reason = over ? target!.Reason : null;
                var children = new List<UIElement>();
                for (var index = 0; index < inColumn.Count; index++)
                {
                    var item = inColumn[index];
                    var key = KeyOf(item);
                    seen.Add(key);
                    if (!cards.TryGetValue(key, out var card)) cards[key] = card = new KanbanCard(this, key, item, ItemTemplate, LabelOf(item));
                    card.Sync(lang.SortableHandle(LabelOf(item)), messages.KanbanInstructions, key == active, ghost is not null && key == active, em);
                    if (over && target!.Index == index) children.Add(view.Indicator);
                    children.Add(card);
                }
                if (over && target!.Index >= inColumn.Count) children.Add(view.Indicator);
                if (inColumn.Count == 0 && !over) children.Add(view.Empty);
                if (!view.List.Children.Cast<UIElement>().SequenceEqual(children))
                {
                    view.List.Children.Clear();
                    foreach (var child in children)
                    {
                        (VisualTreeHelper.GetParent(child) as Panel)?.Children.Remove(child);
                        view.List.Children.Add(child);
                    }
                }
                // 先放好卡片再排列头与间距（间距按列表里的位置留）
                view.Sync(column, inColumn.Count, reason, over, Empty ?? messages.KanbanEmpty, em, ColumnHeight());
                Flex.SetGrow(view, true);
                view.MinWidth = double.IsNaN(ColumnWidth) ? 16 * em : ColumnWidth;
                views.Add(view);
            }
            foreach (var key in cards.Keys.Where(key => !seen.Contains(key)).ToList()) cards.Remove(key);
            if (!board.Children.Cast<UIElement>().SequenceEqual(views))
            {
                board.Children.Clear();
                foreach (var view in views) board.Children.Add(view);
            }
            var sm = this.Resource<double>(ThemeKeys.Space.Sm) * em;
            board.Margin = new Thickness(0, 0, 0, sm);
            if (focused is not null && focused != Keyboard.FocusedElement && focused is UIElement { IsVisible: true }) Keyboard.Focus(focused);
        }
        finally
        {
            reordering = false;
        }
    }

    /// <summary>每列的最大高度：写了用它，不写时窗口高度的 70%（上游的 70vh）。</summary>
    double ColumnHeight() => !double.IsNaN(ColumnMaxHeight) ? ColumnMaxHeight
        : Window.GetWindow(this) is { ActualHeight: > 0 } window ? 0.7 * window.ActualHeight : double.PositiveInfinity;

    /// <summary>列内排序：改 ItemsSource（ObservableCollection 用 Move，别的列表删了再插）并报告；没有订阅 Reordered 时列内不能排序。</summary>
    void Apply(IReadOnlyList<object> next, object item)
    {
        if (reordered is null || ItemsSource is not IList list) return;
        var (from, to) = (list.IndexOf(item), next.ToList().IndexOf(item));
        if (list.GetType().GetMethod("Move", [typeof(int), typeof(int)]) is { } move) move.Invoke(list, [from, to]);
        else
        {
            list.RemoveAt(from);
            list.Insert(to, item);
        }
        if (observed is null) Sync();
        reordered.Invoke(this, new SortableReorderedEventArgs(item, from, to));
    }

    // —— 指针：在卡片上按下、拖动、松开（位置相对自己；WPF 的测试里模拟不了鼠标位置，测试直接调这几个） ——

    /// <summary>在卡片上按下：记下指针在卡片里的位置（影子卡按它跟着走）。</summary>
    internal void Press(string key, Point at)
    {
        if (!IsEnabled || model.Active is not null || !cards.TryGetValue(key, out var card)) return;
        press = (key, at, at - card.TranslatePoint(default, this), card.ActualWidth);
    }

    /// <summary>
    /// 拖到 at：挪开 3 以上才拿起、出影子卡；指针下的列是落点所在的列，位置是这一列里中线在指针上面的卡片数（不算被拖的那张）；
    /// 靠近边缘时自动滚动。
    /// </summary>
    internal void DragTo(string key, Point at)
    {
        if (press is not { } pressed || pressed.Key != key) return;
        if (model.Active is null)
        {
            if (Math.Abs(at.X - pressed.Start.X) + Math.Abs(at.Y - pressed.Start.Y) < 3) return;
            var item = Items.First(candidate => KeyOf(candidate) == key);
            ghost = new KanbanCard(this, key, item, ItemTemplate, LabelOf(item)) { IsHitTestVisible = false, Width = pressed.Width };
            ghost.Sync("", "", true, false, FontSize);
            ghost.Lift();
            overlay.Children.Add(ghost);
            model.Grab(key);
        }
        Canvas.SetLeft(ghost!, at.X - pressed.Grip.X);
        Canvas.SetTop(ghost!, at.Y - pressed.Grip.Y);
        string? over = null;
        var index = 0;
        foreach (var (columnKey, view) in columns)
        {
            var corner = view.TranslatePoint(default, this);
            if (!new Rect(corner, view.RenderSize).Contains(at)) continue;
            over = columnKey;
            index = SortableModel<object>.IndexAt(view.List.Children.OfType<KanbanCard>().Where(card => card.Key != key)
                .Select(card => card.TranslatePoint(new Point(0, card.ActualHeight / 2), this).Y), at.Y);
            AutoScroll(view.Scroll, at, horizontal: false);
            break;
        }
        model.DragOver(key, over, index);
        AutoScroll(scroll, at, horizontal: true);
    }

    /// <summary>指针离滚动区域的边缘不到 40 时朝那边滚（离得越近滚得越快，每次最多 16）。</summary>
    void AutoScroll(ScrollViewer viewer, Point at, bool horizontal)
    {
        if (!viewer.IsDescendantOf(this)) return;
        var point = TranslatePoint(at, viewer);
        const double edge = 40, most = 16;
        var position = horizontal ? point.X : point.Y;
        var size = horizontal ? viewer.ViewportWidth : viewer.ViewportHeight;
        var step = position < edge ? -Math.Min(most, edge - position) : position > size - edge ? Math.Min(most, position - (size - edge)) : 0;
        if (step == 0) return;
        if (horizontal) viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset + step);
        else viewer.ScrollToVerticalOffset(viewer.VerticalOffset + step);
    }

    /// <summary>松开：收起影子卡，放下（别的列报告 Moved 或 Rejected）。</summary>
    internal void Release(string key)
    {
        if (press is not { } pressed || pressed.Key != key) return;
        press = null;
        HideGhost();
        if (model.Active == key) model.Drop(key);
    }

    void HideGhost()
    {
        if (ghost is null) return;
        overlay.Children.Remove(ghost);
        ghost = null;
    }

    /// <summary>把手上的按键：拿起 / 放下、换列、调整位置、取消；处理了返回 true（测试也从这里进来）。</summary>
    internal bool HandleKey(string key, Key pressed)
    {
        if (!IsEnabled) return false;
        if (pressed == Key.Escape && press?.Key == key && model.Active == key)
        {
            press = null;
            HideGhost();
            model.Cancel(key);
            return true;
        }
        SortableKey? mapped = pressed switch
        {
            Key.Space or Key.Enter => SortableKey.Toggle,
            Key.Escape => SortableKey.Escape,
            Key.Up => SortableKey.Up,
            Key.Down => SortableKey.Down,
            Key.Left => SortableKey.Left,
            Key.Right => SortableKey.Right,
            Key.Home => SortableKey.Home,
            Key.End => SortableKey.End,
            _ => null,
        };
        return mapped is { } value && model.Press(key, value, horizontal: false, FlowDirection == FlowDirection.RightToLeft);
    }

    internal void HandleBlur(string key)
    {
        if (press is null && !reordering) model.Blur(key);
    }

    sealed class GroupPeer(KanbanBoard owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(KanbanBoard);
    }
}

/// <summary>看板的一列：surfaceMuted 底、lg 圆角，列头（名称与卡片数）、不能放的原因、竖向滚动的卡片列表；拖到这一列时一圈强调色（不能放时 danger）。</summary>
public sealed class KanbanColumnView : Border
{
    readonly TextBlock title = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly Badge count = new() { Radius = Radius.Full };
    readonly TextBlock reason = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    readonly ContentPresenter emptyContent = new();

    internal KanbanColumnView()
    {
        DockPanel.SetDock(count, Dock.Right);
        Header = new DockPanel { Children = { count, title } };
        Scroll = new ScrollArea { Focusable = false, IsViewportFocusable = false, Content = List };
        Empty = new Border { Child = emptyContent };
        Indicator = new Border();
        DockPanel.SetDock(Header, Dock.Top);
        DockPanel.SetDock(reason, Dock.Top);
        Child = new DockPanel { Children = { Header, reason, Scroll } };
    }

    /// <summary>列头。</summary>
    internal DockPanel Header { get; }

    /// <summary>卡片列表（与插入线、空列提示）。</summary>
    internal StackPanel List { get; } = new();

    /// <summary>列里的滚动区域。</summary>
    internal ScrollArea Scroll { get; }

    /// <summary>空列的提示。</summary>
    internal Border Empty { get; }

    /// <summary>拖到这一列时的落点：卡片之间的一条线。</summary>
    internal Border Indicator { get; }

    /// <summary>卡片数（测试用）。</summary>
    internal Badge CountPart => count;

    /// <summary>不能放的原因（测试用）。</summary>
    internal TextBlock ReasonPart => reason;

    /// <summary>拖到了这一列时：能放 allowed、不能放 denied；不是落点时 null（上游的 data-drop，测试用）。</summary>
    internal string? DropState { get; private set; }

    internal void Sync(KanbanColumn column, int cards, string? why, bool over, object empty, double em, double maxHeight)
    {
        double Space(string key) => this.Resource<double>(key) * em;
        var sm = em * this.Resource<double>(ThemeKeys.FontSize.Sm) / this.Resource<double>(ThemeKeys.FontSize.Md);
        var xs = em * this.Resource<double>(ThemeKeys.FontSize.Xs) / this.Resource<double>(ThemeKeys.FontSize.Md);
        DropState = over ? why is null ? "allowed" : "denied" : null;
        title.Text = column.Label;
        title.FontSize = sm;
        title.FontWeight = this.Resource<FontWeight>(ThemeKeys.FontWeight.Medium);
        AutomationProperties.SetName(this, column.Label);
        count.Content = cards.ToString(System.Globalization.CultureInfo.InvariantCulture);
        count.Tone = cards > 0 ? column.Tone ?? StatusTone.Neutral : StatusTone.Neutral;
        Header.Margin = new Thickness(Space(ThemeKeys.Space.Md), Space(ThemeKeys.Space.Sm), Space(ThemeKeys.Space.Md), Space(ThemeKeys.Space.Sm));
        reason.Text = why;
        reason.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
        reason.FontSize = xs;
        reason.Margin = new Thickness(Space(ThemeKeys.Space.Md), 0, Space(ThemeKeys.Space.Md), Space(ThemeKeys.Space.Xs));
        reason.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.Brush.DangerText);
        // 卡片之间 sm：除了第一个，每个子元素上面留 sm（StackPanel 没有间距属性）
        var gap = Space(ThemeKeys.Space.Sm);
        for (var index = 0; index < List.Children.Count; index++)
            if (List.Children[index] is FrameworkElement child && child != Indicator) child.Margin = new Thickness(0, index == 0 ? 0 : gap, 0, 0);
        List.Margin = new Thickness(Space(ThemeKeys.Space.Sm), 0, Space(ThemeKeys.Space.Sm), Space(ThemeKeys.Space.Sm));
        List.MinHeight = 3 * this.Resource<double>(ThemeKeys.FontSize.Md);
        Scroll.MaxHeight = maxHeight;
        if (empty is string text)
        {
            var note = new TextBlock { Text = text, FontSize = xs, HorizontalAlignment = HorizontalAlignment.Center };
            note.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.Brush.TextMuted);
            emptyContent.Content = note;
        }
        else emptyContent.Content = empty;
        Empty.Padding = new Thickness(0, Space(ThemeKeys.Space.Sm), 0, Space(ThemeKeys.Space.Sm));
        Indicator.Height = 2;
        Indicator.CornerRadius = new CornerRadius(1);
        Indicator.Margin = new Thickness(0, gap - Space(ThemeKeys.Space.Xs), 0, -Space(ThemeKeys.Space.Xs));
        Indicator.SetResourceReference(Border.BackgroundProperty, why is null ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.Danger);
        CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Lg) * em);
        SetResourceReference(BackgroundProperty, ThemeKeys.Brush.SurfaceMuted);
        BorderThickness = new Thickness(2);
        if (over) SetResourceReference(BorderBrushProperty, why is null ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.Danger);
        else BorderBrush = Brushes.Transparent;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ColumnPeer(this);

    sealed class ColumnPeer(KanbanColumnView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(KanbanColumnView);
    }
}

/// <summary>看板的一张卡片：surfaceRaised 底、border 细边、md 圆角、四周 sm；内容在左，把手在右上。键盘拿起时强调色边框与 md 阴影，指针拖动时原位变淡。</summary>
public sealed class KanbanCard : System.Windows.Controls.Grid
{
    readonly KanbanBoard board;
    // 阴影画在单独的一层上：DropShadowEffect 套在正文上会连文字一起投影（同 Card）
    readonly Border shadow = new();
    readonly Border body = new();
    readonly ButtonBase handle = new() { Ripple = false, VerticalAlignment = VerticalAlignment.Top };

    internal KanbanCard(KanbanBoard board, string key, object item, DataTemplate? template, string label)
    {
        this.board = board;
        Key = key;
        handle.Content = new Icon { Data = Lucide.GripVertical, Tone = TextTone.Muted };
        DockPanel.SetDock(handle, Dock.Right);
        var content = new ContentPresenter { Content = template is null ? label : item, ContentTemplate = template };
        body.Child = new DockPanel { Children = { handle, content } };
        Children.Add(shadow);
        Children.Add(body);
        Cursor = Cursors.Hand;
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            // 点在卡片里的按钮、输入框上不算拖动（把手除外）
            if (e.OriginalSource is DependencyObject source && Interactive(source)) return;
            board.Press(Key, e.GetPosition(board));
            CaptureMouse();
        };
        MouseMove += (_, e) =>
        {
            if (IsMouseCaptured) board.DragTo(Key, e.GetPosition(board));
        };
        MouseLeftButtonUp += (_, _) => ReleaseMouseCapture();
        LostMouseCapture += (_, _) => board.Release(Key);
        handle.PreviewKeyDown += (_, e) => e.Handled |= board.HandleKey(Key, e.Key);
        handle.LostKeyboardFocus += (_, _) => board.HandleBlur(Key);
        handle.MouseEnter += (_, _) => PaintHandle();
        handle.MouseLeave += (_, _) => PaintHandle();
    }

    /// <summary>卡片的 key。</summary>
    public string Key { get; }

    /// <summary>把手（测试用）。</summary>
    internal ButtonBase Handle => handle;

    bool Interactive(DependencyObject source)
    {
        for (var at = source; at is not null && at != this; at = VisualTreeHelper.GetParent(at))
            if (at == handle) return false;
            else if (at is System.Windows.Controls.Primitives.ButtonBase or TextBox or ComboBox) return true;
        return false;
    }

    internal void Sync(string name, string instructions, bool active, bool placeholder, double em)
    {
        AutomationProperties.SetName(handle, name);
        AutomationProperties.SetHelpText(handle, instructions);
        var sm = this.Resource<double>(ThemeKeys.Space.Sm) * em;
        body.Padding = new Thickness(sm);
        body.CornerRadius = shadow.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Md) * em);
        body.BorderThickness = new Thickness(1);
        var xxs = this.Resource<double>(ThemeKeys.Space.Xxs) * em;
        handle.Padding = new Thickness(xxs);
        handle.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * em, 0, 0, 0);
        handle.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        body.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.SurfaceRaised);
        shadow.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.SurfaceRaised);
        var lifted = active && !placeholder;
        body.SetResourceReference(Border.BorderBrushProperty, lifted ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.Border);
        if (lifted) shadow.SetResourceReference(EffectProperty, ThemeKeys.Shadow.Md);
        else shadow.ClearValue(EffectProperty);
        Opacity = placeholder ? 0.4 : 1;
        PaintHandle();
    }

    /// <summary>影子卡：lg 阴影、转 1.5°。</summary>
    internal void Lift()
    {
        shadow.SetResourceReference(EffectProperty, ThemeKeys.Shadow.Lg);
        RenderTransform = new RotateTransform(1.5);
    }

    void PaintHandle()
    {
        if (handle.IsMouseOver) handle.SetResourceReference(Control.BackgroundProperty, ThemeKeys.Brush.FillHover);
        else handle.Background = Brushes.Transparent;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    sealed class CardPeer(KanbanCard owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(KanbanCard);
    }
}

/// <summary><see cref="KanbanBoard.Moved"/> 的参数。</summary>
/// <param name="item">卡片的数据。</param>
/// <param name="to">放到的列。</param>
/// <param name="index">在目标列里的位置。</param>
public sealed class KanbanMovedEventArgs(object item, string to, int index) : EventArgs
{
    /// <summary>卡片的数据。</summary>
    public object Item { get; } = item;

    /// <summary>放到的列。</summary>
    public string To { get; } = to;

    /// <summary>在目标列里的位置。</summary>
    public int Index { get; } = index;
}

/// <summary><see cref="KanbanBoard.Rejected"/> 的参数。</summary>
/// <param name="item">卡片的数据。</param>
/// <param name="to">想放到的列。</param>
/// <param name="reason">不能放的原因。</param>
public sealed class KanbanRejectedEventArgs(object item, string to, string reason) : EventArgs
{
    /// <summary>卡片的数据。</summary>
    public object Item { get; } = item;

    /// <summary>想放到的列。</summary>
    public string To { get; } = to;

    /// <summary>不能放的原因。</summary>
    public string Reason { get; } = reason;
}
