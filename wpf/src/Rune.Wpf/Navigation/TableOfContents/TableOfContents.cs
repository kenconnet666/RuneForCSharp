using System.Collections;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 本页目录（vue-tsx-pro 的 TableOfContents）：列出页面的各节标题，滚动时高亮正在读的一节，侧边的指示条滑过去。
/// <code>
/// &lt;rune:TableOfContents ScrollRoot="{Binding ElementName=Article}" Items="{Binding Headings}" /&gt;
/// </code>
/// 目录项是 <see cref="HeadingEntry"/>（标题的名字、文字与级别，2 级起每深一级多缩进一档）；标题按名字（Name）在 <see cref="ScrollRoot"/> 的内容里找。
/// 点目录项把那一节滚到顶上（TS 的页内锚点）。当前节 = 顶部已经越过视口顶边的最后一节；滚到底时最后几节到不了顶边，取视口里的最后一节。
/// 当前节的链接 accentText，读屏的项状态是"当前位置"（TS 的 aria-current="location"）。标题默认取语言包（本页目录），同时是导航的名称。
/// 部件样式键：<see cref="TableOfContentsParts"/>。
/// </summary>
public class TableOfContents : FrameworkElement
{
    /// <summary>目录项。</summary>
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IReadOnlyList<HeadingEntry>), typeof(TableOfContents),
        new FrameworkPropertyMetadata(null, (d, _) => ((TableOfContents)d).Rebuild()));

    /// <summary>目录的标题；不写时取语言包。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(TableOfContents),
        new FrameworkPropertyMetadata(null, (d, _) => ((TableOfContents)d).UpdateTitle()));

    /// <summary>页面在哪个滚动区域里（ScrollViewer、ScrollArea）。</summary>
    public static readonly DependencyProperty ScrollRootProperty = DependencyProperty.Register(nameof(ScrollRoot), typeof(ScrollViewer), typeof(TableOfContents),
        new FrameworkPropertyMetadata(null, (d, e) => ((TableOfContents)d).Watch((ScrollViewer?)e.OldValue, (ScrollViewer?)e.NewValue)));

    static readonly DependencyPropertyKey CurrentKey = DependencyProperty.RegisterReadOnly(nameof(Current), typeof(string), typeof(TableOfContents), new PropertyMetadata(null));

    /// <summary>当前节的名字；还没滚到第一节时为空。</summary>
    public static readonly DependencyProperty CurrentProperty = CurrentKey.DependencyProperty;

    readonly StackPanel root = new();
    readonly Text title = new();
    readonly Border list = new();
    readonly System.Windows.Controls.Grid listContent = new();
    readonly StackPanel links = new();
    readonly Border indicator = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed };
    readonly TranslateTransform shift = new();
    bool placed;

    /// <summary>新建：标题、左边一条细线的列表、指示条。</summary>
    public TableOfContents()
    {
        title.SetResourceReference(StyleProperty, TableOfContentsParts.Title);
        list.SetResourceReference(StyleProperty, TableOfContentsParts.List);
        indicator.SetResourceReference(StyleProperty, TableOfContentsParts.Indicator);
        indicator.RenderTransform = shift;
        listContent.Children.Add(links);
        listContent.Children.Add(indicator);
        list.Child = listContent;
        root.Children.Add(title);
        root.Children.Add(list);
        AddLogicalChild(root);
        AddVisualChild(root);
        Loaded += (_, _) =>
        {
            UpdateTitle();
            Dispatcher.BeginInvoke(Spy, DispatcherPriority.Loaded);
        };
    }

    /// <summary>目录项。</summary>
    public IReadOnlyList<HeadingEntry>? Items { get => (IReadOnlyList<HeadingEntry>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>目录的标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>滚动区域。</summary>
    public ScrollViewer? ScrollRoot { get => (ScrollViewer?)GetValue(ScrollRootProperty); set => SetValue(ScrollRootProperty, value); }

    /// <summary>当前节的名字。</summary>
    public string? Current => (string?)GetValue(CurrentProperty);

    /// <summary>目录链接（测试用）。</summary>
    internal IEnumerable<TableOfContentsLink> Links => links.Children.OfType<TableOfContentsLink>();

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 标题下面留 space.sm（按标题自己的字号）
        title.Margin = new Thickness(0, 0, 0, 0.5 * title.FontSize);
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        PlaceIndicator();
        return finalSize;
    }

    void UpdateTitle() => title.Text = Title ?? Provider.GetLang(this).TableOfContents;

    void Rebuild()
    {
        links.Children.Clear();
        foreach (var item in Items ?? []) links.Children.Add(new TableOfContentsLink(this, item));
        Spy();
    }

    /// <summary>滚动之后重算（滚动区域的范围、视口变了也算在内）：换了滚动区域就换绑。</summary>
    void Watch(ScrollViewer? old, ScrollViewer? scroll)
    {
        if (old is not null) old.ScrollChanged -= OnScrollChanged;
        if (scroll is not null) scroll.ScrollChanged += OnScrollChanged;
        Spy();
    }

    void OnScrollChanged(object sender, ScrollChangedEventArgs e) => Spy();

    /// <summary>标题：按名字在滚动区域的内容里找。</summary>
    FrameworkElement? Heading(string id) => ScrollRoot?.Content is DependencyObject content ? Find(content, id) : null;

    static FrameworkElement? Find(DependencyObject parent, string id)
    {
        if (parent is FrameworkElement { Name: var name } element && name == id) return element;
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            if (Find(child, id) is { } found) return found;
        return null;
    }

    /// <summary>
    /// 滚动高亮（TS 的 useScrollSpy）：顶部已经越过视口顶边（+1 容许取整误差）的最后一节；滚到底时最后几节可能永远到不了顶边，取视口里的最后一节。
    /// </summary>
    internal void Spy()
    {
        if (ScrollRoot is not { } scroll || Items is null || !scroll.IsLoaded) return;
        var atBottom = scroll.VerticalOffset + scroll.ViewportHeight >= scroll.ExtentHeight - 1;
        string? next = null;
        foreach (var item in Items)
        {
            if (Heading(item.Id) is not { IsLoaded: true } heading || !scroll.IsAncestorOf(heading)) continue;
            var top = heading.TranslatePoint(default, scroll).Y;
            if (top <= 1 || (atBottom && top < scroll.ViewportHeight)) next = item.Id;
        }
        if (next == Current) return;
        SetValue(CurrentKey, next);
        foreach (var link in Links) link.SetCurrent(link.Entry.Id == next);
        PlaceIndicator();
    }

    /// <summary>点了目录项：那一节滚到视口顶上。</summary>
    internal void Go(HeadingEntry entry)
    {
        if (ScrollRoot is not { } scroll || Heading(entry.Id) is not { } heading || !scroll.IsAncestorOf(heading)) return;
        scroll.ScrollToVerticalOffset(scroll.VerticalOffset + heading.TranslatePoint(default, scroll).Y);
    }

    /// <summary>指示条对准当前节的链接：第一次直接到位，之后滑过去（durations.md、standard）。</summary>
    void PlaceIndicator()
    {
        var link = Links.FirstOrDefault(item => item.Entry.Id == Current);
        indicator.Visibility = link is null ? Visibility.Collapsed : Visibility.Visible;
        if (link is null || link.ActualHeight <= 0) return;
        var (y, height) = (link.TranslatePoint(default, listContent).Y, link.ActualHeight);
        var duration = placed ? Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200)) : TimeSpan.Zero;
        placed = true;
        if (duration <= TimeSpan.Zero)
        {
            shift.BeginAnimation(TranslateTransform.YProperty, null);
            indicator.BeginAnimation(HeightProperty, null);
            shift.Y = y;
            indicator.Height = height;
            return;
        }
        var easing = TryFindResource("Rune.Easing.Standard") as IEasingFunction;
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, duration) { EasingFunction = easing });
        indicator.BeginAnimation(HeightProperty, new DoubleAnimation(height, duration) { EasingFunction = easing });
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TocPeer(this);

    sealed class TocPeer(TableOfContents owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(TableOfContents);

        // WPF 的读屏节点没有地标类型：本地化的控件类型写"导航"
        protected override string GetLocalizedControlTypeCore() => "导航";

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.title.Text;
    }
}

