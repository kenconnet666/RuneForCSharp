using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 每页条数选择（vue-tsx-pro 的 PageSizeSelect）：分页的配套组件，与 <see cref="PaginationControl"/> 并排放（三样都要时用 <see cref="PaginationBarControl"/>）。
/// 选项文字取语言包（"20 条/页"），默认 sm、名称取语言包（每页条数）。值那一格按最长的选项定宽：换条数时不变宽窄。
/// 是一个 <see cref="SelectControl"/>：外观同 Select；<see cref="PageSize"/> 与 Select 的 Value（字符串）互相同步，用户换了条数发 <see cref="PageSizeChanged"/>。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(PageSizeChanged))]
[Description("每页条数选择。")]
public class PageSizeSelectControl : SelectControl
{
    bool syncing;

    /// <summary>新建：sm，10 条/页。</summary>
    public PageSizeSelectControl()
    {
        TextSize = TextSize.Sm;
        Rebuild();
        ValueChanged += (_, _) =>
        {
            if (syncing || !int.TryParse(Value, out var size) || size == PageSize) return;
            PageSize = size;
            PageSizeChanged?.Invoke(this, size);
        };
    }

    /// <summary>每页条数，默认 10。</summary>
    [Category("Data")]
    [DefaultValue(10)]
    public int PageSize
    {
        get;
        set
        {
            field = value;
            Sync(() => Value = value.ToString());
        }
    } = 10;

    /// <summary>可选的条数，默认 10、20、50、100。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<int> Options { get; set { field = value; Rebuild(); } } = [10, 20, 50, 100];

    /// <summary>用户换了条数（通常把页码回到第 1 页）；代码里改 PageSize 不发。</summary>
    public event EventHandler<int>? PageSizeChanged;

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Rebuild();
        if (string.IsNullOrEmpty(AccessibleName)) AccessibleName = ProviderControl.LangOf(this).PageSizeLabel;
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        FitWidth();
    }

    void Sync(Action change)
    {
        syncing = true;
        try
        {
            change();
        }
        finally
        {
            syncing = false;
        }
    }

    void Rebuild()
    {
        var lang = ProviderControl.LangOf(this);
        Sync(() =>
        {
            ItemsSource = Options.Select(size => new ListboxOption(size.ToString(), lang.PageSizeOption(size))).ToList();
            Value = PageSize.ToString();
        });
        FitWidth();
    }

    /// <summary>值那一格按最长的选项定宽（TS 把所有选项的文字叠在同一格）：量每个选项的文字，取最宽的。</summary>
    void FitWidth()
    {
        var lang = ProviderControl.LangOf(this);
        var font = ValuePart.Font;
        var widest = Options.Select(size => Fonts.Measure(lang.PageSizeOption(size), font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width).DefaultIfEmpty().Max();
        ValuePart.MinimumSize = new Size(widest, 0);
    }
}

/// <summary>
/// 跳页（vue-tsx-pro 的 PageJump）：分页的配套组件，"跳至 [  ] 页"，回车或失焦后跳过去（夹在 1–PageCount），输入框随后清空。
/// 输入框是 <see cref="NumberFieldControl"/>（没有加减按钮、4.5em 宽），读屏念它的名称（语言包的"跳到第几页"），前后的"跳至""页"只是视觉上的提示。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(PageChanged))]
[Description("跳页：输入页码后跳过去。")]
public class PageJumpControl : FlexControl
{
    readonly TextControl before = new() { Tone = TextTone.Muted, Wrap = TextWrap.NoWrap, AccessibleRole = AccessibleRole.None };
    readonly NumberFieldControl input = new() { Stepper = false, Min = 1, Max = 1 };
    readonly TextControl after = new() { Tone = TextTone.Muted, Wrap = TextWrap.NoWrap, AccessibleRole = AccessibleRole.None };

    /// <summary>新建：第 1 页，共 1 页。</summary>
    public PageJumpControl()
    {
        Align = FlexAlign.Center;
        Gap = Space.Sm;
        AutoSize = true;
        Controls.AddRange([before, input, after]);
        input.ValueChanged += (_, _) =>
        {
            if (input.Value is { } target) Jump(target);
        };
        ApplyTextSize();
    }

    /// <summary>当前页；跳页时写回、发 PageChanged。</summary>
    [Category("Behavior")]
    [DefaultValue(1)]
    public int Page { get; set { if (field == value) return; field = value; PageChanged?.Invoke(this, EventArgs.Empty); } } = 1;

    /// <summary>总页数：输入的页码夹在 1–PageCount。</summary>
    [Category("Behavior")]
    [DefaultValue(1)]
    public int PageCount { get; set { field = value; input.Max = Math.Max(1, value); } } = 1;

