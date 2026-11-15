using System.Collections;
using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>VirtualListControl 跟着谁滚动（vue-tsx-pro 的 VirtualListScrolls）。</summary>
public enum VirtualListScroll
{
    /// <summary>自己是滚动区域（默认）：要给它高度（横向时给宽度）。</summary>
    Container,

    /// <summary>随外层的 ScrollAreaControl 滚动：按总高度撑开，放在外层的普通排版里，仍只建看得见的项。</summary>
    Window,
}

/// <summary>
/// 虚拟列表（vue-tsx-pro 的 VirtualList）：只建可见范围内的项（前后各多建 <see cref="Overscan"/> 项），上万条数据也流畅；行高可以不固定：
/// 先按 <see cref="EstimateSize"/> 排，建出来后按控件要的大小量，可视区上方的项大小变了时同步调整滚动位置，看着的内容不跳。
/// <code>
/// var list = new VirtualListControl { ItemsSource = users, ItemFactory = user =&gt; new UserRow((User)user), EstimateSize = 56, Height = 352, AccessibleName = "联系人" };
/// </code>
/// <list type="bullet">
/// <item>每一项的控件由 <see cref="ItemFactory"/> 建（TS 的 children），滚出范围的项释放。数据变了换一个 ItemsSource（或调 <see cref="Refresh"/>）。</item>
/// <item>默认自己是滚动区域；<see cref="Scroll"/> = Window 时随外层的 ScrollAreaControl 滚动。<see cref="Orientation"/> = Horizontal 横向排列与滚动。</item>
/// <item>滚动：<see cref="ScrollToIndex"/>（按 <see cref="ScrollAlign"/> 对齐）、<see cref="ScrollToOffset"/>；可见范围变了发 <see cref="RangeChanged"/>（接 <see cref="InfiniteList{T}.OnRangeChanged"/> 滚动加载）。</item>
/// <item>加载中只用骨架行：没有数据时铺 5 行，已有数据（加载更多）时 2 行接在末尾；没有数据也不在加载时显示 <see cref="Empty"/>。</item>
/// </list>
/// 读屏：有数据时是列表，每项是列表项，说明里是“第几项，共几项”；没有数据时是组。
/// 自己滚动时不受窗口坐标的上限（约 3.2 万像素）限制：项按滚动位置摆在视口里；随外层滚动时列表要撑开成一个控件，总大小受这个上限限制，数据多时用 Container。
/// </summary>
[DefaultEvent(nameof(RangeChanged))]
[Description("虚拟列表：只建看得见的项，上万条数据也流畅。")]
public class VirtualListControl : Control
{
    const int SkeletonRows = 5;
    const int MoreSkeletonRows = 2;

    readonly ScrollAreaControl scroll = new();
    readonly ItemsPanel panel;
    // 每行上下各留 xs：行与行之间是 2 × xs（sm），两头各 xs
    readonly FlexControl skeleton = new() { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Gap = Space.Sm, Visible = false };
    readonly FlexControl more = new() { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Gap = Space.Sm, Visible = false };
    readonly DelayedLoading loading;
    // 建出来的项：序号 → 外层
    readonly Dictionary<int, VirtualListItem> realized = [];
    List<object?> items = [];
    // 每项的大小（设备像素）；量过的是实际大小，否则为 NaN（按估计值）
    double[] measured = [];
    double[] starts = [];
    double[] ends = [];
    ScrollAreaControl? outer;
    VisibleRange? range;
    bool realizing;

