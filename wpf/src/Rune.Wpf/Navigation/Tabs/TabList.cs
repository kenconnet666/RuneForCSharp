using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 一排标签（role="tablist"）：line 时整行底边一道 borderMuted 的线（竖排时列表的结束边）、当前标签下方 2 像素的品牌色指示条（切换时滑过去，durations.md、standard）；
/// soft 时 fill 底、xs 内边距、md 圆角，宽度按标签。方向键在标签之间移动（漫游焦点），到两端后从另一端继续（<see cref="Loop"/>）。
/// 名称写 <c>AutomationProperties.Name</c>。
/// 横排时标签放在一个标签栏里：放不下时列表横向滚动（滚动条藏起来），两头出现滚动按钮（只给指针用），还能滚的那头边缘 1.5em 渐隐；
/// 当前标签滚到外面时滚进来。栏的结束端依次是"全部标签页"菜单（<see cref="ShowAllMenu"/>）与新建按钮（订阅了 <see cref="AddRequested"/> 才有）。
/// 订阅了 <see cref="Reordered"/> 才能排序：拖动标签越过别的标签的中线就换位，标签上按 Alt + ←→ 往前、往后挪一位并告诉读屏新位置；
/// TabList 自己挪动标签，事件里是新的顺序（各标签的值）。竖排时只有列表（没有标签栏）。
/// </summary>
[ContentProperty(nameof(Children))]
public class TabList : FrameworkElement
{
    /// <summary>到两端后是否从另一端继续，默认 true。</summary>
    public static readonly DependencyProperty LoopProperty = DependencyProperty.Register(nameof(Loop), typeof(bool), typeof(TabList),
        new FrameworkPropertyMetadata(true, (d, _) =>
        {
            var list = (TabList)d;
            list.focus.Loop = list.Loop;
        }));

    /// <summary>标签栏结束端"全部标签页"的菜单（标签多、放不下时一眼找到并切过去）。</summary>
    public static readonly DependencyProperty ShowAllMenuProperty = DependencyProperty.Register(nameof(ShowAllMenu), typeof(bool), typeof(TabList),
        new FrameworkPropertyMetadata(false, (d, _) => ((TabList)d).SyncBar()));

    static readonly DependencyProperty IndicatorStartProperty = DependencyProperty.Register("IndicatorStart", typeof(double), typeof(TabList),
        new FrameworkPropertyMetadata(0d, (d, _) => ((TabList)d).layer.InvalidateArrange()));

    static readonly DependencyProperty IndicatorLengthProperty = DependencyProperty.Register("IndicatorLength", typeof(double), typeof(TabList),
        new FrameworkPropertyMetadata(0d, (d, _) => ((TabList)d).layer.InvalidateArrange()));

    /// <summary>拖动越过这么远（像素）才算拖动，不然是点击。</summary>
    const double DragThreshold = 4;

    readonly Strip strip;
    readonly Border indicator = new() { Name = TabListParts.Indicator, IsHitTestVisible = false, CornerRadius = new CornerRadius(1) };
    readonly Layer layer;
    readonly Border box = new();
    readonly ListScroller scroller = new() { Axis = ScrollAreaAxis.X, IsViewportFocusable = false };
    readonly IconButton previous;
    readonly IconButton next;
    readonly LiveRegion announcer = new();
    readonly Bar bar;
    readonly Border frame = new() { Name = TabListParts.Bar };
    readonly RovingFocus focus;
    Menu? allMenu;
    MenuRadioGroup? allGroup;
    IconButton? add;
    EventHandler? addRequested;
    Tabs? owner;
    string? shownValue;
    bool placed;
    bool reveal;
    bool moving;
    List<Tab> shown = [];
    (bool Overflowing, bool Start, bool End, double Width) overflow;
    Tab? pressed;
    Point pressedAt;
    bool dragging;

