using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 页头（vue-tsx-pro 的 PageHeader）：面包屑、返回、标题与标记、说明、结束边的操作，下面还可以放摘要或标签页。列表页、详情页、表单页
/// 开头都是这一块，不用每页自己拿 Flex 拼。
/// <code>
/// &lt;rune:PageHeader Title="PO-2026-0187" Description="华辰精密 · 2026-10-20 下单" Back="OnBack"
///                  Breadcrumbs="{Binding Trail}" Meta="{Binding StatusBadge}" Actions="{Binding OrderActions}"&gt;
///   &lt;rune:Descriptions …/&gt;
/// &lt;/rune:PageHeader&gt;
/// </code>
/// <list type="bullet">
/// <item>标题是 H1（<see cref="HeadingLevel"/> 可改成 2、3：嵌在页面里的一块，抽屉、分栏的右侧），字号固定 xxl；说明是次要色的段落。</item>
/// <item>操作靠结束边，放不下时整块换到标题下面一行。<see cref="Meta"/> 跟在标题后面（状态、标签）。</item>
/// <item>返回：订阅了 <see cref="Back"/>（或给了 <see cref="BackCommand"/>）就显示返回按钮；给了 <see cref="BackHref"/> 时返回是链接（点击时同样发 Back）。
/// 名称取语言包（"返回"）。</item>
/// <item>Content 排在最下面：摘要（Descriptions）、标签页。</item>
/// </list>
/// 子元素的名字在 <see cref="PageHeaderParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class PageHeader : FrameworkElement
{
    /// <summary>标题。</summary>
    public static readonly DependencyProperty TitleProperty = Register(nameof(Title), typeof(string), null);

    /// <summary>标题下面的说明。</summary>
    public static readonly DependencyProperty DescriptionProperty = Register(nameof(Description), typeof(string), null);

    /// <summary>标题的级别（1–3），默认 1。</summary>
    public static readonly DependencyProperty HeadingLevelProperty = DependencyProperty.Register(
        nameof(HeadingLevel), typeof(int), typeof(PageHeader), new FrameworkPropertyMetadata(1, (d, _) => ((PageHeader)d).PlaceTitle()), value => value is >= 1 and <= 3);

    /// <summary>面包屑：最后一项是当前页。</summary>
    public static readonly DependencyProperty BreadcrumbsProperty = Register(nameof(Breadcrumbs), typeof(IReadOnlyList<BreadcrumbItem>), null);

    /// <summary>标题后面的标记：状态、标签。</summary>
    public static readonly DependencyProperty MetaProperty = Register(nameof(Meta), typeof(object), null);

    /// <summary>结束边的操作：按钮（几个时放进一个 Flex）。</summary>
    public static readonly DependencyProperty ActionsProperty = Register(nameof(Actions), typeof(object), null);

    /// <summary>返回的地址：给了时返回是一个链接按钮。</summary>
    public static readonly DependencyProperty BackHrefProperty = Register(nameof(BackHref), typeof(Uri), null);

    /// <summary>点了返回时执行的命令（MVVM 里代替 Back 事件）；给了它也显示返回按钮。</summary>
    public static readonly DependencyProperty BackCommandProperty = Register(nameof(BackCommand), typeof(ICommand), null);

    /// <summary>页头下面的内容：摘要、标签页。</summary>
    public static readonly DependencyProperty ContentProperty = Register(nameof(Content), typeof(object), null);

    /// <summary>点了返回（TS 的 onBack）。</summary>
    public static readonly RoutedEvent BackEvent = EventManager.RegisterRoutedEvent(nameof(Back), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PageHeader));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Md };
    readonly Breadcrumbs breadcrumbs = new() { Name = PageHeaderParts.Breadcrumbs, Visibility = Visibility.Collapsed };
    readonly Flex main = new() { Name = PageHeaderParts.Main, Wrap = FlexWrap.Wrap, Align = FlexAlign.Start, Gap = Space.Md };
    readonly Flex heading = new() { Name = PageHeaderParts.Heading, Direction = FlexDirection.Column, Gap = Space.Xs };
    readonly Flex titleRow = new() { Name = PageHeaderParts.TitleRow, Wrap = FlexWrap.Wrap, Align = FlexAlign.Center, ColumnGap = Space.Md, RowGap = Space.Xs };
    readonly ContentControl meta = new() { Name = PageHeaderParts.Meta, Visibility = Visibility.Collapsed, Focusable = false, VerticalAlignment = VerticalAlignment.Center };
    readonly Paragraph description = new() { Name = PageHeaderParts.Description, Tone = TextTone.Muted, Visibility = Visibility.Collapsed };
    readonly ContentControl actions = new() { Name = PageHeaderParts.Actions, Visibility = Visibility.Collapsed, Focusable = false, HorizontalAlignment = HorizontalAlignment.Right };
    readonly ContentControl content = new() { Visibility = Visibility.Collapsed, Focusable = false };
    Heading title = new H1();
    ButtonBase? back;
    // 订阅了 Back 的个数：有人订阅才显示返回按钮（TS 是给了 onBack 才有）
    int backHandlers;

    static DependencyProperty Register(string name, Type type, object? defaultValue) =>
        DependencyProperty.Register(name, type, typeof(PageHeader), new FrameworkPropertyMetadata(defaultValue, (d, e) => ((PageHeader)d).OnChanged(e.Property)));

    /// <summary>新建：标题是 H1。</summary>
    public PageHeader()
    {
        // 标题一侧占满剩下的宽度，操作靠结束边；换到下一行时也靠结束边（TS 的 margin-inline-start: auto）
        Flex.SetGrow(heading, true);
        Flex.SetGrow(actions, true);
        PlaceTitle();
        heading.Children.Add(titleRow);
        heading.Children.Add(description);
        main.Children.Add(heading);
        main.Children.Add(actions);
        root.Children.Add(breadcrumbs);
        root.Children.Add(main);
        root.Children.Add(content);
        AddLogicalChild(root);
        AddVisualChild(root);
        Loaded += (_, _) => UpdateBack();
    }

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>标题的级别。</summary>
    public int HeadingLevel { get => (int)GetValue(HeadingLevelProperty); set => SetValue(HeadingLevelProperty, value); }

    /// <summary>面包屑。</summary>
    public IReadOnlyList<BreadcrumbItem>? Breadcrumbs { get => (IReadOnlyList<BreadcrumbItem>?)GetValue(BreadcrumbsProperty); set => SetValue(BreadcrumbsProperty, value); }

    /// <summary>标题后面的标记。</summary>
    public object? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <summary>结束边的操作。</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <summary>返回的地址。</summary>
    public Uri? BackHref { get => (Uri?)GetValue(BackHrefProperty); set => SetValue(BackHrefProperty, value); }

    /// <summary>点了返回时执行的命令。</summary>
    public ICommand? BackCommand { get => (ICommand?)GetValue(BackCommandProperty); set => SetValue(BackCommandProperty, value); }

    /// <summary>页头下面的内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>点了返回；订阅了它（或给了 BackHref、BackCommand）才显示返回按钮。</summary>
    public event RoutedEventHandler Back
    {
        add
        {
            AddHandler(BackEvent, value);
            backHandlers++;
            UpdateBack();
        }
        remove
        {
            RemoveHandler(BackEvent, value);
            backHandlers--;
            UpdateBack();
        }
    }

    /// <summary>标题（测试用）。</summary>
    internal Heading TitlePart => title;

    /// <summary>返回按钮；没有返回时为 null（测试用）。</summary>
    internal ButtonBase? BackPart => back;

    void OnChanged(DependencyProperty property)
    {
        if (property == TitleProperty) title.Text = Title;
        else if (property == DescriptionProperty) Show(description, Description, () => description.Text = Description);
        else if (property == BreadcrumbsProperty)
        {
            breadcrumbs.Items = Breadcrumbs;
            breadcrumbs.Visibility = Breadcrumbs is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (property == MetaProperty) Show(meta, Meta, () => meta.Content = Meta);
        else if (property == ActionsProperty) Show(actions, Actions, () => actions.Content = Actions);
        else if (property == ContentProperty) Show(content, Content, () => content.Content = Content);
        else if (property == BackHrefProperty || property == BackCommandProperty) UpdateBack();
    }

    static void Show(UIElement part, object? value, Action set)
    {
        set();
        part.Visibility = value is not null and not "" ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>标题换级别：H1–H3 是不同的控件（读屏念出级别），字号固定 xxl。</summary>
    void PlaceTitle()
    {
        titleRow.Children.Remove(title);
        title = HeadingLevel switch { 2 => new H2(), 3 => new H3(), _ => new H1() };
        title.Name = PageHeaderParts.Title;
        title.Size = TextSize.Xxl;
        title.Text = Title;
        titleRow.Children.Insert(0, title);
        if (!titleRow.Children.Contains(meta)) titleRow.Children.Add(meta);
    }

    /// <summary>返回按钮：给了 BackHref 是图标链接按钮，否则是图标按钮；ghost、中性。</summary>
    void UpdateBack()
    {
        var wanted = BackHref is not null || BackCommand is not null || backHandlers > 0;
        if (back is not null && (!wanted || (back is IconLinkButton) != (BackHref is not null)))
        {
            main.Children.Remove(back);
            back = null;
        }
        if (back is IconLinkButton link) link.Href = BackHref;
        if (!wanted || back is not null) return;
        var button = BackHref is { } href ? new IconLinkButton { Href = href } : new IconButton();
        button.Name = PageHeaderParts.Back;
        button.Icon = Lucide.ArrowLeft;
        button.Variant = ButtonVariant.Ghost;
        button.Tone = StatusTone.Neutral;
        AutomationProperties.SetName(button, PatternsMessages.For(Provider.GetLang(this)).PageHeaderBack);
        button.Click += (_, _) =>
        {
            RaiseEvent(new RoutedEventArgs(BackEvent, this));
            if (BackCommand is { } command && command.CanExecute(null)) command.Execute(null);
        };
        back = button;
        main.Children.Insert(0, back);
    }

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
}

/// <summary>PageHeader 里子元素的名字（代码组合的子元素）。</summary>
public static class PageHeaderParts
{
    /// <summary>面包屑（TS 的 propsBreadcrumbs）：Breadcrumbs。</summary>
    public const string Breadcrumbs = "PART_Breadcrumbs";

    /// <summary>标题行（返回、标题、操作，TS 的 propsMain）：Flex。</summary>
    public const string Main = "PART_Main";

    /// <summary>返回按钮（TS 的 propsBack）：IconButton 或 IconLinkButton。</summary>
    public const string Back = "PART_Back";

    /// <summary>标题与说明（TS 的 propsHeading）：Flex。</summary>
    public const string Heading = "PART_Heading";

    /// <summary>标题与标记那一行（TS 的 propsTitleRow）：Flex。</summary>
    public const string TitleRow = "PART_TitleRow";

    /// <summary>标题（TS 的 propsTitle）：H1–H3。</summary>
    public const string Title = "PART_Title";

    /// <summary>标记（TS 的 propsMeta）：ContentControl。</summary>
    public const string Meta = "PART_Meta";

    /// <summary>说明（TS 的 propsDescription）：Paragraph。</summary>
    public const string Description = "PART_Description";

    /// <summary>操作（TS 的 propsActions）：ContentControl。</summary>
    public const string Actions = "PART_Actions";
}
