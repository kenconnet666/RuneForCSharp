using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 一维布局（vue-tsx-pro 的 Flex，CSS flexbox）：子控件按 Controls 的顺序沿主轴排开，间距是主题的 space 档位。竖排写 Direction = column。
/// <code>
/// var row = new FlexControl { Gap = Space.Sm, Align = FlexAlign.Center };
/// row.Controls.AddRange([icon, title, actions]);
/// row.SetGrow(title, true);   // 分剩下的空间，也能缩到比内容窄（设计器里是子控件的"flex1 上的 Grow"）
/// </code>
/// 不够放时各项按内容宽度加权收缩（CSS 的 flex-shrink: 1），最多缩到内容的最小宽度（最长的词）。自动大小的子控件按分到的宽度取想要的高度（文字换行），
/// stretch 时拉到行高；关掉 AutoSize 的子控件有自己的大小，不拉伸（同 CSS 写了高度的项）。间距按 FlexControl 的字体算（em）。
/// 自己的大小同 Label：自然宽度，不超过布局给的宽度与 MaximumSize（停靠、锚定时由父容器给）。
/// </summary>
[ProvideProperty("Grow", typeof(Control))]
[ProvideProperty("AlignSelf", typeof(Control))]
public class FlexControl : Panel, IExtenderProvider
{
    readonly FlexEngine engine;
    readonly PreferredSizeCache preferred;
    readonly HashSet<Control> growing = [];
    readonly HashSet<Control> overlays = [];
    readonly Dictionary<Control, FlexAlign> selfAligns = [];