    /// <summary>新建：标签栏（滚动按钮、滚动的列表、菜单、新建按钮）与列表里的指示条。</summary>
    public TabList()
    {
        strip = new Strip(this) { Align = FlexAlign.Stretch };
        layer = new Layer(this);
        layer.Children.Add(strip);
        layer.Children.Add(indicator);
        box.Child = layer;
        scroller.Content = box;
        previous = ScrollButton(Lucide.ChevronLeft, -1);
        next = ScrollButton(Lucide.ChevronRight, 1);
        bar = new Bar(this);
        frame.Child = bar;
        AddLogicalChild(frame);
        AddVisualChild(frame);
        SyncBar();

        indicator.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Accent");
        frame.SetResourceReference(Border.BorderBrushProperty, "Rune.Brush.BorderMuted");
        box.SetResourceReference(Border.BorderBrushProperty, "Rune.Brush.BorderMuted");
        focus = new RovingFocus(strip, element => element is Tab);
        // 方向键移动了焦点之后（漫游焦点先处理）：自动切换时切到新聚焦的标签
        strip.AddHandler(KeyDownEvent, new KeyEventHandler((_, _) =>
        {
            if (owner?.Activation == TabsActivation.Automatic && Keyboard.FocusedElement is Tab { IsDisabled: false, Value: { } value } tab && Tabs.Owner(tab) == owner)
                owner.Select(value);
        }), handledEventsToo: true);
        strip.PreviewKeyDown += (_, e) =>
        {
            if (e.OriginalSource is Tab tab && OnReorderKey(tab, e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers)) e.Handled = true;
        };
        strip.PreviewMouseLeftButtonDown += OnStripPressed;
        strip.AddHandler(MouseMoveEvent, new MouseEventHandler(OnStripMoved), handledEventsToo: true);
        strip.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) => EndDrag()), handledEventsToo: true);
        strip.LostMouseCapture += (_, e) =>
        {
            if (e.OriginalSource == strip) EndDrag();
        };
        scroller.ScrollChanged += (_, _) => UpdateOverflow();
        LayoutUpdated += (_, _) =>
        {
            PlaceIndicator();
            RevealSelected();
            UpdateOverflow();
        };
        Loaded += (_, _) => Tabs.Owner(this)?.Sync();
    }

    /// <summary>标签（Tab）。</summary>
    public UIElementCollection Children => strip.Children;

    /// <summary>到两端后是否从另一端继续。</summary>
    public bool Loop { get => (bool)GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>"全部标签页"菜单。</summary>
    public bool ShowAllMenu { get => (bool)GetValue(ShowAllMenuProperty); set => SetValue(ShowAllMenuProperty, value); }

    /// <summary>点了新建按钮（TS 的 onAdd）：使用方加一个标签。订阅了才有新建按钮。</summary>
    public event EventHandler? AddRequested
    {
        add
        {
            addRequested += value;
            SyncBar();
        }
        remove
        {
            addRequested -= value;
            SyncBar();
        }
    }

    /// <summary>拖动或 Alt + ←→ 改了顺序（TS 的 onReorder）：标签已经挪好，参数是新的顺序。订阅了才能排序。</summary>
    public event EventHandler<TabsReorderedEventArgs>? Reordered;

    /// <summary>横排（标签栏只在横排时有）。</summary>
    bool Horizontal => owner?.Orientation != TabsOrientation.Vertical;

    bool Line => owner?.Variant != TabsVariant.Soft;

    /// <summary>列表里的标签，按显示的顺序。</summary>
    List<Tab> TabItems() => [.. strip.Children.OfType<Tab>()];

    /// <summary>读屏念的那段文字（测试用）。</summary>
    internal string Announcement => announcer.Text;

    /// <summary>"全部标签页"菜单（测试用）。</summary>
    internal Menu? AllMenu => allMenu;

    /// <summary>新建按钮（测试用）。</summary>
    internal IconButton? AddButton => add;

    /// <summary>两头的滚动按钮（测试用）。</summary>
    internal (IconButton Previous, IconButton Next) ScrollButtons => (previous, next);

    /// <summary>滚动区域（测试用）。</summary>
    internal ScrollArea Scroller => scroller;

    /// <summary>标签排开的方向（测试用）。</summary>
    internal FlexDirection Direction => strip.Direction;

    /// <summary>元素所在的标签列表（逻辑树上最近的一个）。</summary>
    internal static TabList? Of(DependencyObject element)
    {
        for (var parent = LogicalTreeHelper.GetParent(element); parent is not null; parent = LogicalTreeHelper.GetParent(parent))
            if (parent is TabList list) return list;
        return null;
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { frame }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => frame;

    /// <summary>跟着 Tabs 的样式、方向与选中值：分隔线、底色、方向键的方向；选中值变了把当前标签滚进来。</summary>
    internal void Apply(Tabs tabs)
    {
        owner = tabs;
        var vertical = !Horizontal;
        var line = Line;
        strip.Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        strip.Gap = line && !vertical ? Space.Md : Space.Xs;
        // line 的分隔线横贯整行；soft 的底色只在标签后面（列表按内容宽）
        HorizontalAlignment = line || vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        // 横排 line 的分隔线画在整个标签栏上；竖排在列表的结束边，标签与指示条压在它上面
        frame.BorderThickness = line && !vertical ? new Thickness(0, 0, 0, 1) : default;
        box.BorderThickness = line && vertical ? new Thickness(0, 0, 1, 0) : default;
        if (line) box.ClearValue(Border.BackgroundProperty);
        else box.SetResourceReference(Border.BackgroundProperty, "Rune.Brush.Fill");
        indicator.Visibility = line ? Visibility.Visible : Visibility.Collapsed;
        scroller.Axis = vertical ? ScrollAreaAxis.Y : ScrollAreaAxis.X;
        // 竖排不滚；横排能滚但不显示滚动条（触控板、拖动照样能滚）
        scroller.HorizontalScrollBarVisibility = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden;
        scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        focus.Orientation = vertical ? "vertical" : "horizontal";
        focus.Sync();
        if (shownValue != tabs.SelectedValue)
        {
            shownValue = tabs.SelectedValue;
            reveal = true;
        }
        allGroup?.SetCurrentValue(MenuRadioGroup.ValueProperty, tabs.SelectedValue);
        SyncBar();
        UpdateLabels();
        InvalidateMeasure();
    }

    /// <summary>按钮的读屏名称（语言包）。</summary>
    void UpdateLabels()
    {
        var lang = Provider.GetLang(this);
        AutomationProperties.SetName(previous, lang.TabScrollPrevious);
        AutomationProperties.SetName(next, lang.TabScrollNext);
        if (add is not null) AutomationProperties.SetName(add, lang.TabAdd);
        if (allMenu?.Trigger is { } trigger) AutomationProperties.SetName(trigger, lang.TabList);
        if (allGroup is not null) allGroup.Label = lang.TabList;
    }

    /// <summary>标签栏里的按钮：按需建菜单与新建按钮，竖排时都不要。</summary>
    void SyncBar()
    {
        if (ShowAllMenu && allMenu is null)
        {
            var group = allGroup = new MenuRadioGroup();
            System.ComponentModel.DependencyPropertyDescriptor.FromProperty(MenuRadioGroup.ValueProperty, typeof(MenuRadioGroup)).AddValueChanged(group, (_, _) =>
            {
                if (group.Value is { } value && value != owner?.SelectedValue) owner?.Select(value);
            });
            allMenu = new Menu { Name = TabListParts.AllMenu, Placement = FloatingPlacement.BottomEnd, Trigger = new IconButton { Icon = Lucide.ChevronDown, Size = TextSize.Sm } };
            allMenu.Items.Add(group);
            SyncMenuItems();
        }
        else if (!ShowAllMenu) (allMenu, allGroup) = (null, null);
        if (addRequested is not null && add is null)
        {
            add = new IconButton { Name = TabListParts.Add, Icon = Lucide.Plus, Size = TextSize.Sm };
            add.Click += (_, _) => addRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (addRequested is null) add = null;
        var horizontal = Horizontal;
        UIElement?[] children = [previous, scroller, next, horizontal ? allMenu : null, horizontal ? add : null, announcer];
        if (bar.Children.Cast<UIElement>().SequenceEqual(children.OfType<UIElement>())) return;
        bar.Children.Clear();
        foreach (var child in children.OfType<UIElement>()) bar.Children.Add(child);
        UpdateLabels();
    }

    /// <summary>"全部标签页"菜单的项：按显示的顺序列出标签的文字（标签、文字变了时重建）。</summary>
    internal void SyncMenuItems()
    {
        if (allGroup is null) return;
        allGroup.Items.Clear();
        foreach (var tab in TabItems())
            allGroup.Items.Add(new MenuRadioItem { Value = tab.Value, Content = tab.Label, IsEnabled = !tab.IsDisabled });
        allGroup.SetCurrentValue(MenuRadioGroup.ValueProperty, owner?.SelectedValue);
    }

    IconButton ScrollButton(IconData icon, int direction)
    {
        // 只给指针用：键盘在标签之间移动时，当前标签会自己滚进来
        var button = new IconButton { Name = TabListParts.ScrollButton, Icon = icon, Size = TextSize.Sm, Focusable = false, Visibility = Visibility.Collapsed };
        button.Click += (_, _) => ScrollBy(direction);
        return button;
    }

    /// <summary>滚动按钮：往前（-1）或往后（1）滚大半屏。</summary>
    void ScrollBy(int direction) =>
        scroller.ScrollToHorizontalOffset(Math.Clamp(scroller.HorizontalOffset + direction * scroller.ViewportWidth * 0.8, 0, scroller.ScrollableWidth));

    /// <summary>放不下时：显示滚动按钮（到头的那个不可用），还能滚的那头边缘渐隐。</summary>
    void UpdateOverflow()
    {
        var max = scroller.ScrollableWidth;
        var overflowing = Horizontal && max > 1;
        var state = (overflowing, overflowing && scroller.HorizontalOffset > 1, overflowing && scroller.HorizontalOffset < max - 1, scroller.ViewportWidth);
        if (state == overflow) return;
        overflow = state;
        previous.Visibility = next.Visibility = overflowing ? Visibility.Visible : Visibility.Collapsed;
        previous.IsEnabled = overflow.Start;
        next.IsEnabled = overflow.End;
        scroller.OpacityMask = overflow.Start || overflow.End ? FadeMask(overflow.Start, overflow.End, 1.5 * TextElement.GetFontSize(this) / Math.Max(1, overflow.Width)) : null;
    }

    /// <summary>两端 1.5em 的渐隐（edge 是 1.5em 占视口宽度的比例）。</summary>
    static LinearGradientBrush FadeMask(bool start, bool end, double edge) => new(
    [
        new GradientStop(start ? Colors.Transparent : Colors.Black, 0),
        new GradientStop(Colors.Black, start ? Math.Min(edge, 0.5) : 0),
        new GradientStop(Colors.Black, end ? Math.Max(1 - edge, 0.5) : 1),
        new GradientStop(end ? Colors.Transparent : Colors.Black, 1),
    ], 0);

    /// <summary>切到一个滚到外面的标签（方向键、菜单、程序）：把它滚进来，旁边留 16 像素。</summary>
    void RevealSelected()
    {
        if (!reveal || !Horizontal) return;
        if (TabItems().FirstOrDefault(tab => tab.IsSelected) is not { IsArrangeValid: true } tab || !box.IsAncestorOf(tab)) return;
        reveal = false;
        var view = scroller.ViewportWidth;
        if (scroller.ScrollableWidth <= 0) return;
        var at = tab.TranslatePoint(default, box).X;
        var offset = scroller.HorizontalOffset;
        if (at < offset) offset = at - 16;
        else if (at + tab.ActualWidth > offset + view) offset = at + tab.ActualWidth - view + 16;
        else return;
        scroller.ScrollToHorizontalOffset(Math.Clamp(offset, 0, scroller.ScrollableWidth));
    }

    /// <summary>
    /// 标签加减了（挪动排序不算）：关掉的是当前标签时选它后面（没有时前面）的一个，关掉的标签有焦点时焦点交给当前标签；"全部标签页"菜单跟着重建。
    /// </summary>
    void OnTabsChanged(Tab? removed)
    {
        if (moving) return;
        // 拿下的时候在集合的改动当中（可视子元素的槽位还空着）：先记下位置、选中与焦点，改完再选
        var (index, selected, focused) = removed is null ? (-1, false, false) : (shown.IndexOf(removed), removed.IsSelected, removed.IsKeyboardFocusWithin);
        shown = TabItems();
        Dispatcher.BeginInvoke(() =>
        {
            if (index >= 0 && owner is { } tabs)
            {
                var rest = TabItems();
                if (selected && rest.Count > 0 && rest[Math.Min(index, rest.Count - 1)].Value is { } value) tabs.Select(value);
                if (focused) rest.FirstOrDefault(tab => tab.Value == tabs.SelectedValue)?.Focus();
                focus.Sync();
            }
            SyncMenuItems();
        });
    }

    /// <summary>把标签挪到第 to 个（按显示的顺序），报告新顺序；键盘挪的告诉读屏新位置。WPF 的集合没有挪动：拿下来再放回去，焦点放回它上面。</summary>
    void MoveTab(Tab tab, int to, bool announce)
    {
        var order = TabItems();
        var from = order.IndexOf(tab);
        if (Reordered is null || from < 0 || to < 0 || to >= order.Count || to == from) return;
        var focused = tab.IsKeyboardFocusWithin;
        var target = strip.Children.IndexOf(order[to]);
        moving = true;
        try
        {
            strip.Children.Remove(tab);
            strip.Children.Insert(target, tab);
        }
        finally
        {
            moving = false;
        }
        shown = TabItems();
        if (focused) tab.Focus();
        reveal = true;
        Reordered.Invoke(this, new TabsReorderedEventArgs([.. shown.Select(item => item.Value ?? "")]));
        SyncMenuItems();
        if (announce) announcer.Announce(Provider.GetLang(this).TabMoved(tab.Label, to + 1, order.Count));
    }

    /// <summary>键盘替代拖动：在标签上按 Alt + ←→ 往前、往后挪一位（从右到左排版时反过来）。在漫游焦点之前接（预览）。</summary>
    internal bool OnReorderKey(Tab tab, Key key, ModifierKeys modifiers)
    {
        if (Reordered is null || (modifiers & (ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Windows)) != ModifierKeys.Alt) return false;
        if (key is not (Key.Left or Key.Right) || !TabItems().Contains(tab)) return false;
        var forward = (key == Key.Right) != (FlowDirection == FlowDirection.RightToLeft);
        MoveTab(tab, TabItems().IndexOf(tab) + (forward ? 1 : -1), announce: true);
        return true;
    }

    /// <summary>拖动排序：按下标签时记下（触摸时照常滚动）。</summary>
    void OnStripPressed(object sender, MouseButtonEventArgs e)
    {
        if (Reordered is null || e.StylusDevice is not null) return;
        pressed = Ancestor<Tab>(e.OriginalSource as DependencyObject);
        pressedAt = e.GetPosition(strip);
    }

    /// <summary>拖过门槛后跟着指针：越过别的标签的中线就换位（同 Sortable）。拖动时列表接管指针（标签被拿下放回时不丢）。</summary>
    void OnStripMoved(object sender, MouseEventArgs e)
    {
        if (pressed is null || e.LeftButton != MouseButtonState.Pressed) return;
        var at = e.GetPosition(strip);
        if (!dragging)
        {
            if ((at - pressedAt).Length < DragThreshold) return;
            dragging = true;
            pressed.IsDragging = true;
            strip.CaptureMouse();
        }
        var order = TabItems();
        var from = order.IndexOf(pressed);
        var over = order.FirstOrDefault(tab => tab.TranslatePoint(default, strip).X is var left && at.X >= left && at.X <= left + tab.ActualWidth);
        if (over is null || over == pressed) return;
        var to = order.IndexOf(over);
        // 往后挪时要过了它的中线，往前挪时要回到中线之前
        if (to > from == at.X > over.TranslatePoint(default, strip).X + over.ActualWidth / 2) MoveTab(pressed, to, announce: false);
    }

    void EndDrag()
    {
        if (pressed is not null) pressed.IsDragging = false;
        pressed = null;
        if (dragging && strip.IsMouseCaptured) strip.ReleaseMouseCapture();
        dragging = false;
    }

    static T? Ancestor<T>(DependencyObject? element) where T : DependencyObject
    {
        for (; element is not null; element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
            if (element is T match) return match;
        return null;
    }

    /// <summary>指示条对准当前标签：第一次直接到位，之后滑过去（系统关闭动画时直接到位）。</summary>
    void PlaceIndicator()
    {
        if (owner is null || !Line) return;
        if (TabItems().FirstOrDefault(tab => tab.IsSelected) is not { IsArrangeValid: true } selected || !strip.IsAncestorOf(selected)) return;
        var offset = selected.TranslatePoint(new Point(), strip);
        var (start, length) = Horizontal ? (offset.X, selected.ActualWidth) : (offset.Y, selected.ActualHeight);
        if (Math.Abs(start - (double)GetValue(IndicatorStartProperty)) < 0.01 && Math.Abs(length - (double)GetValue(IndicatorLengthProperty)) < 0.01) return;
        var duration = placed ? Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200)) : TimeSpan.Zero;
        placed = true;
        Animate(IndicatorStartProperty, start, duration);
        Animate(IndicatorLengthProperty, length, duration);
    }

    void Animate(DependencyProperty property, double to, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            BeginAnimation(property, null);
            SetValue(property, to);
            return;
        }
        var from = (double)GetValue(property);
        SetValue(property, to);
        BeginAnimation(property, new DoubleAnimation(from, to, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction, FillBehavior = FillBehavior.Stop });
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 随字号的尺寸：soft 的 xs 内边距、md 圆角
        var em = TextElement.GetFontSize(this);
        box.Padding = Line ? default : new Thickness(0.25 * em);
        box.CornerRadius = Line ? default : new CornerRadius((TryFindResource("Rune.Radius.Md") as double? ?? 0.375) * em);
        frame.Measure(availableSize);
        return frame.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        frame.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>列表的滚动区域：外观同 ScrollArea，只是内部组合用的（上游是列表自己滚动，不是 ScrollArea 组件）。</summary>
    internal sealed class ListScroller : ScrollArea
    {
        static ListScroller() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ListScroller), new FrameworkPropertyMetadata(typeof(ScrollArea)));
    }

    /// <summary>标签排成的一行（一列）：标签加减时告诉列表。</summary>
    sealed class Strip(TabList owner) : Flex
    {
        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
            owner.OnTabsChanged(visualRemoved as Tab);
        }
    }

    /// <summary>列表本身：标签（Flex）与压在上面的指示条。横排时指示条在列表底边；竖排时压在结束边的分隔线上（往外 1 像素）。</summary>
    sealed class Layer(TabList owner) : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            owner.indicator.Measure(default);
            owner.strip.Measure(availableSize);
            return owner.strip.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            owner.strip.Arrange(new Rect(finalSize));
            var start = (double)owner.GetValue(IndicatorStartProperty);
            var length = (double)owner.GetValue(IndicatorLengthProperty);
            owner.indicator.Arrange(owner.Horizontal
                ? new Rect(start, Math.Max(0, finalSize.Height - 2), length, 2)
                : new Rect(Math.Max(0, finalSize.Width - 1), start, 2, length));
            return finalSize;
        }
    }

    /// <summary>标签栏：一行排开，间距 space.xs、竖直居中；列表按内容宽，放不下时收缩（按钮先占位）。读屏的实时区域不占位。</summary>
    sealed class Bar(TabList owner) : Panel
    {
        double Gap => 0.25 * TextElement.GetFontSize(owner);

        List<UIElement> Shown => [.. InternalChildren.Cast<UIElement>().Where(child => child.Visibility == Visibility.Visible && child is not LiveRegion)];

        protected override Size MeasureOverride(Size availableSize)
        {
            owner.announcer.Measure(default);
            var shown = Shown;
            var used = Gap * Math.Max(0, shown.Count - 1);
            var height = 0.0;
            foreach (var child in shown.Where(child => child != owner.scroller))
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                used += child.DesiredSize.Width;
                height = Math.Max(height, child.DesiredSize.Height);
            }
            owner.scroller.Measure(new Size(Math.Max(0, availableSize.Width - used), availableSize.Height));
            height = Math.Max(height, owner.scroller.DesiredSize.Height);
            return new Size(used + owner.scroller.DesiredSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            owner.announcer.Arrange(default);
            var shown = Shown;
            var fixedWidth = shown.Where(child => child != owner.scroller).Sum(child => child.DesiredSize.Width) + Gap * Math.Max(0, shown.Count - 1);
            var x = 0.0;
            foreach (var child in shown)
            {
                var size = child == owner.scroller
                    ? new Size(Math.Min(child.DesiredSize.Width, Math.Max(0, finalSize.Width - fixedWidth)), finalSize.Height)
                    : child.DesiredSize;
                child.Arrange(new Rect(new Point(x, (finalSize.Height - size.Height) / 2), size));
                x += size.Width + Gap;
            }
            return finalSize;
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabListPeer(this);

    /// <summary>读屏：选项卡（tablist）。子元素是标签栏里的按钮与各标签，跳过中间的滚动区域、布局层。</summary>
    sealed class TabListPeer(TabList owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

        protected override string GetClassNameCore() => nameof(TabList);

        protected override List<AutomationPeer> GetChildrenCore()
        {
            UIElement?[] before = [owner.previous.IsVisible ? owner.previous : null];
            UIElement?[] after = [owner.next.IsVisible ? owner.next : null, owner.Horizontal ? owner.allMenu?.Trigger : null, owner.Horizontal ? owner.add : null, owner.announcer];
            return [.. before.Concat(owner.TabItems()).Concat(after).OfType<UIElement>().Select(CreatePeerForElement)];
        }
    }
}

/// <summary>标签改了顺序的参数。</summary>
/// <param name="values">新的顺序：各标签的值。</param>
public sealed class TabsReorderedEventArgs(IReadOnlyList<string> values) : EventArgs
{
    /// <summary>新的顺序：各标签的值。</summary>
    public IReadOnlyList<string> Values { get; } = values;
}

/// <summary>标签列表的部件名（元素的 Name）。</summary>
public static class TabListParts
{
    /// <summary>line 样式的下划线指示条（TS 的 propsIndicator，Border）。</summary>
    public const string Indicator = "PART_Indicator";

    /// <summary>横排时包着列表与右边按钮的标签栏（TS 的 propsBar，Border：line 的分隔线是它的底边）。</summary>
    public const string Bar = "PART_Bar";

    /// <summary>新建按钮（TS 的 propsAdd，IconButton）。</summary>
    public const string Add = "PART_Add";

    /// <summary>标签放不下时两头的滚动按钮（TS 的 propsScrollButton，IconButton）。</summary>
    public const string ScrollButton = "PART_ScrollButton";

    /// <summary>"全部标签页"菜单（Menu，触发器是向下箭头的 IconButton）。</summary>
    public const string AllMenu = "PART_AllMenu";
}
