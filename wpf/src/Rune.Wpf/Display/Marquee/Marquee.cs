using System.Collections;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 跑马灯（vue-tsx-pro 的 Marquee）：内容循环滚动（合作伙伴 logo、公告、标签墙）。内容后面接一份复制，平移到正好一份的长度时与开头重合，无缝循环。
/// <code>
/// &lt;rune:Marquee Speed="40" AutomationProperties.Name="合作伙伴"&gt;
///   &lt;Image Source="…" /&gt; &lt;Image Source="…" /&gt;
/// &lt;/rune:Marquee&gt;
/// </code>
/// <list type="bullet">
/// <item>方向 left / right / up / down；<see cref="Speed"/> 是每秒多少像素，内容变了长度跟着量。</item>
/// <item>指针悬停、焦点在里面时暂停（<see cref="PauseOnHover"/>）；<see cref="IsPaused"/> 由外面控制（WCAG 2.2.2：给用户一个暂停按钮）。</item>
/// <item>系统关闭动画时不滚动，内容可以手动滚动查看（能聚焦，键盘也能滚）。</item>
/// <item>复制的那份是画出来的（VisualBrush）：点不到、读屏里没有，读屏只念一遍。项之间与两份之间的间距都是 <see cref="Gap"/>。</item>
/// </list>
/// 读屏里是一组（UIA 没有 marquee），用 AutomationProperties.Name 命名。
/// </summary>
[ContentProperty(nameof(Children))]
public class Marquee : FrameworkElement
{
    /// <summary>滚动方向，默认 left。</summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction), typeof(MarqueeDirection), typeof(Marquee), new PropertyMetadata(default(MarqueeDirection), (d, _) => ((Marquee)d).Configure()));

    /// <summary>速度：每秒多少像素，默认 40。</summary>
    public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register(nameof(Speed), typeof(double), typeof(Marquee), new PropertyMetadata(MarqueeMotion.Speed));

    /// <summary>指针悬停、焦点在里面时暂停，默认 true。</summary>
    public static readonly DependencyProperty PauseOnHoverProperty = DependencyProperty.Register(
        nameof(PauseOnHover), typeof(bool), typeof(Marquee), new PropertyMetadata(true, (d, _) => ((Marquee)d).Tick()));

    /// <summary>暂停。</summary>
    public static readonly DependencyProperty IsPausedProperty = DependencyProperty.Register(
        nameof(IsPaused), typeof(bool), typeof(Marquee), new PropertyMetadata(false, (d, _) => ((Marquee)d).Tick()));

    /// <summary>两端渐隐。</summary>
    public static readonly DependencyProperty FadeProperty = DependencyProperty.Register(
        nameof(Fade), typeof(bool), typeof(Marquee), new PropertyMetadata(false, (d, _) => ((Marquee)d).ApplyFade()));

