using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 滚动区域（vue-tsx-pro 的 ScrollArea）：浮层滚动条。滚动仍是平台 ScrollViewer 的（滚轮、触控板、键盘、无障碍都不变），
/// 滚动条换成半透明、浮在内容上方的细条：出现、消失都不占位，不改变内容的宽度。
/// <code>
/// &lt;rune:ScrollArea Height="320" AutomationProperties.Name="收件箱"&gt;…&lt;/rune:ScrollArea&gt;
/// &lt;rune:ScrollArea Axis="x" Type="always"&gt;…&lt;/rune:ScrollArea&gt;
/// </code>
/// 高度（或最大高度）由使用方给。滚动条在指针移到区域上或正在滚动时出现（<see cref="Type"/>），停下 <see cref="HideDelay"/> 后淡出；
/// 粗细是 0.5em 的滑块加两侧各 2 像素，滑块至少 20 像素长；拖动滑块、点击轨道翻页都是平台 ScrollBar 的。
/// 内容超出时区域可以用 Tab 聚焦（<see cref="IsViewportFocusable"/>），键盘照常滚动；聚焦时里侧有焦点环。
/// 伪类：<c>:active</c>（滚动条显示着）、<c>:overflowing</c>（内容超出）、<c>:both</c>（两个方向都能滚）。
/// </summary>
[PseudoClasses(":active", ":overflowing", ":both")]
public class ScrollArea : ScrollViewer
{
    /// <summary>可以滚动的方向，默认 y（横向超出的裁掉）。</summary>
    public static readonly StyledProperty<ScrollAreaAxis> AxisProperty = AvaloniaProperty.Register<ScrollArea, ScrollAreaAxis>(nameof(Axis), ScrollAreaAxis.Y);

    /// <summary>滚动条什么时候显示，默认 hover。</summary>
    public static readonly StyledProperty<ScrollbarType> TypeProperty = AvaloniaProperty.Register<ScrollArea, ScrollbarType>(nameof(Type), ScrollbarType.Hover);

    /// <summary>停止滚动、指针离开后多久淡出，默认 600 毫秒。</summary>
    public static readonly StyledProperty<TimeSpan> HideDelayProperty = AvaloniaProperty.Register<ScrollArea, TimeSpan>(nameof(HideDelay), TimeSpan.FromMilliseconds(600));

    /// <summary>内容超出时区域能用 Tab 聚焦（键盘滚动），默认 true；区域里另有自己管焦点的控件（列表框、菜单）时关掉。</summary>
    public static readonly StyledProperty<bool> IsViewportFocusableProperty = AvaloniaProperty.Register<ScrollArea, bool>(nameof(IsViewportFocusable), true);

    /// <summary>内容在某个方向超出了视口。</summary>
    public static readonly DirectProperty<ScrollArea, bool> IsOverflowingProperty = AvaloniaProperty.RegisterDirect<ScrollArea, bool>(nameof(IsOverflowing), area => area.IsOverflowing);

    bool scrolled;
    int hides;
    ScrollBar? vertical;
    ScrollBar? horizontal;

    static ScrollArea()
    {
        // 不能聚焦时不进 Tab 顺序；超出时由 UpdateOverflow 打开
        FocusableProperty.OverrideDefaultValue<ScrollArea>(false);
        AffectsMeasure<ScrollArea>(AxisProperty);
    }

    /// <summary>新建：纵向滚动。</summary>
    public ScrollArea() => ApplyAxis();

    /// <summary>可以滚动的方向。</summary>
    public ScrollAreaAxis Axis { get => GetValue(AxisProperty); set => SetValue(AxisProperty, value); }

