using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 可拖动排序的列表（vue-tsx-pro 的 Sortable）：每项前有一个把手，拖动或用键盘改变顺序。
/// <code>
/// var list = new SortableControl { ItemsSource = tasks, GetKey = task =&gt; ((Todo)task).Id, GetLabel = task =&gt; ((Todo)task).Title, ItemFactory = task =&gt; new TodoRow((Todo)task), AccessibleName = "待办" };
/// </code>
/// <list type="bullet">
/// <item>数据：ItemsSource 是可改的列表（ObservableCollection 用它的 Move，别的 IList 删了再插），顺序变了就地改并发 <see cref="Reordered"/>（上游的 _items 写回新数组）。</item>
/// <item>每项的内容由 <see cref="ItemFactory"/> 建（TS 的 children），不写时是项的名称。</item>
/// <item>指针：在把手上按下拖动（挪开 3 以上才开始），越过别的项的中线时立即重排，被拖的项跟着指针，拖到外层滚动区域的边缘时自动滚动；Esc 取消。</item>
/// <item>键盘：聚焦把手，空格 / Enter 拿起，方向键（竖排 ↑↓，横排 ←→）移动一位，Home / End 移到两端，再按空格放下，Esc 取消；每一步读屏都会播报。</item>
/// </list>
/// 列表自己不滚动：项多时放进 ScrollAreaControl。把手是按钮：名称“移动 项名”，说明是操作方法。控件不能半透明、画不出阴影：拿起的项是品牌色边框、放到最上层。
/// </summary>
[DefaultEvent(nameof(Reordered))]
[Description("可拖动排序的列表：把手拖动或键盘移动。")]
public class SortableControl : Control
{
    // 按 key 留着每一项：重排时挪动它们而不是重建，键盘焦点留在被移动的把手上
    readonly Dictionary<string, SortableItemControl> rows = [];
    readonly SortableModel<object> model;
    List<SortableItemControl> ordered = [];
    INotifyCollectionChanged? observed;
    // 按下时：哪一项、指针的位置、这一项在排列方向上的起点、别的项的中线
    (string Key, Point Start, int RowStart, IReadOnlyList<double> Centers)? press;

