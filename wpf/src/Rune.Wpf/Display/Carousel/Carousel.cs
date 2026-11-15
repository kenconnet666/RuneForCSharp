using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 轮播（vue-tsx-pro 的 Carousel）：横幅、商品图、图片组。一行张片，上一张 / 下一张、页点、可关的自动播放；参考 WAI-ARIA APG 的 Carousel 模式。
/// <code>
/// &lt;rune:Carousel ItemsSource="{Binding Banners}" AutomationProperties.Name="活动"&gt;&lt;rune:Carousel.ItemTemplate&gt;…&lt;/rune:Carousel.ItemTemplate&gt;&lt;/rune:Carousel&gt;
/// &lt;rune:Carousel ItemsSource="{Binding Products}" SlidesPerView="3" Gap="16" Loop="False" /&gt;      一次看三张
/// &lt;rune:Carousel ItemsSource="{Binding Banners}" Autoplay="0:0:6" Index="{Binding Current}" /&gt;      六秒换一张
/// </code>
/// 上游是浏览器的横向滚动加吸附；桌面上张片排成一条，按位移切换（lg 的过渡，系统关闭动画时直接到位）：拖动、Shift + 滚轮、
/// 焦点在张片区时 ← / →（从右到左排版时反过来）都能换，停下时吸附到整张。<see cref="Index"/> 是最前面那张的序号（一次看几张时最大是张数 − SlidesPerView，向上取整），
/// 默认双向绑定。<see cref="Loop"/>（默认 true）到头后从另一头开始；不循环时两头的按钮不可用。
/// 自动播放：<see cref="Autoplay"/> 是间隔（TS 的 autoplay: true 是 5 秒，即 <see cref="DefaultAutoplay"/>）；指针停在上面、键盘焦点进到里面时暂停，
/// 底部有暂停 / 播放按钮；系统关闭动画时一开始就是暂停的。
/// 读屏：轮播是带名称的一组（名称默认“轮播”），每张是一组、名称“第 2 张，共 5 张”；不自动播放时换张会念出来。
/// </summary>
public class Carousel : CompositeControl
{
    /// <summary>TS 的 autoplay: true：每 5 秒换一张。</summary>
    public static readonly TimeSpan DefaultAutoplay = TimeSpan.FromSeconds(5);

