using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 滚动区域（vue-tsx-pro 的 ScrollArea）：浮层滚动条。滚动仍是平台 ScrollViewer 的（滚轮、触控板、键盘、无障碍都不变），
/// 滚动条换成半透明、浮在内容上方的细条：出现、消失都不占位，不改变内容的宽度。
/// <code>
/// &lt;rune:ScrollArea Height="320" AutomationProperties.Name="收件箱"&gt;…&lt;/rune:ScrollArea&gt;
/// &lt;rune:ScrollArea Axis="x" Type="always"&gt;…&lt;/rune:ScrollArea&gt;
/// </code>
/// 高度（或最大高度）由使用方给。滚动条在指针移到区域上或正在滚动时出现（<see cref="Type"/>），停下 <see cref="HideDelay"/> 后淡出
/// （durations.md、standard）；粗细是 0.5em 的滑块加两侧各 2 像素；拖动滑块、点击轨道翻页都是平台 ScrollBar 的。
/// 内容超出时区域可以用 Tab 聚焦（<see cref="IsViewportFocusable"/>），键盘照常滚动；键盘聚焦时里侧有焦点环。
/// 部件（<see cref="ScrollAreaParts"/>）引用部件样式键，Provider 的部件样式能改。
/// </summary>
public class ScrollArea : ScrollViewer
{
    /// <summary>可以滚动的方向，默认 y（横向超出的裁掉）。</summary>
    public static readonly DependencyProperty AxisProperty = DependencyProperty.Register(
        nameof(Axis), typeof(ScrollAreaAxis), typeof(ScrollArea), new FrameworkPropertyMetadata(ScrollAreaAxis.Y, (d, _) => ((ScrollArea)d).ApplyAxis()));

    /// <summary>滚动条什么时候显示，默认 hover。</summary>
    public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(
        nameof(Type), typeof(ScrollbarType), typeof(ScrollArea), new FrameworkPropertyMetadata(ScrollbarType.Hover, (d, _) => ((ScrollArea)d).UpdateActive()));

    /// <summary>停止滚动、指针离开后多久淡出，默认 600 毫秒。</summary>
    public static readonly DependencyProperty HideDelayProperty = DependencyProperty.Register(
        nameof(HideDelay), typeof(TimeSpan), typeof(ScrollArea), new FrameworkPropertyMetadata(TimeSpan.FromMilliseconds(600)));

    /// <summary>内容超出时区域能用 Tab 聚焦（键盘滚动），默认 true；区域里另有自己管焦点的控件（列表框、菜单）时关掉。</summary>
    public static readonly DependencyProperty IsViewportFocusableProperty = DependencyProperty.Register(
        nameof(IsViewportFocusable), typeof(bool), typeof(ScrollArea), new FrameworkPropertyMetadata(true, (d, _) => ((ScrollArea)d).UpdateOverflow()));

    static readonly DependencyPropertyKey IsOverflowingKey = DependencyProperty.RegisterReadOnly(nameof(IsOverflowing), typeof(bool), typeof(ScrollArea), new PropertyMetadata(false));

    /// <summary>内容在某个方向超出了视口。</summary>
    public static readonly DependencyProperty IsOverflowingProperty = IsOverflowingKey.DependencyProperty;

    static readonly DependencyPropertyKey IsScrollbarVisibleKey = DependencyProperty.RegisterReadOnly(nameof(IsScrollbarVisible), typeof(bool), typeof(ScrollArea), new PropertyMetadata(false));

    /// <summary>滚动条显示着（TS 的 data-state="visible"）。</summary>
    public static readonly DependencyProperty IsScrollbarVisibleProperty = IsScrollbarVisibleKey.DependencyProperty;

    static readonly DependencyPropertyKey IsFocusVisibleKey = DependencyProperty.RegisterReadOnly(nameof(IsFocusVisible), typeof(bool), typeof(ScrollArea), new PropertyMetadata(false));

    /// <summary>键盘聚焦着（焦点环显示，同 :focus-visible）。</summary>
    public static readonly DependencyProperty IsFocusVisibleProperty = IsFocusVisibleKey.DependencyProperty;

    bool scrolled;
    int hides;
    ScrollBar? vertical;
    ScrollBar? horizontal;

