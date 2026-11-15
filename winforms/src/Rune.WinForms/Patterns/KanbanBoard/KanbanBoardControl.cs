using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>看板的一列（vue-tsx-pro 的 KanbanColumn）。</summary>
/// <param name="Key">列的标识：GetColumn 返回它。</param>
/// <param name="Label">列名。</param>
/// <param name="Tone">列名旁计数的色调（超时、异常这类要注意的列）；不写时中性色。</param>
public sealed record KanbanColumn(string Key, string Label, StatusTone? Tone = null);

/// <summary>
/// 看板（vue-tsx-pro 的 KanbanBoard）：一列一个状态（或阶段、负责人），卡片在列之间移动。任务看板、招聘流程、工单。
/// <code>
/// var board = new KanbanBoardControl { Columns = columns, ItemsSource = tickets, GetKey = …, GetColumn = …, ItemFactory = ticket =&gt; new TicketCard((Ticket)ticket) };
/// board.Moved += (_, e) =&gt; OpenProcessDialog(e.Item, e.To);
/// </code>
/// <list type="bullet">
/// <item>数据由使用方控制：看板不自己挪卡片，只在放下时报告 <see cref="Moved"/>（使用方改了数据、调 <see cref="Refresh"/> 或换 ItemsSource，卡片才换列；ObservableCollection 的增删自动跟上）。</item>
/// <item><see cref="CanMove"/> 返回不能移动的原因（null 是可以）：拖动时经过的列按它高亮（能放的是强调色，不能放的标红并写出原因），落点处画一条插入线；
/// 放在不能放的列上时卡片弹回、读屏播报原因，并报告 <see cref="Rejected"/>。订阅了 <see cref="Reordered"/> 时列内也能排序（ItemsSource 就地改，同 SortableControl）。</item>
/// <item>指针：在卡片上按下拖动（挪开 3 以上才开始；点在卡片里的按钮、输入框上不算），影子卡跟着指针，原位是虚线框占位，拖到边缘时自动滚动。</item>
/// <item>键盘：每张卡片有把手按钮，空格 / Enter 拿起，← / → 换列（从右到左时对调），↑ / ↓ 调整位置，空格 / Enter 放下，Esc 取消；每一步都有读屏播报。</item>
/// </list>
/// 每张卡片的内容由 <see cref="ItemFactory"/> 建（TS 的 children）。列多时整块横向滚动，每列的卡片多时列自己竖向滚动。
/// 控件不能半透明、画不出阴影：原位的占位是虚线框（上游是变淡），影子卡没有阴影与倾斜。
/// </summary>
[DefaultEvent(nameof(Moved))]
[Description("看板：一列一个状态，卡片在列之间移动。")]
public class KanbanBoardControl : Control
{
    readonly ScrollAreaControl scroll = new() { Axis = ScrollAreaAxis.X, IsViewportFocusable = false };
    readonly ColumnsPanel board;
    readonly Dictionary<string, KanbanColumnControl> columns = [];
    // 按 key 留着每张卡片：换列、重排时挪动它们而不是重建，键盘焦点留在被移动的把手上
    readonly Dictionary<string, KanbanCardControl> cards = [];
    readonly SortableModel<object> model;
    INotifyCollectionChanged? observed;
    EventHandler<SortableReorderedEventArgs>? reordered;
    // 换列、重排时挪动卡片会让把手失焦：这期间的失焦不算放下
    bool reordering;
    // 按下时：哪张卡片、指针的位置（相对自己）、指针在卡片里的位置
    (string Key, Point Start, Size Grip)? press;
    KanbanCardControl? ghost;

