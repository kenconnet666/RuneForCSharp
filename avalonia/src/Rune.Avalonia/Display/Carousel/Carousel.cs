using System.Collections;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 轮播（vue-tsx-pro 的 Carousel）：横幅、商品图、图片组。一行张片，上一张 / 下一张、页点、可关的自动播放；参考 WAI-ARIA APG 的 Carousel 模式。
/// <code>
/// &lt;rune:Carousel ItemsSource="{Binding Banners}" AutomationProperties.Name="活动"&gt;&lt;rune:Carousel.ItemTemplate&gt;…&lt;/rune:Carousel.ItemTemplate&gt;&lt;/rune:Carousel&gt;
/// &lt;rune:Carousel ItemsSource="{Binding Products}" SlidesPerView="3" Gap="16" Loop="False" /&gt;      一次看三张
/// &lt;rune:Carousel ItemsSource="{Binding Banners}" Autoplay="0:0:6" Index="{Binding Current}" /&gt;      六秒换一张
/// </code>
/// 上游是浏览器的横向滚动加吸附；桌面上张片排成一条，按位移切换（lg 的过渡，系统关闭动画时直接到位）：拖动、触摸板横向滑动（或 Shift + 滚轮）、
/// 焦点在张片区时 ← / →（从右到左排版时反过来）都能换，停下时吸附到整张。<see cref="Index"/> 是最前面那张的序号（一次看几张时最大是张数 − SlidesPerView，向上取整），
/// 默认双向绑定。<see cref="Loop"/>（默认 true）到头后从另一头开始；不循环时两头的按钮不可用。
/// 自动播放：<see cref="Autoplay"/> 是间隔（TS 的 autoplay: true 是 5 秒，即 <see cref="DefaultAutoplay"/>）；指针停在上面、键盘焦点进到里面时暂停，
/// 底部有暂停 / 播放按钮；系统关闭动画时一开始就是暂停的。
/// 读屏：轮播是带名称的一组（名称默认“轮播”），每张是一组、名称“第 2 张，共 5 张”；不自动播放时换张会念出来。部件：<see cref="CarouselParts"/>。
/// </summary>
public class Carousel : Control
{
    /// <summary>TS 的 autoplay: true：每 5 秒换一张。</summary>
    public static readonly TimeSpan DefaultAutoplay = TimeSpan.FromSeconds(5);

    /// <summary>每一张的数据（控件原样放进去）。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Carousel, IEnumerable?>(nameof(ItemsSource));