    /// <summary>新建：自动大小。</summary>
    public FlexControl()
    {
        engine = new FlexEngine(this);
        preferred = new PreferredSizeCache(this);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    /// <summary>主轴方向，默认 row。</summary>
    [Category("Layout")]
    [Description("主轴方向：row、column、row-reverse、column-reverse。")]
    public FlexDirection Direction { get; set { field = value; PerformLayout(); } }

    /// <summary>交叉轴对齐（align-items），默认 stretch。</summary>
    [Category("Layout")]
    [Description("交叉轴对齐：start、center、end、stretch（默认）、baseline。")]
    public FlexAlign Align { get; set { field = value; PerformLayout(); } }

    /// <summary>主轴分布（justify-content），默认 start。</summary>
    [Category("Layout")]
    [Description("主轴分布：start（默认）、center、end、between、around、evenly。")]
    public FlexJustify Justify { get; set { field = value; PerformLayout(); } }

    /// <summary>换行；reverse 为反向换行。</summary>
    [Category("Layout")]
    [Description("换行：false（默认）、true、reverse。")]
    public FlexWrap Wrap { get; set { field = value; PerformLayout(); } }

    /// <summary>间距档位，对应主题的 space。</summary>
    [Category("Layout")]
    [Description("间距档位（none、xs、sm、md、lg、xl）。")]
    public Space Gap { get; set { field = value; PerformLayout(); } }

    /// <summary>换行时行与行之间的间距，盖过 Gap。</summary>
    [Category("Layout")]
    [Description("行与行之间的间距，盖过 Gap。")]
    public Space RowGap { get; set { field = value; PerformLayout(); } }

    /// <summary>同一行里项与项之间的间距，盖过 Gap。</summary>
    [Category("Layout")]
    [Description("同一行里项与项之间的间距，盖过 Gap。")]
    public Space ColumnGap { get; set { field = value; PerformLayout(); } }

    /// <summary>行内 flex：WinForms 里控件本来就按内容大小（AutoSize），这个属性只记下意图。</summary>
    [Category("Layout")]
    [Description("行内 flex（inline-flex）：宽度跟着内容。WinForms 的控件默认就是这样。")]
    [DefaultValue(false)]
    public bool Inline { get; set; }

    /// <summary>在外层 FlexControl 里分剩下的空间（同外层的 SetGrow(this, true)，两个写了一个就算）。</summary>
    [Category("Layout")]
    [Description("在外层 FlexControl 里分剩下的空间，并能缩到比内容窄。")]
    [DefaultValue(false)]
    public bool Grow
    {
        get;
        set
        {
            field = value;
            Parent?.PerformLayout(this, nameof(Grow));
        }
    }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    bool ShouldSerializeDirection() => Direction.Value is not null;

    void ResetDirection() => Direction = default;

    bool ShouldSerializeAlign() => Align.Value is not null;

    void ResetAlign() => Align = default;

    bool ShouldSerializeJustify() => Justify.Value is not null;

    void ResetJustify() => Justify = default;

    bool ShouldSerializeWrap() => Wrap.Value is not null;

    void ResetWrap() => Wrap = default;

    bool ShouldSerializeGap() => Gap.Value is not null;

    void ResetGap() => Gap = default;

    bool ShouldSerializeRowGap() => RowGap.Value is not null;

    void ResetRowGap() => RowGap = default;

    bool ShouldSerializeColumnGap() => ColumnGap.Value is not null;

    void ResetColumnGap() => ColumnGap = default;

    /// <summary>直接的子控件都能设 Grow。</summary>
    bool IExtenderProvider.CanExtend(object extendee) => extendee is Control control && control.Parent == this;

    /// <summary>子控件分不分剩下的空间。</summary>
    /// <param name="control">子控件。</param>
    [DisplayName("Grow")]
    [Category("Layout")]
    [Description("在这个 FlexControl 里分剩下的空间，并能缩到比内容窄（文字才能截断）。")]
    [DefaultValue(false)]
    public bool GetGrow(Control control) => growing.Contains(control) || control is FlexControl { Grow: true };

    /// <summary>设子控件分剩下的空间。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">分不分。</param>
    public void SetGrow(Control control, bool value)
    {
        if (value ? growing.Add(control) : growing.Remove(control)) PerformLayout();
    }

    /// <summary>子控件不参加排列（CSS 的绝对定位）：叠在上面，由主人自己摆（标签的关闭按钮、标签栏的按钮）。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">不参加排列。</param>
    internal void SetOverlay(Control control, bool value)
    {
        if (value ? overlays.Add(control) : overlays.Remove(control)) PerformLayout();
    }

    /// <summary>子控件自己的交叉轴对齐（CSS 的 align-self），盖过 Align；没写时是控件自己带的（<see cref="IFlexItem"/>）。</summary>
    /// <param name="control">子控件。</param>
    [DisplayName("AlignSelf")]
    [Category("Layout")]
    [Description("这个子控件自己的交叉轴对齐，盖过 FlexControl 的 Align。")]
    public FlexAlign GetAlignSelf(Control control) => selfAligns.TryGetValue(control, out var align) ? align : (control as IFlexItem)?.AlignSelf ?? default;

    /// <summary>设子控件自己的交叉轴对齐。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">对齐；default 时不再盖过。</param>
    public void SetAlignSelf(Control control, FlexAlign value)
    {
        if (value.Value is null) selfAligns.Remove(control);
        else selfAligns[control] = value;
        PerformLayout();
    }

    /// <summary>一项实际的交叉轴对齐：自己的，没写时是 Align。</summary>
    FlexAlign AlignOf(Control child) => GetAlignSelf(child) is { Value: not null } self ? self : Align;

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        growing.Remove(e.Control!);
        overlays.Remove(e.Control!);
        selfAligns.Remove(e.Control!);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PerformLayout();
    }

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        PerformLayout();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => preferred.Get(proposedSize, engine.Measure);

    /// <summary>flex 排版：量子控件、交给 <see cref="FlexLayout"/> 分，再摆（从右到左时横排镜像）。</summary>
    sealed class FlexEngine(FlexControl owner) : LayoutEngine
    {
        // 子控件想要的大小：一趟计算里会按同样的宽度量好几遍（自然大小、最小大小、分行后的大小），嵌套几层就是指数级的测量
        // （文档应用的一页要几十秒）。只在一趟 Measure 或 Layout 里复用：跨趟留着的话，里层的内容变了外层还拿旧的大小排
        readonly Dictionary<(Control, Size), Size> measured = [];

        Size Preferred(Control child, Size proposed)
        {
            if (!measured.TryGetValue((child, proposed), out var size)) measured[(child, proposed)] = size = child.GetPreferredSize(proposed);
            return size;
        }

        bool IsRow => owner.Direction.Value is null or "row" or "row-reverse";

        double Em => owner.EmPixels();

        (double Main, double Cross) Gaps
        {
            get
            {
                var row = (owner.RowGap.Value is null ? owner.Gap : owner.RowGap).Factor ?? 0;
                var column = (owner.ColumnGap.Value is null ? owner.Gap : owner.ColumnGap).Factor ?? 0;
                return IsRow ? (Math.Round(column * Em), Math.Round(row * Em)) : (Math.Round(row * Em), Math.Round(column * Em));
            }
        }