    /// <summary>项之间（以及两份之间）的间距，默认 lg。</summary>
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(Space), typeof(Marquee), new PropertyMetadata(Space.Lg, (d, _) => ((Marquee)d).Configure()));

    readonly ScrollArea scroller = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Focusable = false };
    readonly StackPanel track = new();
    readonly Flex group = new() { Align = FlexAlign.Center };
    readonly Border copy = new() { IsHitTestVisible = false };
    readonly TranslateTransform shift = new();
    double distance;
    TimeSpan? last;
    bool ticking;

    static Marquee()
    {
        ClipToBoundsProperty.OverrideMetadata(typeof(Marquee), new PropertyMetadata(true));
        TextElement.FontSizeProperty.OverrideMetadata(typeof(Marquee), new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((Marquee)d).Configure()));
    }

    /// <summary>新建：内容一份、后面接一份画出来的复制。</summary>
    public Marquee()
    {
        copy.Background = new VisualBrush(group) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
        track.Children.Add(group);
        track.Children.Add(copy);
        track.RenderTransform = shift;
        scroller.Content = track;
        AddLogicalChild(scroller);
        AddVisualChild(scroller);
        group.SizeChanged += (_, _) => (copy.Width, copy.Height) = (group.ActualWidth, group.ActualHeight);
        Loaded += (_, _) =>
        {
            Configure();
            ApplyFade();
            Tick();
        };
        Unloaded += (_, _) => Stop();
        MouseEnter += (_, _) => Tick();
        MouseLeave += (_, _) => Tick();
        IsKeyboardFocusWithinChanged += (_, _) => Tick();
    }

    /// <summary>内容（一项一项横排或竖排）。</summary>
    public UIElementCollection Children => group.Children;

    /// <summary>滚动方向。</summary>
    public MarqueeDirection Direction { get => (MarqueeDirection)GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

    /// <summary>速度（像素 / 秒）。</summary>
    public double Speed { get => (double)GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <summary>指针悬停、焦点在里面时暂停。</summary>
    public bool PauseOnHover { get => (bool)GetValue(PauseOnHoverProperty); set => SetValue(PauseOnHoverProperty, value); }

    /// <summary>暂停。</summary>
    public bool IsPaused { get => (bool)GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <summary>两端渐隐。</summary>
    public bool Fade { get => (bool)GetValue(FadeProperty); set => SetValue(FadeProperty, value); }

    /// <summary>项之间的间距。</summary>
    public Space Gap { get => (Space)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>一份内容（含它后面的间距）的长度。</summary>
    internal double Length => (Direction.IsVertical ? group.ActualHeight : group.ActualWidth) + Spacing;

    /// <summary>第一份内容现在的位移（测试用）。</summary>
    internal double Offset => Direction.IsVertical ? shift.Y : shift.X;

    /// <summary>复制的那份（测试用）。</summary>
    internal FrameworkElement Copy => copy;

    /// <summary>在动：加载着、系统允许动画、没暂停，悬停或焦点在里面时停下。</summary>
    internal bool Running => IsLoaded && Motion.Enabled && !IsPaused && !(PauseOnHover && (IsMouseOver || IsKeyboardFocusWithin));

    /// <summary>两份之间的间距（像素）：StackPanel 没有间距，用复制那份的起始外边距。</summary>
    double Spacing => Gap.ResourceKey is { } key ? this.Resource<double>(key) * TextElement.GetFontSize(this) : 0;

    /// <summary>往前走一段（测试里不等帧）。</summary>
    internal void Advance(TimeSpan elapsed)
    {
        if (Running) distance += elapsed.TotalSeconds * Speed;
        Place();
    }

    /// <summary>方向与间距：竖着滚时内容竖排；系统关闭动画时不复制、改成可以手动滚动。</summary>
    void Configure()
    {
        var vertical = Direction.IsVertical;
        var gap = Spacing;
        group.Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        group.Gap = Gap;
        track.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        copy.Margin = vertical ? new Thickness(0, gap, 0, 0) : new Thickness(gap, 0, 0, 0);
        var still = !Motion.Enabled;
        copy.Visibility = still ? Visibility.Collapsed : Visibility.Visible;
        Focusable = still;
        var bar = still ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;
        (scroller.HorizontalScrollBarVisibility, scroller.VerticalScrollBarVisibility) = vertical ? (ScrollBarVisibility.Disabled, bar) : (bar, ScrollBarVisibility.Disabled);
        ApplyFade();
        Place();
    }

    void Place()
    {
        var offset = MarqueeMotion.Offset(distance, Length, Direction.IsReverse);
        (shift.X, shift.Y) = Direction.IsVertical ? (0d, offset) : (offset, 0d);
    }

    /// <summary>两端渐隐：起止各 10% 由透明过渡到不透明。</summary>
    void ApplyFade()
    {
        if (!Fade)
        {
            OpacityMask = null;
            return;
        }
        var end = Direction.IsVertical ? new Point(0, 1) : new Point(1, 0);
        OpacityMask = new LinearGradientBrush(
            [new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, 0.1), new GradientStop(Colors.Black, 0.9), new GradientStop(Colors.Transparent, 1)],
            new Point(0, 0), end);
    }

    /// <summary>在动时每帧往前走；停下时不再接帧，再动时从这一帧接着走（暂停的那段不算）。</summary>
    void Tick()
    {
        if (!Running)
        {
            Stop();
            return;
        }
        if (ticking) return;
        ticking = true;
        last = null;
        CompositionTarget.Rendering += Frame;
    }

    void Stop()
    {
        if (!ticking) return;
        ticking = false;
        CompositionTarget.Rendering -= Frame;
    }

    void Frame(object? sender, EventArgs e)
    {
        if (!Running)
        {
            Stop();
            return;
        }
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (last is { } previous && now > previous) Advance(now - previous);
        last = now;
    }

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { scroller }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => scroller;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        scroller.Measure(availableSize);
        return scroller.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        scroller.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new MarqueePeer(this);

    /// <summary>读屏：一组（UIA 没有 marquee 这种控件类型）；复制的那份是画出来的，读屏里没有。</summary>
    sealed class MarqueePeer(Marquee owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Marquee);
    }
}
