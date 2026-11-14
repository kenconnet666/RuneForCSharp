using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 每页条数选择（vue-tsx-pro 的 PageSizeSelect）：分页的配套组件，与 <see cref="Pagination"/> 并排放（三样都要时用 <see cref="PaginationBar"/>）。
/// 选项文字取语言包（"20 条/页"），默认 sm、名称取语言包（每页条数）。值那一格按最长的选项定宽：换条数时不变宽窄。
/// 是一个 <see cref="Select"/>：外观、部件同 Select；<see cref="PageSize"/> 与 Select 的 Value（字符串）互相同步。
/// </summary>
public class PageSizeSelect : Select
{
    /// <summary>每页条数；默认双向绑定。</summary>
    public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(PageSizeSelect),
        new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((PageSizeSelect)d).SetCurrentValue(ValueProperty, ((PageSizeSelect)d).PageSize.ToString())));

    /// <summary>可选的条数，默认 10、20、50、100。</summary>
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(IReadOnlyList<int>), typeof(PageSizeSelect),
        new FrameworkPropertyMetadata(new[] { 10, 20, 50, 100 }, (d, _) => ((PageSizeSelect)d).Rebuild()));

    TextBlock? valuePart;

    /// <summary>新建：sm，10 条/页。</summary>
    public PageSizeSelect()
    {
        Size = TextSize.Sm;
        SelectionMode = SelectionMode.Single;
        Rebuild();
        Loaded += (_, _) =>
        {
            Rebuild();
            if (string.IsNullOrEmpty(AutomationProperties.GetName(this))) AutomationProperties.SetName(this, Provider.GetLang(this).PageSizeLabel);
        };
    }

    /// <summary>每页条数。</summary>
    public int PageSize { get => (int)GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <summary>可选的条数。</summary>
    public IReadOnlyList<int> Options { get => (IReadOnlyList<int>)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }

    /// <summary>用户换了条数（通常把页码回到第 1 页）；代码里改 PageSize 不发。</summary>
    public event EventHandler<int>? PageSizeChanged;

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != ValueProperty || !int.TryParse(Value, out var size) || size == PageSize) return;
        SetCurrentValue(PageSizeProperty, size);
        PageSizeChanged?.Invoke(this, size);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        valuePart = GetTemplateChild(SelectParts.Value) as TextBlock;
        FitWidth();
    }

    void Rebuild()
    {
        var lang = Provider.GetLang(this);
        ItemsSource = Options.Select(size => new ListboxOption(size.ToString(), lang.PageSizeOption(size))).ToList();
        SetCurrentValue(ValueProperty, PageSize.ToString());
        FitWidth();
    }

    /// <summary>值那一格按最长的选项定宽（TS 把所有选项的文字叠在同一格）：量每个选项的文字，取最宽的。</summary>
    void FitWidth()
    {
        if (valuePart is null) return;
        var lang = Provider.GetLang(this);
        var probe = new TextBlock { FontFamily = valuePart.FontFamily, FontSize = valuePart.FontSize, FontWeight = valuePart.FontWeight };
        var widest = 0d;
        foreach (var size in Options)
        {
            probe.Text = lang.PageSizeOption(size);
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, probe.DesiredSize.Width);
        }
        valuePart.MinWidth = Math.Ceiling(widest);
    }
}

/// <summary>
/// 跳页（vue-tsx-pro 的 PageJump）：分页的配套组件，"跳至 [  ] 页"，回车或失焦后跳过去（夹在 1–PageCount），输入框随后清空。
/// 输入框是 <see cref="NumberField"/>（没有加减按钮、4.5em 宽），读屏念它的名称（语言包的"跳到第几页"），前后的"跳至""页"只是视觉上的提示。
/// 部件样式键：<see cref="PageJumpParts"/>。
/// </summary>
public class PageJump : FrameworkElement
{
    /// <summary>当前页；跳页时写回，默认双向绑定。</summary>
    public static readonly DependencyProperty PageProperty = DependencyProperty.Register(nameof(Page), typeof(int), typeof(PageJump),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>总页数：输入的页码夹在 1–PageCount。</summary>
    public static readonly DependencyProperty PageCountProperty = DependencyProperty.Register(nameof(PageCount), typeof(int), typeof(PageJump),
        new FrameworkPropertyMetadata(1, (d, _) => ((PageJump)d).input.Max = Math.Max(1, ((PageJump)d).PageCount)));

    /// <summary>字号档位，默认 sm（同 Pagination）。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(PageJump),
        new FrameworkPropertyMetadata(TextSize.Sm, (d, _) => ((PageJump)d).ApplySize()));

