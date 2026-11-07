using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;

namespace Rune.Avalonia;

/// <summary>
/// 一维布局（vue-tsx-pro 的 Flex，CSS flexbox）：子元素沿主轴排开，间距是主题的 space 档位。不拆 Stack / Group：竖排写 Direction="column"。
/// <code>
/// &lt;rune:Flex Gap="sm" Align="center"&gt;&lt;rune:Icon … /&gt;&lt;rune:Text rune:Flex.Grow="True" Truncate="True"&gt;…&lt;/rune:Text&gt;&lt;/rune:Flex&gt;
/// &lt;rune:Flex Direction="column" Gap="md"&gt;…&lt;/rune:Flex&gt;
/// &lt;rune:Flex Wrap="True" Gap="sm" Justify="between"&gt;…&lt;/rune:Flex&gt;
/// </code>
/// 子元素写 <c>rune:Flex.Grow="True"</c> 分剩下的空间（也能缩到比内容窄，文字才能截断）；Flex 自己的 Grow 是同一个附加属性，放在外层 Flex 里时生效。
/// 不够放时各项按内容宽度加权收缩，最多缩到内容的最小宽度（CSS 的 flex-shrink: 1）。间距按 Flex 自己的字号算（em）。
/// 自己的大小照平台面板的规矩（与 StackPanel、Grid 一样）：VerticalAlignment、HorizontalAlignment 默认 Stretch，放在定高的宿主里会被拉满，
/// Align="center" 的一行这时落在整块高度的中间；CSS 里 flex 容器的高度跟着内容。要贴着上边就写 VerticalAlignment="Top"。
/// </summary>
public class Flex : Panel
{
    /// <summary>主轴方向，默认 row。</summary>
    public static readonly StyledProperty<FlexDirection> DirectionProperty = AvaloniaProperty.Register<Flex, FlexDirection>(nameof(Direction));

    /// <summary>交叉轴对齐（align-items），默认 stretch。</summary>
    public static readonly StyledProperty<FlexAlign> AlignProperty = AvaloniaProperty.Register<Flex, FlexAlign>(nameof(Align));

    /// <summary>主轴分布（justify-content），默认 start。</summary>
    public static readonly StyledProperty<FlexJustify> JustifyProperty = AvaloniaProperty.Register<Flex, FlexJustify>(nameof(Justify));

    /// <summary>换行；reverse 为反向换行。</summary>
    public static readonly StyledProperty<FlexWrap> WrapProperty = AvaloniaProperty.Register<Flex, FlexWrap>(nameof(Wrap));

    /// <summary>间距档位，对应主题的 space。</summary>
    public static readonly StyledProperty<Space> GapProperty = AvaloniaProperty.Register<Flex, Space>(nameof(Gap));

    /// <summary>换行时行与行之间的间距，盖过 Gap。</summary>
    public static readonly StyledProperty<Space> RowGapProperty = AvaloniaProperty.Register<Flex, Space>(nameof(RowGap));

    /// <summary>同一行里项与项之间的间距，盖过 Gap。</summary>
    public static readonly StyledProperty<Space> ColumnGapProperty = AvaloniaProperty.Register<Flex, Space>(nameof(ColumnGap));

    /// <summary>行内 flex（inline-flex）：宽度跟着内容，不撑满。</summary>
    public static readonly StyledProperty<bool> InlineProperty = AvaloniaProperty.Register<Flex, bool>(nameof(Inline));

    /// <summary>在外层 Flex 里分剩下的空间，并能缩到比内容窄（附加属性：任何子元素都能写，Flex 自己也有）。</summary>
    public static readonly AttachedProperty<bool> GrowProperty = AvaloniaProperty.RegisterAttached<Flex, Control, bool>("Grow");

    /// <summary>这一项自己的交叉轴对齐（CSS 的 align-self），盖过 Flex 的 Align（附加属性：竖向的 Separator 用它随行高拉伸）。</summary>
    public static readonly AttachedProperty<FlexAlign> AlignSelfProperty = AvaloniaProperty.RegisterAttached<Flex, Control, FlexAlign>("AlignSelf");

    static readonly StyledProperty<double> RowGapFactorProperty = AvaloniaProperty.Register<Flex, double>("RowGapFactor");
    static readonly StyledProperty<double> ColumnGapFactorProperty = AvaloniaProperty.Register<Flex, double>("ColumnGapFactor");

    readonly TemplateResources resources;
    IDisposable? alignment;
    List<FlexLayout.Line> lines = [];
    double[] ascents = [];
    Size measured;
    double contentMain;

    static Flex()
    {
        AffectsMeasure<Flex>(DirectionProperty, AlignProperty, WrapProperty, RowGapFactorProperty, ColumnGapFactorProperty, TextElement.FontSizeProperty);
        AffectsArrange<Flex>(JustifyProperty);
        AffectsParentMeasure<Flex>(GrowProperty);
        AlignSelfProperty.Changed.AddClassHandler<Control>((control, _) => (control.Parent as Flex)?.InvalidateMeasure());
    }

