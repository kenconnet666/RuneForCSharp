using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace Rune.Avalonia;

/// <summary>
/// 本页目录（vue-tsx-pro 的 TableOfContents）：列出页面的各节标题，滚动时高亮正在读的一节，侧边的指示条滑过去。
/// <code>
/// &lt;rune:TableOfContents ScrollRoot="{Binding #Article}" Items="{Binding Headings}" /&gt;
/// </code>
/// 目录项是 <see cref="HeadingEntry"/>（标题的名字、文字与级别，2 级起每深一级多缩进一档）；标题按名字（Name）在 <see cref="ScrollRoot"/> 的内容里找。
/// 点目录项把那一节滚到顶上（TS 的页内锚点）。当前节 = 顶部已经越过视口顶边的最后一节；滚到底时最后几节到不了顶边，取视口里的最后一节。
/// 当前节的链接 accentText，读屏的项状态是"当前位置"（TS 的 aria-current="location"）。标题默认取语言包（本页目录），同时是导航的名称。
/// </summary>
public class TableOfContents : Control
{
    /// <summary>目录项。</summary>
    public static readonly StyledProperty<IReadOnlyList<HeadingEntry>?> ItemsProperty =
        AvaloniaProperty.Register<TableOfContents, IReadOnlyList<HeadingEntry>?>(nameof(Items));

    /// <summary>目录的标题；不写时取语言包。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<TableOfContents, string?>(nameof(Title));

    /// <summary>页面在哪个滚动区域里（ScrollViewer、ScrollArea）。</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollRootProperty = AvaloniaProperty.Register<TableOfContents, ScrollViewer?>(nameof(ScrollRoot));

    /// <summary>当前节的名字；还没滚到第一节时为空。</summary>
    public static readonly DirectProperty<TableOfContents, string?> CurrentProperty =
        AvaloniaProperty.RegisterDirect<TableOfContents, string?>(nameof(Current), toc => toc.Current);

    readonly StackPanel root = new();
    readonly TextBlock title = new() { Name = TableOfContentsParts.Title };
    readonly Border list = new() { Name = TableOfContentsParts.List };
    readonly Panel listContent = new();
    readonly StackPanel links = new();
    readonly Border indicator = new() { Name = TableOfContentsParts.Indicator, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsVisible = false };
    ScrollViewer? watched;
    bool placed;

    /// <summary>新建：标题、左边一条细线的列表、指示条。读屏里是导航地标。</summary>
    public TableOfContents()
    {
        listContent.Children.AddRange([links, indicator]);
        list.Child = listContent;
        root.Children.AddRange([title, list]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Navigation);
        AutomationProperties.SetLabeledBy(this, title);
    }

    /// <summary>目录项。</summary>
    public IReadOnlyList<HeadingEntry>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>目录的标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>滚动区域。</summary>
    public ScrollViewer? ScrollRoot { get => GetValue(ScrollRootProperty); set => SetValue(ScrollRootProperty, value); }

    /// <summary>当前节的名字。</summary>
    public string? Current { get; private set => SetAndRaise(CurrentProperty, ref field, value); }

