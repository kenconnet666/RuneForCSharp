using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 分页（vue-tsx-pro 的 Pagination）：上一页、页码、省略号、下一页，当前页 soft 品牌色（读屏里是选中的），其余页码是中性的 ghost 按钮。
/// 页码序列用 <see cref="PaginationItems"/>（按钮数固定，翻页时不跳动）。
/// <code>
/// var pagination = new PaginationControl { Total = 320, PageSize = 20, ShowTotal = true };
/// pagination.PageChanged += (_, _) => Load(pagination.Page);
/// </code>
/// 页数写 <see cref="PageCount"/>，或给 <see cref="Total"/> 与 <see cref="PageSize"/> 算出来；<see cref="Page"/> 超出范围时显示时夹回去，翻页发 <see cref="PageChanged"/>。
/// 给了 <see cref="Href"/> 时页码与上一页、下一页是链接按钮（可分享）。不可用时（Enabled=false）都不能点。名称默认取语言包（分页）。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(PageChanged))]
[Description("分页：上一页、页码、省略号、下一页。")]
public class PaginationControl : FlexControl
{
    readonly FlexControl list = new() { Align = FlexAlign.Center, Gap = Space.Xs, AutoSize = true };
    readonly TextControl total = new() { Tone = TextTone.Muted, Visible = false };

    /// <summary>新建：第 1 页。</summary>
    public PaginationControl()
    {
        Align = FlexAlign.Center;
        Wrap = FlexWrap.Wrap;
        Gap = Space.Md;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        Controls.AddRange([list, total]);
        Rebuild();
    }

