using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 可拖动排序的列表（vue-tsx-pro 的 Sortable）：每项前有一个把手，拖动或用键盘改变顺序。
/// <code>
/// &lt;rune:Sortable ItemsSource="{Binding Tasks}" GetKey="…" GetLabel="…" AutomationProperties.Name="待办"&gt;
///     &lt;rune:Sortable.ItemTemplate&gt;&lt;DataTemplate&gt;&lt;TextBlock Text="{Binding Title}" /&gt;&lt;/DataTemplate&gt;&lt;/rune:Sortable.ItemTemplate&gt;
/// &lt;/rune:Sortable&gt;
/// </code>
/// <list type="bullet">
/// <item>数据：ItemsSource 是可改的列表（ObservableCollection 用它的 Move，别的 IList 删了再插），顺序变了就地改并发 <see cref="Reordered"/>（上游的 _items 写回新数组）。</item>
/// <item>指针：在把手上按下拖动（挪开 3 以上才开始），越过别的项的中线时立即重排，被拖的项跟着指针；Esc 取消、回到原来的位置。</item>
/// <item>键盘：聚焦把手，空格 / Enter 拿起，方向键（竖排 ↑↓，横排 ←→）移动一位，Home / End 移到两端，再按空格放下，Esc 取消；每一步读屏都会播报。</item>
/// </list>
/// 列表自己不滚动：项多时在外面包 ScrollArea，拖到边缘时自动滚动。把手是按钮：名称“移动 项名”，说明是操作方法。部件名在 <see cref="SortableParts"/>。
/// </summary>
public class Sortable : Control
{
    /// <summary>各项（可改的列表）。</summary>
    public static readonly StyledProperty<IList?> ItemsSourceProperty = AvaloniaProperty.Register<Sortable, IList?>(nameof(ItemsSource));

    /// <summary>项的 key，同一列表里唯一（必填）。</summary>
    public static readonly StyledProperty<Func<object, string>?> GetKeyProperty = AvaloniaProperty.Register<Sortable, Func<object, string>?>(nameof(GetKey));

    /// <summary>项的名称：把手的名称与读屏播报用它；不写时是 key。</summary>
    public static readonly StyledProperty<Func<object, string>?> GetLabelProperty = AvaloniaProperty.Register<Sortable, Func<object, string>?>(nameof(GetLabel));

    /// <summary>排列方向，默认竖排。</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<Sortable, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>每项内容的数据模板（TS 的 children）。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<Sortable, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>字号：间距、把手按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = global::Avalonia.Controls.Documents.TextElement.FontSizeProperty.AddOwner<Sortable>();

    readonly StackPanel panel = new() { Name = SortableParts.List };
    readonly LiveRegion live = new();
    // 按 key 留着每一项：重排时挪动它们而不是重建，键盘焦点留在被移动的把手上
    readonly Dictionary<string, SortableItem> rows = [];
    readonly SortableModel<object> model;
    INotifyCollectionChanged? observed;
    // 重排时挪动子控件会让把手失焦：这期间的失焦不算放下
    bool reordering;
    // 按下时：哪一项、指针的位置、这一项在排列方向上的起点、别的项的中线
    (string Key, Point Start, double RowStart, IReadOnlyList<double> Centers)? press;

    /// <summary>新建：竖排、间距 xs。</summary>
    public Sortable()
    {
        model = new SortableModel<object>(() => ItemsSource?.Cast<object>().ToList() ?? [], item => GetKey?.Invoke(item) ?? item.ToString() ?? "", () => Provider.GetLang(this))
        {
            GetLabel = item => GetLabel?.Invoke(item) ?? GetKey?.Invoke(item) ?? item.ToString() ?? "",
            Reordered = Apply,
        };
        model.Changed += () =>
        {
            live.Announce(model.Announcement);
            foreach (var (key, row) in rows) row.IsActive = model.Active == key;
        };
        AutomationProperties.SetLiveSetting(live, AutomationLiveSetting.Assertive);
        AutomationProperties.SetAccessibilityView(live, AccessibilityView.Raw);
        LogicalChildren.AddRange([panel, live]);
        VisualChildren.AddRange([panel, live]);
    }

    /// <summary>各项。</summary>
    public IList? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>项的 key。</summary>
    public Func<object, string>? GetKey { get => GetValue(GetKeyProperty); set => SetValue(GetKeyProperty, value); }