    /// <summary>新建：自己滚动、竖排。</summary>
    public VirtualListControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        panel = new ItemsPanel(this);
        for (var row = 0; row < SkeletonRows; row++) skeleton.Controls.Add(new SkeletonControl { Delay = row * 100 });
        for (var row = 0; row < MoreSkeletonRows; row++) more.Controls.Add(new SkeletonControl { Delay = row * 100 });
        loading = new DelayedLoading(_ => UpdateBody());
        scroll.ScrollChanged += (_, _) => Realize();
        AccessibleRole = AccessibleRole.Grouping;
        // 放进 FlexControl 时横向拉满（纵向按写的 Height）；关掉 AutoSize 就用自己的大小
        AutoSize = true;
        Attach();
        UpdateBody();
    }

    /// <summary>数据。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource { get; set { field = value; Refresh(); } }

    /// <summary>建一项的控件（TS 的 children）：拿到数据。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, Control>? ItemFactory { get; set { field = value; Refresh(); } }

    /// <summary>排列方向，默认竖排。</summary>
    [Category("Layout")]
    [DefaultValue(Orientation.Vertical)]
    public Orientation Orientation { get; set { field = value; scroll.Axis = value == Orientation.Horizontal ? ScrollAreaAxis.X : ScrollAreaAxis.Y; Refresh(); } } = Orientation.Vertical;

    /// <summary>跟着谁滚动，默认自己。</summary>
    [Category("Behavior")]
    [DefaultValue(VirtualListScroll.Container)]
    public VirtualListScroll Scroll
    {
        get;
        set
        {
            field = value;
            Detach();
            Attach();
            Refresh();
        }
    }

    /// <summary>每项的估计大小（逻辑像素，按 DPI 放大），默认 40：建出来后按实际大小；骨架行也按它的高度。</summary>
    [Category("Layout")]
    [DefaultValue(40)]
    public int EstimateSize { get; set { field = value; UpdateSkeleton(); Refresh(); } } = 40;

    /// <summary>可见范围前后各多建几项，默认 4：快速滚动时不露白。</summary>
    [Category("Behavior")]
    [DefaultValue(4)]
    public int Overscan { get; set { field = value; Realize(); } } = 4;

    /// <summary>没有数据时显示的控件。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Empty
    {
        get;
        set
        {
            if (field is not null) Controls.Remove(field);
            field = value;
            if (value is not null) Controls.Add(value);
            UpdateBody();
        }
    }

    /// <summary>加载中：没有数据时铺几行骨架，已有数据（加载更多）时骨架接在末尾。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading { get; set { field = value; loading.Update(value); UpdateBody(); } }

    /// <summary>可见范围（不含多建的项）变了：接近末尾时加载更多。</summary>
    public event EventHandler<VisibleRange>? RangeChanged;

    /// <summary>项数。</summary>
    [Browsable(false)]
    public int Count => items.Count;

    /// <summary>建出来的项（测试用）：序号 → 控件。</summary>
    internal IReadOnlyDictionary<int, VirtualListItem> RealizedItems => realized;

    /// <summary>骨架（测试用）。</summary>
    internal (Control Skeleton, Control More) Placeholders => (skeleton, more);

    /// <summary>滚动的那一层：自己的，或外层的（测试用）。</summary>
    internal ScrollAreaControl? Viewer => Scroll == VirtualListScroll.Container ? scroll : outer;

    /// <summary>放项的那一层（测试用）。</summary>
    internal Control ItemsPart => panel;

    bool Horizontal => Orientation == Orientation.Horizontal;

    /// <summary>数据变了：重新取数据、清掉建过的项与量过的大小，重排。</summary>
    public override void Refresh()
    {
        if (panel is null) return;
        items = ItemsSource?.Cast<object?>().ToList() ?? [];
        foreach (var item in realized.Values) item.Dispose();
        realized.Clear();
        measured = [.. Enumerable.Repeat(double.NaN, items.Count)];
        range = null;
        Relayout();
        UpdateBody();
        Realize();
        base.Refresh();
    }

    /// <summary>滚到第 index 项，按 align 对齐（默认已经完整可见就不动）。</summary>
    /// <param name="index">序号。</param>
    /// <param name="align">停在视口的什么位置。</param>
    public void ScrollToIndex(int index, ScrollAlign align = ScrollAlign.Auto)
    {
        if (index < 0 || index >= Count) return;
        // 估计的位置先滚过去、把它建出来量好，再按实际位置对齐一次
        for (var pass = 0; pass < 2; pass++)
        {
            if (Viewer is not { Content: { } content } viewer || Viewport() is not var (offset, size, origin)) return;
            var extent = viewer == scroll ? Total + panel.MoreSize : Horizontal ? content.Width : content.Height;
            SetOffset(VirtualScroll.OffsetFor(origin + starts[index], ends[index] - starts[index], origin + offset, size, extent, align));
            Realize();
        }
    }

    /// <summary>滚到相对列表开头的位置（设备像素）。</summary>
    /// <param name="offset">位置。</param>
    public void ScrollToOffset(int offset)
    {
        if (Viewport() is not var (_, _, origin)) return;
        SetOffset(origin + offset);
        Realize();
    }

    double Total => ends.Length == 0 ? 0 : ends[^1];

    /// <summary>全部项的总大小（设备像素，测试用）。</summary>
    internal int TotalSize => (int)Math.Ceiling(Total);

    double Estimate => LogicalToDeviceUnits(EstimateSize);

    /// <summary>容器模式：放项的那一层在自己的滚动区域里；随外层滚动时直接放在这里。</summary>
    void Attach()
    {
        if (Scroll == VirtualListScroll.Container)
        {
            panel.Controls.Add(more);
            scroll.Content = panel;
            Controls.Add(scroll);
        }
        else
        {
            panel.Controls.Add(more);
            Controls.Add(panel);
        }
        Controls.AddRange([skeleton]);
        PerformLayout();
    }

    void Detach()
    {
        scroll.Content = null;
        Controls.Remove(scroll);
        Controls.Remove(panel);
    }

    /// <summary>可视区：滚动位置（列表开头算起）、视口大小、列表开头在滚动内容里的位置。</summary>
    (double Offset, double Size, double Origin)? Viewport()
    {
        // 还没显示出来时不建项：可见范围要在显示之后才报告（订阅在显示之前）。放项的那一层最后才建句柄，建好时（OnHandleCreated）建第一批
        if (!panel.IsHandleCreated || Viewer is not { } viewer) return null;
        if (viewer == scroll) return (Horizontal ? scroll.ScrollOffset.X : scroll.ScrollOffset.Y, Horizontal ? scroll.ClientSize.Width : scroll.ClientSize.Height, 0);
        // 列表开头在外层内容里的位置：按各层的 Location 往上加到外层（屏幕坐标在排版中途还没同步，不能用）
        var origin = (double)(Horizontal ? viewer.ScrollOffset.X : viewer.ScrollOffset.Y);
        for (Control? at = panel; at is not null && at != viewer; at = at.Parent) origin += Horizontal ? at.Left : at.Top;
        var offset = (Horizontal ? viewer.ScrollOffset.X : viewer.ScrollOffset.Y) - origin;
        return (offset, Horizontal ? viewer.ClientSize.Width : viewer.ClientSize.Height, origin);
    }

    void SetOffset(double offset)
    {
        if (Viewer is not { } viewer) return;
        var value = (int)Math.Max(0, Math.Round(offset));
        viewer.ScrollOffset = Horizontal ? viewer.ScrollOffset with { X = value } : viewer.ScrollOffset with { Y = value };
    }

    /// <summary>按量过的与估计的大小排出每项的起点、终点，放项的那一层跟着总大小。</summary>
    void Relayout()
    {
        starts = new double[items.Count];
        ends = new double[items.Count];
        var at = 0.0;
        for (var index = 0; index < items.Count; index++)
        {
            starts[index] = at;
            at += double.IsNaN(measured[index]) ? Estimate : measured[index];
            ends[index] = at;
        }
        panel.UpdateSize();
    }

    /// <summary>
    /// 建可见范围（前后多建 Overscan 项）里还没建的项，量它们的实际大小；可视区上方的项大小变了时把滚动位置挪同样多（看着的内容不跳）；
    /// 释放范围外的项，摆好建出来的项，可见范围变了就报告。
    /// </summary>
    void Realize()
    {
        if (realizing || panel is null || Viewport() is not var (offset, size, _)) return;
        realizing = true;
        try
        {
            if (VirtualScroll.RangeOf(starts, ends, offset, size) is not { } visible)
            {
                foreach (var item in realized.Values) item.Dispose();
                realized.Clear();
                return;
            }
            var first = Math.Max(0, visible.StartIndex - Overscan);
            var last = Math.Min(Count - 1, visible.EndIndex + Overscan);
            foreach (var index in realized.Keys.Where(index => index < first || index > last).ToList())
            {
                realized[index].Dispose();
                realized.Remove(index);
            }
            var shift = 0.0;
            var changed = false;
            for (var index = first; index <= last; index++)
            {
                if (realized.ContainsKey(index) || ItemFactory is not { } factory) continue;
                var item = new VirtualListItem(factory(items[index]));
                panel.Controls.Add(item);
                realized[index] = item;
                var actual = Measure(item);
                var previous = double.IsNaN(measured[index]) ? Estimate : measured[index];
                if (Math.Abs(actual - previous) < 0.5) continue;
                measured[index] = actual;
                changed = true;
                if (starts[index] < offset) shift += actual - previous;
            }
            if (changed) Relayout();
            if (shift != 0) SetOffset(offset + shift + (Viewport()?.Origin ?? 0));
            // 自己滚动时项按滚动位置摆在视口里（放项的那一层只有视口大）；随外层滚动时那一层按总大小撑开
            var shown = Scroll == VirtualListScroll.Container ? (Horizontal ? scroll.ScrollOffset.X : scroll.ScrollOffset.Y) : 0;
            panel.PlaceMore(shown);
            foreach (var (index, item) in realized)
            {
                item.Describe(index, Count);
                var start = (int)Math.Round(starts[index]) - shown;
                var length = (int)Math.Round(ends[index] - starts[index]);
                item.Bounds = Horizontal ? new Rectangle(start, 0, length, panel.Height) : new Rectangle(0, start, panel.Width, length);
            }
            if (visible == range) return;
            range = visible;
            RangeChanged?.Invoke(this, visible);
        }
        finally
        {
            realizing = false;
        }
    }

    /// <summary>一项要的大小：竖排时按宽度量高度，横排时按高度量宽度。</summary>
    double Measure(VirtualListItem item) => Horizontal
        ? item.GetPreferredSize(new Size(0, panel.Height)).Width
        : item.GetPreferredSize(new Size(panel.Width, 0)).Height;

    /// <summary>有数据时是列表；没有时加载中是骨架，否则是空状态；加载更多时骨架接在末尾。</summary>
    void UpdateBody()
    {
        if (panel is null) return;
        var any = items.Count > 0;
        (Scroll == VirtualListScroll.Container ? (Control)scroll : panel).Visible = any;
        skeleton.Visible = !any && loading.Shown;
        more.Visible = any && loading.Shown;
        if (Empty is { } empty) empty.Visible = !any && !IsLoading;
        AccessibleRole = any ? AccessibleRole.List : AccessibleRole.Grouping;
        panel.UpdateSize();
        PerformLayout();
    }

    /// <summary>骨架行：比一项略矮（高 max(8, 估计大小 − 8)），上下各留 xs。</summary>
    void UpdateSkeleton()
    {
        var xs = (int)MathF.Round(Tokens.Space.Xs * this.EmPixels());
        var height = Math.Max(LogicalToDeviceUnits(8), (int)Estimate - LogicalToDeviceUnits(8));
        skeleton.Padding = more.Padding = new Padding(0, xs, 0, xs);
        foreach (var bar in skeleton.Controls.Cast<Control>().Concat(more.Controls.Cast<Control>()))
        {
            bar.MinimumSize = new Size(0, height);
            bar.MaximumSize = new Size(int.MaxValue, height);
        }
    }

    /// <summary>容器模式时滚动区域占满；没有数据时骨架、空状态占满；随外层滚动时放项的那一层按总大小。</summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var area = ClientRectangle;
        scroll.Bounds = area;
        skeleton.Bounds = area with { Height = skeleton.GetPreferredSize(new Size(area.Width, 0)).Height };
        if (Empty is { } empty) empty.Bounds = area with { Height = Math.Max(area.Height, empty.GetPreferredSize(new Size(area.Width, 0)).Height) };
        if (Scroll == VirtualListScroll.Window)
        {
            panel.Location = Point.Empty;
            panel.UpdateSize();
        }
        Realize();
    }

    /// <summary>自己滚动时按写的高度（宽度跟着外层）；随外层滚动时按全部项的总大小，没有数据时按骨架或空状态。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width is > 0 and < int.MaxValue ? proposedSize.Width : Width;
        if (Scroll == VirtualListScroll.Container) return new Size(width, Height);
        if (items.Count > 0) return panel.Size;
        var body = skeleton.Visible ? skeleton.GetPreferredSize(new Size(width, 0)).Height
            : Empty is { Visible: true } empty ? empty.GetPreferredSize(new Size(width, 0)).Height
            : 0;
        return new Size(width, body);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 放进界面后重新报告一次可见范围（建控件时就取了 DPI 的话，句柄可能已经建好、第一次报告在订阅之前）
        range = null;
        if (outer is not null) outer.ScrollChanged -= OnOuterScroll;
        outer = null;
        for (var at = Parent; at is not null; at = at.Parent)
            if (at is ScrollAreaControl area)
            {
                outer = area;
                break;
            }
        if (outer is not null) outer.ScrollChanged += OnOuterScroll;
        UpdateSkeleton();
    }

    void OnOuterScroll(object? sender, EventArgs e)
    {
        if (Scroll == VirtualListScroll.Window) Realize();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateSkeleton();
        Refresh();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && outer is not null) outer.ScrollChanged -= OnOuterScroll;
        base.Dispose(disposing);
    }

    /// <summary>
    /// 放项的那一层：自己滚动时只有视口大，总大小（加载更多时加上末尾的骨架）交给滚动区域，项按滚动位置摆；随外层滚动时按总大小撑开。
    /// </summary>
    sealed class ItemsPanel : Control, IVirtualScrollContent
    {
        readonly VirtualListControl owner;

        public ItemsPanel(VirtualListControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public override LayoutEngine LayoutEngine => ManualLayout.Instance;

        public int MoreSize => owner.more.Visible ? (owner.Horizontal ? owner.more.GetPreferredSize(new Size(0, Height)).Width : owner.more.GetPreferredSize(new Size(Width, 0)).Height) : 0;

        int Total => (int)Math.Ceiling(owner.Total) + MoreSize;

        public Size Extent(Size view) => owner.Horizontal ? new Size(Total, view.Height) : new Size(view.Width, Total);

        public void ScrollTo(Point offset) => owner.Realize();

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            owner.Realize();
        }

        public override Size GetPreferredSize(Size proposedSize) => owner.Horizontal ? new Size(Total, proposedSize.Height) : new Size(proposedSize.Width, Total);

        /// <summary>总大小变了：滚动区域重排；随外层滚动时自己撑开。</summary>
        public void UpdateSize()
        {
            if (owner.Scroll == VirtualListScroll.Window) Size = owner.Horizontal ? new Size(Total, owner.Height) : new Size(owner.Width, Total);
            Parent?.PerformLayout();
            owner.Parent?.PerformLayout(owner, nameof(Bounds));
        }

        /// <summary>加载更多的骨架接在最后一项后面。</summary>
        public void PlaceMore(int shown)
        {
            var end = (int)Math.Ceiling(owner.Total) - shown;
            owner.more.Bounds = owner.Horizontal ? new Rectangle(end, 0, MoreSize, Height) : new Rectangle(0, end, Width, MoreSize);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // 宽度变了：项的高度可能跟着变，建过的重新量
            foreach (var (index, item) in owner.realized) owner.measured[index] = owner.Measure(item);
            owner.Relayout();
            owner.Realize();
        }
    }
}

/// <summary>VirtualListControl 的一项的外层（上游的 role="listitem"）：读屏是列表项，说明里是“第几项，共几项”。</summary>
sealed class VirtualListItem : Control
{
    public VirtualListItem(Control content)
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.ListItem;
        Content = content;
        // 先取内容自己的大小：加进来时会按外层排一次，外层还是 0×0 的话内容写好的大小就丢了，量不出来
        Size = content.Size;
        Controls.Add(content);
    }

    public Control Content { get; }

    public override LayoutEngine LayoutEngine => ManualLayout.Instance;

    public void Describe(int index, int count)
    {
        AccessibleName = Content.AccessibleName;
        AccessibleDescription = $"第 {index + 1} 项，共 {count} 项";
    }

    public override Size GetPreferredSize(Size proposedSize) => Content.GetPreferredSize(proposedSize);

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Content.Bounds = ClientRectangle;
    }
}