    /// <summary>新建：横向滚动的一排列，按内容高（放进 FlexControl 时横向拉满）。</summary>
    public KanbanBoardControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        board = new ColumnsPanel(this);
        scroll.Content = board;
        Controls.Add(scroll);
        model = new SortableModel<object>(() => Items, KeyOf, () => ProviderControl.LangOf(this))
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
            Announced = model.Announcement;
            if (IsHandleCreated)
                AccessibilityObject.RaiseAutomationNotification(System.Windows.Forms.Automation.AutomationNotificationKind.Other,
                    System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent, model.Announcement);
            Sync();
        };
    }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => ManualLayout.Instance;

    /// <summary>全部卡片的数据。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource
    {
        get;
        set
        {
            if (observed is not null) observed.CollectionChanged -= OnCollectionChanged;
            field = value;
            observed = value as INotifyCollectionChanged;
            if (observed is not null) observed.CollectionChanged += OnCollectionChanged;
            Sync();
        }
    }

    /// <summary>各列：从起始边排。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<KanbanColumn> Columns { get; set { field = value; Sync(); } } = [];

    /// <summary>卡片的标识（必填）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, string>? GetKey { get; set { field = value; Rebuild(); } }

    /// <summary>卡片在哪一列（必填）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, string>? GetColumn { get; set { field = value; Sync(); } }

    /// <summary>卡片的名称：把手的名称、读屏播报；不写时是 key。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, string>? GetLabel { get; set { field = value; Sync(); } }

    /// <summary>能不能移到这一列：返回不能的原因，null 是可以。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, string, string?>? CanMove { get; set; }

    /// <summary>建一张卡片的内容（TS 的 children）；不写时是卡片的名称。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, Control>? ItemFactory { get; set { field = value; Rebuild(); } }

    /// <summary>每列至少多宽（逻辑像素，按 DPI 放大）：放得下时各列平分整行，放不下时横向滚动；0 是 16em。</summary>
    [Category("Layout")]
    [DefaultValue(0)]
    public int ColumnWidth { get; set { field = value; Sync(); } }

    /// <summary>每列的最大高度（逻辑像素）：卡片多时列里竖向滚动；0 是窗体高度的 70%。</summary>
    [Category("Layout")]
    [DefaultValue(0)]
    public int ColumnMaxHeight { get; set { field = value; Sync(); } }

    /// <summary>一列没有卡片时的文字；不写时是“没有”。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? EmptyText { get; set { field = value; Sync(); } }

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
    internal IReadOnlyDictionary<string, KanbanColumnControl> ColumnViews => columns;

    /// <summary>卡片（测试用）。</summary>
    internal IReadOnlyDictionary<string, KanbanCardControl> Cards => cards;

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced { get; private set; }

    /// <summary>正被拿起的卡片（测试用）。</summary>
    internal string? Active => model.Active;

    /// <summary>跟着指针的影子卡（测试用）。</summary>
    internal KanbanCardControl? Ghost => ghost;

    IReadOnlyList<object> Items => ItemsSource?.Cast<object>().ToList() ?? [];

    string KeyOf(object item) => GetKey?.Invoke(item) ?? item.ToString() ?? "";

    string LabelOf(object item) => GetLabel?.Invoke(item) ?? KeyOf(item);

    float Em => this.EmPixels();

    int Px(float space) => (int)MathF.Round(space * Em);

    /// <summary>数据在原处改了（卡片换了列）：重新分到各列。</summary>
    public override void Refresh()
    {
        Sync();
        base.Refresh();
    }

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    void Rebuild()
    {
        foreach (var card in cards.Values) card.Dispose();
        cards.Clear();
        Sync();
    }

    Control Build(object item) => ItemFactory?.Invoke(item) ?? new TextControl { Text = LabelOf(item), Wrap = TextWrap.Wrap };

    /// <summary>
    /// 按数据排出各列与卡片：列头是列名与卡片数（有卡片时按列的色调），拖到了这一列（不是来的那一列）时高亮（能放强调色、不能放标红并写出原因），
    /// 落点处是插入线；空列是“没有”。卡片按 key 留着，只换父控件与位置。
    /// </summary>
    void Sync()
    {
        if (model is null || board is null) return;
        var lang = ProviderControl.LangOf(this);
        var messages = PatternsMessages.For(lang);
        var items = Items;
        var active = model.Active;
        var target = model.Target;
        var from = active is null ? null : model.GroupOf(active);
        var keys = Columns.Select(column => column.Key).ToHashSet();
        foreach (var key in columns.Keys.Where(key => !keys.Contains(key)).ToList())
        {
            columns[key].Dispose();
            columns.Remove(key);
        }
        reordering = true;
        try
        {
            var seen = new HashSet<string>();
            foreach (var column in Columns)
            {
                if (!columns.TryGetValue(column.Key, out var view))
                {
                    columns[column.Key] = view = new KanbanColumnControl();
                    board.Controls.Add(view);
                }
                var inColumn = items.Where(item => GetColumn?.Invoke(item) == column.Key).ToList();
                var over = target is not null && from != column.Key && target.Group == column.Key;
                var ordered = new List<KanbanCardControl>();
                foreach (var item in inColumn)
                {
                    var key = KeyOf(item);
                    seen.Add(key);
                    if (!cards.TryGetValue(key, out var card)) cards[key] = card = new KanbanCardControl(this, key, Build(item));
                    card.Sync(lang.SortableHandle(LabelOf(item)), messages.KanbanInstructions, key == active, ghost is not null && key == active);
                    ordered.Add(card);
                }
                view.Sync(column, ordered, over ? target : null, EmptyText ?? messages.KanbanEmpty, ColumnHeight());
            }
            foreach (var key in cards.Keys.Where(key => !seen.Contains(key)).ToList())
            {
                cards[key].Dispose();
                cards.Remove(key);
            }
        }
        finally
        {
            reordering = false;
        }
        board.PerformLayout();
        Parent?.PerformLayout(this, nameof(Bounds));
        PerformLayout();
    }

    /// <summary>每列的最大高度：写了用它，不写时窗体高度的 70%（上游的 70vh）。</summary>
    int ColumnHeight() => ColumnMaxHeight > 0 ? LogicalToDeviceUnits(ColumnMaxHeight)
        : FindForm() is { ClientSize.Height: > 0 } form ? (int)(0.7 * form.ClientSize.Height) : int.MaxValue;

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

    /// <summary>滚动区域占满；影子卡在最上层。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        scroll.Bounds = ClientRectangle;
    }

    /// <summary>宽度跟着外层，高度是最高的一列加下方的 sm（横向滚动条的位置）。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : Width;
        return new Size(width, board.GetPreferredSize(new Size(width, 0)).Height);
    }

    // —— 指针：在卡片上按下、拖动、松开（位置相对自己；测试也从这里进来） ——

    /// <summary>在卡片上按下：记下指针在卡片里的位置（影子卡按它跟着走）。</summary>
    internal void Press(string key, Point at)
    {
        if (!Enabled || model.Active is not null || !cards.TryGetValue(key, out var card)) return;
        var origin = PointToClient(card.PointToScreen(Point.Empty));
        press = (key, at, new Size(at.X - origin.X, at.Y - origin.Y));
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
            if (Math.Abs(at.X - pressed.Start.X) + Math.Abs(at.Y - pressed.Start.Y) < LogicalToDeviceUnits(3)) return;
            var card = cards[key];
            ghost = new KanbanCardControl(this, key, Build(Items.First(item => KeyOf(item) == key))) { Size = card.Size, Enabled = false };
            ghost.Sync("", "", active: true, placeholder: false);
            Controls.Add(ghost);
            ghost.BringToFront();
            model.Grab(key);
        }
        ghost!.Location = new Point(at.X - pressed.Grip.Width, at.Y - pressed.Grip.Height);
        string? over = null;
        var index = 0;
        foreach (var (columnKey, view) in columns)
        {
            var bounds = RectangleToClient(view.RectangleToScreen(view.ClientRectangle));
            if (!bounds.Contains(at)) continue;
            over = columnKey;
            index = SortableModel<object>.IndexAt(view.Cards.Where(card => card.Key != key)
                .Select(card => (double)PointToClient(card.PointToScreen(new Point(0, card.Height / 2))).Y), at.Y);
            AutoScroll(view.Scroll, at, horizontal: false);
            break;
        }
        model.DragOver(key, over, index);
        AutoScroll(scroll, at, horizontal: true);
    }

    /// <summary>指针离滚动区域的边缘不到 40 时朝那边滚（离得越近滚得越快，每次最多 16）。</summary>
    void AutoScroll(ScrollAreaControl viewer, Point at, bool horizontal)
    {
        var point = viewer.PointToClient(PointToScreen(at));
        var (edge, most) = (LogicalToDeviceUnits(40), LogicalToDeviceUnits(16));
        var position = horizontal ? point.X : point.Y;
        var size = horizontal ? viewer.ClientSize.Width : viewer.ClientSize.Height;
        var step = position < edge ? -Math.Min(most, edge - position) : position > size - edge ? Math.Min(most, position - (size - edge)) : 0;
        if (step != 0) viewer.ScrollOffset = horizontal ? viewer.ScrollOffset with { X = viewer.ScrollOffset.X + step } : viewer.ScrollOffset with { Y = viewer.ScrollOffset.Y + step };
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
        ghost.Dispose();
        ghost = null;
    }

    /// <summary>把手上的按键：拿起 / 放下、换列、调整位置、取消；处理了返回 true。</summary>
    internal bool HandleKey(string key, Keys pressed)
    {
        if (!Enabled) return false;
        if (pressed == Keys.Escape && press?.Key == key && model.Active == key)
        {
            press = null;
            HideGhost();
            model.Cancel(key);
            return true;
        }
        SortableKey? mapped = pressed switch
        {
            Keys.Space or Keys.Enter => SortableKey.Toggle,
            Keys.Escape => SortableKey.Escape,
            Keys.Up => SortableKey.Up,
            Keys.Down => SortableKey.Down,
            Keys.Left => SortableKey.Left,
            Keys.Right => SortableKey.Right,
            Keys.Home => SortableKey.Home,
            Keys.End => SortableKey.End,
            _ => null,
        };
        return mapped is { } value && model.Press(key, value, horizontal: false, RightToLeft == RightToLeft.Yes);
    }

    internal void HandleBlur(string key)
    {
        if (press is null && !reordering) model.Blur(key);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }

    /// <summary>各列的横排：每列至少 ColumnWidth（默认 16em），富余的空间平分（同 Jira 的看板），放不下时外层横向滚动；列与列之间 md，下方留 sm。</summary>
    sealed class ColumnsPanel : Control
    {
        readonly KanbanBoardControl owner;

        public ColumnsPanel(KanbanBoardControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public override LayoutEngine LayoutEngine => ManualLayout.Instance;

        IReadOnlyList<KanbanColumnControl> Views => [.. owner.Columns.Select(column => owner.columns.GetValueOrDefault(column.Key)).OfType<KanbanColumnControl>()];

        int Gap => owner.Px(Tokens.Space.Md);

        int MinColumn => owner.ColumnWidth > 0 ? owner.LogicalToDeviceUnits(owner.ColumnWidth) : owner.Px(16);

        int ColumnWidthIn(int width) => Views.Count == 0 ? 0 : Math.Max(MinColumn, (width - Gap * (Views.Count - 1)) / Views.Count);

        public override Size GetPreferredSize(Size proposedSize)
        {
            var views = Views;
            var available = owner.scroll.ClientSize.Width > 0 ? owner.scroll.ClientSize.Width : proposedSize.Width;
            var column = ColumnWidthIn(available);
            var height = views.Count == 0 ? 0 : views.Max(view => view.GetPreferredSize(new Size(column, 0)).Height);
            return new Size(views.Count * column + Gap * Math.Max(0, views.Count - 1), height + owner.Px(Tokens.Space.Sm));
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var column = ColumnWidthIn(owner.scroll.ClientSize.Width);
            var x = 0;
            foreach (var view in Views)
            {
                view.Bounds = new Rectangle(x, 0, column, view.GetPreferredSize(new Size(column, 0)).Height);
                x += column + Gap;
            }
        }
    }
}

