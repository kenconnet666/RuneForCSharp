using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf;

/// <summary>
/// 一维布局（vue-tsx-pro 的 Flex，CSS flexbox）：子元素沿主轴排开，间距是主题的 space 档位。不拆 Stack / Group：竖排写 Direction="column"。
/// <code>
/// &lt;rune:Flex Gap="sm" Align="center"&gt;&lt;rune:Icon … /&gt;&lt;rune:Text rune:Flex.Grow="True" Truncate="True"&gt;…&lt;/rune:Text&gt;&lt;/rune:Flex&gt;
/// &lt;rune:Flex Direction="column" Gap="md"&gt;…&lt;/rune:Flex&gt;
/// &lt;rune:Flex Wrap="True" Gap="sm" Justify="between"&gt;…&lt;/rune:Flex&gt;
/// </code>
/// 子元素写 <c>rune:Flex.Grow="True"</c> 分剩下的空间（也能缩到比内容窄，文字才能截断）；Flex 自己的 Grow 是同一个附加属性，放在外层 Flex 里时生效。
/// 不够放时各项按内容宽度加权收缩（CSS 的 flex-shrink: 1）。间距按 Flex 自己的字号算（em）。Inline 是主题样式的触发器（宽度跟着内容）。
/// </summary>
public class Flex : Panel
{
    /// <summary>主轴方向，默认 row。</summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction), typeof(FlexDirection), typeof(Flex), new FrameworkPropertyMetadata(default(FlexDirection), FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>交叉轴对齐（align-items），默认 stretch。</summary>
    public static readonly DependencyProperty AlignProperty = DependencyProperty.Register(
        nameof(Align), typeof(FlexAlign), typeof(Flex), new FrameworkPropertyMetadata(default(FlexAlign), FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>主轴分布（justify-content），默认 start。</summary>
    public static readonly DependencyProperty JustifyProperty = DependencyProperty.Register(
        nameof(Justify), typeof(FlexJustify), typeof(Flex), new FrameworkPropertyMetadata(default(FlexJustify), FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>换行；reverse 为反向换行。</summary>
    public static readonly DependencyProperty WrapProperty = DependencyProperty.Register(
        nameof(Wrap), typeof(FlexWrap), typeof(Flex), new FrameworkPropertyMetadata(default(FlexWrap), FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>间距档位，对应主题的 space。</summary>
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(Space), typeof(Flex), new FrameworkPropertyMetadata(default(Space), OnGapChanged));

    /// <summary>换行时行与行之间的间距，盖过 Gap。</summary>
    public static readonly DependencyProperty RowGapProperty = DependencyProperty.Register(
        nameof(RowGap), typeof(Space), typeof(Flex), new FrameworkPropertyMetadata(default(Space), OnGapChanged));

    /// <summary>同一行里项与项之间的间距，盖过 Gap。</summary>
    public static readonly DependencyProperty ColumnGapProperty = DependencyProperty.Register(
        nameof(ColumnGap), typeof(Space), typeof(Flex), new FrameworkPropertyMetadata(default(Space), OnGapChanged));

    /// <summary>行内 flex（inline-flex）：宽度跟着内容，不撑满（主题样式的触发器设 HorizontalAlignment）。</summary>
    public static readonly DependencyProperty InlineProperty = DependencyProperty.Register(
        nameof(Inline), typeof(bool), typeof(Flex), new FrameworkPropertyMetadata(false));

    /// <summary>在外层 Flex 里分剩下的空间，并能缩到比内容窄（附加属性：任何子元素都能写，Flex 自己也有）。</summary>
    public static readonly DependencyProperty GrowProperty = DependencyProperty.RegisterAttached(
        "Grow", typeof(bool), typeof(Flex), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>这一项自己的交叉轴对齐（CSS 的 align-self），盖过 Flex 的 Align（附加属性：竖向的 Separator 用它随行高拉伸）。</summary>
    public static readonly DependencyProperty AlignSelfProperty = DependencyProperty.RegisterAttached(
        "AlignSelf", typeof(FlexAlign), typeof(Flex), new FrameworkPropertyMetadata(default(FlexAlign), FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    static readonly DependencyProperty RowGapFactorProperty = DependencyProperty.Register(
        "RowGapFactor", typeof(double), typeof(Flex), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static readonly DependencyProperty ColumnGapFactorProperty = DependencyProperty.Register(
        "ColumnGapFactor", typeof(double), typeof(Flex), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    List<FlexLayout.Line> lines = [];
    double[] ascents = [];
    Size measured;

    static Flex()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Flex), new FrameworkPropertyMetadata(typeof(Flex)));
        TextElement.FontSizeProperty.OverrideMetadata(typeof(Flex), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));
    }

    /// <summary>主轴方向。</summary>
    public FlexDirection Direction { get => (FlexDirection)GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

    /// <summary>交叉轴对齐。</summary>
    public FlexAlign Align { get => (FlexAlign)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <summary>主轴分布。</summary>
    public FlexJustify Justify { get => (FlexJustify)GetValue(JustifyProperty); set => SetValue(JustifyProperty, value); }

    /// <summary>换行。</summary>
    public FlexWrap Wrap { get => (FlexWrap)GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

    /// <summary>间距档位。</summary>
    public Space Gap { get => (Space)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>行与行之间的间距。</summary>
    public Space RowGap { get => (Space)GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>项与项之间的间距。</summary>
    public Space ColumnGap { get => (Space)GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <summary>行内 flex。</summary>
    public bool Inline { get => (bool)GetValue(InlineProperty); set => SetValue(InlineProperty, value); }

    /// <summary>在外层 Flex 里分剩下的空间。</summary>
    public bool Grow { get => (bool)GetValue(GrowProperty); set => SetValue(GrowProperty, value); }

    /// <summary>读子元素的 Grow。</summary>
    public static bool GetGrow(UIElement element) => (bool)element.GetValue(GrowProperty);

    /// <summary>写子元素的 Grow。</summary>
    public static void SetGrow(UIElement element, bool value) => element.SetValue(GrowProperty, value);

    /// <summary>读子元素的 AlignSelf。</summary>
    public static FlexAlign GetAlignSelf(UIElement element) => (FlexAlign)element.GetValue(AlignSelfProperty);

    /// <summary>写子元素的 AlignSelf。</summary>
    public static void SetAlignSelf(UIElement element, FlexAlign value) => element.SetValue(AlignSelfProperty, value);

    /// <summary>一项实际的交叉轴对齐：自己的 AlignSelf，没写时是 Flex 的 Align。</summary>
    FlexAlign AlignOf(UIElement child) => GetAlignSelf(child) is { Value: not null } self ? self : Align;

    bool IsRow => Direction.Value is null or "row" or "row-reverse";

    bool IsReverse => Direction.Value is "row-reverse" or "column-reverse";

    /// <summary>主轴、交叉轴上的间距（像素）：横排时主轴是 column-gap，竖排时是 row-gap。</summary>
    (double Main, double Cross) Gaps
    {
        get
        {
            var em = TextElement.GetFontSize(this);
            var (row, column) = ((double)GetValue(RowGapFactorProperty) * em, (double)GetValue(ColumnGapFactorProperty) * em);
            return IsRow ? (column, row) : (row, column);
        }
    }

    static void OnGapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var flex = (Flex)d;
        Reference(flex, RowGapFactorProperty, (flex.RowGap.Value is null ? flex.Gap : flex.RowGap).ResourceKey);
        Reference(flex, ColumnGapFactorProperty, (flex.ColumnGap.Value is null ? flex.Gap : flex.ColumnGap).ResourceKey);
    }

    static void Reference(Flex flex, DependencyProperty property, string? key)
    {
        if (key is null) flex.ClearValue(property);
        else flex.SetResourceReference(property, key);
    }

    List<UIElement> Shown() => [.. InternalChildren.Cast<UIElement>().Where(child => child.Visibility != Visibility.Collapsed)];

    static double Main(Size size, bool row) => row ? size.Width : size.Height;

    static double Cross(Size size, bool row) => row ? size.Height : size.Width;

    static Size Of(double main, double cross, bool row) => row ? new Size(main, cross) : new Size(cross, main);

    /// <summary>
    /// 量：先按内容量出基础尺寸，分行、伸缩，再按分到的主轴尺寸量一次（文字换行后变高），得到每行的交叉轴尺寸。
    /// </summary>
    List<FlexLayout.Line> Compute(List<UIElement> children, double main, double cross)
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
                child.Measure(Of(line.Main[k], cross, row));
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
    static double MinMain(UIElement child, bool row) => child is not FrameworkElement element ? 0 : row
        ? double.IsNaN(element.Width) ? element.MinWidth : element.Width
        : double.IsNaN(element.Height) ? element.MinHeight : element.Height;

    /// <summary>
    /// 第一行文字的基线离上边多远：直接的 TextBlock 按字体的度量算（BlockLineHeight 时基线按行高等比放），
    /// 其余按底边（CSS 给没有基线的项合成的基线）。
    /// </summary>
    static double? Baseline(UIElement child)
    {
        if (child is not TextBlock text) return null;
        var family = text.FontFamily;
        var top = text.Padding.Top + text.Margin.Top;
        return text.LineStackingStrategy == LineStackingStrategy.BlockLineHeight && !double.IsNaN(text.LineHeight)
            ? top + text.LineHeight * family.Baseline / family.LineSpacing
            : top + family.Baseline * text.FontSize;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var row = IsRow;
        lines = Compute(Shown(), Main(availableSize, row), Cross(availableSize, row));
        measured = availableSize;
        var gaps = Gaps;
        var main = lines.Select(line => line.Main.Sum() + gaps.Main * (line.Count - 1)).DefaultIfEmpty().Max();
        var cross = lines.Sum(line => line.Cross) + gaps.Cross * Math.Max(0, lines.Count - 1);
        return Of(main, cross, row);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var row = IsRow;
        var children = Shown();
        var (main, cross) = (Main(finalSize, row), Cross(finalSize, row));
        // 摆的主轴尺寸和量的时候不一样（父元素拉伸了）：按摆的尺寸重新分
        if (Math.Abs(main - Main(measured, row)) > 0.01) lines = Compute(children, main, double.IsInfinity(Cross(measured, row)) ? double.PositiveInfinity : cross);
        var gaps = Gaps;
        FlexLayout.Stretch(lines, cross, gaps.Cross, Wrap.Wraps);
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

    sealed class ListPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => Owner.GetType().Name;
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
