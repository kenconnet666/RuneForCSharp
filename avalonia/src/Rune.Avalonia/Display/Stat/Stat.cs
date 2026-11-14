using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Metadata;
using Avalonia.Styling;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 统计数值（vue-tsx-pro 的 Stat）：指标名、数值与单位、可选的图标与环比变化。仪表盘顶部的一排卡片、详情页的关键指标。外观只是内容，放进 Card、Grid 里组合。
/// <code>
/// &lt;rune:Card&gt;&lt;rune:Stat Label="实际销售额" Value="910.36" Unit="万元" Icon="{x:Static rune:Lucide.Wallet}" Change="0.123" ChangeLabel="较上月" /&gt;&lt;/rune:Card&gt;
/// </code>
/// 排版（参考 Ant Design Pro 的 ChartCard）：第一行是图标与指标名、结束边是变化量（放不下时换到下一行）；第二行是数值（xxl、粗体、行高 tight）与单位（xs、次要色）；
/// 再下面是说明（xs、次要色）、内容（进度条、迷你图，占满整宽）与底栏（上方一条分隔线，xs、次要色）。行与行之间 space.xs。
/// 数值是 decimal 时按 <see cref="Format"/> 格式化并在变化时滚动（AnimatedNumber）；写 <see cref="Text"/> 时原样显示（如"—""98.5%"）。
/// 变化量默认按百分比、带符号（+12.3%），上升成功色、下降危险色，<see cref="InvertChange"/> 反过来（成本、耗时）。<see cref="Tone"/> 给数值着色。
/// 加载中（IsLoading）数值与变化量换成骨架，指标名照常显示；很快完成时不闪，出现过骨架之后换回数值时淡入。读屏里是一组，名称是指标名，忙时状态是"加载中"。
/// 放进 <see cref="StatGroup"/> 时起始边有一条竖线、左右各空 space.md。
/// 部件：<c>rune|Stat Flex#PART_Label</c>、<c>Border#PART_Icon</c>、<c>Flex#PART_Value</c>、<c>#PART_Number</c>、<c>rune|Text#PART_Unit</c>、
/// <c>Flex#PART_Change</c>、<c>rune|Text#PART_ChangeLabel</c>、<c>rune|Text#PART_Description</c>、<c>ContentPresenter#PART_Body</c>、<c>Border#PART_Footer</c>。
/// </summary>
public class Stat : Control
{
    /// <summary>指标名。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<Stat, string?>(nameof(Label));

    /// <summary>数值：按 Format 格式化，变化时滚动。</summary>
    public static readonly StyledProperty<decimal> ValueProperty = AvaloniaProperty.Register<Stat, decimal>(nameof(Value));

    /// <summary>原样显示的数值（TS 的字符串 value，如"—""98.5%"）；写了时盖过 Value。</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Stat, string?>(nameof(Text));

    /// <summary>数值的格式，如两位小数、百分比、按万元（Scale = 10000）。</summary>
    public static readonly StyledProperty<NumericFormat?> FormatProperty = AvaloniaProperty.Register<Stat, NumericFormat?>(nameof(Format));

    /// <summary>单位，跟在数值后面、字号小一档。</summary>
    public static readonly StyledProperty<string?> UnitProperty = AvaloniaProperty.Register<Stat, string?>(nameof(Unit));

    /// <summary>指标名前面的图标（装饰，对读屏隐藏）。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Stat, IconData?>(nameof(Icon));

    /// <summary>变化量（环比、同比）：正数向上、负数向下；默认按百分比显示（0.12 → +12%）。不写时没有变化量。</summary>
    public static readonly StyledProperty<decimal?> ChangeProperty = AvaloniaProperty.Register<Stat, decimal?>(nameof(Change));

    /// <summary>变化量的格式，默认百分比、最多 1 位小数、正负都带符号（0 不带）。</summary>
    public static readonly StyledProperty<NumericFormat?> ChangeFormatProperty = AvaloniaProperty.Register<Stat, NumericFormat?>(nameof(ChangeFormat));

    /// <summary>变化量后面的说明，如"较上月"。</summary>
    public static readonly StyledProperty<string?> ChangeLabelProperty = AvaloniaProperty.Register<Stat, string?>(nameof(ChangeLabel));

    /// <summary>下降是好事（成本、耗时、故障数）：下降显示成功色、上升显示危险色。</summary>
    public static readonly StyledProperty<bool> InvertChangeProperty = AvaloniaProperty.Register<Stat, bool>(nameof(InvertChange));

    /// <summary>数值下面的补充说明（一行小字）。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<Stat, string?>(nameof(Description));

    /// <summary>底栏：上方一条分隔线。</summary>
    public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<Stat, object?>(nameof(Footer));