    /// <summary>每一张的数据（控件原样放进去）。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(Carousel),
        new FrameworkPropertyMetadata(null, (d, _) => ((Carousel)d).Rebuild()));

    /// <summary>一张的模板（TS 的 children 渲染函数）。</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(Carousel),
        new FrameworkPropertyMetadata(null, (d, _) => ((Carousel)d).Rebuild()));

    /// <summary>最前面那张的序号（默认双向绑定）。</summary>
    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(nameof(Index), typeof(int), typeof(Carousel),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Carousel)d).OnIndexChanged()));

    /// <summary>一次看几张，默认 1（可以是 2.5：露出下一张的一半）。</summary>
    public static readonly DependencyProperty SlidesPerViewProperty = DependencyProperty.Register(nameof(SlidesPerView), typeof(double), typeof(Carousel),
        new FrameworkPropertyMetadata(1d, (d, _) => ((Carousel)d).Relayout()), value => value is double count && count > 0);

    /// <summary>张与张之间的间距，默认 0。</summary>
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(nameof(Gap), typeof(double), typeof(Carousel),
        new FrameworkPropertyMetadata(0d, (d, _) => ((Carousel)d).Relayout()));

    /// <summary>到头后接着从另一头开始，默认 true。</summary>
    public static readonly DependencyProperty LoopProperty = DependencyProperty.Register(nameof(Loop), typeof(bool), typeof(Carousel),
        new FrameworkPropertyMetadata(true, (d, _) => ((Carousel)d).Sync()));

    /// <summary>自动播放的间隔；null（默认）不自动播放。</summary>
    public static readonly DependencyProperty AutoplayProperty = DependencyProperty.Register(nameof(Autoplay), typeof(TimeSpan?), typeof(Carousel),
        new FrameworkPropertyMetadata(null, (d, _) => ((Carousel)d).Sync()));

    /// <summary>两侧的上一张 / 下一张按钮，默认显示。</summary>
    public static readonly DependencyProperty ArrowsProperty = DependencyProperty.Register(nameof(Arrows), typeof(bool), typeof(Carousel),
        new FrameworkPropertyMetadata(true, (d, _) => ((Carousel)d).Sync()));

    /// <summary>底部的页点，默认显示。</summary>
    public static readonly DependencyProperty DotsProperty = DependencyProperty.Register(nameof(Dots), typeof(bool), typeof(Carousel),
        new FrameworkPropertyMetadata(true, (d, _) => ((Carousel)d).Sync()));

    readonly Track track;
    readonly IconButton previous = new() { Icon = Lucide.ChevronLeft, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Radius = Radius.Full };
    readonly IconButton next = new() { Icon = Lucide.ChevronRight, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Radius = Radius.Full };
    readonly LayoutGrid controls = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    readonly Border controlsBackground = new() { Opacity = 0.75, CornerRadius = new CornerRadius(9999) };
    readonly StackPanel controlRow = new() { Orientation = Orientation.Horizontal };
    readonly IconButton pause = new() { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Radius = Radius.Full };
    readonly LiveRegion live = new();
    bool paused;
    bool hovering;
    bool focused;
    int autoplayVersion;

    static Carousel()
    {
        FocusableProperty.OverrideMetadata(typeof(Carousel), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Carousel), new FrameworkPropertyMetadata(false));
        AutomationProperties.NameProperty.OverrideMetadata(typeof(Carousel), new FrameworkPropertyMetadata(""));
    }

    /// <summary>新建：张片区、两侧按钮、底部的控制条。</summary>
    public Carousel()
    {
        track = new Track(this);
        foreach (var arrow in new[] { previous, next })
        {
            arrow.VerticalAlignment = VerticalAlignment.Center;
            arrow.SetResourceReference(EffectProperty, ThemeKeys.Shadow.Md);
        }
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
        // 上游的控制条是 surface 75% 的底（还有 4px 的背景模糊，桌面上没有）：底单独一层半透明，按钮不跟着透明
        controlsBackground.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.Surface);
        controls.Children.Add(controlsBackground);
        controls.Children.Add(controlRow);
        Root = new LayoutGrid { Children = { track, previous, next, controls, live } };
        // 系统关闭了动画：一开始就暂停（用户点了播放照样播）
        paused = !Motion.Enabled;
        Loaded += (_, _) => Rebuild();
        Unloaded += (_, _) => autoplayVersion++;
        MouseEnter += (_, e) =>
        {
            if (e.StylusDevice is not null) return;
            hovering = true;
            Sync();
        };
        MouseLeave += (_, _) =>
        {
            hovering = false;
            Sync();
        };
        // 键盘焦点进来才暂停（点了上一张、页点后按钮留着焦点，不能因此一直停着）
        IsKeyboardFocusWithinChanged += (_, _) =>
        {
            focused = IsKeyboardFocusWithin && InputManager.Current.MostRecentInputDevice is KeyboardDevice;
            Sync();
        };
    }

    /// <summary>每一张的数据。</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>一张的模板。</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>最前面那张的序号。</summary>
    public int Index { get => (int)GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    /// <summary>一次看几张。</summary>
    public double SlidesPerView { get => (double)GetValue(SlidesPerViewProperty); set => SetValue(SlidesPerViewProperty, value); }

    /// <summary>张与张之间的间距。</summary>
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>到头后接着从另一头开始。</summary>
    public bool Loop { get => (bool)GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>自动播放的间隔。</summary>
    public TimeSpan? Autoplay { get => (TimeSpan?)GetValue(AutoplayProperty); set => SetValue(AutoplayProperty, value); }

    /// <summary>两侧的上一张 / 下一张按钮。</summary>
    public bool Arrows { get => (bool)GetValue(ArrowsProperty); set => SetValue(ArrowsProperty, value); }

    /// <summary>底部的页点。</summary>
    public bool Dots { get => (bool)GetValue(DotsProperty); set => SetValue(DotsProperty, value); }

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
    internal IReadOnlyList<FrameworkElement> Slides => [.. track.Children.Cast<FrameworkElement>()];

    /// <summary>张片区（测试用）。</summary>
    internal Panel Viewport => track;

    void OnIndexChanged()
    {
        track.ScrollTo(Index, animate: true);
        if (!Playing && IsLoaded) live.Announce(Provider.GetLang(this).SlideCounter(Index + 1, Count));
        Sync();
    }

    void Relayout()
    {
        track.InvalidateMeasure();
        Sync();
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
            var slide = new Slide { Content = items[index], ContentTemplate = ItemTemplate };
            AutomationProperties.SetName(slide, lang.SlideCounter(index + 1, items.Count));
            track.Children.Add(slide);
        }
        controlRow.Children.Clear();
        controlRow.Children.Add(pause);
        for (var index = 0; index <= Last; index++)
        {
            var page = index;
            var dot = new Dot();
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
        previous.Visibility = next.Visibility = Arrows && pages > 1 ? Visibility.Visible : Visibility.Collapsed;
        previous.IsEnabled = Loop || Index > 0;
        next.IsEnabled = Loop || Index < Last;
        AutomationProperties.SetName(previous, lang.CarouselPrevious);
        AutomationProperties.SetName(next, lang.CarouselNext);
        var autoplay = Autoplay is { } interval && interval > TimeSpan.Zero;
        pause.Visibility = autoplay ? Visibility.Visible : Visibility.Collapsed;
        pause.Icon = paused ? Lucide.Play : Lucide.Pause;
        AutomationProperties.SetName(pause, paused ? lang.CarouselPlay : lang.CarouselPause);
        var dots = Dots && pages > 1;
        foreach (var dot in controlRow.Children.OfType<Dot>())
        {
            dot.Visibility = dots ? Visibility.Visible : Visibility.Collapsed;
            dot.IsCurrent = controlRow.Children.IndexOf(dot) - 1 == Index;
        }
        controls.Visibility = dots || autoplay ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetLiveSetting(live, Playing ? AutomationLiveSetting.Off : AutomationLiveSetting.Polite);
        _ = Play();
    }

    /// <summary>自动播放：每次换张、暂停条件变了时重新计时，到点换下一张。</summary>
    async Task Play()
    {
        var version = ++autoplayVersion;
        if (!Playing || !IsLoaded) return;
        await Task.Delay(Autoplay!.Value);
        if (version == autoplayVersion && Playing) Go(Index + 1);
    }

    /// <summary>em 的尺寸：两侧按钮离边 sm，控制条离底 sm、左右 xs。</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        double Space(string key) => this.Resource<double>(key) * FontSize;
        previous.Margin = new Thickness(Space(ThemeKeys.Space.Sm), 0, 0, 0);
        next.Margin = new Thickness(0, 0, Space(ThemeKeys.Space.Sm), 0);
        controls.Margin = new Thickness(0, 0, 0, Space(ThemeKeys.Space.Sm));
        controlRow.Margin = new Thickness(Space(ThemeKeys.Space.Xs), 0, Space(ThemeKeys.Space.Xs), 0);
        return base.MeasureOverride(constraint);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CarouselPeer(this);

    /// <summary>读屏：带名称的一组（TS 的 aria-roledescription="carousel"），名称默认“轮播”。</summary>
    sealed class CarouselPeer(Carousel owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Carousel);

        protected override string GetLocalizedControlTypeCore() => "carousel";

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).Carousel;
    }

    /// <summary>一张：读屏是一组（TS 的 aria-roledescription="slide"），名称“第 2 张，共 5 张”。</summary>
    internal sealed class Slide : ContentPresenter
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new SlidePeer(this);

        sealed class SlidePeer(Slide owner) : FrameworkElementAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

            protected override string GetClassNameCore() => "CarouselSlide";

            protected override string GetLocalizedControlTypeCore() => "slide";
        }
    }

    /// <summary>
    /// 页点：点击区域够大（宽 1.25em、高 1.5em，至少 24），看得见的是中间的小圆点（0.5em、borderStrong，悬停 textMuted）；
    /// 当前的那个拉宽到 2em、圆点 1.25em、accent（宽度 md 的过渡）。<see cref="IsCurrent"/> 是 TS 的 aria-current。
    /// </summary>
    internal sealed class Dot : ButtonBase
    {
        readonly Border mark = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        // 主题按类型找：沿用 ButtonBase 的（没有外观，只有焦点环与波纹）
        static Dot() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Dot), new FrameworkPropertyMetadata(typeof(ButtonBase)));

        public Dot()
        {
            Content = mark;
            Padding = default;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            Loaded += (_, _) => Paint(animate: false);
            MouseEnter += (_, _) => Paint(animate: false);
            MouseLeave += (_, _) => Paint(animate: false);
        }

        public bool IsCurrent
        {
            get;
            set
            {
                if (field == value) return;
                field = value;
                Paint(animate: true);
            }
        }

        void Paint(bool animate)
        {
            if (!IsLoaded) return;
            var em = FontSize;
            var duration = animate ? Motion.Duration(this, ThemeKeys.Duration.Md) : TimeSpan.Zero;
            Resize(this, (IsCurrent ? 2 : 1.25) * em, duration);
            Resize(mark, (IsCurrent ? 1.25 : 0.5) * em, duration);
            Height = Math.Max(1.5 * em, 24);
            mark.Height = 0.5 * em;
            mark.CornerRadius = new CornerRadius(0.25 * em);
            mark.SetResourceReference(Border.BackgroundProperty, IsCurrent ? ThemeKeys.Brush.Accent : IsMouseOver ? ThemeKeys.Brush.TextMuted : ThemeKeys.Brush.BorderStrong);
        }

        static void Resize(FrameworkElement element, double width, TimeSpan duration)
        {
            var from = element.ActualWidth;
            element.BeginAnimation(WidthProperty, null);
            element.Width = width;
            if (duration > TimeSpan.Zero && from > 0)
                element.BeginAnimation(WidthProperty, new DoubleAnimation(from, width, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }
    }

    /// <summary>
    /// 张片区：张片排成一行（每张宽 =（宽 − (SlidesPerView − 1) × Gap）÷ SlidesPerView），按位移 Offset 切换；能聚焦（← / → 换张）、裁掉区外的部分。
    /// 拖动（挪开 3 以内不算）跟着指针移，松开吸附到最近的一张；Shift + 滚轮换张。
    /// </summary>
    sealed class Track : Panel
    {
        /// <summary>位移：最前面露出来的位置（动画驱动）。</summary>
        public static readonly DependencyProperty OffsetProperty = DependencyProperty.Register(nameof(Offset), typeof(double), typeof(Track),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsArrange));

        readonly Carousel owner;
        Point? pressedAt;
        double pressedOffset;
        bool dragging;

        public Track(Carousel owner)
        {
            this.owner = owner;
            Focusable = true;
            ClipToBounds = true;
            Background = Brushes.Transparent;
            FocusVisualStyle = null;
            SizeChanged += (_, _) => ScrollTo(owner.Index, animate: false);
        }

        public double Offset { get => (double)GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

        double SlideWidth => Math.Max(0, (ActualWidth - (owner.SlidesPerView - 1) * owner.Gap) / owner.SlidesPerView);

        double MaxOffset => Math.Max(0, Children.Count * (SlideWidth + owner.Gap) - owner.Gap - ActualWidth);

        double OffsetOf(int index) => Math.Min(index * (SlideWidth + owner.Gap), MaxOffset);

        /// <summary>滚到第 index 张（lg 的过渡；拖动后吸附也走这里）。</summary>
        public void ScrollTo(int index, bool animate) => MoveTo(OffsetOf(index), animate && IsLoaded ? Motion.Duration(owner, ThemeKeys.Duration.Lg) : TimeSpan.Zero);

        /// <summary>停在 target：基础值先写成终点，动画只是从当前位置过去（动画结束后撤掉，拖动时直接改基础值）。</summary>
        void MoveTo(double target, TimeSpan duration)
        {
            var from = Offset;
            BeginAnimation(OffsetProperty, null);
            Offset = target;
            if (duration > TimeSpan.Zero && Math.Abs(from - target) > 0.5)
                BeginAnimation(OffsetProperty, new DoubleAnimation(from, target, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
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

        double WidthFor(double width) => Math.Max(0, (width - (owner.SlidesPerView - 1) * owner.Gap) / owner.SlidesPerView);

        protected override Size MeasureOverride(Size availableSize)
        {
            var infinite = double.IsInfinity(availableSize.Width);
            var width = infinite ? 0 : WidthFor(availableSize.Width);
            var height = 0d;
            foreach (UIElement child in Children)
            {
                child.Measure(new Size(width, availableSize.Height));
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(infinite ? 0 : availableSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = WidthFor(finalSize.Width);
            for (var index = 0; index < Children.Count; index++)
                Children[index].Arrange(new Rect(index * (width + owner.Gap) - Offset, 0, width, finalSize.Height));
            return finalSize;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            pressedAt = e.GetPosition(this);
            pressedOffset = Offset;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (pressedAt is not { } from || e.LeftButton != MouseButtonState.Pressed) return;
            var dx = e.GetPosition(this).X - from.X;
            if (!dragging)
            {
                if (Math.Abs(dx) < 3) return;
                dragging = true;
                CaptureMouse();
            }
            MoveTo(Math.Clamp(pressedOffset - dx, 0, MaxOffset), TimeSpan.Zero);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            pressedAt = null;
            if (!dragging) return;
            dragging = false;
            ReleaseMouseCapture();
            var target = Nearest();
            if (target == owner.Index) ScrollTo(target, animate: true);
            else owner.SetCurrentValue(IndexProperty, target);
            e.Handled = true;
        }

        /// <summary>WPF 的滚轮事件只有竖向：Shift + 滚轮换张（触摸板横向滑动收不到）。</summary>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || e.Delta == 0) return;
            owner.Go(owner.Index + (e.Delta < 0 ? 1 : -1));
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
