using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>VirtualList 跟着谁滚动（vue-tsx-pro 的 VirtualListScrolls）。</summary>
public enum VirtualListScroll
{
    /// <summary>自己是滚动区域（默认）：要给它高度（横向时给宽度）。</summary>
    Container,

    /// <summary>随外层（页面的滚动区域）滚动：按总高度撑开，放在外层的普通排版里，仍只建看得见的项。</summary>
    Window,
}

/// <summary>
/// 虚拟列表（vue-tsx-pro 的 VirtualList）：只建可见范围内的项，上万条数据也流畅；行高可以不固定（平台的虚拟化面板按实际大小排）。
/// <code>
/// &lt;rune:VirtualList ItemsSource="{Binding Users}" Height="352" AutomationProperties.Name="联系人"&gt;
///     &lt;rune:VirtualList.ItemTemplate&gt;&lt;DataTemplate&gt;&lt;local:UserRow /&gt;&lt;/DataTemplate&gt;&lt;/rune:VirtualList.ItemTemplate&gt;
/// &lt;/rune:VirtualList&gt;
/// </code>
/// <list type="bullet">
/// <item>默认自己是滚动区域，高度由使用方给；<see cref="Scroll"/> = Window 时随外层滚动。<see cref="Orientation"/> = Horizontal 横向排列与滚动。</item>
/// <item>滚动：<see cref="ScrollToIndex"/>（按 <see cref="ScrollAlign"/> 对齐）、<see cref="ScrollToOffset"/>；可见范围变了发 <see cref="RangeChanged"/>（接 <see cref="InfiniteList{T}.OnRangeChanged"/> 滚动加载）。</item>
/// <item>加载中只用骨架行（高 <see cref="EstimateSize"/>）：没有数据时铺 5 行，已有数据（加载更多）时 2 行接在末尾；没有数据也不在加载时显示 <see cref="Empty"/>。</item>
/// </list>
/// 读屏：有数据时是列表（名称写 AutomationProperties.Name），每项带同级位置与总数（没建的项也算在总数里）；没有数据时是组（空状态、骨架不是列表项）。
/// </summary>
public class VirtualList : Control
{
    /// <summary>数据。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<VirtualList, IEnumerable?>(nameof(ItemsSource));

    /// <summary>一项的数据模板（TS 的 children）。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<VirtualList, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>排列方向，默认竖排。</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<VirtualList, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>跟着谁滚动，默认自己。</summary>
    public static readonly StyledProperty<VirtualListScroll> ScrollProperty = AvaloniaProperty.Register<VirtualList, VirtualListScroll>(nameof(Scroll));

    /// <summary>每项的估计大小（骨架行按它的高度），默认 40。</summary>
    public static readonly StyledProperty<double> EstimateSizeProperty = AvaloniaProperty.Register<VirtualList, double>(nameof(EstimateSize), 40);

    /// <summary>没有数据时显示的内容。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<VirtualList, object?>(nameof(Empty));

    /// <summary>加载中：没有数据时铺几行骨架，已有数据（加载更多）时骨架接在末尾。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<VirtualList, bool>(nameof(IsLoading));

    const int SkeletonRows = 5;
    const int MoreSkeletonRows = 2;

    readonly ScrollArea scroll = new() { Name = VirtualListParts.Scroll };
    readonly VirtualListItems items = new() { Name = VirtualListParts.List };
    readonly StackPanel skeleton = new() { Name = VirtualListParts.Skeleton, IsVisible = false };
    readonly StackPanel more = new() { IsVisible = false };
    readonly ContentPresenter empty = new() { Name = VirtualListParts.Empty, IsVisible = false };
    readonly StackPanel content = new();
    readonly DelayedLoading loading;
    VisibleRange? range;
    Control root;