    /// <summary>数值下面占满整宽的补充内容：进度条、迷你图。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Stat, object?>(nameof(Content));

    /// <summary>数值的色调：超时、缺漏这类要引起注意的数用 danger、warning；不写时是正文色。</summary>
    public static readonly StyledProperty<TextTone> ToneProperty = AvaloniaProperty.Register<Stat, TextTone>(nameof(Tone));

    /// <summary>加载中：数值与变化量换成骨架；很快完成时不闪。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Stat, bool>(nameof(IsLoading));

    /// <summary>字号（继承），图标框的大小按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Stat>();

    /// <summary>变化量的默认格式（TS 的 CHANGE_FORMAT）。</summary>
    public static NumericFormat DefaultChangeFormat { get; } = new() { Style = NumericStyle.Percent, MaximumFractionDigits = 1, SignDisplay = NumericSign.ExceptZero };

    readonly Border root = new();
    readonly StatLayout column = new();
    readonly Flex label = new() { Name = StatParts.Label, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Border iconBox = new() { Name = StatParts.Icon };
    readonly Icon icon = new() { HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
    readonly Text labelText = new();
    readonly Flex value = new() { Name = StatParts.Value, Wrap = true, Align = FlexAlign.Baseline, Gap = Space.Xs };
    readonly AnimatedNumber number = new() { Name = StatParts.Number };
    readonly Text text = new() { Name = StatParts.Number, Numeric = true };
    readonly Text unit = new() { Name = StatParts.Unit };
    readonly Skeleton numberSkeleton = new();
    readonly Flex change = new() { Name = StatParts.Change, Align = FlexAlign.Center, Gap = Space.Xs };
    // 箭头与数字之间一个空格宽（TS 里是一个空格）
    readonly Flex trend = new() { Align = FlexAlign.Center, Gap = Space.Xs };
    readonly Icon trendIcon = new();
    readonly AnimatedNumber changeNumber = new();
    readonly Text changeLabel = new() { Name = StatParts.ChangeLabel };
    readonly Skeleton changeSkeleton = new();
    readonly Text description = new() { Name = StatParts.Description };
    readonly ContentPresenter body = new() { Name = StatParts.Body };
    readonly Border footer = new() { Name = StatParts.Footer };
    readonly ContentPresenter footerContent = new();
    readonly TemplateResources resources;
    DelayedLoading loading;
    IDisposable? trendTint;
    bool revealing;

    static Stat() => AffectsMeasure<Stat>(FontSizeProperty);

    /// <summary>新建：数值 0。</summary>
    public Stat()
    {
        resources = new TemplateResources(this);
        iconBox.Child = icon;
        label.Children.AddRange([iconBox, labelText]);
        // 标签已经是这一组的名称，不再单独念；图标是装饰
        AutomationProperties.SetAccessibilityView(label, AccessibilityView.Raw);
        trend.Children.AddRange([trendIcon, changeNumber]);
        change.Children.AddRange([trend, changeLabel, changeSkeleton]);
        value.Children.AddRange([number, text, unit, numberSkeleton]);
        footer.Child = footerContent;
        // 子元素按读屏的顺序（指标名、数值、变化量……），变化量在排版时放到第一行的结束边
        column.Children.AddRange([label, value, change, description, body, footer]);
        root.Child = column;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        loading = new DelayedLoading(ShowSkeleton);
        Update();
        UpdateMetrics();
    }

    /// <summary>指标名。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>数值。</summary>
    public decimal Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>原样显示的数值。</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>数值的格式。</summary>
    public NumericFormat? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>单位。</summary>
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <summary>指标名前面的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>变化量。</summary>
    public decimal? Change { get => GetValue(ChangeProperty); set => SetValue(ChangeProperty, value); }

    /// <summary>变化量的格式。</summary>
    public NumericFormat? ChangeFormat { get => GetValue(ChangeFormatProperty); set => SetValue(ChangeFormatProperty, value); }

    /// <summary>变化量后面的说明。</summary>
    public string? ChangeLabel { get => GetValue(ChangeLabelProperty); set => SetValue(ChangeLabelProperty, value); }

    /// <summary>下降是好事。</summary>
    public bool InvertChange { get => GetValue(InvertChangeProperty); set => SetValue(InvertChangeProperty, value); }

    /// <summary>补充说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>底栏。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>补充内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>数值的色调。</summary>
    public TextTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

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
    internal bool SkeletonShown => numberSkeleton.IsVisible;

    /// <summary>出现过骨架、换回数值时淡入（测试用）。</summary>
    internal bool Revealing => revealing;

    /// <summary>部件（测试用）。</summary>
    internal (Border Icon, AnimatedNumber Number, Text Text, Text Unit, Flex Change, AnimatedNumber ChangeNumber, Text ChangeLabel, Border Footer) Parts =>
        (iconBox, number, text, unit, change, changeNumber, changeLabel, footer);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty) UpdateMetrics();
        else if (change.Property == IsLoadingProperty) loading.Update(IsLoading);
        else if (change.Property != ValueProperty || Text is not null) Update();
        else number.Value = Value;
    }

