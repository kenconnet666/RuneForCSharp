using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 统计数值（vue-tsx-pro 的 Stat）：指标名、数值与单位、可选的图标与环比变化。仪表盘顶部的一排卡片、详情页的关键指标。外观只是内容，放进 Card、Grid 里组合。
/// <code>
/// &lt;rune:Card&gt;&lt;rune:Stat Label="实际销售额" Value="910.36" Unit="万元" Icon="{x:Static rune:Lucide.Wallet}" Change="0.123" ChangeLabel="较上月" /&gt;&lt;/rune:Card&gt;
/// </code>
/// 排版（参考 Ant Design Pro 的 ChartCard）：第一行是图标与指标名（sm）、结束边是变化量（xs，放不下时换到下一行）；第二行是数值（xxl、粗体、行高 tight）与单位（xs、次要色）；
/// 再下面是说明（xs、次要色）、内容（进度条、迷你图，占满整宽）与底栏（上方一条分隔线，xs、次要色）。行与行之间 space.xs。
/// 数值是 decimal 时按 <see cref="Format"/> 格式化并在变化时滚动（AnimatedNumber）；写 <see cref="Text"/> 时原样显示（如"—""98.5%"）。
/// 变化量默认按百分比、带符号（+12.3%），上升成功色、下降危险色，<see cref="InvertChange"/> 反过来（成本、耗时）。<see cref="Tone"/> 给数值着色。
/// 加载中（IsLoading）数值与变化量换成骨架，指标名照常显示；很快完成时不闪，出现过骨架之后换回数值时淡入。读屏里是一组，名称是指标名，忙时状态是"加载中"。
/// 放进 <see cref="StatGroup"/> 时起始边有一条竖线、左右各空 space.md。部件样式键：<see cref="StatParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class Stat : Control
{
    /// <summary>指标名。</summary>
    public static readonly DependencyProperty LabelProperty = Register(nameof(Label), typeof(string), null);

    /// <summary>数值：按 Format 格式化，变化时滚动。</summary>
    public static readonly DependencyProperty ValueProperty = Register(nameof(Value), typeof(decimal), 0m);

    /// <summary>原样显示的数值（TS 的字符串 value，如"—""98.5%"）；写了时盖过 Value。</summary>
    public static readonly DependencyProperty TextProperty = Register(nameof(Text), typeof(string), null);

    /// <summary>数值的格式，如两位小数、百分比、按万元（Scale = 10000）。</summary>
    public static readonly DependencyProperty FormatProperty = Register(nameof(Format), typeof(NumericFormat), null);

    /// <summary>单位，跟在数值后面、字号小一档。</summary>
    public static readonly DependencyProperty UnitProperty = Register(nameof(Unit), typeof(string), null);

    /// <summary>指标名前面的图标（装饰，对读屏隐藏）。</summary>
    public static readonly DependencyProperty IconProperty = Register(nameof(Icon), typeof(IconData), null);

    /// <summary>变化量（环比、同比）：正数向上、负数向下；默认按百分比显示（0.12 → +12%）。不写时没有变化量。</summary>
    public static readonly DependencyProperty ChangeProperty = Register(nameof(Change), typeof(decimal?), null);

    /// <summary>变化量的格式，默认百分比、最多 1 位小数、正负都带符号（0 不带）。</summary>
    public static readonly DependencyProperty ChangeFormatProperty = Register(nameof(ChangeFormat), typeof(NumericFormat), null);

    /// <summary>变化量后面的说明，如"较上月"。</summary>
    public static readonly DependencyProperty ChangeLabelProperty = Register(nameof(ChangeLabel), typeof(string), null);

    /// <summary>下降是好事（成本、耗时、故障数）：下降显示成功色、上升显示危险色。</summary>
    public static readonly DependencyProperty InvertChangeProperty = Register(nameof(InvertChange), typeof(bool), false);

    /// <summary>数值下面的补充说明（一行小字）。</summary>
    public static readonly DependencyProperty DescriptionProperty = Register(nameof(Description), typeof(string), null);

    /// <summary>底栏：上方一条分隔线。</summary>
    public static readonly DependencyProperty FooterProperty = Register(nameof(Footer), typeof(object), null);

    /// <summary>数值下面占满整宽的补充内容：进度条、迷你图。</summary>
    public static readonly DependencyProperty ContentProperty = Register(nameof(Content), typeof(object), null);

    /// <summary>数值的色调：超时、缺漏这类要引起注意的数用 danger、warning；不写时是正文色。</summary>
    public static readonly DependencyProperty ToneProperty = Register(nameof(Tone), typeof(TextTone), default(TextTone));

    /// <summary>加载中：数值与变化量换成骨架；很快完成时不闪。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(Stat), new FrameworkPropertyMetadata(false, (d, _) => ((Stat)d).loading.Update(((Stat)d).IsLoading)));

    /// <summary>变化量的默认格式（TS 的 CHANGE_FORMAT）。</summary>
    public static NumericFormat DefaultChangeFormat { get; } = new() { Style = NumericStyle.Percent, MaximumFractionDigits = 1, SignDisplay = NumericSign.ExceptZero };

    readonly Border root = new();
    readonly StatLayout column = new();
    readonly Flex label = new() { Name = "PART_Label", Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Border iconBox = new() { Name = "PART_Icon" };
    readonly Icon icon = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Text labelText = new();
    readonly Flex value = new() { Name = "PART_Value", Wrap = true, Align = FlexAlign.Baseline, Gap = Space.Xs };
    readonly AnimatedNumber number = new() { Name = "PART_Number" };
    readonly Text text = new() { Name = "PART_Number", Numeric = true };
    readonly Text unit = new() { Name = "PART_Unit" };
    readonly Skeleton numberSkeleton = new();
    readonly Flex change = new() { Name = "PART_Change", Align = FlexAlign.Center, Gap = Space.Xs };
    // 箭头与数字之间一个空格宽（TS 里是一个空格）
    readonly Flex trend = new() { Align = FlexAlign.Center, Gap = Space.Xs };
    readonly Icon trendIcon = new();
    readonly AnimatedNumber changeNumber = new();
    readonly Text changeLabel = new() { Name = "PART_ChangeLabel" };
    readonly Skeleton changeSkeleton = new();
    readonly Text description = new() { Name = "PART_Description" };
    readonly ContentPresenter body = new() { Name = "PART_Body" };
    readonly Border footer = new() { Name = "PART_Footer" };
    readonly ContentPresenter footerContent = new();
    DelayedLoading loading;
    bool revealing;

    static Stat()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Stat), new FrameworkPropertyMetadata(typeof(Stat)));
        FocusableProperty.OverrideMetadata(typeof(Stat), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Stat), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(Stat), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((Stat)d).UpdateMetrics()));
    }

    /// <summary>新建：数值 0。</summary>
    public Stat()
    {
        foreach (var (part, key) in new (FrameworkElement, PartKey)[]
        {
            (label, StatParts.Label), (iconBox, StatParts.Icon), (value, StatParts.Value), (unit, StatParts.Unit), (change, StatParts.Change),
            (changeLabel, StatParts.ChangeLabel), (description, StatParts.Description), (body, StatParts.Body), (footer, StatParts.Footer),
        }) part.SetResourceReference(StyleProperty, key);
        number.SetResourceReference(StyleProperty, StatParts.Number);
        text.SetResourceReference(StyleProperty, StatParts.Text);
        iconBox.Child = icon;
        label.Children.Add(iconBox);
        label.Children.Add(labelText);
        trend.Children.Add(trendIcon);
        trend.Children.Add(changeNumber);
        foreach (var part in new UIElement[] { trend, changeLabel, changeSkeleton }) change.Children.Add(part);
        foreach (var part in new UIElement[] { number, text, unit, numberSkeleton }) value.Children.Add(part);
        footer.Child = footerContent;
        // 子元素按读屏的顺序（指标名、数值、变化量……），变化量在排版时放到第一行的结束边
        foreach (var part in new UIElement[] { label, value, change, description, body, footer }) column.Children.Add(part);
        root.Child = column;
        // 组里的竖线与内边距写在 Stat 自己的 BorderThickness、Padding 上（样式盖得过，inspect 也按 Stat 报），里面的 Border 绑过去
        foreach (var (target, source) in new[] { (Border.PaddingProperty, nameof(Padding)), (Border.BorderThicknessProperty, nameof(BorderThickness)), (Border.BorderBrushProperty, nameof(BorderBrush)) })
            root.SetBinding(target, new System.Windows.Data.Binding(source) { Source = this });
        AddVisualChild(root);
        AddLogicalChild(root);
        loading = new DelayedLoading(ShowSkeleton);
        Loaded += (_, _) =>
        {
            UpdateGrouped();
            if (!IsLoading || loading.Shown) return;
            loading.Stop();
            loading = new DelayedLoading(ShowSkeleton, loading: true);
            ShowSkeleton(true);
        };
        Unloaded += (_, _) => loading.Stop();
        Update();
        UpdateMetrics();
    }

    static DependencyProperty Register(string name, Type type, object? fallback) =>
        DependencyProperty.Register(name, type, typeof(Stat), new FrameworkPropertyMetadata(fallback, (d, e) => ((Stat)d).OnPartChanged(e)));

    /// <summary>指标名。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>数值。</summary>
    public decimal Value { get => (decimal)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>原样显示的数值。</summary>
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>数值的格式。</summary>
    public NumericFormat? Format { get => (NumericFormat?)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>单位。</summary>
    public string? Unit { get => (string?)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <summary>指标名前面的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>变化量。</summary>
    public decimal? Change { get => (decimal?)GetValue(ChangeProperty); set => SetValue(ChangeProperty, value); }

    /// <summary>变化量的格式。</summary>
    public NumericFormat? ChangeFormat { get => (NumericFormat?)GetValue(ChangeFormatProperty); set => SetValue(ChangeFormatProperty, value); }

    /// <summary>变化量后面的说明。</summary>
    public string? ChangeLabel { get => (string?)GetValue(ChangeLabelProperty); set => SetValue(ChangeLabelProperty, value); }

    /// <summary>下降是好事。</summary>
    public bool InvertChange { get => (bool)GetValue(InvertChangeProperty); set => SetValue(InvertChangeProperty, value); }

    /// <summary>补充说明。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>底栏。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>补充内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>数值的色调。</summary>
    public TextTone Tone { get => (TextTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>变化的方向：up、down、flat（TS 的 data-trend）；没有变化量时为 null。</summary>
    public string? Direction => Change is not { } delta ? null : delta > 0 ? "up" : delta < 0 ? "down" : "flat";

    /// <summary>变化量的色调：持平是次要色，改善是成功色，变差是危险色。</summary>
    internal TextTone ChangeTone => Direction switch
    {
        "up" => InvertChange ? TextTone.Danger : TextTone.Success,
        "down" => InvertChange ? TextTone.Success : TextTone.Danger,
        _ => TextTone.Muted,
    };

    /// <summary>骨架在显示吗（测试用）。</summary>
    internal bool SkeletonShown => numberSkeleton.Visibility == Visibility.Visible;

    /// <summary>出现过骨架、换回数值时淡入（测试用）。</summary>
    internal bool Revealing => revealing;

    /// <summary>部件（测试用）。</summary>
    internal (Border Icon, AnimatedNumber Number, Text Text, Text Unit, Flex Change, AnimatedNumber ChangeNumber, Text ChangeLabel, Border Footer) Parts =>
        (iconBox, number, text, unit, change, changeNumber, changeLabel, footer);

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    void OnPartChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.Property == ValueProperty && Text is null) number.Value = Value;
        else Update();
    }

    void Update()
    {
        icon.Data = Icon;
        iconBox.Visibility = Shown(Icon is not null);
        labelText.Text = Label;
        AutomationProperties.SetName(this, Label ?? "");
        number.Value = Value;
        number.Format = Format;
        number.Tone = text.Tone = Tone;
        text.Text = Text;
        unit.Text = Unit;
        changeNumber.Value = Change ?? 0;
        changeNumber.Format = ChangeFormat ?? DefaultChangeFormat;
        trendIcon.Data = Direction == "up" ? Lucide.TrendingUp : Lucide.TrendingDown;
        trendIcon.Visibility = Shown(Direction is "up" or "down");
        if (ChangeTone.BrushKey is { } key) trend.SetResourceReference(TextElement.ForegroundProperty, key);
        else trend.ClearValue(TextElement.ForegroundProperty);
        changeLabel.Text = ChangeLabel;
        description.Text = Description;
        description.Visibility = Shown(!string.IsNullOrEmpty(Description));
        body.Content = Content;
        body.Visibility = Shown(Content is not null);
        footerContent.Content = Footer;
        footer.Visibility = Shown(Footer is not null);
        ShowParts(loading.Shown);
    }

    static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>数值、变化量与骨架互换；没有变化量时整块不显示。</summary>
    void ShowParts(bool skeleton)
    {
        number.Visibility = Shown(!skeleton && Text is null);
        text.Visibility = Shown(!skeleton && Text is not null);
        unit.Visibility = Shown(!skeleton && Unit is not null);
        numberSkeleton.Visibility = Shown(skeleton);
        change.Visibility = Shown(Change is not null);
        trend.Visibility = Shown(!skeleton);
        changeLabel.Visibility = Shown(!skeleton && ChangeLabel is not null);
        changeSkeleton.Visibility = Shown(skeleton);
    }

    /// <summary>骨架与数值互换：出现过骨架之后换回数值时数值与变化量淡入（一开始就有数值的不播）。</summary>
    void ShowSkeleton(bool shown)
    {
        ShowParts(shown);
        if (shown) revealing = true;
        if (shown || !revealing) return;
        var duration = Motion.Duration(this, "Rune.Duration.Lg", TimeSpan.FromMilliseconds(300));
        if (duration <= TimeSpan.Zero) return;
        foreach (var part in new UIElement[] { value, change })
            part.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = TryFindResource("Rune.Easing.Enter") as IEasingFunction });
    }

    /// <summary>在 StatGroup 里：起始边一条竖线、左右各空 space.md；竖线往起始方向挪一条线宽，每行第一个的竖线被组裁掉。</summary>
    void UpdateGrouped()
    {
        var grouped = Parent is StatGroup || VisualTreeHelper.GetParent(this) is StatGroup;
        var line = Resource("Rune.BorderWidth.Xs", 1.0);
        var md = Resource("Rune.Space.Md", 0.75) * FontSize;
        SetCurrentValue(BorderThicknessProperty, grouped ? new Thickness(line, 0, 0, 0) : default);
        SetCurrentValue(PaddingProperty, grouped ? new Thickness(md, 0, md, 0) : default);
        root.Margin = grouped ? new Thickness(-line, 0, 0, 0) : default;
        if (grouped) SetResourceReference(BorderBrushProperty, "Rune.Brush.BorderMuted");
    }

    double Resource(string key, double fallback) => TryFindResource(key) is double value ? value : fallback;

    /// <summary>em 尺寸：指标名一行 sm（图标框 1.75em、圆角 md 按它算）；数值 xxl；单位、变化量、说明、底栏 xs；骨架同数值、变化量高。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        var (xs, sm, xxl) = (Resource("Rune.FontSize.Xs", 12.0), Resource("Rune.FontSize.Sm", 13.0), Resource("Rune.FontSize.Xxl", 24.0));
        label.SetValue(TextElement.FontSizeProperty, sm);
        iconBox.Width = iconBox.Height = 1.75 * sm;
        iconBox.CornerRadius = new CornerRadius(Resource("Rune.Radius.Md", 0.4) * sm);
        icon.Size = sm;
        column.Gap = Resource("Rune.Space.Xs", 0.25) * em;
        column.ColumnGap = Resource("Rune.Space.Sm", 0.5) * em;
        number.FontSize = text.FontSize = xxl;
        unit.FontSize = description.FontSize = xs;
        change.SetValue(TextElement.FontSizeProperty, xs);
        footer.SetValue(TextElement.FontSizeProperty, xs);
        // 底栏是 xs 的字号：上面多空 space.xs、线下面空 space.sm 都按它算
        footer.Margin = new Thickness(0, Resource("Rune.Space.Xs", 0.25) * xs, 0, 0);
        footer.Padding = new Thickness(0, Resource("Rune.Space.Sm", 0.5) * xs, 0, 0);
        footer.BorderThickness = new Thickness(0, Resource("Rune.BorderWidth.Xs", 1.0), 0, 0);
        numberSkeleton.Width = 4 * xxl;
        numberSkeleton.Height = 1.25 * xxl;
        changeSkeleton.Width = 6 * xs;
        changeSkeleton.Height = 1.5 * xs;
        UpdateGrouped();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        root.Measure(constraint);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        root.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new StatPeer(this);

    /// <summary>读屏：一组，名称是指标名（指标名一行不另念），里面是数值、变化量与说明；加载中状态是"加载中"。</summary>
    sealed class StatPeer(Stat owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Stat);

        protected override string GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();

        protected override List<AutomationPeer>? GetChildrenCore() =>
            base.GetChildrenCore()?.Where(peer => peer is not UIElementAutomationPeer { Owner: var element } || (element != owner.label && !owner.label.IsAncestorOf(element))).ToList();
    }
}

