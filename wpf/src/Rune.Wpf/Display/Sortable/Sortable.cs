using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 可拖动排序的列表（vue-tsx-pro 的 Sortable）：每项前有一个把手，拖动或用键盘改变顺序。
/// <code>
/// &lt;rune:Sortable ItemsSource="{Binding Tasks}" GetKey="…" GetLabel="…" ItemTemplate="{StaticResource TaskRow}" AutomationProperties.Name="待办" /&gt;
/// </code>
/// <list type="bullet">
/// <item>数据：ItemsSource 是可改的列表（ObservableCollection 用它的 Move，别的 IList 删了再插），顺序变了就地改并发 <see cref="Reordered"/>（上游的 _items 写回新数组）。</item>
/// <item>指针：在把手上按下拖动（挪开 3 以上才开始），越过别的项的中线时立即重排，被拖的项跟着指针，拖到外层滚动区域的边缘时自动滚动；Esc 取消。</item>
/// <item>键盘：聚焦把手，空格 / Enter 拿起，方向键（竖排 ↑↓，横排 ←→）移动一位，Home / End 移到两端，再按空格放下，Esc 取消；每一步读屏都会播报。</item>
/// </list>
/// 列表自己不滚动：项多时在外面包 ScrollArea。把手是按钮：名称“移动 项名”，说明是操作方法。
/// </summary>
public class Sortable : CompositeControl
{
    /// <summary>各项（可改的列表）。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IList), typeof(Sortable),
        new PropertyMetadata(null, (sender, e) => ((Sortable)sender).Observe((IList?)e.NewValue)));

    /// <summary>项的 key，同一列表里唯一（必填）。</summary>
    public static readonly DependencyProperty GetKeyProperty = DependencyProperty.Register(nameof(GetKey), typeof(Func<object, string>), typeof(Sortable),
        new PropertyMetadata(null, (sender, _) => ((Sortable)sender).Rebuild()));

    /// <summary>项的名称：把手的名称与读屏播报用它；不写时是 key。</summary>
    public static readonly DependencyProperty GetLabelProperty = DependencyProperty.Register(nameof(GetLabel), typeof(Func<object, string>), typeof(Sortable),
        new PropertyMetadata(null, (sender, _) => ((Sortable)sender).Rebuild()));

    /// <summary>排列方向，默认竖排。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(Sortable),
        new PropertyMetadata(Orientation.Vertical, (sender, _) => ((Sortable)sender).Sync()));

    /// <summary>每项内容的数据模板（TS 的 children）。</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(Sortable),
        new PropertyMetadata(null, (sender, _) => ((Sortable)sender).Rebuild()));

    readonly StackPanel panel = new();
    readonly LiveRegion live = new();
    // 按 key 留着每一项：重排时挪动它们而不是重建，键盘焦点留在被移动的把手上
    readonly Dictionary<string, SortableItem> rows = [];
    readonly SortableModel<object> model;
    INotifyCollectionChanged? observed;
    // 重排时挪动子控件会让把手失焦：这期间的失焦不算放下
    bool reordering;
    // 按下时：哪一项、指针的位置、这一项在排列方向上的起点、别的项的中线
    (string Key, Point Start, double RowStart, IReadOnlyList<double> Centers)? press;

    static Sortable() => IsEnabledProperty.OverrideMetadata(typeof(Sortable), new FrameworkPropertyMetadata(true, (sender, _) => ((Sortable)sender).Sync()));

    /// <summary>新建：竖排、间距 xs。</summary>
    public Sortable()
    {
        model = new SortableModel<object>(() => ItemsSource?.Cast<object>().ToList() ?? [], KeyOf, () => Provider.GetLang(this))
        {
            GetLabel = LabelOf,
            Reordered = Apply,
        };
        model.Changed += () =>
        {
            live.Announce(model.Announcement);
            foreach (var (key, row) in rows) row.IsActive = model.Active == key;
        };
        AutomationProperties.SetLiveSetting(live, AutomationLiveSetting.Assertive);
        Root = new System.Windows.Controls.Grid { Children = { panel, live } };
        Loaded += (_, _) => Sync();
    }

    /// <summary>各项。</summary>
    public IList? ItemsSource { get => (IList?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>项的 key。</summary>
    public Func<object, string>? GetKey { get => (Func<object, string>?)GetValue(GetKeyProperty); set => SetValue(GetKeyProperty, value); }

    /// <summary>项的名称。</summary>
    public Func<object, string>? GetLabel { get => (Func<object, string>?)GetValue(GetLabelProperty); set => SetValue(GetLabelProperty, value); }

    /// <summary>排列方向。</summary>
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>每项内容的数据模板。</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>顺序变了（拖动、键盘）：从第几位移到第几位，ItemsSource 已经改好。</summary>
    public event EventHandler<SortableReorderedEventArgs>? Reordered;

    /// <summary>各项（测试用），按显示的顺序。</summary>
    internal IReadOnlyList<SortableItem> Items => [.. panel.Children.OfType<SortableItem>()];

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced => live.Text;

    /// <summary>正被拿起的项（测试用）。</summary>
    internal string? Active => model.Active;

    bool Horizontal => Orientation == Orientation.Horizontal;

    string KeyOf(object item) => GetKey?.Invoke(item) ?? item.ToString() ?? "";

    string LabelOf(object item) => GetLabel?.Invoke(item) ?? KeyOf(item);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    void Observe(IList? list)
    {
        if (observed is not null) observed.CollectionChanged -= OnCollectionChanged;
        observed = list as INotifyCollectionChanged;
        if (observed is not null) observed.CollectionChanged += OnCollectionChanged;
        Sync();
    }

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    void Rebuild()
    {
        panel.Children.Clear();
        rows.Clear();
        Sync();
    }

    /// <summary>按数据排出各项：已有的项按 key 留着，挪到新的位置；间距 xs。</summary>
    void Sync()
    {
        var items = ItemsSource?.Cast<object>().ToList() ?? [];
        var keys = new HashSet<string>();
        var ordered = new List<SortableItem>();
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * FontSize;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var key = KeyOf(item);
            keys.Add(key);
            if (!rows.TryGetValue(key, out var row)) rows[key] = row = new SortableItem(this, key);
            row.Bind(item, ItemTemplate, LabelOf(item));
            row.Sync(model.HandleName(key), model.Instructions, IsEnabled, model.Active == key);
            // 间距：除了最后一项，排列方向的末端留 xs
            row.Margin = index == items.Count - 1 ? default : Horizontal ? new Thickness(0, 0, xs, 0) : new Thickness(0, 0, 0, xs);
            AutomationProperties.SetPositionInSet(row, index + 1);
            AutomationProperties.SetSizeOfSet(row, items.Count);
            ordered.Add(row);
        }
        foreach (var key in rows.Keys.Where(key => !keys.Contains(key)).ToList()) rows.Remove(key);
        if (!panel.Children.OfType<SortableItem>().SequenceEqual(ordered))
        {
            var focused = Keyboard.FocusedElement as IInputElement;
            reordering = true;
            try
            {
                panel.Children.Clear();
                foreach (var row in ordered) panel.Children.Add(row);
                if (focused is not null && focused != Keyboard.FocusedElement) Keyboard.Focus(focused);
            }
            finally
            {
                reordering = false;
            }
        }
        panel.Orientation = Orientation;
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

    // —— 指针：把手上按下、拖动、松开（SortableItem 转过来；WPF 的测试里模拟不了鼠标位置，测试直接调这几个） ——

    /// <summary>在把手上按下（相对自己的位置）：记下这一项的起点与别的项的中线。</summary>
    internal void Press(string key, Point at)
    {
        if (!IsEnabled || model.Active is not null) return;
        var start = Position(rows[key]);
        var centers = Items.Where(other => other.Key != key)
            .Select(other => Horizontal ? Position(other) + other.ActualWidth / 2 : Position(other) + other.ActualHeight / 2).ToList();
        press = (key, at, start, centers);
    }

    double Position(SortableItem row)
    {
        var at = row.TranslatePoint(default, this);
        // 位置里去掉跟着指针的位移：要的是排版的位置
        return Horizontal ? at.X - row.Translate.X : at.Y - row.Translate.Y;
    }

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
        var shift = delta - (Position(row) - pressed.RowStart);
        row.Translate = Horizontal ? new Vector(shift, 0) : new Vector(0, shift);
        AutoScroll(at);
    }

    /// <summary>指针离外层滚动区域的边缘不到 40 时朝那边滚（离得越近滚得越快，每次最多 16）。</summary>
    void AutoScroll(Point at)
    {
        DependencyObject? parent = this;
        while (parent is not null and not ScrollViewer) parent = VisualTreeHelper.GetParent(parent);
        if (parent is not ScrollViewer viewer) return;
        const double edge = 40, most = 16;
        var point = TranslatePoint(at, viewer);
        var position = Horizontal ? point.X : point.Y;
        var size = Horizontal ? viewer.ViewportWidth : viewer.ViewportHeight;
        var step = position < edge ? -Math.Min(most, edge - position) : position > size - edge ? Math.Min(most, position - (size - edge)) : 0;
        if (step == 0) return;
        if (Horizontal) viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset + step);
        else viewer.ScrollToVerticalOffset(viewer.VerticalOffset + step);
    }

    /// <summary>松开：放下，被拖的项回到排版的位置。</summary>
    internal void Release(string key)
    {
        if (press is not { } pressed || pressed.Key != key) return;
        press = null;
        rows[key].Translate = default;
        if (model.Active == key) model.Drop(key);
    }

    /// <summary>把手上的按键：拿起 / 放下、移动、取消；处理了返回 true（测试也从这里进来）。</summary>
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

    sealed class ListPeer(Sortable owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => nameof(Sortable);
    }
}

