using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Rune.Avalonia;

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
public class Marquee : Control
{
    /// <summary>滚动方向，默认 left。</summary>
    public static readonly StyledProperty<MarqueeDirection> DirectionProperty = AvaloniaProperty.Register<Marquee, MarqueeDirection>(nameof(Direction));

    /// <summary>速度：每秒多少像素，默认 40。</summary>
    public static readonly StyledProperty<double> SpeedProperty = AvaloniaProperty.Register<Marquee, double>(nameof(Speed), MarqueeMotion.Speed);

    /// <summary>指针悬停、焦点在里面时暂停，默认 true。</summary>
    public static readonly StyledProperty<bool> PauseOnHoverProperty = AvaloniaProperty.Register<Marquee, bool>(nameof(PauseOnHover), true);

    /// <summary>暂停。</summary>
    public static readonly StyledProperty<bool> IsPausedProperty = AvaloniaProperty.Register<Marquee, bool>(nameof(IsPaused));

    /// <summary>两端渐隐。</summary>
    public static readonly StyledProperty<bool> FadeProperty = AvaloniaProperty.Register<Marquee, bool>(nameof(Fade));

    /// <summary>项之间（以及两份之间）的间距，默认 lg。</summary>
    public static readonly StyledProperty<Space> GapProperty = AvaloniaProperty.Register<Marquee, Space>(nameof(Gap), Space.Lg);

    readonly ScrollArea scroller = new() { HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden };
    readonly StackPanel track = new();
    readonly Flex group = new() { Align = FlexAlign.Center };
    readonly Border copy = new() { IsHitTestVisible = false };
    readonly TranslateTransform shift = new();
    double distance;
    TimeSpan? last;
    bool ticking;

    static Marquee()
    {
        AffectsMeasure<Marquee>(DirectionProperty, GapProperty, TextElement.FontSizeProperty);
        ClipToBoundsProperty.OverrideDefaultValue<Marquee>(true);
    }

    /// <summary>新建：内容一份、后面接一份画出来的复制。</summary>
    public Marquee()
    {
        AutomationProperties.SetAccessibilityView(copy, AccessibilityView.Raw);
        copy.Background = new VisualBrush(group) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
        track.Children.Add(group);
        track.Children.Add(copy);
        track.RenderTransform = shift;
        scroller.Content = track;
        LogicalChildren.Add(scroller);
        VisualChildren.Add(scroller);
        group.LayoutUpdated += (_, _) => FitCopy();
    }

    /// <summary>内容（一项一项横排或竖排）。</summary>
    [Content]
    public global::Avalonia.Controls.Controls Children => group.Children;

    /// <summary>滚动方向。</summary>
    public MarqueeDirection Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

    /// <summary>速度（像素 / 秒）。</summary>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <summary>指针悬停、焦点在里面时暂停。</summary>
    public bool PauseOnHover { get => GetValue(PauseOnHoverProperty); set => SetValue(PauseOnHoverProperty, value); }

    /// <summary>暂停。</summary>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <summary>两端渐隐。</summary>
    public bool Fade { get => GetValue(FadeProperty); set => SetValue(FadeProperty, value); }

    /// <summary>项之间的间距。</summary>
    public Space Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>一份内容（含它后面的间距）的长度。</summary>
    internal double Length => (Direction.IsVertical ? group.Bounds.Height : group.Bounds.Width) + track.Spacing;

    /// <summary>第一份内容现在的位移（测试用）。</summary>
    internal double Offset => Direction.IsVertical ? shift.Y : shift.X;

    /// <summary>复制的那份（测试用）。</summary>
    internal Control Copy => copy;

    /// <summary>在动：挂在界面上、系统允许动画、没暂停，悬停或焦点在里面时停下。</summary>
    internal bool Running => VisualRoot is not null && Motion.Enabled && !IsPaused
        && !(PauseOnHover && (IsPointerOver || IsKeyboardFocusWithin));

    /// <summary>往前走一段（测试里不等帧）。</summary>
    internal void Advance(TimeSpan elapsed)
    {
        if (Running) distance += elapsed.TotalSeconds * Speed;
        Place();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DirectionProperty || change.Property == GapProperty || change.Property == TextElement.FontSizeProperty) Configure();
        else if (change.Property == FadeProperty || change.Property == BoundsProperty) ApplyFade();
        if (change.Property == IsPausedProperty || change.Property == IsPointerOverProperty || change.Property == IsKeyboardFocusWithinProperty || change.Property == PauseOnHoverProperty)
            Tick();
    }

    /// <summary>方向与间距：竖着滚时内容竖排；系统关闭动画时不复制、改成可以手动滚动。</summary>
    void Configure()
    {
        var vertical = Direction.IsVertical;
        var gap = Gap.ResourceKey is { } key ? this.Resource<double>(key) * TextElement.GetFontSize(this) : 0;
        group.Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        group.Gap = Gap;
        track.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        track.Spacing = gap;
        var still = !Motion.Enabled;
        copy.IsVisible = !still;
        Focusable = still;
        var bar = still ? global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto : global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        (scroller.HorizontalScrollBarVisibility, scroller.VerticalScrollBarVisibility) = vertical
            ? (global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, bar)
            : (bar, global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        Place();
    }

    /// <summary>复制的那份与第一份一样大（VisualBrush 按第一份画）。</summary>
    void FitCopy()
    {
        var size = group.Bounds.Size;
        if (copy.Width == size.Width && copy.Height == size.Height) return;
        (copy.Width, copy.Height) = (size.Width, size.Height);
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
        var vertical = Direction.IsVertical;
        OpacityMask = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = vertical ? new RelativePoint(0, 1, RelativeUnit.Relative) : new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, 0.1), new GradientStop(Colors.Black, 0.9), new GradientStop(Colors.Transparent, 1) },
        };
    }

    /// <summary>在动时每帧往前走；停下时不再要帧，再动时从这一帧接着走（暂停的那段不算）。</summary>
    void Tick()
    {
        if (!Running || ticking || TopLevel.GetTopLevel(this) is not { } top) return;
        ticking = true;
        last = null;
        top.RequestAnimationFrame(Frame);
    }

    void Frame(TimeSpan now)
    {
        ticking = false;
        if (!Running) return;
        if (last is { } previous) Advance(now - previous);
        last = now;
        ticking = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Frame);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Configure();
        ApplyFade();
        Tick();
    }

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

    /// <summary>读屏：一组（UIA 没有 marquee 这种控件类型）。</summary>
    sealed class MarqueePeer(Marquee owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Marquee);
    }
}