/// <summary>
/// Stat 的排版（TS 是带 order 的可换行 flex）：第一行是指标名（占满剩下的宽度）与结束边的变化量，放不下时变化量换到下一行；
/// 之后数值、说明、内容、底栏各占一整行。行与行之间 Gap，指标名与变化量之间 ColumnGap；隐藏的不占位。子元素的顺序是读屏的顺序。
/// </summary>
sealed class StatLayout : Panel
{
    public double Gap { get; set; }

    public double ColumnGap { get; set; }

    // 子元素：0 指标名、1 数值、2 变化量、之后各占一整行
    List<(UIElement Child, int Row)> Rows(double width)
    {
        var (label, change) = (InternalChildren[0], InternalChildren[2]);
        var wrapped = change.Visibility == Visibility.Visible && label.Visibility == Visibility.Visible && label.DesiredSize.Width + ColumnGap + change.DesiredSize.Width > width;
        var rows = new List<(UIElement, int)> { (label, 0), (change, wrapped ? 1 : 0), (InternalChildren[1], 2) };
        for (var index = 3; index < InternalChildren.Count; index++) rows.Add((InternalChildren[index], index));
        return [.. rows.Where(row => row.Item1.Visibility != Visibility.Collapsed)];
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren) child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var width = double.IsInfinity(availableSize.Width) ? InternalChildren.Cast<UIElement>().Max(child => child.DesiredSize.Width) : availableSize.Width;
        return new Size(width, Stack(width, arrange: false));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Stack(finalSize.Width, arrange: true);
        return finalSize;
    }

    double Stack(double width, bool arrange)
    {
        var y = 0.0;
        var first = true;
        var (label, change) = (InternalChildren[0], InternalChildren[2]);
        foreach (var group in Rows(width).GroupBy(row => row.Row))
        {
            if (!first) y += Gap;
            first = false;
            var height = group.Max(row => row.Child.DesiredSize.Height);
            var shared = group.Count() > 1;
            if (arrange)
                foreach (var (child, _) in group)
                {
                    // 第一行：指标名靠起始边、变化量靠结束边，竖直居中；其余占满整宽
                    var size = child.DesiredSize;
                    var inRow = child == label || child == change;
                    var x = child == change && shared ? width - size.Width : 0;
                    var w = child == label && shared ? width - change.DesiredSize.Width - ColumnGap : child == change ? size.Width : width;
                    child.Arrange(new Rect(x, y + (inRow ? (height - size.Height) / 2 : 0), Math.Max(0, w), inRow ? size.Height : height));
                }
            y += height;
        }
        return y;
    }
}