        List<Control> Shown() => [.. owner.Controls.Cast<Control>().Where(child => (child.Visible || !owner.Visible) && !owner.overlays.Contains(child))];

        /// <summary>
        /// 子控件在主轴上给了 main 时的大小（自动大小的按宽度取高度；main 为无穷时是自然大小），夹在它的最小、最大尺寸之间
        /// （平台设边界时也会这样夹，排的时候不并上的话位置就按没夹的大小算，后面的项叠上来）。
        /// </summary>
        Size SizeOf(Control child, double main, double cross)
        {
            if (!child.AutoSize) return child.Size;
            var row = IsRow;
            var proposed = row
                ? new Size(double.IsInfinity(main) ? 0 : (int)Math.Round(main), 0)
                : new Size(double.IsInfinity(cross) ? 0 : (int)Math.Round(cross), 0);
            var size = Preferred(child, proposed);
            var (min, max) = (child.MinimumSize, child.MaximumSize);
            var width = Math.Max(size.Width, min.Width);
            var height = Math.Max(size.Height, min.Height);
            return new Size(max.Width > 0 ? Math.Min(width, max.Width) : width, max.Height > 0 ? Math.Min(height, max.Height) : height);
        }

        double Main(Size size) => IsRow ? size.Width : size.Height;

        double Cross(Size size) => IsRow ? size.Height : size.Width;

        /// <summary>
        /// 收缩时最多缩到多小（CSS 的 min-width: auto，内容的最小宽度）：关掉 AutoSize 的是自己的大小；自动大小的横排时
        /// 按几乎为 0 的宽度取想要的大小（WinForms 的想要大小不受给的宽度限制：文字给出最长的词，不换行的给出自然宽度），
        /// 竖排时是内容的高度。
        /// </summary>
        double MinMain(Control child, double cross) => !child.AutoSize ? Main(child.Size)
            : IsRow ? Preferred(child, new Size(2, 0)).Width
            : SizeOf(child, double.PositiveInfinity, cross).Height;

        (List<FlexLayout.Line> Lines, Size[] Sizes, double[] Ascents) Compute(List<Control> children, double main, double cross)
        {
            var items = children.Select(child => new FlexLayout.Item(Main(SizeOf(child, double.PositiveInfinity, cross)), owner.GetGrow(child))).ToList();
            var lines = FlexLayout.Distribute(items, main, Gaps.Main, owner.Wrap.Wraps, i => MinMain(children[i], cross));
            var sizes = new Size[children.Count];
            var ascents = new double[children.Count];
            foreach (var line in lines)
            {
                var crosses = new (double Cross, double Ascent, FlexAlign Align)[line.Count];
                for (var k = 0; k < line.Count; k++)
                {
                    var i = line.Start + k;
                    sizes[i] = SizeOf(children[i], line.Main[k], cross);
                    var size = Cross(sizes[i]);
                    crosses[k] = (size, ascents[i] = (children[i] as IBaseline)?.Baseline ?? size, owner.AlignOf(children[i]));
                }
                line.Cross = FlexLayout.LineCross(crosses).Cross;
            }
            return (lines, sizes, ascents);
        }