    void Update()
    {
        icon.Data = Icon;
        iconBox.IsVisible = Icon is not null;
        labelText.Text = Label;
        AutomationProperties.SetName(this, Label);
        number.Value = Value;
        number.Format = Format;
        number.Tone = text.Tone = Tone;
        text.Text = Text;
        unit.Text = Unit;
        changeNumber.Value = Change ?? 0;
        changeNumber.Format = ChangeFormat ?? DefaultChangeFormat;
        trendIcon.Data = Direction == "up" ? Lucide.TrendingUp : Lucide.TrendingDown;
        trendIcon.IsVisible = Direction is "up" or "down";
        trendTint?.Dispose();
        trendTint = ChangeTone.BrushKey is { } key ? trend.Bind(TextElement.ForegroundProperty, trend.GetResourceObservable(key)) : null;
        changeLabel.Text = ChangeLabel;
        changeLabel.IsVisible = ChangeLabel is not null;
        description.Text = Description;
        description.IsVisible = !string.IsNullOrEmpty(Description);
        body.Content = Content;
        body.IsVisible = Content is not null;
        footerContent.Content = Footer;
        footer.IsVisible = Footer is not null;
        PseudoClasses.Set(":up", Direction == "up");
        PseudoClasses.Set(":down", Direction == "down");
        ShowParts(loading.Shown);
    }

    /// <summary>数值、变化量与骨架互换；没有变化量时整块不显示。</summary>
    void ShowParts(bool skeleton)
    {
        number.IsVisible = !skeleton && Text is null;
        text.IsVisible = !skeleton && Text is not null;
        unit.IsVisible = !skeleton && Unit is not null;
        numberSkeleton.IsVisible = skeleton;
        change.IsVisible = Change is not null;
        trend.IsVisible = !skeleton;
        changeLabel.IsVisible = !skeleton && ChangeLabel is not null;
        changeSkeleton.IsVisible = skeleton;
    }