    /// <summary>新建：竖排、间距 xs，按内容大小（放进 FlexControl 时横向拉满）。</summary>
    public SortableControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AccessibleRole = AccessibleRole.List;
        model = new SortableModel<object>(() => ItemsSource?.Cast<object>().ToList() ?? [], KeyOf, () => ProviderControl.LangOf(this))
        {
            GetLabel = LabelOf,
            Reordered = Apply,
        };
        model.Changed += () =>
        {
            Announced = model.Announcement;
            if (IsHandleCreated)
                AccessibilityObject.RaiseAutomationNotification(System.Windows.Forms.Automation.AutomationNotificationKind.Other,
                    System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent, model.Announcement);
            foreach (var (key, row) in rows) row.IsActive = model.Active == key;
            rows.GetValueOrDefault(model.Active ?? "")?.BringToFront();
        };
    }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => ManualLayout.Instance;

    /// <summary>各项（可改的列表）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IList? ItemsSource
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

    /// <summary>项的 key，同一列表里唯一（必填）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, string>? GetKey { get; set { field = value; Rebuild(); } }

    /// <summary>项的名称：把手的名称与读屏播报用它；不写时是 key。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, string>? GetLabel { get; set { field = value; Rebuild(); } }

    /// <summary>建一项的内容（TS 的 children）；不写时是项的名称。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object, Control>? ItemFactory { get; set { field = value; Rebuild(); } }

    /// <summary>排列方向，默认竖排。</summary>
    [Category("Layout")]
    [DefaultValue(Orientation.Vertical)]
    public Orientation Orientation { get; set { field = value; Sync(); } } = Orientation.Vertical;

    /// <summary>顺序变了（拖动、键盘）：从第几位移到第几位，ItemsSource 已经改好。</summary>
    public event EventHandler<SortableReorderedEventArgs>? Reordered;

    /// <summary>各项（测试用），按显示的顺序。</summary>
    internal IReadOnlyList<SortableItemControl> Items => ordered;

    /// <summary>读屏最后念的话（测试用）。</summary>
    internal string? Announced { get; private set; }

    /// <summary>正被拿起的项（测试用）。</summary>
    internal string? Active => model.Active;

    bool Horizontal => Orientation == Orientation.Horizontal;

    int Gap => (int)MathF.Round(Tokens.Space.Xs * this.EmPixels());

    string KeyOf(object item) => GetKey?.Invoke(item) ?? item.ToString() ?? "";

    string LabelOf(object item) => GetLabel?.Invoke(item) ?? KeyOf(item);

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    void Rebuild()
    {
        foreach (var row in rows.Values) row.Dispose();
        rows.Clear();
        ordered = [];
        Sync();
    }

    /// <summary>按数据排出各项：已有的项按 key 留着；排版按 ordered 的顺序，不动子控件的集合（挪动会让把手失焦）。</summary>
    void Sync()
    {
        if (model is null) return;
        var items = ItemsSource?.Cast<object>().ToList() ?? [];
        var next = new List<SortableItemControl>();
        foreach (var item in items)
        {
            var key = KeyOf(item);
            if (!rows.TryGetValue(key, out var row))
            {
                rows[key] = row = new SortableItemControl(this, key, ItemFactory?.Invoke(item) ?? new TextControl { Text = LabelOf(item) });
                Controls.Add(row);
            }
            row.Sync(model.HandleName(key), model.Instructions, Enabled, model.Active == key);
            next.Add(row);
        }
        foreach (var key in rows.Keys.Where(key => next.All(row => row.Key != key)).ToList())
        {
            rows[key].Dispose();
            rows.Remove(key);
        }
        ordered = next;
        for (var index = 0; index < ordered.Count; index++) ordered[index].AccessibleDescription = $"第 {index + 1} 项，共 {ordered.Count} 项";
        Parent?.PerformLayout(this, nameof(Bounds));
        PerformLayout();
    }

    /// <summary>竖排时每项拉满宽、按内容高；横排时每项按内容宽、拉满高；项与项之间 xs；被拖的项再挪开跟着指针的位移。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var at = 0;
        foreach (var row in ordered)
        {
            var size = row.GetPreferredSize(Horizontal ? new Size(0, ClientSize.Height) : new Size(ClientSize.Width, 0));
            row.Bounds = Horizontal
                ? new Rectangle(at + row.Translate, 0, size.Width, ClientSize.Height)
                : new Rectangle(0, at + row.Translate, ClientSize.Width, size.Height);
            row.LayoutStart = at;
            at += (Horizontal ? size.Width : size.Height) + Gap;
        }
    }

    /// <summary>竖排：宽同外层、高是各项之和；横排：宽是各项之和、高是最高的一项。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : Width;
        if (ordered.Count == 0) return new Size(width, 0);
        var sizes = ordered.Select(row => row.GetPreferredSize(Horizontal ? Size.Empty : new Size(width, 0))).ToList();
        var gaps = Gap * (ordered.Count - 1);
        return Horizontal ? new Size(sizes.Sum(size => size.Width) + gaps, sizes.Max(size => size.Height)) : new Size(width, sizes.Sum(size => size.Height) + gaps);
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

    // —— 指针：把手上按下、拖动、松开（位置相对自己；测试也从这里进来） ——

    /// <summary>在把手上按下：记下这一项的起点与别的项的中线。</summary>
    internal void Press(string key, Point at)
    {
        if (!Enabled || model.Active is not null) return;
        var centers = ordered.Where(row => row.Key != key)
            .Select(row => row.LayoutStart + (Horizontal ? row.Width : row.Height) / 2.0).ToList();
        press = (key, at, rows[key].LayoutStart, centers);
    }

    /// <summary>拖到 at：挪开 3 以上才拿起；指针越过了几个别的项的中线就排到第几位，被拖的项跟着指针。</summary>
    internal void DragTo(string key, Point at)
    {
        if (press is not { } pressed || pressed.Key != key) return;
        if (model.Active is null)
        {
            if (Math.Abs(at.X - pressed.Start.X) + Math.Abs(at.Y - pressed.Start.Y) < LogicalToDeviceUnits(3)) return;
            model.Grab(key);
        }
        var delta = Horizontal ? at.X - pressed.Start.X : at.Y - pressed.Start.Y;
        model.DragOver(key, "", SortableModel<object>.IndexAt(pressed.Centers, Horizontal ? at.X : at.Y));
        var row = rows[key];
        row.Translate = 0;
        PerformLayout();
        // 跟着指针：位移 = 指针挪开的距离 − 它在排版里的位置变化
        row.Translate = delta - (row.LayoutStart - pressed.RowStart);
        PerformLayout();
        AutoScroll(at);
    }

    /// <summary>指针离外层滚动区域的边缘不到 40 时朝那边滚（离得越近滚得越快，每次最多 16）。</summary>
    void AutoScroll(Point at)
    {
        ScrollAreaControl? viewer = null;
        for (var parent = Parent; parent is not null && viewer is null; parent = parent.Parent) viewer = parent as ScrollAreaControl;
        if (viewer is null) return;
        var point = viewer.PointToClient(PointToScreen(at));
        var (edge, most) = (LogicalToDeviceUnits(40), LogicalToDeviceUnits(16));
        var position = Horizontal ? point.X : point.Y;
        var size = Horizontal ? viewer.ClientSize.Width : viewer.ClientSize.Height;
        var step = position < edge ? -Math.Min(most, edge - position) : position > size - edge ? Math.Min(most, position - (size - edge)) : 0;
        if (step != 0) viewer.ScrollOffset = Horizontal ? viewer.ScrollOffset with { X = viewer.ScrollOffset.X + step } : viewer.ScrollOffset with { Y = viewer.ScrollOffset.Y + step };
    }

    /// <summary>松开：放下，被拖的项回到排版的位置。</summary>
    internal void Release(string key)
    {
        if (press is not { } pressed || pressed.Key != key) return;
        press = null;
        rows[key].Translate = 0;
        PerformLayout();
        if (model.Active == key) model.Drop(key);
    }

    /// <summary>把手上的按键：拿起 / 放下、移动、取消；处理了返回 true。</summary>
    internal bool HandleKey(string key, Keys pressed)
    {
        if (!Enabled) return false;
        // 指针拖着时 Esc：取消，回到原来的位置
        if (pressed == Keys.Escape && press?.Key == key && model.Active == key)
        {
            press = null;
            rows[key].Translate = 0;
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
        return mapped is { } value && model.Press(key, value, Horizontal, RightToLeft == RightToLeft.Yes);
    }

    internal void HandleBlur(string key)
    {
        if (press is null) model.Blur(key);
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }
}