        /// <summary>想要的大小：主轴按内容，不超过布局给的宽度与 MaximumSize（同 Label）。</summary>
        public Size Measure(Size proposed)
        {
            var limit = proposed.Width is > 1 and < int.MaxValue ? proposed.Width : int.MaxValue;
            if (owner.MaximumSize.Width > 0) limit = Math.Min(limit, owner.MaximumSize.Width);
            measured.Clear();
            var padding = owner.Padding;
            var width = limit == int.MaxValue ? double.PositiveInfinity : limit - padding.Horizontal;
            var (main, cross) = IsRow ? (width, double.PositiveInfinity) : (double.PositiveInfinity, width);
            var (lines, _, _) = Compute(Shown(), main, cross);
            var gaps = Gaps;
            var used = lines.Select(line => line.Main.Sum() + gaps.Main * (line.Count - 1)).DefaultIfEmpty().Max();
            var across = lines.Sum(line => line.Cross) + gaps.Cross * Math.Max(0, lines.Count - 1);
            var size = IsRow ? new Size((int)Math.Ceiling(used), (int)Math.Ceiling(across)) : new Size((int)Math.Ceiling(across), (int)Math.Ceiling(used));
            return size + padding.Size;
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            measured.Clear();
            var area = owner.DisplayRectangle;
            var row = IsRow;
            var children = Shown();
            var (main, cross) = row ? (area.Width, area.Height) : (area.Height, area.Width);
            var (lines, sizes, ascents) = Compute(children, main, row ? double.PositiveInfinity : cross);
            var gaps = Gaps;
            FlexLayout.Stretch(lines, cross, gaps.Cross, owner.Wrap.Wraps);
            var reverse = owner.Direction.Value is "row-reverse" or "column-reverse";
            var mirror = row && owner.RightToLeft == RightToLeft.Yes;
            var ordered = owner.Wrap.Value == "reverse" ? Enumerable.Reverse(lines) : lines;
            var y = 0d;
            foreach (var line in ordered)
            {
                var positions = FlexLayout.Positions(line.Main, gaps.Main, main, owner.Justify, reverse);
                var items = Enumerable.Range(0, line.Count).Select(k => (Cross(sizes[line.Start + k]), ascents[line.Start + k], owner.AlignOf(children[line.Start + k]))).ToList();
                var lineAscent = FlexLayout.LineCross(items).Ascent;
                for (var k = 0; k < line.Count; k++)
                {
                    var child = children[line.Start + k];
                    var (offset, size) = FlexLayout.Align(items[k].Item1, line.Cross, items[k].Item3, items[k].Item2, lineAscent);
                    // 关掉 AutoSize 的子控件有自己的大小：不拉伸
                    if (!child.AutoSize && size > items[k].Item1) size = items[k].Item1;
                    var x = mirror ? main - positions[k] - line.Main[k] : positions[k];
                    var bounds = row
                        ? new Rectangle(area.X + (int)Math.Round(x), area.Y + (int)Math.Round(y + offset), (int)Math.Round(line.Main[k]), (int)Math.Round(size))
                        : new Rectangle(area.X + (int)Math.Round(y + offset), area.Y + (int)Math.Round(x), (int)Math.Round(size), (int)Math.Round(line.Main[k]));
                    child.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                }
                y += line.Cross + gaps.Cross;
            }
            return owner.AutoSize;
        }
    }
}

/// <summary>
/// 放进 FlexControl 时自己带的 align-self（组件的样式要求，如竖向的分隔线随行高拉伸）；FlexControl 的 SetAlignSelf 盖过它。
/// </summary>
interface IFlexItem
{
    /// <summary>自己的交叉轴对齐；default 时跟随 Align。</summary>
    FlexAlign AlignSelf { get; }
}

/// <summary>flex 布局的列表（vue-tsx-pro 的 FlexList，ul 去掉圆点）：读屏里是列表。</summary>
public class FlexListControl : FlexControl
{
    /// <summary>新建：读屏里是列表。</summary>
    public FlexListControl() => AccessibleRole = AccessibleRole.List;
}

/// <summary>有序列表的 flex 布局（vue-tsx-pro 的 FlexOrderedList，ol 去掉编号）：面包屑、步骤、时间线。读屏里是列表。</summary>
public class FlexOrderedListControl : FlexListControl;