    readonly Flex root = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly TextBlock before = new();
    readonly NumberField input = new() { Stepper = false, Min = 1, Max = 1 };
    readonly TextBlock after = new();

    /// <summary>新建：第 1 页，共 1 页。</summary>
    public PageJump()
    {
        before.SetResourceReference(StyleProperty, PageJumpParts.Before);
        input.SetResourceReference(StyleProperty, PageJumpParts.Input);
        after.SetResourceReference(StyleProperty, PageJumpParts.After);
        root.Children.Add(before);
        root.Children.Add(input);
        root.Children.Add(after);
        AddLogicalChild(root);
        AddVisualChild(root);
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(NumberField.ValueProperty, typeof(NumberField)).AddValueChanged(input, (_, _) =>
        {
            if (input.Value is { } target) Jump(target);
        });
        Loaded += (_, _) =>
        {
            var lang = Provider.GetLang(this);
            before.Text = lang.PageJumpBefore;
            before.Visibility = lang.PageJumpBefore.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            after.Text = lang.PageJumpAfter;
            after.Visibility = lang.PageJumpAfter.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(input, lang.PageJumpLabel);
        };
        ApplySize();
    }

    /// <summary>当前页。</summary>
    public int Page { get => (int)GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <summary>总页数。</summary>
    public int PageCount { get => (int)GetValue(PageCountProperty); set => SetValue(PageCountProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>输入框（测试用）。</summary>
    internal NumberField Input => input;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        // 输入框 4.5em 宽（按它自己的字号）
        if (e.Property == TextElement.FontSizeProperty) input.Width = 4.5 * input.FontSize;
    }

    void ApplySize()
    {
        input.Size = Size;
        if (Size.Pixels is { } pixels) SetValue(TextElement.FontSizeProperty, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(TextElement.FontSizeProperty, key);
        else ClearValue(TextElement.FontSizeProperty);
    }

    /// <summary>跳过去（四舍五入、夹在 1–PageCount），输入框清空：它只在跳之前暂存页码。</summary>
    void Jump(decimal target)
    {
        var next = (int)Math.Clamp(Math.Round(target, MidpointRounding.AwayFromZero), 1, Math.Max(1, PageCount));
        if (next != Page) SetCurrentValue(PageProperty, next);
        input.Value = null;
    }
}

/// <summary>
/// 分页栏（vue-tsx-pro 的 PaginationBar）：表格、列表下方的一整行，起始边是"第 1–20 条，共 320 条"，结束边是页码、每页条数、跳页；
/// 拼的是 <see cref="Pagination"/>、<see cref="PageSizeSelect"/>、<see cref="PageJump"/>，换每页条数时回到第 1 页。
/// <code>
/// &lt;rune:PaginationBar Page="{Binding Page}" PageSize="{Binding Size}" Total="320" /&gt;
/// </code>
/// 按自己的宽度取舍（TS 的容器查询）：窄于 36em 时不显示跳页，窄于 24em 时总数单独一行。只要其中几样时用 ShowTotal、ShowSizeSelect、ShowJump 关掉，
/// 总数的写法可以用 <see cref="TotalText"/> 自己给。不可用时（IsEnabled=false）都不能操作。分页导航的名称写在 <see cref="PaginationPart"/> 上。
/// 部件样式键：<see cref="PaginationBarParts"/>。
/// </summary>
public class PaginationBar : FrameworkElement
{
    /// <summary>当前页，从 1 开始；默认双向绑定。</summary>
    public static readonly DependencyProperty PageProperty = DependencyProperty.Register(nameof(Page), typeof(int), typeof(PaginationBar),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));

    /// <summary>每页条数，默认 10；默认双向绑定。</summary>
    public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(PaginationBar),
        new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));

    /// <summary>总条数。</summary>
    public static readonly DependencyProperty TotalProperty = DependencyProperty.Register(nameof(Total), typeof(int), typeof(PaginationBar), new FrameworkPropertyMetadata(0, Changed));