    static ScrollArea()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ScrollArea), new FrameworkPropertyMetadata(typeof(ScrollArea)));
        // 不能聚焦时不进 Tab 顺序；超出时由 UpdateOverflow 打开
        FocusableProperty.OverrideMetadata(typeof(ScrollArea), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(ScrollArea), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize,
            FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((ScrollArea)d).SizeBars()));
    }

    /// <summary>新建：纵向滚动。</summary>
    public ScrollArea() => ApplyAxis();

    /// <summary>可以滚动的方向。</summary>
    public ScrollAreaAxis Axis { get => (ScrollAreaAxis)GetValue(AxisProperty); set => SetValue(AxisProperty, value); }

    /// <summary>滚动条什么时候显示。</summary>
    public ScrollbarType Type { get => (ScrollbarType)GetValue(TypeProperty); set => SetValue(TypeProperty, value); }

    /// <summary>停止滚动、指针离开后多久淡出。</summary>
    public TimeSpan HideDelay { get => (TimeSpan)GetValue(HideDelayProperty); set => SetValue(HideDelayProperty, value); }

    /// <summary>内容超出时区域能用 Tab 聚焦。</summary>
    public bool IsViewportFocusable { get => (bool)GetValue(IsViewportFocusableProperty); set => SetValue(IsViewportFocusableProperty, value); }

    /// <summary>内容在某个方向超出了视口。</summary>
    public bool IsOverflowing => (bool)GetValue(IsOverflowingProperty);

    /// <summary>滚动条显示着。</summary>
    public bool IsScrollbarVisible => (bool)GetValue(IsScrollbarVisibleProperty);

    /// <summary>键盘聚焦着。</summary>
    public bool IsFocusVisible => (bool)GetValue(IsFocusVisibleProperty);

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        vertical = GetTemplateChild("PART_VerticalScrollBar") as ScrollBar;
        horizontal = GetTemplateChild("PART_HorizontalScrollBar") as ScrollBar;
        SizeBars();
        Fade(animate: false);
    }

    /// <inheritdoc />
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        base.OnScrollChanged(e);
        if (e.ExtentHeightChange != 0 || e.ExtentWidthChange != 0 || e.ViewportHeightChange != 0 || e.ViewportWidthChange != 0) UpdateOverflow();
        if (e.VerticalChange != 0 || e.HorizontalChange != 0) Reveal(scrolled: true);
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        Reveal(scrolled: false);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ScheduleHide();
    }

    /// <summary>焦点环只在键盘聚焦时显示（同 :focus-visible）。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        SetValue(IsFocusVisibleKey, e.NewFocus == this && InputManager.Current.MostRecentInputDevice is KeyboardDevice);
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        SetValue(IsFocusVisibleKey, false);
    }

    /// <summary>方向换成平台的滚动条设置：只滚的方向是 Auto，另一个方向禁用（超出的裁掉）。</summary>
    void ApplyAxis()
    {
        var axis = Axis.Value ?? "y";
        SetCurrentValue(HorizontalScrollBarVisibilityProperty, axis is "x" or "both" ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
        SetCurrentValue(VerticalScrollBarVisibilityProperty, axis is "y" or "both" ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
        SizeBars();
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

    /// <summary>内容是否超出（差一个像素以内不算）；超出时能聚焦。</summary>
    void UpdateOverflow()
    {
        var overflowing = ExtentWidth > ViewportWidth + 1 || ExtentHeight > ViewportHeight + 1;
        SetValue(IsOverflowingKey, overflowing);
        SetCurrentValue(FocusableProperty, overflowing && IsViewportFocusable);
        UpdateActive();
    }

    void Reveal(bool scrolled)
    {
        if (scrolled) this.scrolled = true;
        UpdateActive();
        ScheduleHide();
    }

    /// <summary>停下 <see cref="HideDelay"/> 后淡出（指针还在区域上、type 为 hover 时不淡出）；新的显示作废旧的等待。</summary>
    async void ScheduleHide()
    {
        var version = ++hides;
        await Task.Delay(HideDelay);
        if (version != hides) return;
        scrolled = false;
        UpdateActive();
    }

    void UpdateActive()
    {
        var type = Type.Value ?? "hover";
        var active = IsOverflowing && (type == "always" || scrolled || (type == "hover" && IsMouseOver));
        if (active == IsScrollbarVisible) return;
        SetValue(IsScrollbarVisibleKey, active);
        Fade(animate: true);
    }

    /// <summary>滚动条淡入淡出（durations.md、standard；系统关闭动画时直接到位）；隐藏时不接收指针。</summary>
    void Fade(bool animate)
    {
        var visible = IsScrollbarVisible;
        var duration = animate ? Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200)) : TimeSpan.Zero;
        foreach (var bar in new[] { vertical, horizontal })
        {
            if (bar is null) continue;
            bar.IsHitTestVisible = visible;
            bar.Opacity = visible ? 1 : 0;
            bar.BeginAnimation(OpacityProperty, duration <= TimeSpan.Zero ? null
                : new DoubleAnimation(visible ? 1 : 0, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction, FillBehavior = FillBehavior.Stop });
        }
    }
}

/// <summary>滚动区域的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:ScrollAreaParts.Thumb}" TargetType="Thumb"&gt;</c>。</summary>
public static class ScrollAreaParts
{
    /// <summary>滚动条的轨道（两个方向都用它，TS 的 propsScrollbar）。</summary>
    public static readonly PartKey Scrollbar = new(typeof(ScrollArea), nameof(Scrollbar), typeof(ScrollBar));

    /// <summary>滑块（TS 的 propsThumb）。</summary>
    public static readonly PartKey Thumb = new(typeof(ScrollArea), nameof(Thumb), typeof(Thumb));
}