    /// <summary>当前页，从 1 开始。</summary>
    [Category("Behavior")]
    [Description("当前页，从 1 开始。")]
    [DefaultValue(1)]
    public int Page
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Rebuild();
            PageChanged?.Invoke(this, EventArgs.Empty);
        }
    } = 1;

    /// <summary>总页数；不写时按 Total 与 PageSize 算。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public int? PageCount { get; set { field = value; Rebuild(); } }

    /// <summary>总条数：给了就能算页数，ShowTotal 时显示"共 n 条"。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public int? Total { get; set { field = value; Rebuild(); } }

    /// <summary>每页条数，默认 10。</summary>
    [Category("Behavior")]
    [DefaultValue(10)]
    public int PageSize { get; set { field = value; Rebuild(); } } = 10;

    /// <summary>当前页左右各显示几页，默认 1。</summary>
    [Category("Appearance")]
    [DefaultValue(1)]
    public int Siblings { get; set { field = value; Rebuild(); } } = 1;

    /// <summary>首尾各固定显示几页，默认 1。</summary>
    [Category("Appearance")]
    [DefaultValue(1)]
    public int Boundaries { get; set { field = value; Rebuild(); } } = 1;

    /// <summary>显示总条数（需要 Total）。</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    public bool ShowTotal { get; set { field = value; Rebuild(); } }

    /// <summary>给了就渲染为链接：每页的地址。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<int, string>? Href { get; set { field = value; Rebuild(); } }

    /// <summary>字号档位，按钮随之缩放，默认 sm。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get; set { field = value; Rebuild(); } } = TextSize.Sm;

    /// <summary>翻页了（点了页码、上一页、下一页，或代码里改了 Page）。</summary>
    public event EventHandler? PageChanged;

    bool ShouldSerializeTextSize() => TextSize != TextSize.Sm;

    void ResetTextSize() => TextSize = TextSize.Sm;

    /// <summary>总页数（至少 1）。</summary>
    [Browsable(false)]
    public int Count => PaginationItems.PageCount(PageCount, Total, PageSize);

    /// <summary>页码列表（测试用）：上一页、页码或省略号、下一页。</summary>
    internal FlexControl List => list;

    /// <summary>总条数（测试用）。</summary>
    internal TextControl TotalPart => total;

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Rebuild();
    }

    void Go(int target) => Page = Math.Clamp(target, 1, Count);

    void Rebuild()
    {
        list.SuspendLayout();
        list.Controls.Cast<Control>().DisposeAll();
        var lang = ProviderControl.LangOf(this);
        var count = Count;
        // 显示用的当前页：总数变少（筛选后）时 Page 可能超出范围，夹回去，不显示不存在的页
        var current = Math.Clamp(Page, 1, count);
        list.Controls.Add(Step(-1, current, count, lang));
        foreach (var item in PaginationItems.Of(current, count, Siblings, Boundaries))
            list.Controls.Add(item is { } page ? PageButton(page, page == current, lang) : Ellipsis());
        list.Controls.Add(Step(1, current, count, lang));
        UpdateWidths();
        list.ResumeLayout();
        total.Visible = ShowTotal && Total is not null;
        total.Text = Total is { } sum ? lang.PaginationTotal(sum) : "";
        total.TextSize = TextSize;
    }

    /// <summary>页码按钮至少一个控件高度宽（翻页时位置不晃）、左右内边距 space.xs（按钮默认 1em）。</summary>
    static readonly ButtonSx PageSx = new() { PaddingInline = Tokens.Space.Xs, MinWidth = Tokens.Size.Control };

    /// <summary>一个页码：当前页 soft 品牌色，其余中性的 ghost；有 Href 时是链接按钮。</summary>
    Control PageButton(int page, bool current, Lang lang)
    {
        var variant = current ? ButtonVariant.Soft : ButtonVariant.Ghost;
        var tone = current ? StatusTone.Accent : StatusTone.Neutral;
        Control button = Href is { } href
            ? new LinkButtonControl { Href = new Uri(href(page), UriKind.RelativeOrAbsolute), Text = page.ToString(), TextSize = TextSize, Variant = variant, Tone = tone, Sx = PageSx }
            : new ButtonControl { Text = page.ToString(), TextSize = TextSize, Variant = variant, Tone = tone, Sx = PageSx };
        button.AccessibleName = lang.PageLabel(page);
        if (current) button.AccessibleDescription = "当前页";
        button.Click += (_, _) => Go(page);
        return button;
    }

    /// <summary>上一页、下一页：到头时不可用；有 Href 时也是链接，与页码一样可分享。</summary>
    Control Step(int direction, int current, int count, Lang lang)
    {
        var target = current + direction;
        var outside = target < 1 || target > count;
        var icon = direction < 0 ? Lucide.ChevronLeft : Lucide.ChevronRight;
        var name = direction < 0 ? lang.PreviousPage : lang.NextPage;
        Control button = Href is { } href
            ? new IconLinkButtonControl { Href = new Uri(href(outside ? current : target), UriKind.RelativeOrAbsolute), Icon = icon, TextSize = TextSize, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, AccessibleName = name }
            : new IconButtonControl { Icon = icon, TextSize = TextSize, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, AccessibleName = name };
        button.Enabled = !outside;
        button.Click += (_, _) => Go(target);
        return button;
    }

    /// <summary>省略号：次要色，读屏不念，与页码按钮同宽。</summary>
    static Control Ellipsis() => new IconControl { Data = Lucide.Ellipsis, Tone = TextTone.Muted, AccessibleRole = AccessibleRole.None };

    /// <summary>省略号与页码按钮同宽（一个控件高度）：按所在显示器的 DPI，挂上窗体、换显示器时重算。</summary>
    void UpdateWidths()
    {
        var width = (int)MathF.Round(Tokens.Size.Control * (TextSize.Pixels ?? Tokens.FontSize.Md) * DeviceDpi / 96f);
        foreach (var ellipsis in list.Controls.OfType<IconControl>()) ellipsis.MinimumSize = new Size(width, 0);
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateWidths();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateWidths();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new PaginationAccessibleObject(this);

    /// <summary>名称没写时取语言包（分页），换语言后跟着换。</summary>
    sealed class PaginationAccessibleObject(PaginationControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name : ProviderControl.LangOf(owner).Pagination;
    }
}