    /// <summary>每页条数的选项，默认 10、20、50、100。</summary>
    public static readonly DependencyProperty PageSizeOptionsProperty = DependencyProperty.Register(nameof(PageSizeOptions), typeof(IReadOnlyList<int>), typeof(PaginationBar),
        new FrameworkPropertyMetadata(new[] { 10, 20, 50, 100 }, Changed));

    /// <summary>起始边显示总数，默认 true。</summary>
    public static readonly DependencyProperty ShowTotalProperty = DependencyProperty.Register(nameof(ShowTotal), typeof(bool), typeof(PaginationBar), new FrameworkPropertyMetadata(true, Changed));

    /// <summary>总数的写法（TS 的 showTotal 给函数）：拿到这一页的起止与总数；不写时是"第 1–20 条，共 320 条"。</summary>
    public static readonly DependencyProperty TotalTextProperty = DependencyProperty.Register(nameof(TotalText), typeof(Func<PageRange, string>), typeof(PaginationBar), new FrameworkPropertyMetadata(null, Changed));

    /// <summary>每页条数选择，默认 true。</summary>
    public static readonly DependencyProperty ShowSizeSelectProperty = DependencyProperty.Register(nameof(ShowSizeSelect), typeof(bool), typeof(PaginationBar), new FrameworkPropertyMetadata(true, Changed));

    /// <summary>跳页，默认 true（窄于 36em、只有一页时不显示）。</summary>
    public static readonly DependencyProperty ShowJumpProperty = DependencyProperty.Register(nameof(ShowJump), typeof(bool), typeof(PaginationBar), new FrameworkPropertyMetadata(true, Changed));