/// <summary>SortableControl 的一项：把手与内容，画 surface 底、borderMuted 细边、md 圆角；拿起时品牌色边框。</summary>
sealed class SortableItemControl : Control
{
    readonly SortableControl owner;
    readonly Control content;

    public SortableItemControl(SortableControl owner, string key, Control content)
    {
        this.owner = owner;
        this.content = content;
        Key = key;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.ListItem;
        Handle = new SortableHandle(owner, key);
        Controls.AddRange([Handle, content]);
    }

    public override LayoutEngine LayoutEngine => ManualLayout.Instance;

    public string Key { get; }

    /// <summary>把手。</summary>
    public new SortableHandle Handle { get; }

    /// <summary>排版时的起点（不含跟着指针的位移）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int LayoutStart { get; set; }

    /// <summary>被拖着时跟着指针的位移。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Translate { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsActive
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Invalidate();
        }
    }

    public void Sync(string name, string instructions, bool enabled, bool active)
    {
        Handle.AccessibleName = name;
        Handle.AccessibleDescription = instructions;
        Handle.Enabled = enabled;
        IsActive = active;
        AccessibleName = content.AccessibleName ?? name;
    }

    int Xs => (int)MathF.Round(Tokens.Space.Xs * owner.EmPixels());

    int Sm => (int)MathF.Round(Tokens.Space.Sm * owner.EmPixels());

    /// <summary>把手 max(1.75em, 最小点击区) 见方。</summary>
    int HandleSide => Math.Max((int)MathF.Round(1.75f * owner.EmPixels()), LogicalToDeviceUnits(24));

    int Border => Math.Max(1, LogicalToDeviceUnits(1));

    /// <summary>上下 xs、起始边 xs（把手自带留白）、结束边 sm；把手与内容之间 sm。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var chrome = Xs + HandleSide + Sm + Sm + 2 * Border;
        var inner = content.GetPreferredSize(proposedSize.Width > 0 ? new Size(Math.Max(0, proposedSize.Width - chrome), 0) : Size.Empty);
        return new Size(chrome + inner.Width, Math.Max(HandleSide, inner.Height) + 2 * Xs + 2 * Border);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var side = HandleSide;
        var area = new Rectangle(Border + Xs, Border + Xs, Math.Max(0, Width - 2 * Border - Xs - Sm), Math.Max(0, Height - 2 * Border - 2 * Xs));
        Handle.Bounds = new Rectangle(area.Left, area.Top + (area.Height - side) / 2, side, side);
        var left = Handle.Right + Sm;
        var size = content.GetPreferredSize(new Size(Math.Max(0, area.Right - left), 0));
        content.Bounds = new Rectangle(left, area.Top + Math.Max(0, (area.Height - size.Height) / 2), Math.Max(0, area.Right - left), Math.Min(area.Height, size.Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = ProviderControl.ThemeOf(this).Colors;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var border = Border;
        var box = new RectangleF(border / 2f, border / 2f, Width - border, Height - border);
        using var shape = Drawing.RoundedRectangle(box, ProviderControl.ThemeOf(this).Radii.Md * owner.EmPixels());
        using (var fill = new SolidBrush(colors.Surface)) e.Graphics.FillPath(fill, shape);
        using var pen = new Pen(IsActive ? colors.Accent : colors.BorderMuted, border);
        e.Graphics.DrawPath(pen, shape);
    }
}

/// <summary>SortableControl 的把手：图标按钮（ghost、中性色、不出波纹）；空格、Enter、方向键先交给排序。</summary>
sealed class SortableHandle : IconButtonControl
{
    readonly SortableControl owner;
    readonly string key;

    public SortableHandle(SortableControl owner, string key)
    {
        this.owner = owner;
        this.key = key;
        Icon = Lucide.GripVertical;
        Variant = ButtonVariant.Ghost;
        Tone = StatusTone.Neutral;
        Ripple = false;
        Cursor = Cursors.SizeAll;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Escape || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (owner.HandleKey(key, e.KeyCode))
        {
            e.Handled = e.SuppressKeyPress = true;
            return;
        }
        base.OnKeyDown(e);
    }

    // 空格松开时按钮会点击：排序已经在按下时处理了
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) return;
        base.OnKeyUp(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        owner.HandleBlur(key);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) owner.Press(key, owner.PointToClient(PointToScreen(e.Location)));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button == MouseButtons.Left) owner.DragTo(key, owner.PointToClient(PointToScreen(e.Location)));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        owner.Release(key);
    }
}

/// <summary><see cref="SortableControl.Reordered"/> 的参数。</summary>
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