/// <summary>Sortable 的一项：把手（按钮）与内容；拿起时浮起来（品牌色边框、md 阴影，盖在别的项上面）。</summary>
public sealed class SortableItem : System.Windows.Controls.Grid
{
    readonly Sortable owner;
    // 阴影画在单独的一层上：DropShadowEffect 套在正文上会连文字一起投影（同 Card）
    readonly Border shadow = new();
    readonly Border body = new();
    readonly ButtonBase handle = new() { Ripple = false, Cursor = Cursors.SizeAll };
    readonly ContentPresenter content = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TranslateTransform translate = new();

    internal SortableItem(Sortable owner, string key)
    {
        this.owner = owner;
        Key = key;
        handle.Content = new Icon { Data = Lucide.GripVertical, Tone = TextTone.Muted };
        handle.HorizontalContentAlignment = HorizontalAlignment.Center;
        handle.VerticalContentAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(handle, Dock.Left);
        body.Child = new DockPanel { Children = { handle, content } };
        Children.Add(shadow);
        Children.Add(body);
        RenderTransform = translate;
        handle.PreviewMouseLeftButtonDown += (_, e) =>
        {
            owner.Press(Key, e.GetPosition(owner));
            handle.CaptureMouse();
            e.Handled = true;
        };
        handle.MouseMove += (_, e) =>
        {
            if (handle.IsMouseCaptured) owner.DragTo(Key, e.GetPosition(owner));
        };
        handle.MouseLeftButtonUp += (_, _) => handle.ReleaseMouseCapture();
        handle.LostMouseCapture += (_, _) => owner.Release(Key);
        // 预览阶段先接：空格、Enter 交给排序，不当成按钮的点击
        handle.PreviewKeyDown += (_, e) => e.Handled |= owner.HandleKey(Key, e.Key);
        handle.LostKeyboardFocus += (_, _) => owner.HandleBlur(Key);
        handle.MouseEnter += (_, _) => Paint();
        handle.MouseLeave += (_, _) => Paint();
    }