/// <summary>
/// 一排指标（vue-tsx-pro 的 StatGroup）：按宽度换行（每个不窄于 MinItemWidth，默认 9rem），同一行里指标之间一条竖线（borderMuted）。
/// 是一个 Grid：行间距 space.md、没有栏间距，裁掉每行第一个往外挪出去的竖线。里面的链接悬停才有下划线。
/// </summary>
public class StatGroup : Grid
{
    /// <summary>新建：每个指标最窄 9rem。</summary>
    public StatGroup()
    {
        MinItemWidth = new Length("9rem");
        RowGap = Space.Md;
        ColumnGap = Space.None;
        ClipToBounds = true;
        Link.SetUnderlineScope(this, LinkUnderline.Hover);
    }
}

/// <summary>Stat 的部件样式键。</summary>
public static class StatParts
{
    /// <summary>指标名（TS 的 propsLabel）。</summary>
    public static readonly PartKey Label = new(typeof(Stat), nameof(Label), typeof(Flex));

    /// <summary>图标框（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Stat), nameof(Icon), typeof(Border));

    /// <summary>数值一行（TS 的 propsValue）。</summary>
    public static readonly PartKey Value = new(typeof(Stat), nameof(Value), typeof(Flex));

    /// <summary>数字（TS 的 propsNumber）：AnimatedNumber。</summary>
    public static readonly PartKey Number = new(typeof(Stat), nameof(Number), typeof(AnimatedNumber));

