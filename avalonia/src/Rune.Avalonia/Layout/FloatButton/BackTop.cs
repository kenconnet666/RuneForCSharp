using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 回到顶部（vue-tsx-pro 的 BackTop）：长内容滚下去之后，角落出现一个按钮，点了平滑滚回顶部（系统关闭动画时直接跳回）。
/// <code>
/// &lt;Panel&gt;&lt;rune:ScrollArea x:Name="Page"&gt;…&lt;/rune:ScrollArea&gt;&lt;rune:BackTop Target="{Binding #Page}" Threshold="200" /&gt;&lt;/Panel&gt;
/// </code>
/// 是一个 <see cref="FloatButton"/>（向上的箭头，名称与提示取语言包“回到顶部”）。滚动的容器是 <see cref="Target"/>；不写时是外层最近的 ScrollViewer，
/// 再没有时是窗口里的第一个（TS 的整页）。滚过 <see cref="Threshold"/>（默认 400）才淡入，没出现时不能点、读屏看不到；在 <see cref="FloatButtonGroup"/> 里没出现时不占位。
/// 滚回去之后焦点移到容器开头第一个能聚焦的元素，键盘用户不用再从按钮的位置往回 Tab。
/// </summary>
public class BackTop : FloatButton
{
    /// <summary>滚动的容器；不写时找外层最近的，再没有时是窗口里的第一个。</summary>
    public static readonly StyledProperty<ScrollViewer?> TargetProperty = AvaloniaProperty.Register<BackTop, ScrollViewer?>(nameof(Target));

    /// <summary>滚过多少后出现，默认 400。</summary>
    public static readonly StyledProperty<double> ThresholdProperty = AvaloniaProperty.Register<BackTop, double>(nameof(Threshold), 400);

    /// <summary>滚回顶部的进度：0 在开始的位置，1 到顶（动画驱动）。</summary>
    static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<BackTop, double>("Progress");

    ScrollViewer? scroller;
    double from;
    bool scrolling;

    /// <summary>新建：向上的箭头，没出现。</summary>
    public BackTop()
    {
        Icon = Lucide.ArrowUp;
        Click += (_, _) => _ = ScrollToTop();
        UpdateShown();
    }

    /// <summary>滚动的容器。</summary>
    public ScrollViewer? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>滚过多少后出现。</summary>
    public double Threshold { get => GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }

    /// <summary>出现着（滚过了 Threshold）。</summary>
    public bool IsShown { get; private set; }

    bool Grouped => this.FindLogicalAncestorOfType<FloatButtonGroup>() is not null;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncTip();
        // 窗口里的第一个滚动容器要等内容排好才找得到
        Dispatcher.UIThread.Post(Attach, DispatcherPriority.Loaded);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Listen(null);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetProperty && VisualRoot is not null) Attach();
        else if (change.Property == ThresholdProperty) UpdateShown();
        else if (change.Property == ProgressProperty && scrolling && scroller is not null) scroller.Offset = scroller.Offset.WithY(from * (1 - change.GetNewValue<double>()));
    }

    /// <summary>名称默认取语言包。</summary>
    private protected override void SyncTip()
    {
        if (AutomationProperties.GetName(this) is null && VisualRoot is not null) AutomationProperties.SetName(this, Provider.GetLang(this).BackToTop);
        base.SyncTip();
    }

    void Attach() => Listen(Target ?? this.FindAncestorOfType<ScrollViewer>()
        ?? (TopLevel.GetTopLevel(this) is { } top ? top.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() : null));

    void Listen(ScrollViewer? next)
    {
        if (scroller == next) return;
        if (scroller is not null) scroller.ScrollChanged -= OnScrolled;
        scroller = next;
        if (scroller is not null) scroller.ScrollChanged += OnScrolled;
        UpdateShown();
    }

    void OnScrolled(object? sender, ScrollChangedEventArgs e) => UpdateShown();

    /// <summary>
    /// 滚过了就出现。单独用时淡入淡出（md），没出现时不能点、读屏看不到；在组里没出现时不占位。
    /// </summary>
    void UpdateShown()
    {
        var shown = (scroller?.Offset.Y ?? 0) > Threshold;
        IsShown = shown;
        PseudoClasses.Set(":shown", shown);
        var grouped = Grouped;
        IsVisible = !grouped || shown;
        IsHitTestVisible = shown;
        Button.IsEnabled = shown;
        AutomationProperties.SetAccessibilityView(Button, shown ? AccessibilityView.Default : AccessibilityView.Raw);
        if (grouped)
        {
            Opacity = 1;
            return;
        }
        var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
        Transitions = duration > TimeSpan.Zero ? [new DoubleTransition { Property = OpacityProperty, Duration = duration }] : null;
        Opacity = shown ? 1 : 0;
    }

    /// <summary>平滑滚回顶部（lg；系统关闭动画时直接跳回），焦点移到容器开头。</summary>
    async Task ScrollToTop()
    {
        if (scroller is not { } target) return;
        from = target.Offset.Y;
        var duration = Motion.Duration(this, ThemeKeys.Duration.Lg);
        if (duration > TimeSpan.Zero)
        {
            // 动画结束后进度回到基础值 0：只在播放期间跟着滚，结束时直接到顶
            scrolling = true;
            await new Animation
            {
                Duration = duration,
                Easing = this.Resource<global::Avalonia.Animation.Easings.Easing>(ThemeKeys.Easing.Standard),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new global::Avalonia.Styling.Setter(ProgressProperty, 0d) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new global::Avalonia.Styling.Setter(ProgressProperty, 1d) } },
                },
            }.RunAsync(this);
            scrolling = false;
        }
        target.Offset = target.Offset.WithY(0);
        // 滚到顶之后再聚焦：聚焦会把元素滚进视野，先聚焦就跳过了平滑滚动
        var first = target.GetVisualDescendants().OfType<InputElement>()
            .FirstOrDefault(element => element.Focusable && element.IsEffectivelyEnabled && element.IsEffectivelyVisible && element != target);
        (first ?? (target.Focusable ? target : null))?.Focus(NavigationMethod.Tab);
    }
}
