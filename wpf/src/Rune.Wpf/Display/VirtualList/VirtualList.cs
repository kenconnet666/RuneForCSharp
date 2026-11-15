using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>VirtualList 跟着谁滚动（vue-tsx-pro 的 VirtualListScrolls）。</summary>
public enum VirtualListScroll
{
    /// <summary>自己是滚动区域（默认）：要给它高度（横向时给宽度）。</summary>
    Container,

    /// <summary>
    /// 随外层（页面的 ScrollViewer）滚动：按总高度撑开，放在外层的普通排版里。WPF 的虚拟化面板只在自己是滚动的那一层时虚拟化，
    /// 这时会建出全部项：数据多时用 Container。
    /// </summary>
    Window,
}

/// <summary>
/// 虚拟列表（vue-tsx-pro 的 VirtualList）：只建可见范围内的项，上万条数据也流畅；行高可以不固定（平台的虚拟化面板按实际大小排）。
/// <code>
/// &lt;rune:VirtualList ItemsSource="{Binding Users}" Height="352" ItemTemplate="{StaticResource UserRow}" AutomationProperties.Name="联系人" /&gt;
/// </code>
/// <list type="bullet">
/// <item>默认自己是滚动区域，高度由使用方给；<see cref="Scroll"/> = Window 时随外层滚动。<see cref="Orientation"/> = Horizontal 横向排列与滚动。</item>
/// <item>滚动：<see cref="ScrollToIndex"/>（按 <see cref="ScrollAlign"/> 对齐）、<see cref="ScrollToOffset"/>；可见范围变了发 <see cref="RangeChanged"/>（接 <see cref="InfiniteList{T}.OnRangeChanged"/> 滚动加载）。</item>
/// <item>加载中只用骨架行（高 <see cref="EstimateSize"/>）：没有数据时铺 5 行，已有数据（加载更多）时 2 行接在末尾；没有数据也不在加载时显示 <see cref="Empty"/>。</item>
/// </list>
/// 读屏：有数据时是列表（名称写 AutomationProperties.Name），每项带同级位置与总数（没建的项也算在总数里）；没有数据时是组。部件样式键：<see cref="VirtualListParts"/>。
/// </summary>
public class VirtualList : CompositeControl
{
    /// <summary>数据。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(VirtualList),
        new PropertyMetadata(null, (sender, e) => ((VirtualList)sender).OnItemsSourceChanged()));

    /// <summary>一项的数据模板（TS 的 children）。</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(VirtualList),
        new PropertyMetadata(null, (sender, e) => ((VirtualList)sender).items.ItemTemplate = (DataTemplate?)e.NewValue));

    /// <summary>排列方向，默认竖排。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(VirtualList),
        new PropertyMetadata(Orientation.Vertical, (sender, _) => ((VirtualList)sender).UpdateTemplate()));

    /// <summary>跟着谁滚动，默认自己。</summary>
    public static readonly DependencyProperty ScrollProperty = DependencyProperty.Register(nameof(Scroll), typeof(VirtualListScroll), typeof(VirtualList),
        new PropertyMetadata(VirtualListScroll.Container, (sender, _) => ((VirtualList)sender).UpdateTemplate()));

    /// <summary>每项的估计大小（骨架行按它的高度），默认 40。</summary>
    public static readonly DependencyProperty EstimateSizeProperty = DependencyProperty.Register(nameof(EstimateSize), typeof(double), typeof(VirtualList),
        new PropertyMetadata(40.0, (sender, _) => ((VirtualList)sender).UpdateSkeleton()));

    /// <summary>没有数据时显示的内容。</summary>
    public static readonly DependencyProperty EmptyProperty = DependencyProperty.Register(nameof(Empty), typeof(object), typeof(VirtualList),
        new PropertyMetadata(null, (sender, _) => ((VirtualList)sender).UpdateBody()));

    /// <summary>加载中：没有数据时铺几行骨架，已有数据（加载更多）时骨架接在末尾。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(VirtualList),
        new PropertyMetadata(false, (sender, e) => ((VirtualList)sender).loading.Update((bool)e.NewValue)));

    const int SkeletonRows = 5;
    const int MoreSkeletonRows = 2;

    readonly VirtualListItems items = new();
    readonly StackPanel skeleton = new() { Visibility = Visibility.Collapsed };
    readonly StackPanel more = new() { Visibility = Visibility.Collapsed };
    readonly ContentPresenter empty = new() { Visibility = Visibility.Collapsed };
    // 第一行是列表（与骨架、空状态叠在一起，占满给的高度：滚动区域才有确定的视口），第二行是加载更多的骨架
    readonly System.Windows.Controls.Grid root = new() { RowDefinitions = { new RowDefinition(), new RowDefinition { Height = GridLength.Auto } } };
    readonly DelayedLoading loading;
    ScrollViewer? outer;
    VisibleRange? range;

    /// <summary>新建：一列虚拟化的项。</summary>
    public VirtualList()
    {
        loading = new DelayedLoading(_ => UpdateBody());
        for (var row = 0; row < SkeletonRows; row++) skeleton.Children.Add(SkeletonRow(row));
        for (var row = 0; row < MoreSkeletonRows; row++) more.Children.Add(SkeletonRow(row));
        items.SetResourceReference(StyleProperty, VirtualListParts.List);
        UpdateTemplate();
        System.Windows.Controls.Grid.SetRow(more, 1);
        root.Children.Add(items);
        root.Children.Add(skeleton);
        root.Children.Add(empty);
        root.Children.Add(more);
        Root = root;
        AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => UpdateRange()));
        items.LayoutUpdated += (_, _) => UpdateRange();
        Loaded += (_, _) =>
        {
            UpdateSkeleton();
            UpdateTemplate();
            if (outer is not null) outer.ScrollChanged -= OnOuterScroll;
            outer = Scroll == VirtualListScroll.Window ? FindAncestor<ScrollViewer>(this) : null;
            if (outer is not null) outer.ScrollChanged += OnOuterScroll;
        };
        UpdateBody();
    }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>一项的数据模板。</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>排列方向。</summary>
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>跟着谁滚动。</summary>
    public VirtualListScroll Scroll { get => (VirtualListScroll)GetValue(ScrollProperty); set => SetValue(ScrollProperty, value); }

    /// <summary>每项的估计大小。</summary>
    public double EstimateSize { get => (double)GetValue(EstimateSizeProperty); set => SetValue(EstimateSizeProperty, value); }

    /// <summary>没有数据时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>可见范围（不含多建的项）变了：接近末尾时加载更多。</summary>
    public event EventHandler<VisibleRange>? RangeChanged;

    /// <summary>建出来的项（测试用）。</summary>
    internal IEnumerable<FrameworkElement> RealizedItems => Enumerable.Range(0, items.Items.Count)
        .Select(index => items.ItemContainerGenerator.ContainerFromIndex(index)).OfType<FrameworkElement>();

    /// <summary>骨架与空状态（测试用）。</summary>
    internal (StackPanel Skeleton, StackPanel More, ContentPresenter Empty) Placeholders => (skeleton, more, empty);

    /// <summary>滚动的那一层：自己的，或外层的（测试用）。</summary>
    internal ScrollViewer? Viewer => Scroll == VirtualListScroll.Container ? FindDescendant<ScrollViewer>(items) : outer ?? FindAncestor<ScrollViewer>(this);

    internal int Count => items.Items.Count;

    bool Horizontal => Orientation == Orientation.Horizontal;

    /// <summary>滚到第 index 项，按 align 对齐（默认已经完整可见就不动）。</summary>
    /// <param name="index">序号。</param>
    /// <param name="align">停在视口的什么位置。</param>
    public void ScrollToIndex(int index, ScrollAlign align = ScrollAlign.Auto)
    {
        if (index < 0 || index >= Count || Viewer is not { } viewer) return;
        // 先让虚拟化的面板把它建出来，再按它的实际位置对齐
        FindDescendant<VirtualizingStackPanel>(items)?.BringIndexIntoViewPublic(index);
        UpdateLayout();
        if (items.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container) return;
        var at = container.TransformToAncestor(viewer).Transform(default);
        var offset = Horizontal ? viewer.HorizontalOffset : viewer.VerticalOffset;
        var start = (Horizontal ? at.X : at.Y) + offset;
        var size = Horizontal ? container.ActualWidth : container.ActualHeight;
        var target = VirtualScroll.OffsetFor(start, size, offset, Horizontal ? viewer.ViewportWidth : viewer.ViewportHeight, Horizontal ? viewer.ExtentWidth : viewer.ExtentHeight, align);
        if (Horizontal) viewer.ScrollToHorizontalOffset(target);
        else viewer.ScrollToVerticalOffset(target);
        viewer.UpdateLayout();
    }

    /// <summary>滚到相对列表开头的位置。</summary>
    /// <param name="offset">位置（设备无关像素）。</param>
    public void ScrollToOffset(double offset)
    {
        if (Viewer is not { } viewer) return;
        // 随外层滚动时，列表开头离外层内容开头还有一段
        var start = Scroll == VirtualListScroll.Window ? (Horizontal ? items.TransformToAncestor(viewer).Transform(default).X + viewer.HorizontalOffset : items.TransformToAncestor(viewer).Transform(default).Y + viewer.VerticalOffset) : 0;
        if (Horizontal) viewer.ScrollToHorizontalOffset(start + offset);
        else viewer.ScrollToVerticalOffset(start + offset);
        viewer.UpdateLayout();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new VirtualListPeer(this);

    void OnOuterScroll(object? sender, ScrollChangedEventArgs e) => UpdateRange();

    void OnItemsSourceChanged()
    {
        items.ItemsSource = ItemsSource;
        UpdateBody();
    }

    /// <summary>方向与滚动方式：面板跟着方向；自己滚动时模板里是滚动区域，随外层滚动时只有项。</summary>
    void UpdateTemplate()
    {
        items.Container = Scroll == VirtualListScroll.Container;
        items.Horizontal = Horizontal;
        items.Rebuild();
    }

    /// <summary>有数据时是列表；没有时加载中是骨架，否则是空状态；加载更多时骨架接在末尾。</summary>
    void UpdateBody()
    {
        var any = ItemsSource?.GetEnumerator().MoveNext() == true;
        items.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        skeleton.Visibility = !any && loading.Shown ? Visibility.Visible : Visibility.Collapsed;
        more.Visibility = any && loading.Shown ? Visibility.Visible : Visibility.Collapsed;
        empty.Visibility = !any && !IsLoading ? Visibility.Visible : Visibility.Collapsed;
        empty.Content = Empty;
        items.SizeOfSet = Count;
    }

    /// <summary>骨架行：上下各留 xs，比一项略矮（高 max(8, 估计大小 − 8)）。</summary>
    static Border SkeletonRow(int row) => new() { Child = new Skeleton { Delay = row * 100 } };

    void UpdateSkeleton()
    {
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * FontSize;
        foreach (var row in skeleton.Children.Cast<Border>().Concat(more.Children.Cast<Border>()))
        {
            row.Padding = new Thickness(0, xs, 0, xs);
            ((FrameworkElement)row.Child).Height = Math.Max(8, EstimateSize - 8);
        }
    }

    /// <summary>按建出来的项与视口算可见范围，变了就报告。</summary>
    void UpdateRange()
    {
        if (Viewer is not { } viewer || Count == 0 || !viewer.IsLoaded) return;
        var viewport = Horizontal ? viewer.ViewportWidth : viewer.ViewportHeight;
        var shown = new List<int>();
        for (var index = 0; index < Count; index++)
        {
            if (items.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement { IsVisible: true } container || !container.IsDescendantOf(viewer)) continue;
            var at = container.TransformToAncestor(viewer).Transform(default);
            var start = Horizontal ? at.X : at.Y;
            var end = start + (Horizontal ? container.ActualWidth : container.ActualHeight);
            if (end > 0 && start < viewport) shown.Add(index);
        }
        if (shown.Count == 0) return;
        var next = new VisibleRange(shown.Min(), shown.Max());
        if (next == range) return;
        range = next;
        RangeChanged?.Invoke(this, next);
    }

    internal static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T found) return found;
            if (FindDescendant<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        for (var at = VisualTreeHelper.GetParent(child); at is not null; at = VisualTreeHelper.GetParent(at))
            if (at is T found) return found;
        return null;
    }

    sealed class VirtualListPeer(VirtualList owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => owner.Count > 0 ? AutomationControlType.List : AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(VirtualList);
    }
}

