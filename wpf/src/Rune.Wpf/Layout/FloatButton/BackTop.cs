using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 回到顶部（vue-tsx-pro 的 BackTop）：长内容滚下去之后，角落出现一个按钮，点了平滑滚回顶部（系统关闭动画时直接跳回）。
/// <code>
/// &lt;Grid&gt;&lt;rune:ScrollArea x:Name="Page"&gt;…&lt;/rune:ScrollArea&gt;&lt;rune:BackTop Target="{Binding ElementName=Page}" Threshold="200" /&gt;&lt;/Grid&gt;
/// </code>
/// 是一个 <see cref="FloatButton"/>（向上的箭头，名称与提示取语言包“回到顶部”）。滚动的容器是 <see cref="Target"/>；不写时是外层最近的 ScrollViewer，
/// 再没有时是窗口里的第一个（TS 的整页）。滚过 <see cref="Threshold"/>（默认 400）才淡入，没出现时不能点、读屏看不到；在 <see cref="FloatButtonGroup"/> 里没出现时不占位。
/// 滚回去之后焦点移到容器开头第一个能聚焦的元素，键盘用户不用再从按钮的位置往回 Tab。
/// </summary>
public class BackTop : FloatButton
{
    /// <summary>滚动的容器；不写时找外层最近的，再没有时是窗口里的第一个。</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(nameof(Target), typeof(ScrollViewer), typeof(BackTop),
        new FrameworkPropertyMetadata(null, (d, _) => ((BackTop)d).Attach()));

    /// <summary>滚过多少后出现，默认 400。</summary>
    public static readonly DependencyProperty ThresholdProperty = DependencyProperty.Register(nameof(Threshold), typeof(double), typeof(BackTop),
        new FrameworkPropertyMetadata(400d, (d, _) => ((BackTop)d).UpdateShown()));

    /// <summary>滚回顶部的进度：0 在开始的位置，1 到顶（动画驱动）。</summary>
    static readonly DependencyProperty ProgressProperty = DependencyProperty.Register("Progress", typeof(double), typeof(BackTop),
        new FrameworkPropertyMetadata(0d, (d, e) => ((BackTop)d).OnProgress((double)e.NewValue)));

    ScrollViewer? scroller;
    double from;
    bool scrolling;

    /// <summary>新建：向上的箭头，没出现。</summary>
    public BackTop()
    {
        Icon = Lucide.ArrowUp;
        Click += (_, _) => ScrollToTop();
        Loaded += (_, _) =>
        {
            SyncTip();
            // 窗口里的第一个滚动容器要等内容排好才找得到
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Attach);
        };
        Unloaded += (_, _) => Listen(null);
        UpdateShown();
    }

    /// <summary>滚动的容器。</summary>
    public ScrollViewer? Target { get => (ScrollViewer?)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>滚过多少后出现。</summary>
    public double Threshold { get => (double)GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }

    /// <summary>出现着（滚过了 Threshold）。</summary>
    public bool IsShown { get; private set; }

    /// <summary>名称默认取语言包。</summary>
    private protected override void SyncTip()
    {
        if (string.IsNullOrEmpty(AutomationProperties.GetName(this)) && IsLoaded) AutomationProperties.SetName(this, Provider.GetLang(this).BackToTop);
        else base.SyncTip();
    }

    void Attach()
    {
        if (!IsLoaded) return;
        Listen(Target ?? Ancestor() ?? (Window.GetWindow(this) is { } window ? First(window) : null));
    }

    ScrollViewer? Ancestor()
    {
        for (var node = VisualTreeHelper.GetParent(this); node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is ScrollViewer viewer) return viewer;
        return null;
    }

    static ScrollViewer? First(DependencyObject parent) => WalkFirst<ScrollViewer>(parent, _ => true);

    static T? WalkFirst<T>(DependencyObject parent, Func<T, bool> match) where T : class
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T found && match(found)) return found;
            if (WalkFirst(child, match) is { } nested) return nested;
        }
        return null;
    }

    void Listen(ScrollViewer? next)
    {
        if (scroller == next) return;
        if (scroller is not null) scroller.ScrollChanged -= OnScrolled;
        scroller = next;
        if (scroller is not null) scroller.ScrollChanged += OnScrolled;
        UpdateShown();
    }

    void OnScrolled(object sender, ScrollChangedEventArgs e) => UpdateShown();

    /// <summary>滚过了就出现。单独用时淡入淡出（md），没出现时不能点、读屏看不到；在组里没出现时不占位。</summary>
    void UpdateShown()
    {
        var shown = (scroller?.VerticalOffset ?? 0) > Threshold;
        IsShown = shown;
        var grouped = Grouped;
        Visibility = !grouped || shown ? Visibility.Visible : Visibility.Collapsed;
        IsHitTestVisible = shown;
        Button.IsEnabled = shown;
        // 没出现时读屏看不到（TS 的 aria-hidden）：按钮不进读屏的树
        AutomationProperties.SetIsOffscreenBehavior(Button, shown ? IsOffscreenBehavior.Default : IsOffscreenBehavior.Offscreen);
        var target = grouped || shown ? 1d : 0d;
        var duration = grouped ? TimeSpan.Zero : Motion.Duration(this, ThemeKeys.Duration.Md);
        BeginAnimation(OpacityProperty, duration > TimeSpan.Zero ? new DoubleAnimation(target, duration) : null);
        Opacity = target;
    }

    /// <summary>平滑滚回顶部（lg；系统关闭动画时直接跳回），焦点移到容器开头。</summary>
    void ScrollToTop()
    {
        if (scroller is not { } target) return;
        from = target.VerticalOffset;
        var duration = Motion.Duration(this, ThemeKeys.Duration.Lg);
        if (duration <= TimeSpan.Zero)
        {
            target.ScrollToVerticalOffset(0);
            FocusStart(target);
        }
        else
        {
            // 动画停下后进度回到基础值 0：只在播放期间跟着滚，结束时直接到顶
            var animation = new DoubleAnimation(0, 1, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop };
            animation.Completed += (_, _) =>
            {
                scrolling = false;
                target.ScrollToVerticalOffset(0);
                FocusStart(target);
            };
            scrolling = true;
            BeginAnimation(ProgressProperty, animation);
        }
    }

    /// <summary>滚到顶之后焦点移到开头第一个能聚焦的元素（先聚焦的话，聚焦把它滚进视野，就跳过了平滑滚动）。</summary>
    static void FocusStart(ScrollViewer target) =>
        WalkFirst<UIElement>(target, element => element.Focusable && element.IsEnabled && element.IsVisible && element is not ScrollViewer and not System.Windows.Controls.Primitives.ScrollBar)?.Focus();

    void OnProgress(double progress)
    {
        if (scrolling) scroller?.ScrollToVerticalOffset(from * (1 - progress));
    }
}