/// <summary>看板的一列：surfaceMuted 底、lg 圆角，列头（名称与卡片数）、不能放的原因、竖向滚动的卡片列表；拖到这一列时一圈强调色（不能放时 danger）。</summary>
sealed class KanbanColumnControl : Control
{
    readonly TextControl title = new() { TextSize = TextSize.Sm, Weight = TextWeight.Medium };
    readonly BadgeControl count = new() { Radius = Radius.Full };
    readonly TextControl reason = new() { TextSize = TextSize.Xs, Tone = TextTone.Danger, Wrap = TextWrap.Wrap, Visible = false };
    readonly ListPanel list;
    readonly TextControl empty = new() { TextSize = TextSize.Xs, Tone = TextTone.Muted, Align = TextAlign.Center };

    public KanbanColumnControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Grouping;
        list = new ListPanel(this);
        list.Controls.Add(empty);
        Scroll = new ScrollAreaControl { IsViewportFocusable = false, Content = list };
        Controls.AddRange([title, count, reason, Scroll]);
    }

    public override LayoutEngine LayoutEngine => ManualLayout.Instance;

    /// <summary>列里的滚动区域。</summary>
    public ScrollAreaControl Scroll { get; }

    /// <summary>按顺序的卡片。</summary>
    public IReadOnlyList<KanbanCardControl> Cards { get; private set; } = [];

    /// <summary>卡片数（测试用）。</summary>
    public BadgeControl CountPart => count;

    /// <summary>不能放的原因（测试用）。</summary>
    public TextControl ReasonPart => reason;

    /// <summary>空列的提示（测试用）。</summary>
    public TextControl EmptyPart => empty;

    /// <summary>拖到了这一列时：能放 allowed、不能放 denied；不是落点时 null（上游的 data-drop）。</summary>
    public string? DropState { get; private set; }

    /// <summary>插入线在第几张卡片前面；不是落点时为 null（测试用）。</summary>
    public int? IndicatorIndex { get; private set; }

    int MaxHeight { get; set; } = int.MaxValue;

    float Em => this.EmPixels();

    int Px(float space) => (int)MathF.Round(space * Em);

    public void Sync(KanbanColumn column, IReadOnlyList<KanbanCardControl> cards, SortableTarget? target, string emptyText, int maxHeight)
    {
        DropState = target is null ? null : target.Reason is null ? "allowed" : "denied";
        IndicatorIndex = target?.Index;
        title.Text = column.Label;
        AccessibleName = column.Label;
        count.Text = cards.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        count.Tone = cards.Count > 0 ? column.Tone ?? StatusTone.Neutral : StatusTone.Neutral;
        reason.Text = target?.Reason ?? "";
        reason.Visible = target?.Reason is not null;
        empty.Text = emptyText;
        empty.Visible = cards.Count == 0 && target is null;
        foreach (var card in cards.Where(card => card.Parent != list)) list.Controls.Add(card);
        Cards = cards;
        MaxHeight = maxHeight;
        list.PerformLayout();
        Scroll.PerformLayout();
        PerformLayout();
        Invalidate();
        list.Invalidate();
    }

    /// <summary>列头上下 sm、左右 md；原因左右 md、下方 xs；列表左右 sm、下方 sm，最高 MaxHeight。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
        var (header, reasonHeight) = Measure(width);
        var listHeight = Math.Min(list.GetPreferredSize(new Size(width - 2 * Px(Tokens.Space.Sm), 0)).Height, MaxHeight);
        return new Size(width, header + reasonHeight + listHeight + Px(Tokens.Space.Sm));
    }

    (int Header, int Reason) Measure(int width)
    {
        var (sm, md, xs) = (Px(Tokens.Space.Sm), Px(Tokens.Space.Md), Px(Tokens.Space.Xs));
        var header = Math.Max(title.GetPreferredSize(Size.Empty).Height, count.GetPreferredSize(Size.Empty).Height) + 2 * sm;
        var reasonHeight = reason.Visible ? reason.GetPreferredSize(new Size(width - 2 * md, 0)).Height + xs : 0;
        return (header, reasonHeight);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var (sm, md) = (Px(Tokens.Space.Sm), Px(Tokens.Space.Md));
        var (header, reasonHeight) = Measure(Width);
        var badge = count.GetPreferredSize(Size.Empty);
        count.Bounds = new Rectangle(Width - md - badge.Width, (header - badge.Height) / 2, badge.Width, badge.Height);
        var label = title.GetPreferredSize(Size.Empty);
        title.Bounds = new Rectangle(md, (header - label.Height) / 2, Math.Max(0, count.Left - sm - md), label.Height);
        reason.Bounds = new Rectangle(md, header, Math.Max(0, Width - 2 * md), Math.Max(0, reasonHeight - Px(Tokens.Space.Xs)));
        var top = header + reasonHeight;
        Scroll.Bounds = new Rectangle(sm, top, Math.Max(0, Width - 2 * sm), Math.Max(0, Height - top - sm));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = ProviderControl.ThemeOf(this).Colors;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var ring = LogicalToDeviceUnits(2);
        using var shape = Drawing.RoundedRectangle(new RectangleF(ring / 2f, ring / 2f, Width - ring, Height - ring), ProviderControl.ThemeOf(this).Radii.Lg * Em);
        using (var fill = new SolidBrush(colors.SurfaceMuted)) e.Graphics.FillPath(fill, shape);
        if (DropState is null) return;
        using var pen = new Pen(DropState == "allowed" ? colors.Accent : colors.Danger, ring);
        e.Graphics.DrawPath(pen, shape);
    }

    /// <summary>卡片列表：卡片之间 sm，至少 3em 高；落点处画一条 2 像素的线（能放强调色、不能放 danger）。</summary>
    sealed class ListPanel : Control
    {
        readonly KanbanColumnControl owner;

        public ListPanel(KanbanColumnControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public override LayoutEngine LayoutEngine => ManualLayout.Instance;

        int Gap => owner.Px(Tokens.Space.Sm);

        int Line => Math.Max(2, LogicalToDeviceUnits(2));

        public override Size GetPreferredSize(Size proposedSize)
        {
            var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
            var heights = owner.Cards.Select(card => card.GetPreferredSize(new Size(width, 0)).Height).ToList();
            var body = owner.empty.Visible ? owner.empty.GetPreferredSize(new Size(width, 0)).Height + 2 * Gap : heights.Sum() + Gap * Math.Max(0, heights.Count - 1);
            if (owner.IndicatorIndex is not null) body += Gap;
            return new Size(width, Math.Max(body, owner.Px(3)));
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (owner.empty.Visible)
            {
                var size = owner.empty.GetPreferredSize(new Size(Width, 0));
                owner.empty.Bounds = new Rectangle(0, Gap, Width, size.Height);
            }
            var y = 0;
            for (var index = 0; index < owner.Cards.Count; index++)
            {
                // 插入线占一份间距
                if (owner.IndicatorIndex == index) y += Gap;
                var card = owner.Cards[index];
                var height = card.GetPreferredSize(new Size(Width, 0)).Height;
                card.Bounds = new Rectangle(0, y, Width, height);
                y += height + Gap;
            }
            Invalidate();
        }

        /// <summary>插入线画在落点那张卡片上方的间距中间；落在最后时画在最后一张下面。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (owner.IndicatorIndex is not { } index) return;
            var y = index < owner.Cards.Count ? owner.Cards[index].Top - Gap / 2 : owner.Cards.Count == 0 ? Gap / 2 : owner.Cards[^1].Bottom + Gap / 2;
            var colors = ProviderControl.ThemeOf(this).Colors;
            using var brush = new SolidBrush(owner.DropState == "denied" ? colors.Danger : colors.Accent);
            e.Graphics.FillRectangle(brush, 0, y - Line / 2, Width, Line);
        }
    }
}