    /// <summary>新建。</summary>
    public Flex() => resources = new TemplateResources(this);

    /// <summary>主轴方向。</summary>
    public FlexDirection Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

    /// <summary>交叉轴对齐。</summary>
    public FlexAlign Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>主轴分布。</summary>
    public FlexJustify Justify { get => GetValue(JustifyProperty); set => SetValue(JustifyProperty, value); }

    /// <summary>换行。</summary>
    public FlexWrap Wrap { get => GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

    /// <summary>间距档位。</summary>
    public Space Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>行与行之间的间距。</summary>
    public Space RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>项与项之间的间距。</summary>
    public Space ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <summary>行内 flex。</summary>
    public bool Inline { get => GetValue(InlineProperty); set => SetValue(InlineProperty, value); }

    /// <summary>在外层 Flex 里分剩下的空间。</summary>
    public bool Grow { get => GetValue(GrowProperty); set => SetValue(GrowProperty, value); }

    /// <summary>读子元素的 Grow。</summary>
    public static bool GetGrow(Control element) => element.GetValue(GrowProperty);

    /// <summary>写子元素的 Grow。</summary>
    public static void SetGrow(Control element, bool value) => element.SetValue(GrowProperty, value);

    /// <summary>读子元素的 AlignSelf。</summary>
    public static FlexAlign GetAlignSelf(Control element) => element.GetValue(AlignSelfProperty);

    /// <summary>写子元素的 AlignSelf。</summary>
    public static void SetAlignSelf(Control element, FlexAlign value) => element.SetValue(AlignSelfProperty, value);

    /// <summary>一项实际的交叉轴对齐：自己的 AlignSelf，没写时是 Flex 的 Align。</summary>
    FlexAlign AlignOf(Control child) => GetAlignSelf(child) is { Value: not null } self ? self : Align;

    bool IsRow => Direction.Value is null or "row" or "row-reverse";

    bool IsReverse => Direction.Value is "row-reverse" or "column-reverse";

    /// <summary>主轴、交叉轴上的间距（像素）：横排时主轴是 column-gap，竖排时是 row-gap。</summary>
    (double Main, double Cross) Gaps
    {
        get
        {
            var (row, column) = (GetValue(RowGapFactorProperty) * Em, GetValue(ColumnGapFactorProperty) * Em);
            return IsRow ? (column, row) : (row, column);
        }
    }

    double Em => GetValue(TextElement.FontSizeProperty);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GapProperty || change.Property == RowGapProperty || change.Property == ColumnGapProperty)
        {
            resources.Bind(RowGapFactorProperty, (RowGap.Value is null ? Gap : RowGap).ResourceKey);
            resources.Bind(ColumnGapFactorProperty, (ColumnGap.Value is null ? Gap : ColumnGap).ResourceKey);
        }
        else if (change.Property == InlineProperty)
        {
            // 模板优先级：使用方写的 HorizontalAlignment 盖得过
            alignment?.Dispose();
            alignment = Inline ? SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left, BindingPriority.Template) : null;
        }
    }

    List<Control> Shown() => [.. Children.Where(child => child.IsVisible)];

    static double Main(Size size, bool row) => row ? size.Width : size.Height;

    static double Cross(Size size, bool row) => row ? size.Height : size.Width;

    static Size Of(double main, double cross, bool row) => row ? new Size(main, cross) : new Size(cross, main);

    /// <summary>
    /// 量：先按内容量出基础尺寸，分行、伸缩，再按分到的主轴尺寸量一次（文字换行后变高），得到每行的交叉轴尺寸。
    /// </summary>
    List<FlexLayout.Line> Compute(List<Control> children, double main, double cross)
    {
        var row = IsRow;
        var gaps = Gaps;
        var items = new FlexLayout.Item[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            children[i].Measure(Of(double.PositiveInfinity, cross, row));
            items[i] = new FlexLayout.Item(Main(children[i].DesiredSize, row), GetGrow(children[i]));
        }
        var result = FlexLayout.Distribute(items, main, gaps.Main, Wrap.Wraps, i => MinMain(children[i], row));
        ascents = new double[children.Count];
        foreach (var line in result)
        {
            var sizes = new (double Cross, double Ascent, FlexAlign Align)[line.Count];
            for (var k = 0; k < line.Count; k++)
            {
                var child = children[line.Start + k];
                // 分到的就是自己要的（没有伸缩）时照样按不限量：按分到的尺寸量，子元素以后长高了会被这个约束夹住、报不上来
                var given = line.Main[k];
                child.Measure(Of(Math.Abs(given - items[line.Start + k].Basis) < 0.01 ? double.PositiveInfinity : given, cross, row));
                var size = Cross(child.DesiredSize, row);
                sizes[k] = (size, ascents[line.Start + k] = Baseline(child) ?? size, AlignOf(child));
            }
            line.Cross = FlexLayout.LineCross(sizes).Cross;
        }
        return result;
    }

    /// <summary>
    /// 收缩时最多缩到多小：写了宽（高）就是它，否则是 MinWidth（MinHeight）。CSS 还会停在内容的最小宽度（最长的词），
    /// 平台的测量结果不会超过给的尺寸、量不出这个值，所以同 <c>min-width: 0</c>：文字换行或截断。
    /// </summary>
    static double MinMain(Control child, bool row) => row
        ? double.IsNaN(child.Width) ? child.MinWidth : child.Width
        : double.IsNaN(child.Height) ? child.MinHeight : child.Height;

    /// <summary>第一行文字的基线离上边多远：直接的 TextBlock 才有，其余按底边（CSS 给没有基线的项合成的基线）。</summary>
    static double? Baseline(Control child) =>
        child is TextBlock { TextLayout.TextLines: [var first, ..] } text ? text.Padding.Top + first.Baseline : null;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var row = IsRow;
        var children = Shown();
        lines = Compute(children, Main(availableSize, row), Cross(availableSize, row));
        measured = availableSize;
        var gaps = Gaps;
        var main = contentMain = lines.Select(line => line.Main.Sum() + gaps.Main * (line.Count - 1)).DefaultIfEmpty().Max();
        var cross = lines.Sum(line => line.Cross) + gaps.Cross * Math.Max(0, lines.Count - 1);
        return Of(main, cross, row);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var row = IsRow;
        var children = Shown();
        var (main, cross) = (Main(finalSize, row), Cross(finalSize, row));
        // 摆的主轴尺寸和量的时候不一样（父元素拉伸了）：按摆的尺寸重新分。量的时候不限、摆的正好是内容的尺寸时分法不变，
        // 不重分：重分会按有限的尺寸重量子元素，子元素以后长高了会被这个约束夹住、报不上来
        if (Math.Abs(main - Main(measured, row)) > 0.01 && Math.Abs(main - contentMain) > 0.01) lines = Compute(children, main, double.IsInfinity(Cross(measured, row)) ? double.PositiveInfinity : cross);
        var gaps = Gaps;
        var wrap = Wrap.Wraps;
        FlexLayout.Stretch(lines, cross, gaps.Cross, wrap);
        var ordered = Wrap.Value == "reverse" ? Enumerable.Reverse(lines) : lines;
        var y = 0d;
        foreach (var line in ordered)
        {
            var positions = FlexLayout.Positions(line.Main, gaps.Main, main, Justify, IsReverse);
            var items = Enumerable.Range(0, line.Count).Select(k => (Cross(children[line.Start + k].DesiredSize, row), ascents[line.Start + k], AlignOf(children[line.Start + k]))).ToList();
            var lineAscent = FlexLayout.LineCross(items).Ascent;
            for (var k = 0; k < line.Count; k++)
            {
                var (offset, size) = FlexLayout.Align(items[k].Item1, line.Cross, items[k].Item3, items[k].Item2, lineAscent);
                var rect = row
                    ? new Rect(positions[k], y + offset, line.Main[k], size)
                    : new Rect(y + offset, positions[k], size, line.Main[k]);
                children[line.Start + k].Arrange(rect);
            }
            y += line.Cross + gaps.Cross;
        }
        return finalSize;
    }
}

/// <summary>flex 布局的列表（vue-tsx-pro 的 FlexList，ul 去掉圆点）：读屏里是列表。</summary>
public class FlexList : Flex
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    sealed class ListPeer(Control owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}

/// <summary>有序列表的 flex 布局（vue-tsx-pro 的 FlexOrderedList，ol 去掉编号）：面包屑、步骤、时间线。读屏里是列表。</summary>
public class FlexOrderedList : FlexList;

/// <summary>主轴方向（vue-tsx-pro 的 FlexDirections）。</summary>
/// <param name="Value">取值（row、column、row-reverse、column-reverse）。</param>
[TypeConverter(typeof(VariantConverter<FlexDirection>))]
public readonly record struct FlexDirection(string Value) : IVariant<FlexDirection>
{
    /// <summary>横排：子元素从起始边排到结束边。</summary>
    public static readonly FlexDirection Row = new("row");

    /// <summary>竖排：子元素从上到下。</summary>
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

/// <summary>换行（vue-tsx-pro 的 wrap：boolean | 'reverse'）。XAML 里写 True、False 或 reverse；C# 里 bool 能直接赋值。</summary>
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

    /// <summary>XAML 的 True、False 不分大小写。</summary>
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