    /// <summary>滚动条什么时候显示。</summary>
    public ScrollbarType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }

    /// <summary>停止滚动、指针离开后多久淡出。</summary>
    public TimeSpan HideDelay { get => GetValue(HideDelayProperty); set => SetValue(HideDelayProperty, value); }

    /// <summary>内容超出时区域能用 Tab 聚焦。</summary>
    public bool IsViewportFocusable { get => GetValue(IsViewportFocusableProperty); set => SetValue(IsViewportFocusableProperty, value); }

    /// <summary>内容在某个方向超出了视口。</summary>
    public bool IsOverflowing { get; private set => SetAndRaise(IsOverflowingProperty, ref field, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ScrollArea);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AxisProperty) ApplyAxis();
        else if (change.Property == FontSizeProperty) SizeBars();
        else if (change.Property == ExtentProperty || change.Property == ViewportProperty || change.Property == IsViewportFocusableProperty) UpdateOverflow();
        else if (change.Property == OffsetProperty) Reveal(scrolled: true);
        else if (change.Property == TypeProperty) UpdateActive();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        vertical = e.NameScope.Find<ScrollBar>(ScrollAreaParts.VerticalScrollbar);
        horizontal = e.NameScope.Find<ScrollBar>(ScrollAreaParts.HorizontalScrollbar);
        SizeBars();
        var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
        var easing = this.Resource<Easing>(ThemeKeys.Easing.Standard);
        foreach (var name in new[] { ScrollAreaParts.VerticalScrollbar, ScrollAreaParts.HorizontalScrollbar })
            if (e.NameScope.Find<ScrollBar>(name) is { } bar && duration > TimeSpan.Zero)
                bar.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = duration, Easing = easing }];
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Reveal(scrolled: false);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ScheduleHide();
    }

    /// <summary>
    /// 滚动条的粗细：0.5em（space.sm）的滑块加两侧各 2 像素（borderWidths.sm）；两个方向都有时各自让出右下角，两条不重叠。
    /// </summary>
    void SizeBars()
    {
        var thickness = 0.5 * FontSize + 4;
        var both = Axis.Value == "both";
        if (vertical is not null)
        {
            vertical.Width = thickness;
            vertical.Margin = new Thickness(0, 0, 0, both ? thickness : 0);
        }
        if (horizontal is not null)
        {
            horizontal.Height = thickness;
            horizontal.Margin = new Thickness(0, 0, both ? thickness : 0, 0);
        }
    }

    /// <summary>方向换成平台的滚动条设置：只滚的方向是 Auto，另一个方向禁用（超出的裁掉）。</summary>
    void ApplyAxis()
    {
        var axis = Axis.Value ?? "y";
        SetCurrentValue(HorizontalScrollBarVisibilityProperty, axis is "x" or "both" ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
        SetCurrentValue(VerticalScrollBarVisibilityProperty, axis is "y" or "both" ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
        PseudoClasses.Set(":both", axis == "both");
        SizeBars();
    }

    /// <summary>内容是否超出（差一个像素以内不算）；超出时能聚焦。</summary>
    void UpdateOverflow()
    {
        IsOverflowing = Extent.Width > Viewport.Width + 1 || Extent.Height > Viewport.Height + 1;
        PseudoClasses.Set(":overflowing", IsOverflowing);
        SetCurrentValue(FocusableProperty, IsOverflowing && IsViewportFocusable);
        UpdateActive();
    }

    /// <summary>滚动或指针进入：显示一阵（scroll 型只认滚动），停下后按 <see cref="HideDelay"/> 淡出。</summary>
    void Reveal(bool scrolled)
    {
        if (scrolled) this.scrolled = true;
        UpdateActive();
        ScheduleHide();
    }

    /// <summary>
    /// 停下 <see cref="HideDelay"/> 后淡出（指针还在区域上、type 为 hover 时不淡出）。等待用 Task.Delay 再回到 UI 线程
    /// （headless 测试的调度器不走 DispatcherTimer）；新的显示作废旧的等待。
    /// </summary>
    void ScheduleHide()
    {
        var version = ++hides;
        _ = Task.Delay(HideDelay).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            if (version != hides) return;
            scrolled = false;
            UpdateActive();
        }), TaskScheduler.Default);
    }

    void UpdateActive()
    {
        var type = Type.Value ?? "hover";
        var active = IsOverflowing && (type == "always" || scrolled || (type == "hover" && IsPointerOver));
        PseudoClasses.Set(":active", active);
    }
}

/// <summary>滚动区域的部件名：样式里用 <c>rune|ScrollArea /template/ ScrollBar#PART_VerticalScrollBar</c> 选中（TS 的 propsScrollbar、propsThumb）。</summary>
public static class ScrollAreaParts
{
    /// <summary>视口（真正滚动的那一层，TS 的 propsViewport）。</summary>
    public const string Viewport = "PART_ContentPresenter";

    /// <summary>纵向滚动条的轨道。</summary>
    public const string VerticalScrollbar = "PART_VerticalScrollBar";

    /// <summary>横向滚动条的轨道。</summary>
    public const string HorizontalScrollbar = "PART_HorizontalScrollBar";

    /// <summary>滑块（两个方向都叫这个名字）。</summary>
    public const string Thumb = "PART_Thumb";
}