/// <summary>看板的一张卡片：surfaceRaised 底、border 细边、md 圆角、四周 sm；内容在左，把手在右上。键盘拿起时强调色边框，指针拖动时原位是虚线框占位。</summary>
sealed class KanbanCardControl : Control
{
    readonly KanbanBoardControl board;
    readonly Control content;
    bool active;
    bool placeholder;

    public KanbanCardControl(KanbanBoardControl board, string key, Control content)
    {
        this.board = board;
        this.content = content;
        Key = key;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.ListItem;
        Cursor = Cursors.Hand;
        Handle = new KanbanHandle(board, key);
        Controls.AddRange([Handle, content]);
        Track(content);
    }

    public override LayoutEngine LayoutEngine => ManualLayout.Instance;

    public string Key { get; }

    /// <summary>把手。</summary>
    public new KanbanHandle Handle { get; }

    float Em => board.EmPixels();

    int Px(float space) => (int)MathF.Round(space * Em);

    /// <summary>按下、拖动挂在卡片与内容里的每一层上（WinForms 的鼠标事件落在最里面的子控件上）；按钮、输入框不算。</summary>
    void Track(Control control)
    {
        if (control is IButtonControl or TextBoxBase or ComboBox) return;
        control.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) board.Press(Key, board.PointToClient(control.PointToScreen(e.Location)));
        };
        control.MouseMove += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) board.DragTo(Key, board.PointToClient(control.PointToScreen(e.Location)));
        };
        control.MouseUp += (_, _) => board.Release(Key);
        foreach (Control child in control.Controls) Track(child);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) board.Press(Key, board.PointToClient(PointToScreen(e.Location)));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button == MouseButtons.Left) board.DragTo(Key, board.PointToClient(PointToScreen(e.Location)));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        board.Release(Key);
    }

    public void Sync(string name, string instructions, bool active, bool placeholder)
    {
        Handle.AccessibleName = name;
        Handle.AccessibleDescription = instructions;
        AccessibleName = content.AccessibleName ?? name;
        this.active = active;
        this.placeholder = placeholder;
        // 占位：内容藏起来、只画虚线框（控件不能半透明）
        content.Visible = Handle.Visible = !placeholder;
        Invalidate();
    }

    /// <summary>四周 sm；把手在右上，与内容之间 xs。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var sm = Px(Tokens.Space.Sm);
        var handle = Handle.GetPreferredSize(Size.Empty);
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
        var inner = content.GetPreferredSize(new Size(Math.Max(0, width - 2 * sm - handle.Width - Px(Tokens.Space.Xs)), 0));
        return new Size(width, Math.Max(inner.Height, handle.Height) + 2 * sm);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var sm = Px(Tokens.Space.Sm);
        var handle = Handle.GetPreferredSize(Size.Empty);
        Handle.Bounds = new Rectangle(Width - sm - handle.Width, sm, handle.Width, handle.Height);
        var right = Handle.Left - Px(Tokens.Space.Xs);
        var size = content.GetPreferredSize(new Size(Math.Max(0, right - sm), 0));
        content.Bounds = new Rectangle(sm, sm, Math.Max(0, right - sm), size.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ProviderControl.ThemeOf(this);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var line = Math.Max(1, LogicalToDeviceUnits(1));
        using var shape = Drawing.RoundedRectangle(new RectangleF(line / 2f, line / 2f, Width - line, Height - line), theme.Radii.Md * Em);
        if (placeholder)
        {
            using var dashed = new Pen(theme.Colors.BorderStrong, line) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawPath(dashed, shape);
            return;
        }
        using (var fill = new SolidBrush(theme.Colors.SurfaceRaised)) e.Graphics.FillPath(fill, shape);
        using var pen = new Pen(active ? theme.Colors.Accent : theme.Colors.Border, line);
        e.Graphics.DrawPath(pen, shape);
    }
}