/// <summary>
/// 翻页导航（vue-tsx-pro 的 Pager）：文章末尾的"上一篇 / 下一篇"，两张链接卡片（提示一行带箭头、次要色 sm，标题 accentText、中等字重；
/// border 边框、md 圆角，悬停时边框换品牌色）。够宽时并排两列，窄时上下排；只有下一篇时它仍在结束边。页码式的分页用 <see cref="PaginationControl"/>。
/// <code>
/// var pager = new PagerControl { Previous = new("/button", "Button 按钮"), Next = new("/dialog", "Dialog 对话框") };
/// </code>
/// 名称默认取语言包（翻页）。
/// </summary>
[ToolboxItem(true)]
[Description("翻页导航：上一篇、下一篇。")]
public class PagerControl : FlexControl
{
    /// <summary>新建：没有链接。够宽时两列（每张卡片至少 14rem、平分），放不下时换行。</summary>
    public PagerControl()
    {
        Wrap = FlexWrap.Wrap;
        Gap = Space.Md;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
    }

    /// <summary>上一篇；第一篇没有。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PagerLink? Previous { get; set { field = value; Rebuild(); } }

    /// <summary>下一篇；最后一篇没有。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PagerLink? Next { get; set { field = value; Rebuild(); } }

    /// <summary>两张卡片（测试用）。</summary>
    internal IEnumerable<PagerLinkCard> Cards => Controls.OfType<PagerLinkCard>();

    void Rebuild()
    {
        SuspendLayout();
        Controls.Cast<Control>().DisposeAll();
        var lang = ProviderControl.LangOf(this);
        // 只有下一篇时前面空一格：下一篇仍在结束边那一列
        if (Previous is not null || Next is not null) Add(Previous is { } previous ? new PagerLinkCard(previous, next: false, lang) : new Panel());
        if (Next is { } next) Add(new PagerLinkCard(next, next: true, lang));
        ResumeLayout();
    }

    void Add(Control card)
    {
        card.MinimumSize = new Size(LogicalToDeviceUnits(14 * 16), 0);
        SetGrow(card, true);
        Controls.Add(card);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new PagerAccessibleObject(this);

    /// <summary>名称没写时取语言包（翻页）。</summary>
    sealed class PagerAccessibleObject(PagerControl owner) : ControlAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name : ProviderControl.LangOf(owner).PagerLabel;
    }
}

/// <summary>翻页导航的一侧。</summary>
/// <param name="Href">地址。</param>
/// <param name="Label">文章标题。</param>
public sealed record PagerLink(string Href, string Label);

/// <summary>翻页导航的一张链接卡片：提示（上一篇、下一篇，带箭头）与标题；点了打开地址。下一篇靠结束边对齐。读屏里是链接。</summary>
sealed class PagerLinkCard : PressableFlex
{
    public PagerLinkCard(PagerLink link, bool next, Lang lang)
    {
        Link = link;
        Direction = FlexDirection.Column;
        Gap = Space.Xs;
        Align = next ? FlexAlign.End : FlexAlign.Start;
        AccessibleRole = AccessibleRole.Link;
        AccessibleName = link.Label;
        var arrow = new IconControl { Data = next ? Lucide.ArrowRight : Lucide.ArrowLeft, FlipInRtl = true, Tone = TextTone.Muted };
        var hintText = new TextControl { Text = next ? lang.PagerNext : lang.PagerPrevious, Tone = TextTone.Muted, TextSize = TextSize.Sm };
        var hint = new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs, AutoSize = true };
        hint.Controls.AddRange(next ? [hintText, arrow] : [arrow, hintText]);
        Controls.AddRange([hint, new TextControl { Text = link.Label, Tone = TextTone.Accent, Weight = TextWeight.Medium }]);
        Click += (_, _) => LinkLauncher.Open(new Uri(link.Href, UriKind.RelativeOrAbsolute));
    }

    public PagerLink Link { get; }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        var md = (int)MathF.Round(Tokens.Space.Md * this.EmPixels());
        Padding = new Padding(md);
    }

    /// <inheritdoc />
    protected override float Radius(ThemeTokens theme) => theme.Radii.Md * this.EmPixels();

    /// <summary>xs 的边框：平时 border，悬停品牌色。</summary>
    protected override void PaintSurface(Graphics graphics, ThemeTokens theme, float radius)
    {
        var line = LogicalToDeviceUnits(1);
        using var path = Drawing.RoundedRectangle(new RectangleF(line / 2f, line / 2f, Width - line, Height - line), radius);
        using var pen = new Pen(Hovered ? theme.Colors.Accent : theme.Colors.Border, line);
        graphics.DrawPath(pen, path);
    }
}
