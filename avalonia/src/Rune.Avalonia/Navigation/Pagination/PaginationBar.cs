using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 跳页（vue-tsx-pro 的 PageJump）：分页的配套组件，"跳至 [  ] 页"，回车或失焦后跳过去（夹在 1–PageCount），输入框随后清空。
/// <code>
/// &lt;rune:PageJump Page="{Binding Page}" PageCount="32" /&gt;
/// </code>
/// 输入框是 <see cref="NumberField"/>（没有加减按钮、4.5em 宽），读屏念它的名称（语言包的"跳到第几页"），前后的"跳至""页"只是视觉上的提示。
/// </summary>
public class PageJump : Control
{
    /// <summary>当前页；跳页时写回，默认双向绑定。</summary>
    public static readonly StyledProperty<int> PageProperty =
        AvaloniaProperty.Register<PageJump, int>(nameof(Page), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>总页数：输入的页码夹在 1–PageCount。</summary>
    public static readonly StyledProperty<int> PageCountProperty = AvaloniaProperty.Register<PageJump, int>(nameof(PageCount), 1);

    /// <summary>字号档位，默认 sm（同 Pagination）。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<PageJump, TextSize>(nameof(Size), TextSize.Sm);

    readonly Flex root = new() { Align = FlexAlign.Center, Gap = Space.Sm };
    readonly TextBlock before = new() { Name = PageJumpParts.Before };
    readonly NumberField input = new() { Name = PageJumpParts.Input, Stepper = false, Min = 1 };
    readonly TextBlock after = new() { Name = PageJumpParts.After };
    readonly TemplateResources resources;

    /// <summary>新建：第 1 页，共 1 页。</summary>
    public PageJump()
    {
        resources = new TemplateResources(this);
        // 前后的文字只是视觉提示：读屏念输入框的名称
        AutomationProperties.SetAccessibilityView(before, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(after, AccessibilityView.Raw);
        root.Children.AddRange([before, input, after]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        input.PropertyChanged += (_, e) =>
        {
            if (e.Property == NumberField.ValueProperty && input.Value is { } target) Jump(target);
        };
        ApplySize();
    }

    /// <summary>当前页。</summary>
    public int Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <summary>总页数。</summary>
    public int PageCount { get => GetValue(PageCountProperty); set => SetValue(PageCountProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>输入框（测试用）。</summary>
    internal NumberField Input => input;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageCountProperty) input.Max = Math.Max(1, PageCount);
        else if (change.Property == SizeProperty) ApplySize();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        var lang = Provider.GetLang(this);
        before.Text = lang.PageJumpBefore;
        before.IsVisible = lang.PageJumpBefore.Length > 0;
        after.Text = lang.PageJumpAfter;
        after.IsVisible = lang.PageJumpAfter.Length > 0;
        AutomationProperties.SetName(input, lang.PageJumpLabel);
    }

    void ApplySize()
    {
        input.Size = Size;
        if (Size.Pixels is { } pixels) resources.Set(TextElement.FontSizeProperty, pixels);
        else resources.Bind(TextElement.FontSizeProperty, Size.ResourceKey);
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
/// </summary>
public class PaginationBar : Control
{
    /// <summary>当前页，从 1 开始；默认双向绑定。</summary>
    public static readonly StyledProperty<int> PageProperty =
        AvaloniaProperty.Register<PaginationBar, int>(nameof(Page), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>每页条数，默认 10；默认双向绑定。</summary>
    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<PaginationBar, int>(nameof(PageSize), 10, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>总条数。</summary>
    public static readonly StyledProperty<int> TotalProperty = AvaloniaProperty.Register<PaginationBar, int>(nameof(Total));

    /// <summary>每页条数的选项，默认 10、20、50、100。</summary>
    public static readonly StyledProperty<IReadOnlyList<int>> PageSizeOptionsProperty =
        AvaloniaProperty.Register<PaginationBar, IReadOnlyList<int>>(nameof(PageSizeOptions), [10, 20, 50, 100]);

    /// <summary>起始边显示总数，默认 true。</summary>
    public static readonly StyledProperty<bool> ShowTotalProperty = AvaloniaProperty.Register<PaginationBar, bool>(nameof(ShowTotal), true);

    /// <summary>总数的写法（TS 的 showTotal 给函数）：拿到这一页的起止与总数；不写时是"第 1–20 条，共 320 条"。</summary>
    public static readonly StyledProperty<Func<PageRange, string>?> TotalTextProperty = AvaloniaProperty.Register<PaginationBar, Func<PageRange, string>?>(nameof(TotalText));

    /// <summary>每页条数选择，默认 true。</summary>
    public static readonly StyledProperty<bool> ShowSizeSelectProperty = AvaloniaProperty.Register<PaginationBar, bool>(nameof(ShowSizeSelect), true);

    /// <summary>跳页，默认 true（窄于 36em、只有一页时不显示）。</summary>
    public static readonly StyledProperty<bool> ShowJumpProperty = AvaloniaProperty.Register<PaginationBar, bool>(nameof(ShowJump), true);

    /// <summary>字号档位，各部分随之缩放，默认 sm。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<PaginationBar, TextSize>(nameof(Size), TextSize.Sm);

    readonly Flex root = new() { Name = PaginationBarParts.Root, Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, Justify = FlexJustify.Between, RowGap = Space.Sm, ColumnGap = Space.Md };
    readonly TextBlock total = new() { Name = PaginationBarParts.Total };
    readonly Flex controls = new() { Name = PaginationBarParts.Controls, Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, Justify = FlexJustify.End, RowGap = Space.Sm, ColumnGap = Space.Md };
    readonly Pagination pagination = new() { Name = PaginationBarParts.Pagination };
    readonly PageSizeSelect sizeSelect = new() { Name = PaginationBarParts.SizeSelect };
    readonly PageJump jump = new() { Name = PaginationBarParts.Jump };
    readonly TemplateResources resources;
    bool narrow;
    bool compact;

    /// <summary>新建：第 1 页，10 条/页。</summary>
    public PaginationBar()
    {
        resources = new TemplateResources(this);
        // 结束边那一组：与总数同一行时排在右边；换到下一行时也靠右（TS 的 margin-inline-start: auto）
        Flex.SetGrow(controls, true);
        controls.Children.AddRange([pagination, sizeSelect, jump]);
        root.Children.AddRange([total, controls]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        pagination.PropertyChanged += (_, e) =>
        {
            if (e.Property == Pagination.PageProperty) SetCurrentValue(PageProperty, pagination.Page);
        };
        jump.PropertyChanged += (_, e) =>
        {
            if (e.Property == PageJump.PageProperty) SetCurrentValue(PageProperty, jump.Page);
        };
        sizeSelect.PageSizeChanged += (_, size) =>
        {
            SetCurrentValue(PageSizeProperty, size);
            SetCurrentValue(PageProperty, 1);
        };
        ApplySize();
        Sync();
    }

    /// <summary>当前页。</summary>
    public int Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <summary>每页条数。</summary>
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <summary>总条数。</summary>
    public int Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <summary>每页条数的选项。</summary>
    public IReadOnlyList<int> PageSizeOptions { get => GetValue(PageSizeOptionsProperty); set => SetValue(PageSizeOptionsProperty, value); }

    /// <summary>起始边显示总数。</summary>
    public bool ShowTotal { get => GetValue(ShowTotalProperty); set => SetValue(ShowTotalProperty, value); }

    /// <summary>总数的写法。</summary>
    public Func<PageRange, string>? TotalText { get => GetValue(TotalTextProperty); set => SetValue(TotalTextProperty, value); }

    /// <summary>每页条数选择。</summary>
    public bool ShowSizeSelect { get => GetValue(ShowSizeSelectProperty); set => SetValue(ShowSizeSelectProperty, value); }

    /// <summary>跳页。</summary>
    public bool ShowJump { get => GetValue(ShowJumpProperty); set => SetValue(ShowJumpProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

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
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty) ApplySize();
        if (change.Property == PageProperty || change.Property == PageSizeProperty || change.Property == TotalProperty || change.Property == PageSizeOptionsProperty
            || change.Property == ShowTotalProperty || change.Property == TotalTextProperty || change.Property == ShowSizeSelectProperty || change.Property == ShowJumpProperty)
            Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Sync();
    }

    void ApplySize()
    {
        pagination.Size = sizeSelect.Size = jump.Size = Size;
        if (Size.Pixels is { } pixels) resources.Set(TextElement.FontSizeProperty, pixels);
        else resources.Bind(TextElement.FontSizeProperty, Size.ResourceKey);
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
        total.IsVisible = ShowTotal;
        total.Text = TotalText?.Invoke(range) ?? (range.Total == 0 ? lang.PaginationTotal(0) : lang.PaginationRange(range.From, range.To, range.Total));
        sizeSelect.IsVisible = ShowSizeSelect;
        jump.IsVisible = ShowJump && pagination.Count > 1 && !narrow;
        // 窄时总数单独一行（TS 的 flex-basis: 100%）
        total.MinWidth = compact ? Bounds.Width : 0;
    }

    /// <summary>按自己的宽度取舍（TS 的容器查询）：窄于 36em 不显示跳页，窄于 24em 总数单独一行。</summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var em = GetValue(TextElement.FontSizeProperty);
        var (wasNarrow, wasCompact) = (narrow, compact);
        narrow = e.NewSize.Width < 36 * em;
        compact = e.NewSize.Width < 24 * em;
        if (narrow != wasNarrow || compact != wasCompact || compact) Sync();
    }
}

/// <summary>当前页显示的是哪几条（vue-tsx-pro 的 PageRange）。</summary>
/// <param name="From">第一条的序号，从 1 开始；没有数据时是 0。</param>
/// <param name="To">最后一条的序号。</param>
/// <param name="Total">总条数。</param>
public readonly record struct PageRange(int From, int To, int Total);

/// <summary>PageJump 的部件名（样式选择器用）。</summary>
public static class PageJumpParts
{
    /// <summary>输入框前的文字（TextBlock，"跳至"）。</summary>
    public const string Before = "PART_Before";

    /// <summary>输入框（rune:NumberField）。</summary>
    public const string Input = "PART_Input";

    /// <summary>输入框后的文字（TextBlock，"页"）。</summary>
    public const string After = "PART_After";
}

/// <summary>PaginationBar 的部件名（样式选择器用）。</summary>
public static class PaginationBarParts
{
    /// <summary>整行（rune:Flex）。</summary>
    public const string Root = "PART_Root";

    /// <summary>总数（TextBlock）。</summary>
    public const string Total = "PART_Total";

    /// <summary>结束边那一组（rune:Flex：页码、条数、跳页）。</summary>
    public const string Controls = "PART_Controls";

    /// <summary>页码（rune:Pagination）。</summary>
    public const string Pagination = "PART_Pagination";

    /// <summary>每页条数（rune:PageSizeSelect）。</summary>
    public const string SizeSelect = "PART_SizeSelect";

    /// <summary>跳页（rune:PageJump）。</summary>
    public const string Jump = "PART_Jump";
}