    /// <summary>新建：滚动区域里一列虚拟化的项。</summary>
    public VirtualList()
    {
        loading = new DelayedLoading(_ => UpdateBody());
        for (var row = 0; row < SkeletonRows; row++) skeleton.Children.Add(SkeletonRow(row));
        for (var row = 0; row < MoreSkeletonRows; row++) more.Children.Add(SkeletonRow(row));
        content.Children.AddRange([items, more, skeleton, empty]);
        items.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel { Orientation = Orientation });
        items.ContainerPrepared += (_, e) => Position(e.Container, e.Index);
        items.ContainerIndexChanged += (_, e) => Position(e.Container, e.NewIndex);
        items.EffectiveViewportChanged += (_, _) => UpdateRange();
        items.LayoutUpdated += (_, _) => UpdateRange();
        root = content;
        Attach();
        UpdateBody();
    }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>一项的数据模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>排列方向。</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>跟着谁滚动。</summary>
    public VirtualListScroll Scroll { get => GetValue(ScrollProperty); set => SetValue(ScrollProperty, value); }

    /// <summary>每项的估计大小。</summary>
    public double EstimateSize { get => GetValue(EstimateSizeProperty); set => SetValue(EstimateSizeProperty, value); }

    /// <summary>没有数据时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>可见范围（不含多建的项）变了：接近末尾时加载更多。</summary>
    public event EventHandler<VisibleRange>? RangeChanged;

    /// <summary>建出来的项（测试用）。</summary>
    internal IEnumerable<Control> RealizedItems => items.GetRealizedContainers();

    /// <summary>骨架（测试用）：没有数据时的、加载更多时接在末尾的。</summary>
    internal (StackPanel Skeleton, StackPanel More, ContentPresenter Empty) Placeholders => (skeleton, more, empty);

    /// <summary>滚动区域（测试用）。</summary>
    internal ScrollArea ScrollPart => scroll;

    int Count => items.ItemCount;

    /// <summary>滚到第 index 项，按 align 对齐（默认已经完整可见就不动）。</summary>
    /// <param name="index">序号。</param>
    /// <param name="align">停在视口的什么位置。</param>
    public void ScrollToIndex(int index, ScrollAlign align = ScrollAlign.Auto)
    {
        if (index < 0 || index >= Count || Viewer() is not { } viewer) return;
        // 先让平台把它建出来（滚到看得见），再按它的实际位置对齐
        items.ScrollIntoView(index);
        items.UpdateLayout();
        if (items.ContainerFromIndex(index) is not { } container || container.TranslatePoint(default, viewer) is not { } at) return;
        var horizontal = Orientation == Orientation.Horizontal;
        var offset = horizontal ? viewer.Offset.X : viewer.Offset.Y;
        var start = (horizontal ? at.X : at.Y) + offset;
        var size = horizontal ? container.Bounds.Width : container.Bounds.Height;
        var target = VirtualScroll.OffsetFor(start, size, offset, horizontal ? viewer.Viewport.Width : viewer.Viewport.Height, horizontal ? viewer.Extent.Width : viewer.Extent.Height, align);
        viewer.Offset = horizontal ? viewer.Offset.WithX(target) : viewer.Offset.WithY(target);
    }

    /// <summary>滚到相对列表开头的位置。</summary>
    /// <param name="offset">位置（设备无关像素）。</param>
    public void ScrollToOffset(double offset)
    {
        if (Viewer() is not { } viewer) return;
        var start = items.TranslatePoint(default, viewer) is { } at ? (Orientation == Orientation.Horizontal ? at.X + viewer.Offset.X : at.Y + viewer.Offset.Y) : 0;
        viewer.Offset = Orientation == Orientation.Horizontal ? viewer.Offset.WithX(start + offset) : viewer.Offset.WithY(start + offset);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            items.ItemsSource = ItemsSource;
            UpdateBody();
        }
        else if (change.Property == ItemTemplateProperty) items.ItemTemplate = ItemTemplate;
        else if (change.Property == IsLoadingProperty) loading.Update(IsLoading);
        else if (change.Property == EmptyProperty) UpdateBody();
        else if (change.Property == EstimateSizeProperty) UpdateSkeleton();
        else if (change.Property == OrientationProperty)
        {
            items.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel { Orientation = Orientation });
            content.Orientation = Orientation;
            scroll.Axis = Orientation == Orientation.Horizontal ? ScrollAreaAxis.X : ScrollAreaAxis.Y;
        }
        else if (change.Property == ScrollProperty)
        {
            Detach();
            Attach();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(global::Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        loading.Update(IsLoading);
        UpdateSkeleton();
        UpdateBody();
    }

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
    protected override AutomationPeer OnCreateAutomationPeer() => new VirtualListPeer(this);

    /// <summary>容器模式：内容放进自己的滚动区域；随外层滚动时内容直接放在这里。</summary>
    void Attach()
    {
        root = Scroll == VirtualListScroll.Container ? scroll : content;
        if (root == scroll) scroll.Content = content;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        InvalidateMeasure();
    }

    void Detach()
    {
        LogicalChildren.Remove(root);
        VisualChildren.Remove(root);
        scroll.Content = null;
    }

    /// <summary>滚动的那一层：自己的滚动区域，或外层的。</summary>
    ScrollViewer? Viewer() => Scroll == VirtualListScroll.Container ? scroll : this.FindAncestorOfType<ScrollViewer>();

    /// <summary>每项的同级位置与总数（读屏念“第几项，共几项”）。</summary>
    void Position(Control container, int index)
    {
        AutomationProperties.SetPositionInSet(container, index + 1);
        AutomationProperties.SetSizeOfSet(container, Count);
    }

    /// <summary>有数据时是列表；没有时加载中是骨架，否则是空状态；加载更多时骨架接在末尾。</summary>
    void UpdateBody()
    {
        var any = ItemsSource?.GetEnumerator().MoveNext() == true;
        items.IsVisible = any;
        skeleton.IsVisible = !any && loading.Shown;
        more.IsVisible = any && loading.Shown;
        empty.IsVisible = !any && !IsLoading;
        empty.Content = Empty;
        foreach (var container in items.GetRealizedContainers()) AutomationProperties.SetSizeOfSet(container, Count);
    }

    /// <summary>骨架行：上下各留 xs，比一项略矮（高 max(8, 估计大小 − 8)）。</summary>
    Control SkeletonRow(int row) => new Border { Child = new Skeleton { Delay = row * 100 } };

    void UpdateSkeleton()
    {
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * this.Resource<double>(ThemeKeys.FontSize.Md);
        foreach (var row in skeleton.Children.Concat(more.Children).OfType<Border>())
        {
            row.Padding = new Thickness(0, xs);
            row.Child!.Height = Math.Max(8, EstimateSize - 8);
        }
    }

    /// <summary>按建出来的项与视口算可见范围，变了就报告。</summary>
    void UpdateRange()
    {
        if (Viewer() is not { } viewer || Count == 0) return;
        var horizontal = Orientation == Orientation.Horizontal;
        var viewport = horizontal ? viewer.Viewport.Width : viewer.Viewport.Height;
        var shown = items.GetRealizedContainers()
            .Select(container => (Index: items.IndexFromContainer(container), At: container.TranslatePoint(default, viewer), container.Bounds))
            .Where(item => item.Index >= 0 && item.At is not null && item.Bounds.Width > 0)
            .Where(item =>
            {
                var start = horizontal ? item.At!.Value.X : item.At!.Value.Y;
                var end = start + (horizontal ? item.Bounds.Width : item.Bounds.Height);
                return end > 0 && start < viewport;
            })
            .Select(item => item.Index)
            .ToList();
        if (shown.Count == 0) return;
        var next = new VisibleRange(shown.Min(), shown.Max());
        if (next == range) return;
        range = next;
        RangeChanged?.Invoke(this, next);
    }

    sealed class VirtualListPeer(VirtualList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.Count > 0 ? AutomationControlType.List : AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>VirtualList 的项列表：模板只要一个 ItemsPresenter（主题里没有 ItemsControl 的外观）；每项的外层是读屏的列表项。</summary>
sealed class VirtualListItems : ItemsControl
{
    public VirtualListItems() =>
        Template = new FuncControlTemplate<ItemsControl>((owner, _) => new ItemsPresenter { Name = "PART_ItemsPresenter", [!ItemsPresenter.ItemsPanelProperty] = owner[!ItemsPanelProperty] });

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new VirtualListItem();
}

/// <summary>VirtualList 的一项（上游的 role="listitem"）。</summary>
sealed class VirtualListItem : ContentPresenter
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ItemPeer(this);

    sealed class ItemPeer(VirtualListItem owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>VirtualList 的部件名（上游的 data-part）。</summary>
public static class VirtualListParts
{
    /// <summary>滚动区域（容器模式）。</summary>
    public const string Scroll = "PART_Scroll";

    /// <summary>项的列表。</summary>
    public const string List = "PART_List";

    /// <summary>没有数据时的骨架。</summary>
    public const string Skeleton = "PART_Skeleton";

    /// <summary>空状态。</summary>
    public const string Empty = "PART_Empty";
}