    /// <summary>目录链接（测试用）。</summary>
    internal IEnumerable<TableOfContentsLink> Links => links.Children.OfType<TableOfContentsLink>();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty) Rebuild();
        else if (change.Property == TitleProperty) title.Text = Title ?? Provider.GetLang(this).TableOfContents;
        else if (change.Property == ScrollRootProperty) Watch(ScrollRoot);
        else if (change.Property == TextElement.FontSizeProperty) Indent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        title.Text = Title ?? Provider.GetLang(this).TableOfContents;
    }

    void Rebuild()
    {
        links.Children.Clear();
        foreach (var item in Items ?? []) links.Children.Add(new TableOfContentsLink(this, item));
        Indent();
        Spy();
    }

    /// <summary>按级别缩进：2 级贴着细线留一档，往下每级多一档（0.75em）。</summary>
    void Indent()
    {
        foreach (var link in Links) link.Indent();
    }

    /// <summary>滚动与排版之后都重算（TS 在滚动、窗口变化与挂载后量）：换了滚动区域就换绑。</summary>
    void Watch(ScrollViewer? scroll)
    {
        if (watched is not null)
        {
            watched.ScrollChanged -= OnScrollChanged;
            watched.LayoutUpdated -= OnLayoutUpdated;
        }
        watched = scroll;
        if (scroll is not null)
        {
            scroll.ScrollChanged += OnScrollChanged;
            scroll.LayoutUpdated += OnLayoutUpdated;
        }
        Spy();
    }

    void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => Spy();

    void OnLayoutUpdated(object? sender, EventArgs e) => Spy();

    /// <summary>标题：按名字在滚动区域的内容里找。</summary>
    Control? Heading(string id) => (ScrollRoot?.Content as Control)?.GetSelfAndLogicalDescendants().OfType<Control>().FirstOrDefault(control => control.Name == id);

    /// <summary>
    /// 滚动高亮（TS 的 useScrollSpy）：顶部已经越过视口顶边（+1 容许取整误差）的最后一节；滚到底时最后几节可能永远到不了顶边，取视口里的最后一节。
    /// </summary>
    internal void Spy()
    {
        if (ScrollRoot is not { } scroll || Items is null) return;
        var atBottom = scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - 1;
        string? next = null;
        foreach (var item in Items)
        {
            if (Heading(item.Id) is not { } heading || heading.TranslatePoint(default, scroll) is not { } at) continue;
            if (at.Y <= 1 || (atBottom && at.Y < scroll.Viewport.Height)) next = item.Id;
        }
        if (next == Current) return;
        Current = next;
        foreach (var link in Links) link.SetCurrent(link.Entry.Id == next);
        PlaceIndicator();
    }

    /// <summary>点了目录项：那一节滚到视口顶上。</summary>
    internal void Go(HeadingEntry entry)
    {
        if (ScrollRoot is not { } scroll || Heading(entry.Id) is not { } heading || heading.TranslatePoint(default, scroll) is not { } at) return;
        scroll.Offset = new Vector(scroll.Offset.X, scroll.Offset.Y + at.Y);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        PlaceIndicator();
        return size;
    }

    /// <summary>指示条对准当前节的链接：第一次直接到位，之后滑过去（durations.md、standard）。</summary>
    void PlaceIndicator()
    {
        var link = Links.FirstOrDefault(item => item.Entry.Id == Current);
        indicator.IsVisible = link is not null;
        if (link is null || link.Bounds.Height <= 0) return;
        if (placed && indicator.Transitions is null)
        {
            var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
            var easing = this.Resource<global::Avalonia.Animation.Easings.Easing>(ThemeKeys.Easing.Standard);
            indicator.Transitions =
            [
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = duration, Easing = easing },
                new DoubleTransition { Property = HeightProperty, Duration = duration, Easing = easing },
            ];
        }
        indicator.Height = link.Bounds.Height;
        indicator.RenderTransform = TransformOperations.Parse(FormattableString.Invariant($"translate(0px, {link.Bounds.Y}px)"));
        placed = true;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TocPeer(this);

    sealed class TocPeer(TableOfContents owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(TableOfContents);

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.title.Text;
    }
}

/// <summary>目录里的一项：标题的名字（Name，TS 的 id）、文字与级别（2 是二级标题）。</summary>
/// <param name="Id">标题控件的 Name。</param>
/// <param name="Label">标题文字。</param>
/// <param name="Level">级别：2 起，每深一级多缩进一档。</param>
public sealed record HeadingEntry(string Id, string Label, int Level);

/// <summary>TableOfContents 里的部件名。</summary>
public static class TableOfContentsParts
{
    /// <summary>标题（TextBlock）。</summary>
    public const string Title = "PART_Title";

    /// <summary>列表（Border：左边一条细线）。</summary>
    public const string List = "PART_List";

    /// <summary>指示条（Border）。</summary>
    public const string Indicator = "PART_Indicator";

    /// <summary>目录链接（rune:TableOfContentsLink，每项一个）。</summary>
    public const string Link = "PART_Link";
}

/// <summary>
/// 目录链接（TS 的 a）：上下 space.xs、右边 space.xs，左边按级别缩进，xs 圆角，sm 字号、行高 snug；平时 textMuted，悬停 text，当前节 accentText（悬停也是）。
/// 键盘聚焦时焦点环。读屏里是链接。
/// </summary>
public sealed class TableOfContentsLink : ButtonBase
{
    readonly TableOfContents owner;

    internal TableOfContentsLink(TableOfContents toc, HeadingEntry entry)
    {
        owner = toc;
        Entry = entry;
        Name = TableOfContentsParts.Link;
        Content = entry.Label;
    }

    /// <summary>对应的目录项。</summary>
    public HeadingEntry Entry { get; }

    /// <summary>是不是当前节。</summary>
    public bool IsCurrent { get; private set; }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TableOfContentsLink);

    internal void SetCurrent(bool value)
    {
        IsCurrent = value;
        PseudoClasses.Set(":current", value);
    }

    /// <summary>左边按级别缩进（2 级一档，往下每级多一档，每档 0.75em），其余三边 space.xs。</summary>
    internal void Indent()
    {
        var em = TextElement.GetFontSize(this);
        Padding = new Thickness(Math.Max(Entry.Level - 1, 1) * 0.75 * em, 0.25 * em, 0.25 * em, 0.25 * em);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextElement.FontSizeProperty) Indent();
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

    sealed class LinkPeer(TableOfContentsLink owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

        protected override string GetClassNameCore() => nameof(TableOfContentsLink);

        protected override string? GetItemStatusCore() => owner.IsCurrent ? "当前位置" : base.GetItemStatusCore();
    }
}