    /// <summary>项的 key。</summary>
    public string Key { get; }

    /// <summary>把手（测试用）。</summary>
    internal ButtonBase Handle => handle;

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

    /// <summary>内容：数据模板，没有时是项的名称。</summary>
    internal void Bind(object item, DataTemplate? template, string label)
    {
        content.Content = template is null ? label : item;
        content.ContentTemplate = template;
    }

    /// <summary>名称、说明、可不可用；外观：surface 底、borderMuted 细边、md 圆角，上下 xs、起始边 xs（把手自带留白）、结束边 sm；把手 max(1.75em, 最小点击区)。</summary>
    internal void Sync(string name, string instructions, bool enabled, bool active)
    {
        AutomationProperties.SetName(handle, name);
        AutomationProperties.SetHelpText(handle, instructions);
        handle.IsEnabled = enabled;
        var em = owner.FontSize;
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * em;
        var sm = this.Resource<double>(ThemeKeys.Space.Sm) * em;
        body.Padding = new Thickness(xs, xs, sm, xs);
        body.CornerRadius = shadow.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Md) * em);
        body.BorderThickness = new Thickness(1);
        var side = Math.Max(1.75 * em, this.Resource<double>(ThemeKeys.Size.MinTarget));
        handle.Width = handle.Height = side;
        handle.Margin = new Thickness(0, 0, sm, 0);
        handle.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        IsActive = active;
    }

    void Paint()
    {
        body.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.Surface);
        body.SetResourceReference(Border.BorderBrushProperty, IsActive ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.BorderMuted);
        shadow.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.Surface);
        if (IsActive) shadow.SetResourceReference(EffectProperty, ThemeKeys.Shadow.Md);
        else shadow.ClearValue(EffectProperty);
        Panel.SetZIndex(this, IsActive ? 1 : 0);
        if (handle.IsMouseOver && handle.IsEnabled) handle.SetResourceReference(Control.BackgroundProperty, ThemeKeys.Brush.FillHover);
        else handle.Background = Brushes.Transparent;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ItemPeer(this);

    sealed class ItemPeer(SortableItem owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(SortableItem);
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