    /// <summary>一张的模板（TS 的 children 渲染函数）。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<Carousel, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>最前面那张的序号（默认双向绑定）。</summary>
    public static readonly StyledProperty<int> IndexProperty = AvaloniaProperty.Register<Carousel, int>(nameof(Index), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>一次看几张，默认 1（可以是 2.5：露出下一张的一半）。</summary>
    public static readonly StyledProperty<double> SlidesPerViewProperty = AvaloniaProperty.Register<Carousel, double>(nameof(SlidesPerView), 1, validate: value => value > 0);

    /// <summary>张与张之间的间距，默认 0。</summary>
    public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<Carousel, double>(nameof(Gap));

    /// <summary>到头后接着从另一头开始，默认 true。</summary>
    public static readonly StyledProperty<bool> LoopProperty = AvaloniaProperty.Register<Carousel, bool>(nameof(Loop), true);

    /// <summary>自动播放的间隔；null（默认）不自动播放。</summary>
    public static readonly StyledProperty<TimeSpan?> AutoplayProperty = AvaloniaProperty.Register<Carousel, TimeSpan?>(nameof(Autoplay));

    /// <summary>两侧的上一张 / 下一张按钮，默认显示。</summary>
    public static readonly StyledProperty<bool> ArrowsProperty = AvaloniaProperty.Register<Carousel, bool>(nameof(Arrows), true);

    /// <summary>底部的页点，默认显示。</summary>
    public static readonly StyledProperty<bool> DotsProperty = AvaloniaProperty.Register<Carousel, bool>(nameof(Dots), true);

    readonly Track track;
    readonly IconButton previous = new() { Name = CarouselParts.Previous, Icon = Lucide.ChevronLeft, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Radius = Radius.Full };
    readonly IconButton next = new() { Name = CarouselParts.Next, Icon = Lucide.ChevronRight, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Radius = Radius.Full };
    readonly Border controls = new() { Name = CarouselParts.Controls, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    readonly StackPanel controlRow = new() { Orientation = Orientation.Horizontal };
    readonly IconButton pause = new() { Name = CarouselParts.Pause, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Radius = Radius.Full };
    readonly LiveRegion live = new();
    readonly Panel root = new();
    bool paused;
    bool hovering;
    bool focused;
    int autoplayVersion;

    /// <summary>新建：张片区、两侧按钮、底部的控制条。</summary>
    public Carousel()
    {
        track = new Track(this) { Name = CarouselParts.Viewport };
        foreach (var arrow in new[] { previous, next }) arrow.VerticalAlignment = VerticalAlignment.Center;
        previous.HorizontalAlignment = HorizontalAlignment.Left;
        next.HorizontalAlignment = HorizontalAlignment.Right;
        previous.Click += (_, _) => Go(Index - 1);
        next.Click += (_, _) => Go(Index + 1);
        pause.Click += (_, _) =>
        {
            paused = !paused;
            // 明确点了“播放”：焦点还在这个按钮上也恢复
            if (!paused) focused = false;
            Sync();
        };
        controls.Child = controlRow;
        root.Children.AddRange([track, previous, next, controls, live]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        // 系统关闭了动画：一开始就暂停（用户点了播放照样播）
        paused = !Motion.Enabled;
    }

    /// <summary>每一张的数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>一张的模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>最前面那张的序号。</summary>
    public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    /// <summary>一次看几张。</summary>
    public double SlidesPerView { get => GetValue(SlidesPerViewProperty); set => SetValue(SlidesPerViewProperty, value); }

    /// <summary>张与张之间的间距。</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>到头后接着从另一头开始。</summary>
    public bool Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>自动播放的间隔。</summary>
    public TimeSpan? Autoplay { get => GetValue(AutoplayProperty); set => SetValue(AutoplayProperty, value); }

    /// <summary>两侧的上一张 / 下一张按钮。</summary>
    public bool Arrows { get => GetValue(ArrowsProperty); set => SetValue(ArrowsProperty, value); }

    /// <summary>底部的页点。</summary>
    public bool Dots { get => GetValue(DotsProperty); set => SetValue(DotsProperty, value); }

    /// <summary>张数。</summary>
    int Count => track.Children.Count;

    /// <summary>最后一个序号：一次看 1.5 张这种向上取整，滚到底就算到了最后。</summary>
    int Last => Math.Max(0, (int)Math.Ceiling(Count - SlidesPerView));

    /// <summary>正在自动播放：有间隔、不止一页、没暂停、指针不在上面、键盘焦点不在里面。</summary>
    internal bool Playing => Autoplay is { } interval && interval > TimeSpan.Zero && Last > 0 && !paused && !hovering && !focused;

    /// <summary>上一张、下一张、暂停按钮（测试用）。</summary>
    internal (IconButton Previous, IconButton Next, IconButton Pause) Buttons => (previous, next, pause);

    /// <summary>页点（测试用）。</summary>
    internal IReadOnlyList<Dot> DotButtons => [.. controlRow.Children.OfType<Dot>()];

    /// <summary>每一张（测试用）。</summary>
    internal IReadOnlyList<Control> Slides => [.. track.Children];

    /// <summary>张片区（测试用）。</summary>
    internal Panel Viewport => track;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        autoplayVersion++;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == ItemTemplateProperty) Rebuild();
        else if (change.Property == IndexProperty)
        {
            track.ScrollTo(Index, animate: true);
            if (!Playing && VisualRoot is not null) live.Announce(Provider.GetLang(this).SlideCounter(Index + 1, Count));
            Sync();
        }
        else if (change.Property == SlidesPerViewProperty || change.Property == GapProperty)
        {
            track.InvalidateMeasure();
            Sync();
        }
        else if (change.Property == LoopProperty || change.Property == AutoplayProperty || change.Property == ArrowsProperty || change.Property == DotsProperty
                 || change.Property == AutomationProperties.NameProperty) Sync();
    }

    /// <summary>按数据重建每一张（控件原样放进去，数据套模板）。</summary>
    void Rebuild()
    {
        // 控件原样放进去时，旧的那张要先放手，否则控件还挂在旧的张上，放不进新的
        foreach (var slide in track.Children.OfType<Slide>()) slide.Content = null;
        track.Children.Clear();
        var lang = Provider.GetLang(this);
        var items = ItemsSource?.Cast<object?>().ToList() ?? [];
        for (var index = 0; index < items.Count; index++)
        {
            var slide = new Slide { Name = CarouselParts.Slide, Content = items[index], ContentTemplate = ItemTemplate };
            AutomationProperties.SetName(slide, lang.SlideCounter(index + 1, items.Count));
            track.Children.Add(slide);
        }
        controlRow.Children.Clear();
        controlRow.Children.Add(pause);
        for (var index = 0; index <= Last; index++)
        {
            var page = index;
            var dot = new Dot { Name = CarouselParts.Dot };
            AutomationProperties.SetName(dot, lang.SlideCounter(page + 1, items.Count));
            dot.Click += (_, _) => Go(page);
            controlRow.Children.Add(dot);
        }
        if (Index > Last) SetCurrentValue(IndexProperty, Last);
        Sync();
    }

    /// <summary>换到第 target 张：Loop 时绕回另一头，否则停在两头。</summary>
    internal void Go(int target)
    {
        var pages = Last + 1;
        SetCurrentValue(IndexProperty, Loop ? (target % pages + pages) % pages : Math.Clamp(target, 0, Last));
    }

    /// <summary>按钮、页点、名称跟着状态；重新开始自动播放的计时。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        var pages = Last + 1;
        previous.IsVisible = next.IsVisible = Arrows && pages > 1;
        previous.IsEnabled = Loop || Index > 0;
        next.IsEnabled = Loop || Index < Last;
        AutomationProperties.SetName(previous, lang.CarouselPrevious);
        AutomationProperties.SetName(next, lang.CarouselNext);
        var autoplay = Autoplay is { } interval && interval > TimeSpan.Zero;
        pause.IsVisible = autoplay;
        pause.Icon = paused ? Lucide.Play : Lucide.Pause;
        AutomationProperties.SetName(pause, paused ? lang.CarouselPlay : lang.CarouselPause);
        foreach (var dot in controlRow.Children.OfType<Dot>())
        {
            dot.IsVisible = Dots && pages > 1;
            dot.IsCurrent = controlRow.Children.IndexOf(dot) - 1 == Index;
        }
        controls.IsVisible = (Dots && pages > 1) || autoplay;
        AutomationProperties.SetLiveSetting(live, Playing ? AutomationLiveSetting.Off : AutomationLiveSetting.Polite);
        _ = Play();
    }