    /// <summary>字号档位，默认 sm（同 Pagination）。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get; set { field = value; ApplyTextSize(); } } = TextSize.Sm;

    /// <summary>跳页了（Page 变了）。</summary>
    public event EventHandler? PageChanged;

    bool ShouldSerializeTextSize() => TextSize != TextSize.Sm;

    void ResetTextSize() => TextSize = TextSize.Sm;

    /// <summary>输入框（测试用）。</summary>
    internal NumberFieldControl Input => input;

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        var lang = ProviderControl.LangOf(this);
        before.Text = lang.PageJumpBefore;
        before.Visible = lang.PageJumpBefore.Length > 0;
        after.Text = lang.PageJumpAfter;
        after.Visible = lang.PageJumpAfter.Length > 0;
        input.AccessibleName = lang.PageJumpLabel;
        ApplyTextSize();
    }

    /// <summary>字号档位与输入框的宽度（4.5em）。</summary>
    void ApplyTextSize()
    {
        input.TextSize = before.TextSize = after.TextSize = TextSize;
        var em = (TextSize.Pixels ?? Tokens.FontSize.Md) * DeviceDpi / 96f;
        input.MinimumSize = input.MaximumSize = new Size((int)MathF.Round(4.5f * em), 0);
    }

    /// <summary>跳过去（四舍五入、夹在 1–PageCount），输入框清空：它只在跳之前暂存页码。</summary>
    void Jump(decimal target)
    {
        Page = (int)Math.Clamp(Math.Round(target, MidpointRounding.AwayFromZero), 1, Math.Max(1, PageCount));
        input.Value = null;
    }
}

/// <summary>
/// 分页栏（vue-tsx-pro 的 PaginationBar）：表格、列表下方的一整行，起始边是"第 1–20 条，共 320 条"，结束边是页码、每页条数、跳页；
/// 拼的是 <see cref="PaginationControl"/>、<see cref="PageSizeSelectControl"/>、<see cref="PageJumpControl"/>，换每页条数时回到第 1 页。
/// <code>
/// var bar = new PaginationBarControl { Total = 320, Dock = DockStyle.Bottom };
/// bar.PageChanged += (_, _) => Load(bar.Page, bar.PageSize);
/// </code>
/// 按自己的宽度取舍（TS 的容器查询）：窄于 36em 时不显示跳页，窄于 24em 时总数单独一行。只要其中几样时用 ShowTotal、ShowSizeSelect、ShowJump 关掉，
/// 总数的写法可以用 <see cref="TotalText"/> 自己给。不可用时（Enabled=false）都不能操作。分页导航的名称写在 <see cref="PaginationPart"/> 上。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(PageChanged))]
[Description("分页栏：总数、页码、每页条数、跳页。")]
public class PaginationBarControl : FlexControl
{
    readonly TextControl total = new() { Tone = TextTone.Muted };
    readonly FlexControl controls = new() { Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, Justify = FlexJustify.End, RowGap = Space.Sm, ColumnGap = Space.Md, AutoSize = true };
    readonly PaginationControl pagination = new();
    readonly PageSizeSelectControl sizeSelect = new();
    readonly PageJumpControl jump = new();
    bool syncing;

    /// <summary>新建：第 1 页，10 条/页。</summary>
    public PaginationBarControl()
    {
        Wrap = FlexWrap.Wrap;
        Align = FlexAlign.Center;
        Justify = FlexJustify.Between;
        RowGap = Space.Sm;
        ColumnGap = Space.Md;
        AutoSize = true;
        controls.Controls.AddRange([pagination, sizeSelect, jump]);
        Controls.AddRange([total, controls]);
        // 结束边那一组：与总数同一行时排在右边；换到下一行时也靠右（TS 的 margin-inline-start: auto）
        SetGrow(controls, true);
        pagination.PageChanged += (_, _) => GoTo(pagination.Page);
        jump.PageChanged += (_, _) => GoTo(jump.Page);
        sizeSelect.PageSizeChanged += (_, size) =>
        {
            PageSize = size;
            GoTo(1);
        };
        Sync();
    }

    /// <summary>当前页，从 1 开始。</summary>
    [Category("Behavior")]
    [DefaultValue(1)]
    public int Page { get; set { field = value; Sync(); } } = 1;

    /// <summary>每页条数，默认 10。</summary>
    [Category("Behavior")]
    [DefaultValue(10)]
    public int PageSize { get; set { field = value; Sync(); } } = 10;

    /// <summary>总条数。</summary>
    [Category("Behavior")]
    [DefaultValue(0)]
    public int Total { get; set { field = value; Sync(); } }