    /// <summary>原样显示的数值（TS 的 propsNumber，字符串值时）。</summary>
    public static readonly PartKey Text = new(typeof(Stat), nameof(Text), typeof(Rune.Wpf.Text));

    /// <summary>单位（TS 的 propsUnit）。</summary>
    public static readonly PartKey Unit = new(typeof(Stat), nameof(Unit), typeof(Rune.Wpf.Text));

    /// <summary>变化量（TS 的 propsChange）。</summary>
    public static readonly PartKey Change = new(typeof(Stat), nameof(Change), typeof(Flex));

    /// <summary>变化量后面的说明（TS 的 propsChangeLabel）。</summary>
    public static readonly PartKey ChangeLabel = new(typeof(Stat), nameof(ChangeLabel), typeof(Rune.Wpf.Text));

    /// <summary>补充说明（TS 的 propsDescription）。</summary>
    public static readonly PartKey Description = new(typeof(Stat), nameof(Description), typeof(Rune.Wpf.Text));

    /// <summary>补充内容（TS 的 propsBody）。</summary>
    public static readonly PartKey Body = new(typeof(Stat), nameof(Body), typeof(ContentPresenter));

    /// <summary>底栏（TS 的 propsFooter）。</summary>
    public static readonly PartKey Footer = new(typeof(Stat), nameof(Footer), typeof(Border));
}
