using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 分页（vue-tsx-pro 的 Pagination）：上一页、页码、省略号、下一页，当前页 soft 品牌色、读屏的项状态是"当前页"，其余页码是中性的 ghost 按钮。
/// 页码序列用 <see cref="PaginationItems"/>（按钮数固定，翻页时不跳动）。
/// <code>
/// &lt;rune:Pagination Page="{Binding Page}" Total="320" PageSize="20" ShowTotal="True" /&gt;
/// </code>
/// 页数写 <see cref="PageCount"/>，或给 <see cref="Total"/> 与 <see cref="PageSize"/> 算出来；<see cref="Page"/> 默认双向绑定，超出范围时显示时夹回去。
/// 给了 <see cref="Href"/> 时页码与上一页、下一页是链接（可分享）。不可用时（IsEnabled=false）都不能点。名称默认取语言包（分页）。
/// </summary>
public class Pagination : Control
{
    /// <summary>当前页，从 1 开始；默认双向绑定。</summary>
    public static readonly StyledProperty<int> PageProperty =
        AvaloniaProperty.Register<Pagination, int>(nameof(Page), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>总页数；不写时按 Total 与 PageSize 算。</summary>
    public static readonly StyledProperty<int?> PageCountProperty = AvaloniaProperty.Register<Pagination, int?>(nameof(PageCount));

    /// <summary>总条数：给了就能算页数，ShowTotal 时显示"共 n 条"。</summary>
    public static readonly StyledProperty<int?> TotalProperty = AvaloniaProperty.Register<Pagination, int?>(nameof(Total));

    /// <summary>每页条数，默认 10。</summary>
    public static readonly StyledProperty<int> PageSizeProperty = AvaloniaProperty.Register<Pagination, int>(nameof(PageSize), 10);

    /// <summary>当前页左右各显示几页，默认 1。</summary>
    public static readonly StyledProperty<int> SiblingsProperty = AvaloniaProperty.Register<Pagination, int>(nameof(Siblings), 1);

    /// <summary>首尾各固定显示几页，默认 1。</summary>
    public static readonly StyledProperty<int> BoundariesProperty = AvaloniaProperty.Register<Pagination, int>(nameof(Boundaries), 1);

    /// <summary>显示总条数（需要 Total）。</summary>
    public static readonly StyledProperty<bool> ShowTotalProperty = AvaloniaProperty.Register<Pagination, bool>(nameof(ShowTotal));

    /// <summary>给了就渲染为链接：每页的地址。</summary>
    public static readonly StyledProperty<Func<int, string>?> HrefProperty = AvaloniaProperty.Register<Pagination, Func<int, string>?>(nameof(Href));

    /// <summary>字号档位，按钮随之缩放，默认 sm。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Pagination, TextSize>(nameof(Size), TextSize.Sm);

    readonly Flex root = new() { Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Gap = Space.Md };
    readonly Flex list = new() { Name = PaginationParts.List, Align = FlexAlign.Center, Gap = Space.Xs };
    readonly TextBlock total = new() { Name = PaginationParts.Total, IsVisible = false };
    readonly TemplateResources resources;

    /// <summary>新建：第 1 页。读屏里是导航地标。</summary>
    public Pagination()
    {
        resources = new TemplateResources(this);
        root.Children.AddRange([list, total]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Navigation);
        ApplySize();
    }

    /// <summary>当前页。</summary>
    public int Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <summary>总页数。</summary>
    public int? PageCount { get => GetValue(PageCountProperty); set => SetValue(PageCountProperty, value); }

    /// <summary>总条数。</summary>
    public int? Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <summary>每页条数。</summary>
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <summary>当前页左右各显示几页。</summary>
    public int Siblings { get => GetValue(SiblingsProperty); set => SetValue(SiblingsProperty, value); }

    /// <summary>首尾各固定显示几页。</summary>
    public int Boundaries { get => GetValue(BoundariesProperty); set => SetValue(BoundariesProperty, value); }

    /// <summary>显示总条数。</summary>
    public bool ShowTotal { get => GetValue(ShowTotalProperty); set => SetValue(ShowTotalProperty, value); }

    /// <summary>每页的地址。</summary>
    public Func<int, string>? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>总页数（至少 1）。</summary>
    public int Count => PaginationItems.PageCount(PageCount, Total, PageSize);

    /// <summary>显示用的当前页：总数变少（筛选后）时 Page 可能超出范围，夹回去，不显示不存在的页。</summary>
    int Shown => Math.Clamp(Page, 1, Count);

    /// <summary>页码列表（测试用）：上一页、页码或省略号、下一页。</summary>
    internal Flex List => list;

    /// <summary>总条数（测试用）。</summary>
    internal TextBlock TotalPart => total;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty) ApplySize();
        if (change.Property == PageProperty || change.Property == PageCountProperty || change.Property == TotalProperty || change.Property == PageSizeProperty
            || change.Property == SiblingsProperty || change.Property == BoundariesProperty || change.Property == ShowTotalProperty
            || change.Property == HrefProperty || change.Property == SizeProperty)
            Rebuild();
        else if (change.Property == TextElement.FontSizeProperty) UpdateEllipses();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Rebuild();
    }

    void ApplySize()
    {
        if (Size.Pixels is { } pixels) resources.Set(TextElement.FontSizeProperty, pixels);
        else resources.Bind(TextElement.FontSizeProperty, Size.ResourceKey);
    }

    /// <summary>翻到某一页（夹在 1 与总页数之间）。</summary>
    void Go(int target) => SetCurrentValue(PageProperty, Math.Clamp(target, 1, Count));

    void Rebuild()
    {
        list.Children.Clear();
        var lang = Provider.GetLang(this);
        var count = Count;
        var current = Shown;
        list.Children.Add(Step(-1, current, count, lang));
        foreach (var item in PaginationItems.Of(current, count, Siblings, Boundaries))
            list.Children.Add(item is { } page ? PageButton(page, page == current, lang) : Ellipsis());
        list.Children.Add(Step(1, current, count, lang));
        total.IsVisible = ShowTotal && Total is not null;
        total.Text = Total is { } sum ? lang.PaginationTotal(sum) : null;
        UpdateEllipses();
    }

    /// <summary>一个页码：当前页 soft 品牌色，其余中性的 ghost；有 Href 时是链接按钮。</summary>
    Button PageButton(int page, bool current, Lang lang)
    {
        Button button = Href is { } href ? new LinkButton { Href = new Uri(href(page), UriKind.RelativeOrAbsolute) } : new Button();
        button.Name = PaginationParts.Page;
        button.Content = page.ToString();
        button.Size = Size;
        button.Variant = current ? ButtonVariant.Soft : ButtonVariant.Ghost;
        button.Tone = current ? StatusTone.Accent : StatusTone.Neutral;
        AutomationProperties.SetName(button, lang.PageLabel(page));
        if (current) AutomationProperties.SetItemStatus(button, "当前页");
        button.Click += (_, _) => Go(page);
        return button;
    }

    /// <summary>上一页、下一页：到头时不可用；有 Href 时也是链接，与页码一样可分享。从右到左时箭头翻转。</summary>
    IconButton Step(int direction, int current, int count, Lang lang)
    {
        var target = current + direction;
        var outside = target < 1 || target > count;
        IconButton button = Href is { } href ? new IconLinkButton { Href = new Uri(href(outside ? current : target), UriKind.RelativeOrAbsolute) } : new IconButton();
        button.Name = PaginationParts.Step;
        button.Icon = direction < 0 ? Lucide.ChevronLeft : Lucide.ChevronRight;
        button.Size = Size;
        button.Variant = ButtonVariant.Ghost;
        button.Tone = StatusTone.Neutral;
        button.IsEnabled = !outside;
        AutomationProperties.SetName(button, direction < 0 ? lang.PreviousPage : lang.NextPage);
        button.Click += (_, _) => Go(target);
        return button;
    }

    /// <summary>省略号：读屏不念，与页码按钮同宽。</summary>
    static Border Ellipsis()
    {
        var border = new Border { Name = PaginationParts.Ellipsis, Child = new Icon { Data = Lucide.Ellipsis, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center } };
        AutomationProperties.SetAccessibilityView(border, AccessibilityView.Raw);
        return border;
    }

    /// <summary>省略号与页码按钮同宽（控件高度，按字号）：Border 没有字号，不能用 rune:Em。</summary>
    void UpdateEllipses()
    {
        var width = this.Resource<double>(ThemeKeys.Size.Control) * GetValue(TextElement.FontSizeProperty);
        foreach (var border in list.Children.OfType<Border>()) border.MinWidth = width;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new PaginationPeer(this);

    /// <summary>名称没写时取语言包（分页），换语言后跟着换。</summary>
    sealed class PaginationPeer(Pagination owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).Pagination;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>Pagination 的部件名（样式选择器用）。</summary>
public static class PaginationParts
{
    /// <summary>页码列表（rune:Flex）。</summary>
    public const string List = "PART_List";

    /// <summary>页码按钮（rune:Button，有 Href 时是 rune:LinkButton）。</summary>
    public const string Page = "PART_Page";

    /// <summary>上一页、下一页（rune:IconButton，有 Href 时是 rune:IconLinkButton）。</summary>
    public const string Step = "PART_Step";

    /// <summary>省略号（Border）。</summary>
    public const string Ellipsis = "PART_Ellipsis";

    /// <summary>总条数（TextBlock）。</summary>
    public const string Total = "PART_Total";
}