    /// <summary>字号档位，各部分随之缩放，默认 sm。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(PaginationBar),
        new FrameworkPropertyMetadata(TextSize.Sm, (d, _) => ((PaginationBar)d).ApplySize()));

    readonly Flex root = new() { Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, Justify = FlexJustify.Between, RowGap = Space.Sm, ColumnGap = Space.Md };
    readonly TextBlock total = new();
    readonly Flex controls = new() { Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, Justify = FlexJustify.End, RowGap = Space.Sm, ColumnGap = Space.Md };
    readonly Pagination pagination = new();
    readonly PageSizeSelect sizeSelect = new();
    readonly PageJump jump = new();
    bool narrow;
    bool compact;

    /// <summary>新建：第 1 页，10 条/页。</summary>
    public PaginationBar()
    {
        total.SetResourceReference(StyleProperty, PaginationBarParts.Total);
        // 结束边那一组：与总数同一行时排在右边；换到下一行时也靠右（TS 的 margin-inline-start: auto）
        Flex.SetGrow(controls, true);
        controls.Children.Add(pagination);
        controls.Children.Add(sizeSelect);
        controls.Children.Add(jump);
        root.Children.Add(total);
        root.Children.Add(controls);
        AddLogicalChild(root);
        AddVisualChild(root);
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Pagination.PageProperty, typeof(Pagination)).AddValueChanged(pagination, (_, _) => SetCurrentValue(PageProperty, pagination.Page));
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(PageJump.PageProperty, typeof(PageJump)).AddValueChanged(jump, (_, _) => SetCurrentValue(PageProperty, jump.Page));
        sizeSelect.PageSizeChanged += (_, size) =>
        {
            SetCurrentValue(PageSizeProperty, size);
            SetCurrentValue(PageProperty, 1);
        };
        Loaded += (_, _) => Sync();
        ApplySize();
        Sync();
    }

    /// <summary>当前页。</summary>
    public int Page { get => (int)GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <summary>每页条数。</summary>
    public int PageSize { get => (int)GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <summary>总条数。</summary>
    public int Total { get => (int)GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <summary>每页条数的选项。</summary>
    public IReadOnlyList<int> PageSizeOptions { get => (IReadOnlyList<int>)GetValue(PageSizeOptionsProperty); set => SetValue(PageSizeOptionsProperty, value); }

    /// <summary>起始边显示总数。</summary>
    public bool ShowTotal { get => (bool)GetValue(ShowTotalProperty); set => SetValue(ShowTotalProperty, value); }

    /// <summary>总数的写法。</summary>
    public Func<PageRange, string>? TotalText { get => (Func<PageRange, string>?)GetValue(TotalTextProperty); set => SetValue(TotalTextProperty, value); }

    /// <summary>每页条数选择。</summary>
    public bool ShowSizeSelect { get => (bool)GetValue(ShowSizeSelectProperty); set => SetValue(ShowSizeSelectProperty, value); }

    /// <summary>跳页。</summary>
    public bool ShowJump { get => (bool)GetValue(ShowJumpProperty); set => SetValue(ShowJumpProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>页码部件：分页导航的名称写在它上面（TS 的 propsPagination 的 aria-label）。</summary>
    public Pagination PaginationPart => pagination;

    /// <summary>每页条数部件。</summary>
    public PageSizeSelect SizeSelectPart => sizeSelect;

    /// <summary>跳页部件。</summary>
    public PageJump JumpPart => jump;

    /// <summary>总数文字（测试用）。</summary>
    internal TextBlock TotalPart => total;

    /// <summary>当前页显示的是哪几条（Page 超出范围时按夹回去的页算，同 Pagination）。</summary>
    public PageRange Range
    {
        get
        {
            var count = Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));
            var current = Math.Clamp(Page, 1, count);
            return Total == 0 ? new PageRange(0, 0, 0) : new PageRange((current - 1) * PageSize + 1, Math.Min(current * PageSize, Total), Total);
        }
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <summary>按自己的宽度取舍（TS 的容器查询）：窄于 36em 不显示跳页，窄于 24em 总数单独一行。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var em = TextElement.GetFontSize(this);
        var (wasNarrow, wasCompact) = (narrow, compact);
        narrow = availableSize.Width < 36 * em;
        compact = availableSize.Width < 24 * em;
        if (narrow != wasNarrow || compact != wasCompact) Sync();
        // 窄时总数单独一行（TS 的 flex-basis: 100%）
        total.MinWidth = compact && !double.IsInfinity(availableSize.Width) ? availableSize.Width : 0;
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PaginationBar)d).Sync();

    void ApplySize()
    {
        pagination.Size = sizeSelect.Size = jump.Size = Size;
        if (Size.Pixels is { } pixels) SetValue(TextElement.FontSizeProperty, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(TextElement.FontSizeProperty, key);
        else ClearValue(TextElement.FontSizeProperty);
    }

    void Sync()
    {
        var lang = Provider.GetLang(this);
        var range = Range;
        pagination.Page = Page;
        pagination.Total = Total;
        pagination.PageSize = PageSize;
        sizeSelect.Options = PageSizeOptions;
        sizeSelect.PageSize = PageSize;
        jump.Page = Page;
        jump.PageCount = pagination.Count;
        total.Visibility = ShowTotal ? Visibility.Visible : Visibility.Collapsed;
        total.Text = TotalText?.Invoke(range) ?? (range.Total == 0 ? lang.PaginationTotal(0) : lang.PaginationRange(range.From, range.To, range.Total));
        sizeSelect.Visibility = ShowSizeSelect ? Visibility.Visible : Visibility.Collapsed;
        jump.Visibility = ShowJump && pagination.Count > 1 && !narrow ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>当前页显示的是哪几条（vue-tsx-pro 的 PageRange）。</summary>
/// <param name="From">第一条的序号，从 1 开始；没有数据时是 0。</param>
/// <param name="To">最后一条的序号。</param>
/// <param name="Total">总条数。</param>
public readonly record struct PageRange(int From, int To, int Total);

/// <summary>PageJump 的部件样式键。</summary>
public static class PageJumpParts
{
    /// <summary>输入框前的文字（"跳至"）。</summary>
    public static readonly PartKey Before = new(typeof(PageJump), nameof(Before), typeof(TextBlock));

    /// <summary>输入框。</summary>
    public static readonly PartKey Input = new(typeof(PageJump), nameof(Input), typeof(NumberField));

    /// <summary>输入框后的文字（"页"）。</summary>
    public static readonly PartKey After = new(typeof(PageJump), nameof(After), typeof(TextBlock));
}

/// <summary>PaginationBar 的部件样式键（页码、条数、跳页用 PaginationPart、SizeSelectPart、JumpPart 直接改）。</summary>
public static class PaginationBarParts
{
    /// <summary>总数。</summary>
    public static readonly PartKey Total = new(typeof(PaginationBar), nameof(Total), typeof(TextBlock));
}