/// <summary>VirtualList 的项列表：模板与面板按方向、滚动方式在代码里换（样式键 <see cref="VirtualListParts.List"/> 管其余外观）；每项的外层是读屏的列表项。</summary>
sealed class VirtualListItems : ItemsControl
{
    public bool Container { get; set; } = true;

    public bool Horizontal { get; set; }

    public int SizeOfSet
    {
        get;
        set
        {
            field = value;
            for (var index = 0; index < Items.Count; index++)
                if (ItemContainerGenerator.ContainerFromIndex(index) is DependencyObject container) AutomationProperties.SetSizeOfSet(container, value);
        }
    }

    /// <summary>换面板与模板：自己滚动时模板是滚动区域（浮层滚动条、可以用键盘滚动），随外层滚动时只有 ItemsPresenter。</summary>
    public void Rebuild()
    {
        var panel = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
        panel.SetValue(VirtualizingStackPanel.OrientationProperty, Horizontal ? Orientation.Horizontal : Orientation.Vertical);
        ItemsPanel = new ItemsPanelTemplate(panel);
        var presenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        FrameworkElementFactory tree = presenter;
        if (Container)
        {
            tree = new FrameworkElementFactory(typeof(ScrollArea));
            tree.SetValue(ScrollViewer.CanContentScrollProperty, true);
            tree.SetValue(ScrollArea.AxisProperty, Horizontal ? ScrollAreaAxis.X : ScrollAreaAxis.Y);
            tree.AppendChild(presenter);
        }
        Template = new ControlTemplate(typeof(ItemsControl)) { VisualTree = tree };
    }

    protected override DependencyObject GetContainerForItemOverride() => new VirtualListItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is VirtualListItem;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        AutomationProperties.SetPositionInSet(element, ItemContainerGenerator.IndexFromContainer(element) + 1);
        AutomationProperties.SetSizeOfSet(element, Items.Count);
    }

    // 列表层只排版（上游的 role="presentation"）：读屏里各项直接挂在 VirtualList 下
    protected override AutomationPeer OnCreateAutomationPeer() => new PresentationPeer(this);

    sealed class PresentationPeer(VirtualListItems owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>VirtualList 的一项（上游的 role="listitem"）。</summary>
sealed class VirtualListItem : ContentPresenter
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ItemPeer(this);

    sealed class ItemPeer(VirtualListItem owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => "VirtualListItem";
    }
}

/// <summary>VirtualList 的部件样式键（DESIGN §7）。</summary>
public static class VirtualListParts
{
    /// <summary>项的列表（ItemsControl：虚拟化的设置；模板与面板由 VirtualList 按方向、滚动方式换）。</summary>
    public static readonly PartKey List = new(typeof(VirtualList), nameof(List), typeof(ItemsControl));
}