    /// <summary>骨架与数值互换：出现过骨架之后换回数值时数值与变化量淡入（一开始就有数值的不播）。</summary>
    void ShowSkeleton(bool shown)
    {
        ShowParts(shown);
        if (shown) revealing = true;
        if (shown || !revealing) return;
        var duration = Motion.Duration(this, "Rune.Duration.Lg", TimeSpan.FromMilliseconds(300));
        if (duration <= TimeSpan.Zero) return;
        foreach (var part in new Control[] { value, change })
            _ = new Animation
            {
                Duration = duration,
                Easing = this.Resource<Easing>("Rune.Easing.Enter", new CubicEaseOut()),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d) } },
                },
            }.RunAsync(part);
    }

    /// <summary>挂上时已经在加载就立即显示骨架（没有内容可看，空白比骨架更糟）。</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateGrouped();
        if (!IsLoading || loading.Shown) return;
        loading.Stop();
        loading = new DelayedLoading(ShowSkeleton, loading: true);
        ShowSkeleton(true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        loading.Stop();
    }

    /// <summary>在 StatGroup 里：起始边一条竖线、左右各空 space.md；竖线往起始方向挪一条线宽，每行第一个的竖线被组裁掉。</summary>
    void UpdateGrouped()
    {
        var grouped = Parent is StatGroup;
        var line = this.Resource("Rune.BorderWidth.Xs", 1.0);
        var md = this.Resource("Rune.Space.Md", 0.75) * FontSize;
        root.BorderThickness = grouped ? new Thickness(line, 0, 0, 0) : default;
        root.Padding = grouped ? new Thickness(md, 0) : default;
        root.Margin = grouped ? new Thickness(-line, 0, 0, 0) : default;
        PseudoClasses.Set(":grouped", grouped);
    }

    /// <summary>em 尺寸：图标框 1.75em、圆角 md；数值 xxl；指标名 sm；单位、变化量、说明、底栏 xs；骨架同数值、变化量高。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        double Token(string key, double fallback) => this.Resource(key, fallback);
        var (xs, sm, xxl) = (Token("Rune.FontSize.Xs", 12.0), Token("Rune.FontSize.Sm", 13.0), Token("Rune.FontSize.Xxl", 24.0));
        // 指标名一行是 sm 的字号：图标框 1.75em、图标 1em 按它算
        label.SetValue(TextElement.FontSizeProperty, sm);
        iconBox.Width = iconBox.Height = 1.75 * sm;
        iconBox.CornerRadius = new CornerRadius(Token("Rune.Radius.Md", 0.4) * sm);
        icon.Size = sm;
        column.Gap = Token("Rune.Space.Xs", 0.25) * em;
        column.ColumnGap = Token("Rune.Space.Sm", 0.5) * em;
        number.SetValue(TextElement.FontSizeProperty, xxl);
        text.FontSize = xxl;
        unit.FontSize = description.FontSize = xs;
        change.SetValue(TextElement.FontSizeProperty, xs);
        footer.SetValue(TextElement.FontSizeProperty, xs);
        // 底栏是 xs 的字号：上面多空 space.xs、线下面空 space.sm 都按它算
        footer.Margin = new Thickness(0, Token("Rune.Space.Xs", 0.25) * xs, 0, 0);
        footer.Padding = new Thickness(0, Token("Rune.Space.Sm", 0.5) * xs, 0, 0);
        footer.BorderThickness = new Thickness(0, Token("Rune.BorderWidth.Xs", 1.0), 0, 0);
        numberSkeleton.Width = 4 * xxl;
        numberSkeleton.Height = 1.25 * xxl;
        changeSkeleton.Width = 6 * xs;
        changeSkeleton.Height = 1.5 * xs;
        UpdateGrouped();
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new StatPeer(this);

    /// <summary>读屏：一组，名称是指标名，里面是数值、变化量与说明；加载中状态是"加载中"。</summary>
    sealed class StatPeer(Stat owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;

        protected override string? GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();
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
    IEnumerable<(Control Child, int Row)> Rows(double width, out bool wrapped)
    {
        var (label, change) = (Children[0], Children[2]);
        wrapped = change.IsVisible && label.IsVisible && label.DesiredSize.Width + ColumnGap + change.DesiredSize.Width > width;
        var rows = new List<(Control, int)> { (label, 0), (change, wrapped ? 1 : 0), (Children[1], 2) };
        rows.AddRange(Children.Skip(3).Select((child, index) => (child, 3 + index)));
        return rows.Where(row => row.Item1.IsVisible);
    }

    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        foreach (var child in Children) child.Measure(new LayoutSize(availableSize.Width, double.PositiveInfinity));
        var width = double.IsInfinity(availableSize.Width) ? Children.Max(child => child.DesiredSize.Width) : availableSize.Width;
        return new LayoutSize(width, Stack(width, arrange: false));
    }

    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        Stack(finalSize.Width, arrange: true);
        return finalSize;
    }

    double Stack(double width, bool arrange)
    {
        var y = 0.0;
        var first = true;
        foreach (var group in Rows(width, out _).GroupBy(row => row.Row))
        {
            if (!first) y += Gap;
            first = false;
            var height = group.Max(row => row.Child.DesiredSize.Height);
            if (arrange)
                foreach (var (child, _) in group)
                {
                    // 第一行：指标名靠起始边、变化量靠结束边，竖直居中；其余占满整宽
                    var isChange = child == Children[2];
                    var isLabel = child == Children[0];
                    var size = child.DesiredSize;
                    var x = isChange && group.Count() > 1 ? width - size.Width : 0;
                    var w = isLabel && group.Count() > 1 ? width - Children[2].DesiredSize.Width - ColumnGap : isChange ? size.Width : width;
                    child.Arrange(new Rect(x, y + (isLabel || isChange ? (height - size.Height) / 2 : 0), Math.Max(0, w), isLabel || isChange ? size.Height : height));
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

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Grid);
}

/// <summary>Stat 的部件名。</summary>
public static class StatParts
{
    /// <summary>指标名（TS 的 propsLabel）。</summary>
    public const string Label = "PART_Label";

    /// <summary>图标框（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>数值一行（TS 的 propsValue）。</summary>
    public const string Value = "PART_Value";

    /// <summary>数字（TS 的 propsNumber）：AnimatedNumber 或原样显示的 Text。</summary>
    public const string Number = "PART_Number";

    /// <summary>单位（TS 的 propsUnit）。</summary>
    public const string Unit = "PART_Unit";

    /// <summary>变化量（TS 的 propsChange）。</summary>
    public const string Change = "PART_Change";

    /// <summary>变化量后面的说明（TS 的 propsChangeLabel）。</summary>
    public const string ChangeLabel = "PART_ChangeLabel";

    /// <summary>补充说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>补充内容（TS 的 propsBody）。</summary>
    public const string Body = "PART_Body";

    /// <summary>底栏（TS 的 propsFooter）。</summary>
    public const string Footer = "PART_Footer";
}