    /// <summary>自动播放：每次换张、暂停条件变了时重新计时，到点换下一张。</summary>
    async Task Play()
    {
        var version = ++autoplayVersion;
        if (!Playing || VisualRoot is null) return;
        await Task.Delay(Autoplay!.Value);
        if (version == autoplayVersion && Playing) Go(Index + 1);
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Mouse) return;
        hovering = true;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        hovering = false;
        Sync();
    }

    /// <summary>键盘焦点进来才暂停（点了上一张、页点后按钮留着焦点，不能因此一直停着）。</summary>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.NavigationMethod is not (NavigationMethod.Tab or NavigationMethod.Directional)) return;
        focused = true;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (IsKeyboardFocusWithin) return;
        focused = false;
        Sync();
    }

    /// <summary>em 的尺寸：两侧按钮离边 sm，控制条离底 sm、左右 xs、圆角 full、surface 75% 底。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var em = global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);
        double Space(string key) => this.Resource<double>(key) * em;
        previous.Margin = new Thickness(Space(ThemeKeys.Space.Sm), 0, 0, 0);
        next.Margin = new Thickness(0, 0, Space(ThemeKeys.Space.Sm), 0);
        controls.Margin = new Thickness(0, 0, 0, Space(ThemeKeys.Space.Sm));
        controls.Padding = new Thickness(Space(ThemeKeys.Space.Xs), 0);
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
    protected override AutomationPeer OnCreateAutomationPeer() => new CarouselPeer(this);

    /// <summary>读屏：带名称的一组（TS 的 aria-roledescription="carousel"），名称默认“轮播”。</summary>
    sealed class CarouselPeer(Carousel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "carousel";

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).Carousel;
    }

    /// <summary>一张：读屏是一组（TS 的 aria-roledescription="slide"），名称“第 2 张，共 5 张”。</summary>
    internal sealed class Slide : ContentPresenter
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new SlidePeer(this);

        sealed class SlidePeer(Slide owner) : ControlAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

            protected override string GetLocalizedControlTypeCore() => "slide";
        }
    }

    /// <summary>
    /// 页点：点击区域够大（宽 1.25em、高 1.5em，至少 24），看得见的是中间的小圆点（0.5em、borderStrong，悬停 textMuted）；
    /// 当前的那个拉宽到 2em、圆点 1.25em、accent（宽度 md 的过渡）。伪类 :current（TS 的 aria-current）。
    /// </summary>
    internal sealed class Dot : ButtonBase
    {
        readonly Border mark = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        public Dot()
        {
            Content = mark;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
        }

        // 主题按类型找：沿用 ButtonBase 的（没有外观，只有焦点环与波纹）
        protected override Type StyleKeyOverride => typeof(ButtonBase);

        public bool IsCurrent
        {
            get;
            set
            {
                field = value;
                PseudoClasses.Set(":current", value);
                Paint();
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsPointerOverProperty || change.Property == global::Avalonia.Controls.Documents.TextElement.FontSizeProperty) Paint();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Paint();
        }

        void Paint()
        {
            if (VisualRoot is null) return;
            var em = global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);
            var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
            Transitions = duration > TimeSpan.Zero ? [new DoubleTransition { Property = WidthProperty, Duration = duration, Easing = new CubicEaseOut() }] : null;
            mark.Transitions = Transitions is null ? null : [new DoubleTransition { Property = WidthProperty, Duration = duration, Easing = new CubicEaseOut() }];
            Width = (IsCurrent ? 2 : 1.25) * em;
            Height = Math.Max(1.5 * em, 24);
            mark.Width = (IsCurrent ? 1.25 : 0.5) * em;
            mark.Height = 0.5 * em;
            mark.CornerRadius = new CornerRadius(0.25 * em);
            mark.Background = this.Resource<IBrush>(IsCurrent ? ThemeKeys.Brush.Accent : IsPointerOver ? ThemeKeys.Brush.TextMuted : ThemeKeys.Brush.BorderStrong);
        }
    }

    /// <summary>
    /// 张片区：张片排成一行（每张宽 =（宽 − (SlidesPerView − 1) × Gap）÷ SlidesPerView），按位移 Offset 切换；能聚焦（← / → 换张）、裁掉区外的部分。
    /// 拖动（挪开 3 以内不算）跟着指针移，松开吸附到最近的一张；横向滚轮（或 Shift + 滚轮）换张。
    /// </summary>
    sealed class Track : Panel
    {
        /// <summary>位移：最前面露出来的位置（过渡驱动）。</summary>
        public static readonly StyledProperty<double> OffsetProperty = AvaloniaProperty.Register<Track, double>(nameof(Offset));

        readonly Carousel owner;
        Point? pressedAt;
        double pressedOffset;
        bool dragging;

        static Track() => AffectsArrange<Track>(OffsetProperty);

        public Track(Carousel owner)
        {
            this.owner = owner;
            Focusable = true;
            ClipToBounds = true;
            Background = Brushes.Transparent;
        }

        public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

        double SlideWidth => Math.Max(0, (Bounds.Width - (owner.SlidesPerView - 1) * owner.Gap) / owner.SlidesPerView);

        double MaxOffset => Math.Max(0, Children.Count * (SlideWidth + owner.Gap) - owner.Gap - Bounds.Width);

        double OffsetOf(int index) => Math.Min(index * (SlideWidth + owner.Gap), MaxOffset);

        /// <summary>滚到第 index 张（lg 的过渡；拖动后吸附也走这里）。</summary>
        public void ScrollTo(int index, bool animate)
        {
            var duration = animate && VisualRoot is not null ? Motion.Duration(owner, ThemeKeys.Duration.Lg) : TimeSpan.Zero;
            Transitions = duration > TimeSpan.Zero ? [new DoubleTransition { Property = OffsetProperty, Duration = duration, Easing = new CubicEaseOut() }] : null;
            Offset = OffsetOf(index);
        }

        /// <summary>停在哪一张：离哪张对齐的位置最近（滚到底就是最后一张）。</summary>
        int Nearest()
        {
            if (Offset >= MaxOffset - 1) return owner.Last;
            var best = 0;
            for (var index = 1; index <= owner.Last; index++)
                if (Math.Abs(Offset - OffsetOf(index)) < Math.Abs(Offset - OffsetOf(best))) best = index;
            return best;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? 0 : Math.Max(0, (availableSize.Width - (owner.SlidesPerView - 1) * owner.Gap) / owner.SlidesPerView);
            var height = 0d;
            foreach (var child in Children)
            {
                child.Measure(new Size(width, availableSize.Height));
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = Math.Max(0, (finalSize.Width - (owner.SlidesPerView - 1) * owner.Gap) / owner.SlidesPerView);
            for (var index = 0; index < Children.Count; index++)
                Children[index].Arrange(new Rect(index * (width + owner.Gap) - Offset, 0, width, finalSize.Height));
            return finalSize;
        }

        /// <summary>宽度变了（窗口缩放）：停在当前这一张。</summary>
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            ScrollTo(owner.Index, animate: false);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            pressedAt = e.GetPosition(this);
            pressedOffset = Offset;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (pressedAt is not { } from) return;
            var dx = e.GetPosition(this).X - from.X;
            if (!dragging)
            {
                if (Math.Abs(dx) < 3) return;
                dragging = true;
                e.Pointer.Capture(this);
                Transitions = null;
            }
            Offset = Math.Clamp(pressedOffset - dx, 0, MaxOffset);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            pressedAt = null;
            if (!dragging) return;
            dragging = false;
            e.Pointer.Capture(null);
            var target = Nearest();
            if (target == owner.Index) ScrollTo(target, animate: true);
            else owner.SetCurrentValue(IndexProperty, target);
            e.Handled = true;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            var delta = e.Delta.X != 0 ? e.Delta.X : e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? e.Delta.Y : 0;
            if (delta == 0) return;
            owner.Go(owner.Index + (delta < 0 ? 1 : -1));
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key is not (Key.Left or Key.Right)) return;
            var forward = (e.Key == Key.Right) != (owner.FlowDirection == FlowDirection.RightToLeft);
            owner.Go(owner.Index + (forward ? 1 : -1));
            e.Handled = true;
        }
    }
}

/// <summary>Carousel 的部件名。</summary>
public static class CarouselParts
{
    /// <summary>张片区（TS 的 propsViewport）。</summary>
    public const string Viewport = "PART_Viewport";

    /// <summary>每一张（TS 的 propsSlide）。</summary>
    public const string Slide = "PART_Slide";

    /// <summary>上一张（TS 的 propsPrevious）。</summary>
    public const string Previous = "PART_Previous";

    /// <summary>下一张（TS 的 propsNext）。</summary>
    public const string Next = "PART_Next";

    /// <summary>底部控制条（TS 的 propsControls）。</summary>
    public const string Controls = "PART_Controls";

    /// <summary>暂停 / 播放（TS 的 propsPause）。</summary>
    public const string Pause = "PART_Pause";

    /// <summary>页点（TS 的 propsDot）。</summary>
    public const string Dot = "PART_Dot";
}