/// <summary>卡片的拖动把手：图标按钮（ghost、中性色、不出波纹）；空格、Enter、方向键先交给看板。</summary>
sealed class KanbanHandle : IconButtonControl
{
    readonly KanbanBoardControl board;
    readonly string key;

    public KanbanHandle(KanbanBoardControl board, string key)
    {
        this.board = board;
        this.key = key;
        Icon = Lucide.GripVertical;
        Variant = ButtonVariant.Ghost;
        Tone = StatusTone.Neutral;
        TextSize = TextSize.Xs;
        Ripple = false;
        Cursor = Cursors.SizeAll;
    }

    /// <summary>同上游：1em 的图标加四周 xxs，不按最小点击区放大（卡片本身就能拖）。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        // 用自己的字体（同看板的）：基类构造时就会来量，那时看板的字段还没赋值
        var em = this.EmPixels();
        var side = (int)MathF.Round(em + 2 * Tokens.Space.Xxs * em);
        return new Size(side, side);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Escape || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (board.HandleKey(key, e.KeyCode))
        {
            e.Handled = e.SuppressKeyPress = true;
            return;
        }
        base.OnKeyDown(e);
    }

    // 空格松开时按钮会点击：看板已经在按下时处理了
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) return;
        base.OnKeyUp(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        board.HandleBlur(key);
    }
}

/// <summary><see cref="KanbanBoardControl.Moved"/> 的参数。</summary>
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

/// <summary><see cref="KanbanBoardControl.Rejected"/> 的参数。</summary>
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