    /// <summary>项的名称。</summary>
    public Func<object, string>? GetLabel { get => GetValue(GetLabelProperty); set => SetValue(GetLabelProperty, value); }

    /// <summary>排列方向。</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>每项内容的数据模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>顺序变了（拖动、键盘）：从第几位移到第几位，ItemsSource 已经改好。</summary>
    public event EventHandler<SortableReorderedEventArgs>? Reordered;

    /// <summary>各项（测试用），按显示的顺序。</summary>
    internal IReadOnlyList<SortableItem> Items => [.. panel.Children.OfType<SortableItem>()];

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced => live.Text;

    /// <summary>正被拿起的项（测试用）。</summary>
    internal string? Active => model.Active;

    bool Horizontal => Orientation == Orientation.Horizontal;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            if (observed is not null) observed.CollectionChanged -= OnCollectionChanged;
            observed = ItemsSource as INotifyCollectionChanged;
            if (observed is not null) observed.CollectionChanged += OnCollectionChanged;
            Sync();
        }
        else if (change.Property == GetKeyProperty || change.Property == GetLabelProperty || change.Property == ItemTemplateProperty)
        {
            foreach (var row in rows.Values) row.Detach();
            rows.Clear();
            Sync();
        }
        else if (change.Property == OrientationProperty || change.Property == FontSizeProperty || change.Property == IsEnabledProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(global::Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Sync();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    /// <summary>按数据排出各项：已有的项按 key 留着，挪到新的位置；间距 xs，横排时不收缩。</summary>
    void Sync()
    {
        var items = ItemsSource?.Cast<object>().ToList() ?? [];
        var keys = new HashSet<string>();
        var ordered = new List<SortableItem>();
        foreach (var item in items)
        {
            var key = GetKey?.Invoke(item) ?? item.ToString() ?? "";
            keys.Add(key);
            if (!rows.TryGetValue(key, out var row)) rows[key] = row = new SortableItem(this, key);
            row.Item = item;
            row.Content = ItemTemplate is { } template ? template.Build(item) : new TextBlock { Text = GetLabel?.Invoke(item) ?? key };
            row.Sync(model.HandleName(key), model.Instructions, IsEnabled, model.Active == key);
            ordered.Add(row);
        }
        foreach (var key in rows.Keys.Where(key => !keys.Contains(key)).ToList())
        {
            rows[key].Detach();
            rows.Remove(key);
        }
        // 只在顺序真的变了时挪：挪动会让把手失焦
        if (!panel.Children.OfType<SortableItem>().SequenceEqual(ordered))
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            reordering = true;
            try
            {
                panel.Children.Clear();
                panel.Children.AddRange(ordered);
                focused?.Focus();
            }
            finally
            {
                reordering = false;
            }
        }
        panel.Orientation = Orientation;
        panel.Spacing = this.Resource<double>(ThemeKeys.Space.Xs) * FontSize;
        for (var index = 0; index < ordered.Count; index++)
        {
            AutomationProperties.SetPositionInSet(ordered[index], index + 1);
            AutomationProperties.SetSizeOfSet(ordered[index], ordered.Count);
        }
    }

    /// <summary>模型重排了：改 ItemsSource（ObservableCollection 用 Move，别的列表删了再插）并报告。</summary>
    void Apply(IReadOnlyList<object> next, object item)
    {
        if (ItemsSource is not { } list) return;
        var (from, to) = (list.IndexOf(item), next.ToList().IndexOf(item));
        if (list.GetType().GetMethod("Move", [typeof(int), typeof(int)]) is { } move) move.Invoke(list, [from, to]);
        else
        {
            list.RemoveAt(from);
            list.Insert(to, item);
        }
        if (observed is null) Sync();
        Reordered?.Invoke(this, new SortableReorderedEventArgs(item, from, to));
    }

    // —— 指针：把手上按下、拖动、松开（SortableItem 转过来） ——

    internal void HandlePressed(string key, PointerPressedEventArgs e)
    {
        if (!IsEnabled || model.Active is not null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Press(key, e.GetPosition(this));
        e.Pointer.Capture(rows[key].Handle);
        e.Handled = true;
    }

    /// <summary>在把手上按下（相对自己的位置；测试也从这里进来）：记下这一项的起点与别的项的中线。</summary>
    internal void Press(string key, Point at)
    {
        var row = rows[key];
        var centers = Items.Where(other => other.Key != key)
            .Select(other => Horizontal ? other.Bounds.X + other.Bounds.Width / 2 : other.Bounds.Y + other.Bounds.Height / 2).ToList();
        press = (key, at, Horizontal ? row.Bounds.X : row.Bounds.Y, centers);
    }

    internal void HandleMoved(string key, PointerEventArgs e) => DragTo(key, e.GetPosition(this));

    /// <summary>拖到 at（相对自己）：挪开 3 以上才拿起；指针越过了几个别的项的中线就排到第几位，被拖的项跟着指针。</summary>
    internal void DragTo(string key, Point at)
    {
        if (press is not { } pressed || pressed.Key != key) return;
        if (model.Active is null)
        {
            if (Math.Abs(at.X - pressed.Start.X) + Math.Abs(at.Y - pressed.Start.Y) < 3) return;
            model.Grab(key);
        }
        var delta = Horizontal ? at.X - pressed.Start.X : at.Y - pressed.Start.Y;
        model.DragOver(key, "", SortableModel<object>.IndexAt(pressed.Centers, Horizontal ? at.X : at.Y));
        UpdateLayout();
        // 跟着指针：位移 = 指针挪开的距离 − 它在排版里的位置变化
        var row = rows[key];
        var shift = delta - ((Horizontal ? row.Bounds.X : row.Bounds.Y) - pressed.RowStart);
        row.Translate = Horizontal ? new Vector(shift, 0) : new Vector(0, shift);
        AutoScroll(at);
    }

    /// <summary>指针离外层滚动区域的边缘不到 40 时朝那边滚（离得越近滚得越快，每次最多 16）。</summary>
    void AutoScroll(Point at)
    {
        if (this.FindAncestorOfType<ScrollViewer>() is not { } viewer || this.TranslatePoint(at, viewer) is not { } point) return;
        const double edge = 40, most = 16;
        var position = Horizontal ? point.X : point.Y;
        var size = Horizontal ? viewer.Viewport.Width : viewer.Viewport.Height;
        var step = position < edge ? -Math.Min(most, edge - position) : position > size - edge ? Math.Min(most, position - (size - edge)) : 0;
        if (step != 0) viewer.Offset = Horizontal ? viewer.Offset.WithX(viewer.Offset.X + step) : viewer.Offset.WithY(viewer.Offset.Y + step);
    }

    /// <summary>松开：放下，被拖的项回到排版的位置。</summary>
    internal void Release(string key)
    {
        if (press is not { } pressed || pressed.Key != key) return;
        press = null;
        rows[key].Translate = default;
        if (model.Active == key) model.Drop(key);
    }

    // —— 键盘：把手转过来 ——

    internal bool HandleKey(string key, Key pressed)
    {
        if (!IsEnabled) return false;
        // 指针拖着时 Esc：取消，回到原来的位置
        if (pressed == Key.Escape && press?.Key == key && model.Active == key)
        {
            press = null;
            rows[key].Translate = default;
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
        return mapped is { } value && model.Press(key, value, Horizontal, FlowDirection == FlowDirection.RightToLeft);
    }

    internal void HandleBlur(string key)
    {
        if (press is null && !reordering) model.Blur(key);
    }

    sealed class ListPeer(Sortable owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}

/// <summary>Sortable 的一项：把手（按钮）与内容；拿起时浮起来（品牌色边框、md 阴影，盖在别的项上面）。</summary>
public sealed class SortableItem : Border
{
    readonly Sortable owner;
    readonly ButtonBase handle = new() { Name = SortableParts.Handle, Ripple = false, Focusable = true };
    readonly ContentPresenter content = new() { Name = SortableParts.Content };
    readonly TranslateTransform translate = new();

    internal SortableItem(Sortable owner, string key)
    {
        this.owner = owner;
        Key = key;
        Name = SortableParts.Item;
        handle.Content = new Icon { Data = Lucide.GripVertical, Tone = TextTone.Muted };
        handle.Cursor = new Cursor(StandardCursorType.SizeAll);
        DockPanel.SetDock(handle, Dock.Left);
        Child = new DockPanel { Children = { handle, content } };
        RenderTransform = translate;
        handle.AddHandler(PointerPressedEvent, (_, e) => owner.HandlePressed(Key, e), handledEventsToo: true);
        handle.PointerMoved += (_, e) => owner.HandleMoved(Key, e);
        handle.PointerReleased += (_, _) => owner.Release(Key);
        handle.PointerCaptureLost += (_, _) => owner.Release(Key);
        // 隧道阶段先接：空格、Enter 交给排序，不当成按钮的点击
        handle.AddHandler(KeyDownEvent, (_, e) => e.Handled |= owner.HandleKey(Key, e.Key), global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        handle.LostFocus += (_, _) => owner.HandleBlur(Key);
        handle.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsPointerOverProperty) Paint();
        };
    }

    /// <summary>项的 key。</summary>
    public string Key { get; }

    /// <summary>项的数据。</summary>
    public object? Item { get; internal set; }

    /// <summary>把手（测试用）。</summary>
    internal ButtonBase Handle => handle;

    /// <summary>内容。</summary>
    internal Control? Content { get => content.Content as Control; set => content.Content = value; }

    /// <summary>被拖着时跟着指针的位移。</summary>
    internal Vector Translate
    {
        get => new(translate.X, translate.Y);
        set => (translate.X, translate.Y) = (value.X, value.Y);
    }

    /// <summary>正被拿起。</summary>
    internal bool IsActive
    {
        get;
        set
        {
            field = value;
            Paint();
        }
    }

    internal void Detach() => (Parent as Panel)?.Children.Remove(this);

    /// <summary>名称、说明、可不可用；外观：surface 底、borderMuted 细边、md 圆角，上下 xs、起始边 xs（把手自带留白）、结束边 sm；把手 max(1.75em, 最小点击区)。</summary>
    internal void Sync(string name, string instructions, bool enabled, bool active)
    {
        AutomationProperties.SetName(handle, name);
        AutomationProperties.SetHelpText(handle, instructions);
        handle.IsEnabled = enabled;
        var em = owner.FontSize;
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * em;
        var sm = this.Resource<double>(ThemeKeys.Space.Sm) * em;
        Padding = new Thickness(xs, xs, sm, xs);
        CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Md) * em);
        BorderThickness = new Thickness(1);
        var side = Math.Max(1.75 * em, this.Resource<double>(ThemeKeys.Size.MinTarget));
        handle.Width = handle.Height = side;
        handle.Margin = new Thickness(0, 0, sm, 0);
        handle.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        handle.HorizontalContentAlignment = HorizontalAlignment.Center;
        handle.VerticalContentAlignment = VerticalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        IsActive = active;
    }

    void Paint()
    {
        Bind(BackgroundProperty, this.GetResourceObservable(ThemeKeys.Brush.Surface));
        Bind(BorderBrushProperty, this.GetResourceObservable(IsActive ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.BorderMuted));
        BoxShadow = IsActive ? this.Resource<BoxShadows>(ThemeKeys.Shadow.Md) : default;
        ZIndex = IsActive ? 1 : 0;
        // 把手悬停时 fillHover 底
        if (handle.IsPointerOver) handle.Bind(ButtonBase.BackgroundProperty, handle.GetResourceObservable(ThemeKeys.Brush.FillHover));
        else handle.Background = Brushes.Transparent;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ItemPeer(this);

    sealed class ItemPeer(SortableItem owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    }
}

/// <summary><see cref="Sortable.Reordered"/> 的参数。</summary>
/// <param name="item">被移动的项。</param>
/// <param name="from">原来的位置。</param>
/// <param name="to">新的位置。</param>
public sealed class SortableReorderedEventArgs(object item, int from, int to) : EventArgs
{
    /// <summary>被移动的项。</summary>
    public object Item { get; } = item;

    /// <summary>原来的位置。</summary>
    public int From { get; } = from;

    /// <summary>新的位置。</summary>
    public int To { get; } = to;
}

/// <summary>Sortable 的部件名（上游的 data-part）。</summary>
public static class SortableParts
{
    /// <summary>项的列表。</summary>
    public const string List = "PART_List";

    /// <summary>一项。</summary>
    public const string Item = "PART_Item";

    /// <summary>把手。</summary>
    public const string Handle = "PART_Handle";

    /// <summary>内容。</summary>
    public const string Content = "PART_Content";
}