/// <summary>主轴方向（vue-tsx-pro 的 FlexDirections）。</summary>
/// <param name="Value">取值（row、column、row-reverse、column-reverse）。</param>
[TypeConverter(typeof(VariantConverter<FlexDirection>))]
public readonly record struct FlexDirection(string Value) : IVariant<FlexDirection>
{
    /// <summary>横排：子控件从起始边排到结束边。</summary>
    public static readonly FlexDirection Row = new("row");

    /// <summary>竖排：子控件从上到下。</summary>
    public static readonly FlexDirection Column = new("column");

    /// <summary>横排，顺序反过来。</summary>
    public static readonly FlexDirection RowReverse = new("row-reverse");

    /// <summary>竖排，顺序反过来。</summary>
    public static readonly FlexDirection ColumnReverse = new("column-reverse");

    /// <inheritdoc />
    public static IReadOnlyList<FlexDirection> Known { get; } = [Row, Column, RowReverse, ColumnReverse];

    /// <inheritdoc />
    public static FlexDirection Parse(string value)
    {
        var direction = new FlexDirection(value);
        Variants.WarnIfUnknown(direction);
        return direction;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>交叉轴对齐（vue-tsx-pro 的 FlexAligns，align-items）：横排时是竖直方向，竖排时是水平方向。</summary>
/// <param name="Value">取值（start、center、end、stretch、baseline）。</param>
[TypeConverter(typeof(VariantConverter<FlexAlign>))]
public readonly record struct FlexAlign(string Value) : IVariant<FlexAlign>
{
    /// <summary>贴着交叉轴的起点（横排时顶部对齐）。</summary>
    public static readonly FlexAlign Start = new("start");

    /// <summary>居中。</summary>
    public static readonly FlexAlign Center = new("center");

    /// <summary>贴着交叉轴的终点（横排时底部对齐）。</summary>
    public static readonly FlexAlign End = new("end");

    /// <summary>拉伸填满交叉轴（默认）。</summary>
    public static readonly FlexAlign Stretch = new("stretch");

    /// <summary>按文字基线对齐：字号不同的文字、图标与文字混排。</summary>
    public static readonly FlexAlign Baseline = new("baseline");

    /// <inheritdoc />
    public static IReadOnlyList<FlexAlign> Known { get; } = [Start, Center, End, Stretch, Baseline];

    /// <inheritdoc />
    public static FlexAlign Parse(string value)
    {
        var align = new FlexAlign(value);
        Variants.WarnIfUnknown(align);
        return align;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>主轴分布（vue-tsx-pro 的 FlexJustifies，justify-content）。</summary>
/// <param name="Value">取值（start、center、end、between、around、evenly）。</param>
[TypeConverter(typeof(VariantConverter<FlexJustify>))]
public readonly record struct FlexJustify(string Value) : IVariant<FlexJustify>
{
    /// <summary>靠主轴起点（默认）。</summary>
    public static readonly FlexJustify Start = new("start");

    /// <summary>居中。</summary>
    public static readonly FlexJustify Center = new("center");

    /// <summary>靠主轴终点。</summary>
    public static readonly FlexJustify End = new("end");

    /// <summary>两端对齐，中间等距：首尾贴边。</summary>
    public static readonly FlexJustify Between = new("between");

    /// <summary>每项两侧留白相等：首尾留白是中间的一半。</summary>
    public static readonly FlexJustify Around = new("around");

    /// <summary>所有空隙相等，包括首尾。</summary>
    public static readonly FlexJustify Evenly = new("evenly");

    /// <inheritdoc />
    public static IReadOnlyList<FlexJustify> Known { get; } = [Start, Center, End, Between, Around, Evenly];

    /// <inheritdoc />
    public static FlexJustify Parse(string value)
    {
        var justify = new FlexJustify(value);
        Variants.WarnIfUnknown(justify);
        return justify;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>换行（vue-tsx-pro 的 wrap：boolean | 'reverse'）。属性窗口里写 true、false 或 reverse；C# 里 bool 能直接赋值。</summary>
/// <param name="Value">取值（false、true、reverse）。</param>
[TypeConverter(typeof(VariantConverter<FlexWrap>))]
public readonly record struct FlexWrap(string Value) : IVariant<FlexWrap>
{
    /// <summary>不换行（默认）。</summary>
    public static readonly FlexWrap NoWrap = new("false");

    /// <summary>放不下时换行。</summary>
    public static readonly FlexWrap Wrap = new("true");

    /// <summary>反向换行：新的行在上面。</summary>
    public static readonly FlexWrap Reverse = new("reverse");

    /// <inheritdoc />
    public static IReadOnlyList<FlexWrap> Known { get; } = [NoWrap, Wrap, Reverse];

    /// <summary>会换行（true 或 reverse）。</summary>
    public bool Wraps => Value is "true" or "reverse";

    /// <summary>bool：true 换行，false 不换行。</summary>
    /// <param name="wrap">换不换行。</param>
    public static implicit operator FlexWrap(bool wrap) => wrap ? Wrap : NoWrap;

    /// <summary>True、False 不分大小写。</summary>
    /// <inheritdoc />
    public static FlexWrap Parse(string value)
    {
        var wrap = new FlexWrap(value.ToLower(CultureInfo.InvariantCulture));
        Variants.WarnIfUnknown(wrap);
        return wrap;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