    /// <summary>每页条数的选项，默认 10、20、50、100。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<int> PageSizeOptions { get; set { field = value; Sync(); } } = [10, 20, 50, 100];

    /// <summary>起始边显示总数，默认 true。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool ShowTotal { get; set { field = value; Sync(); } } = true;

    /// <summary>总数的写法（TS 的 showTotal 给函数）：拿到这一页的起止与总数；不写时是"第 1–20 条，共 320 条"。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<PageRange, string>? TotalText { get; set { field = value; Sync(); } }

    /// <summary>每页条数选择，默认 true。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool ShowSizeSelect { get; set { field = value; Sync(); } } = true;

    /// <summary>跳页，默认 true（窄于 36em、只有一页时不显示）。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool ShowJump { get; set { field = value; Sync(); } } = true;

    /// <summary>字号档位，各部分随之缩放，默认 sm。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get; set { field = value; Sync(); } } = TextSize.Sm;

    /// <summary>翻页了，或换了每页条数（回到第 1 页）。</summary>
    public event EventHandler? PageChanged;

    bool ShouldSerializeTextSize() => TextSize != TextSize.Sm;

    void ResetTextSize() => TextSize = TextSize.Sm;

    /// <summary>页码部件：分页导航的名称写在它上面（TS 的 propsPagination 的 aria-label）。</summary>
    [Browsable(false)]
    public PaginationControl PaginationPart => pagination;

    /// <summary>每页条数部件。</summary>
    [Browsable(false)]
    public PageSizeSelectControl SizeSelectPart => sizeSelect;

    /// <summary>跳页部件。</summary>
    [Browsable(false)]
    public PageJumpControl JumpPart => jump;

    /// <summary>总数文字（测试用）。</summary>
    internal TextControl TotalPart => total;

    /// <summary>当前页显示的是哪几条（Page 超出范围时按夹回去的页算，同 Pagination）。</summary>
    [Browsable(false)]
    public PageRange Range
    {
        get
        {
            var count = Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));
            var current = Math.Clamp(Page, 1, count);
            return Total == 0 ? new PageRange(0, 0, 0) : new PageRange((current - 1) * PageSize + 1, Math.Min(current * PageSize, Total), Total);
        }
    }

    /// <summary>窄于 36em 不显示跳页（按所在显示器的 DPI 与字号档位算）。</summary>
    bool Narrow => Width < 36 * Em;

    /// <summary>窄于 24em 总数单独一行。</summary>
    bool Compact => Width < 24 * Em;

    float Em => (TextSize.Pixels ?? Tokens.FontSize.Md) * DeviceDpi / 96f;

    void GoTo(int page)
    {
        if (syncing) return;
        Page = page;
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }

    /// <summary>按自己的宽度取舍（TS 的容器查询）。</summary>
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        var jumpShown = ShowJump && pagination.Count > 1 && !Narrow;
        var totalWidth = Compact ? Width : 0;
        if (jump.Visible == jumpShown && total.MinimumSize.Width == totalWidth) return;
        // 排版中途改子控件会再触发排版：挪到这一轮排完之后（还没有窗口时直接改）
        if (IsHandleCreated) BeginInvoke(Sync);
        else Sync();
    }

    void Sync()
    {
        if (pagination is null) return;
        syncing = true;
        try
        {
            var lang = ProviderControl.LangOf(this);
            var range = Range;
            pagination.TextSize = sizeSelect.TextSize = jump.TextSize = total.TextSize = TextSize;
            pagination.Total = Total;
            pagination.PageSize = PageSize;
            pagination.Page = Page;
            sizeSelect.Options = PageSizeOptions;
            sizeSelect.PageSize = PageSize;
            jump.PageCount = pagination.Count;
            jump.Page = Page;
            total.Visible = ShowTotal;
            total.Text = TotalText?.Invoke(range) ?? (range.Total == 0 ? lang.PaginationTotal(0) : lang.PaginationRange(range.From, range.To, range.Total));
            // 窄时总数单独一行（TS 的 flex-basis: 100%）
            total.MinimumSize = new Size(Compact ? Width : 0, 0);
            sizeSelect.Visible = ShowSizeSelect;
            jump.Visible = ShowJump && pagination.Count > 1 && !Narrow;
        }
        finally
        {
            syncing = false;
        }
    }
}

/// <summary>当前页显示的是哪几条（vue-tsx-pro 的 PageRange）。</summary>
/// <param name="From">第一条的序号，从 1 开始；没有数据时是 0。</param>
/// <param name="To">最后一条的序号。</param>
/// <param name="Total">总条数。</param>
public readonly record struct PageRange(int From, int To, int Total);