/// <summary>目录里的一项：标题的名字（Name，TS 的 id）、文字与级别（2 是二级标题）。</summary>
/// <param name="Id">标题元素的 Name。</param>
/// <param name="Label">标题文字。</param>
/// <param name="Level">级别：2 起，每深一级多缩进一档。</param>
public sealed record HeadingEntry(string Id, string Label, int Level);

/// <summary>TableOfContents 部件的样式键（TableOfContents.xaml 里是默认外观）。</summary>
public static class TableOfContentsParts
{
    /// <summary>标题。</summary>
    public static readonly PartKey Title = new(typeof(TableOfContents), nameof(Title), typeof(Text));

    /// <summary>列表（左边一条细线）。</summary>
    public static readonly PartKey List = new(typeof(TableOfContents), nameof(List), typeof(Border));

    /// <summary>指示条。</summary>
    public static readonly PartKey Indicator = new(typeof(TableOfContents), nameof(Indicator), typeof(Border));

    /// <summary>目录链接（每项一个）。</summary>
    public static readonly PartKey Link = new(typeof(TableOfContents), nameof(Link), typeof(TableOfContentsLink));
}

/// <summary>
/// 目录链接（TS 的 a）：上下 space.xs、右边 space.xs，左边按级别缩进，xs 圆角，sm 字号、行高 snug；平时 textMuted，悬停 text，当前节 accentText（悬停也是）。
/// 键盘聚焦时焦点环。读屏里是链接。
/// </summary>
public sealed class TableOfContentsLink : ButtonBase
{
    static readonly DependencyPropertyKey IsCurrentKey = DependencyProperty.RegisterReadOnly(nameof(IsCurrent), typeof(bool), typeof(TableOfContentsLink), new PropertyMetadata(false));

    /// <summary>是不是当前节。</summary>
    public static readonly DependencyProperty IsCurrentProperty = IsCurrentKey.DependencyProperty;

    readonly TableOfContents owner;

    internal TableOfContentsLink(TableOfContents toc, HeadingEntry entry)
    {
        owner = toc;
        Entry = entry;
        Content = entry.Label;
        SetResourceReference(StyleProperty, TableOfContentsParts.Link);
    }

    /// <summary>对应的目录项。</summary>
    public HeadingEntry Entry { get; }

    /// <summary>是不是当前节。</summary>
    public bool IsCurrent => (bool)GetValue(IsCurrentProperty);

    internal void SetCurrent(bool value) => SetValue(IsCurrentKey, value);

    /// <summary>左边按级别缩进（2 级一档，往下每级多一档，每档 0.75em），其余三边 space.xs。</summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != TextElement.FontSizeProperty) return;
        var em = FontSize;
        Padding = new Thickness(Math.Max(Entry.Level - 1, 1) * 0.75 * em, 0.25 * em, 0.25 * em, 0.25 * em);
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (IsDisabled) return;
        base.OnClick();
        owner.Go(Entry);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LinkPeer(this);

    sealed class LinkPeer(TableOfContentsLink owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override string GetClassNameCore() => nameof(TableOfContentsLink);

        protected override string GetItemStatusCore() => owner.IsCurrent ? "当前位置" : base.GetItemStatusCore();
    }
}
