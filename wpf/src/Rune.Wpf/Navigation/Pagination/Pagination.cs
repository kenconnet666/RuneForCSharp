using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 分页（vue-tsx-pro 的 Pagination）：上一页、页码、省略号、下一页，当前页 soft 品牌色、读屏的项状态是"当前页"，其余页码是中性的 ghost 按钮。
/// 页码序列用 <see cref="PaginationItems"/>（按钮数固定，翻页时不跳动）。
/// <code>
/// &lt;rune:Pagination Page="{Binding Page}" Total="320" PageSize="20" ShowTotal="True" /&gt;
/// </code>
/// 页数写 <see cref="PageCount"/>，或给 <see cref="Total"/> 与 <see cref="PageSize"/> 算出来；<see cref="Page"/> 默认双向绑定，超出范围时显示时夹回去。
/// 给了 <see cref="Href"/> 时页码与上一页、下一页是链接（可分享）。不可用时（IsEnabled=false）都不能点。名称默认取语言包（分页）。
/// 部件样式键：<see cref="PaginationParts"/>。
/// </summary>
public class Pagination : FrameworkElement
{
    /// <summary>当前页，从 1 开始；默认双向绑定。</summary>
    public static readonly DependencyProperty PageProperty = DependencyProperty.Register(nameof(Page), typeof(int), typeof(Pagination),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));

    /// <summary>总页数；不写时按 Total 与 PageSize 算。</summary>
    public static readonly DependencyProperty PageCountProperty = DependencyProperty.Register(nameof(PageCount), typeof(int?), typeof(Pagination), new FrameworkPropertyMetadata(null, Changed));

    /// <summary>总条数：给了就能算页数，ShowTotal 时显示"共 n 条"。</summary>
    public static readonly DependencyProperty TotalProperty = DependencyProperty.Register(nameof(Total), typeof(int?), typeof(Pagination), new FrameworkPropertyMetadata(null, Changed));

    /// <summary>每页条数，默认 10。</summary>
    public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(Pagination), new FrameworkPropertyMetadata(10, Changed));

    /// <summary>当前页左右各显示几页，默认 1。</summary>
    public static readonly DependencyProperty SiblingsProperty = DependencyProperty.Register(nameof(Siblings), typeof(int), typeof(Pagination), new FrameworkPropertyMetadata(1, Changed));

    /// <summary>首尾各固定显示几页，默认 1。</summary>
    public static readonly DependencyProperty BoundariesProperty = DependencyProperty.Register(nameof(Boundaries), typeof(int), typeof(Pagination), new FrameworkPropertyMetadata(1, Changed));

    /// <summary>显示总条数（需要 Total）。</summary>
    public static readonly DependencyProperty ShowTotalProperty = DependencyProperty.Register(nameof(ShowTotal), typeof(bool), typeof(Pagination), new FrameworkPropertyMetadata(false, Changed));

    /// <summary>给了就渲染为链接：每页的地址。</summary>
    public static readonly DependencyProperty HrefProperty = DependencyProperty.Register(nameof(Href), typeof(Func<int, string>), typeof(Pagination), new FrameworkPropertyMetadata(null, Changed));

    /// <summary>字号档位，按钮随之缩放，默认 sm。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Pagination),
        new FrameworkPropertyMetadata(TextSize.Sm, (d, e) =>
        {
            ((Pagination)d).ApplySize();
            Changed(d, e);
        }));

    readonly Flex root = new() { Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Gap = Space.Md };
    readonly Flex list = new() { Align = FlexAlign.Center, Gap = Space.Xs };
    readonly TextBlock total = new() { Visibility = Visibility.Collapsed };

    /// <summary>新建：第 1 页。</summary>
    public Pagination()
    {
        list.SetResourceReference(StyleProperty, PaginationParts.List);
        total.SetResourceReference(StyleProperty, PaginationParts.Total);
        root.Children.Add(list);
        root.Children.Add(total);
        AddLogicalChild(root);
        AddVisualChild(root);
        ApplySize();
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>当前页。</summary>
    public int Page { get => (int)GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <summary>总页数。</summary>
    public int? PageCount { get => (int?)GetValue(PageCountProperty); set => SetValue(PageCountProperty, value); }

    /// <summary>总条数。</summary>
    public int? Total { get => (int?)GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    /// <summary>每页条数。</summary>
    public int PageSize { get => (int)GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <summary>当前页左右各显示几页。</summary>
    public int Siblings { get => (int)GetValue(SiblingsProperty); set => SetValue(SiblingsProperty, value); }

    /// <summary>首尾各固定显示几页。</summary>
    public int Boundaries { get => (int)GetValue(BoundariesProperty); set => SetValue(BoundariesProperty, value); }

    /// <summary>显示总条数。</summary>
    public bool ShowTotal { get => (bool)GetValue(ShowTotalProperty); set => SetValue(ShowTotalProperty, value); }

    /// <summary>每页的地址。</summary>
    public Func<int, string>? Href { get => (Func<int, string>?)GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>总页数（至少 1）。</summary>
    public int Count => PaginationItems.PageCount(PageCount, Total, PageSize);

    /// <summary>显示用的当前页：总数变少（筛选后）时 Page 可能超出范围，夹回去，不显示不存在的页。</summary>
    int Shown => Math.Clamp(Page, 1, Count);

    /// <summary>页码列表（测试用）：上一页、页码或省略号、下一页。</summary>
    internal Flex List => list;

    /// <summary>总条数（测试用）。</summary>
    internal TextBlock TotalPart => total;

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
        if (e.Property == TextElement.FontSizeProperty) UpdateEllipses();
    }

    static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Pagination)d).Rebuild();

    /// <summary>档位是主题的字号（资源引用），写成像素数时是本地值：省略号按它算宽度。</summary>
    void ApplySize()
    {
        if (Size.Pixels is { } pixels) SetValue(TextElement.FontSizeProperty, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(TextElement.FontSizeProperty, key);
        else ClearValue(TextElement.FontSizeProperty);
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
        total.Visibility = ShowTotal && Total is not null ? Visibility.Visible : Visibility.Collapsed;
        total.Text = Total is { } sum ? lang.PaginationTotal(sum) : null;
        UpdateEllipses();
    }

    /// <summary>一个页码：当前页 soft 品牌色，其余中性的 ghost；有 Href 时是链接按钮。</summary>
    Button PageButton(int page, bool current, Lang lang)
    {
        Button button = Href is { } href ? new LinkButton { Href = new Uri(href(page), UriKind.RelativeOrAbsolute) } : new Button();
        button.SetResourceReference(StyleProperty, PaginationParts.Page);
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
        button.SetResourceReference(StyleProperty, PaginationParts.Step);
        button.Icon = direction < 0 ? Lucide.ChevronLeft : Lucide.ChevronRight;
        button.Size = Size;
        button.Variant = ButtonVariant.Ghost;
        button.Tone = StatusTone.Neutral;
        button.IsEnabled = !outside;
        AutomationProperties.SetName(button, direction < 0 ? lang.PreviousPage : lang.NextPage);
        button.Click += (_, _) => Go(target);
        return button;
    }

    /// <summary>省略号（没有名称的图标，读屏不念），与页码按钮同宽。</summary>
    static Border Ellipsis()
    {
        var border = new Border { Child = new Icon { Data = Lucide.Ellipsis, HorizontalAlignment = HorizontalAlignment.Center } };
        border.SetResourceReference(StyleProperty, PaginationParts.Ellipsis);
        return border;
    }

    /// <summary>省略号与页码按钮同宽（控件高度，按字号）。</summary>
    void UpdateEllipses()
    {
        var width = this.Resource<double>(ThemeKeys.Size.Control) * TextElement.GetFontSize(this);
        foreach (var border in list.Children.OfType<Border>()) border.MinWidth = width;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new PaginationPeer(this);

    sealed class PaginationPeer(Pagination owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Pagination);

        // WPF 的读屏节点没有地标类型：本地化的控件类型写"导航"
        protected override string GetLocalizedControlTypeCore() => "导航";

        /// <summary>名称没写时取语言包（分页），换语言后跟着换。</summary>
        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).Pagination;
    }
}

/// <summary>Pagination 的部件样式键。</summary>
public static class PaginationParts
{
    /// <summary>页码列表。</summary>
    public static readonly PartKey List = new(typeof(Pagination), nameof(List), typeof(Flex));

    /// <summary>页码按钮（有 Href 时是链接按钮）。</summary>
    public static readonly PartKey Page = new(typeof(Pagination), nameof(Page), typeof(Button));

    /// <summary>上一页、下一页（有 Href 时是图标链接按钮）。</summary>
    public static readonly PartKey Step = new(typeof(Pagination), nameof(Step), typeof(IconButton));

    /// <summary>省略号。</summary>
    public static readonly PartKey Ellipsis = new(typeof(Pagination), nameof(Ellipsis), typeof(Border));

    /// <summary>总条数。</summary>
    public static readonly PartKey Total = new(typeof(Pagination), nameof(Total), typeof(TextBlock));
}
