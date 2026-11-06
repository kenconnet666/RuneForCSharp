using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 背景色的过渡（WPF 没有 CSS 那样的属性过渡）：Border 的 Background 换了，就从当前显示的颜色插值到新颜色。
/// 用在组件模板的 Border 上，档位、悬停这些触发器换背景时自动过渡：
/// <code>&lt;Border Background="{TemplateBinding Background}" rune:BrushTransition.Enabled="True" /&gt;</code>
/// 时长在开始时从元素所在作用域查（<see cref="DurationKeyProperty"/>），Provider 可以改时长或设成 0 关掉。
/// 实现上的两个要点（docs/DESIGN.md）：
/// 关键帧里的画刷会被冻结成副本、颜色动画传不过去，所以用 SetCurrentValue 换上一支正在播颜色动画的画刷——
/// 只换当前值、不动值来源（TemplateBinding 还在），来源再变时照常覆盖并触发下一次过渡；
/// 观察 Background 用绑定到自身的附加属性，不用 DependencyPropertyDescriptor.AddValueChanged（它强引用元素，会泄漏）。
/// </summary>
public static class BrushTransition
{
    /// <summary>默认的时长 token（tools/token-gen 生成的资源键）：悬停、档位切换这类颜色变化。</summary>
    public const string DefaultDurationKey = "Rune.Duration.Sm";

    /// <summary>查不到时长 token（没合并 RuneTheme）时用 vue-tsx-pro 的 sm 档（120ms）。</summary>
    static readonly TimeSpan fallback = TimeSpan.FromMilliseconds(120);

    /// <summary>开启背景色过渡。</summary>
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(BrushTransition), new PropertyMetadata(false, OnEnabledChanged));

    /// <summary>时长 token 的资源键，默认 <see cref="DefaultDurationKey"/>。</summary>
    public static readonly DependencyProperty DurationKeyProperty = DependencyProperty.RegisterAttached(
        "DurationKey", typeof(object), typeof(BrushTransition), new PropertyMetadata(DefaultDurationKey));

    /// <summary>绑定到 Background 的观察属性：值变化时拿到旧值与新值。</summary>
    static readonly DependencyProperty ObservedProperty = DependencyProperty.RegisterAttached(
        "Observed", typeof(Brush), typeof(BrushTransition), new PropertyMetadata(null, OnObservedChanged));

    [ThreadStatic] static bool applying;

    /// <summary>读是否开启。</summary>
    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);

    /// <summary>设是否开启。</summary>
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    /// <summary>读时长 token 的资源键。</summary>
    public static object GetDurationKey(DependencyObject d) => d.GetValue(DurationKeyProperty);

    /// <summary>设时长 token 的资源键。</summary>
    public static void SetDurationKey(DependencyObject d, object value) => d.SetValue(DurationKeyProperty, value);

    static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border border) return;
        if ((bool)e.NewValue) BindingOperations.SetBinding(border, ObservedProperty, new Binding(nameof(Border.Background)) { Source = border });
        else BindingOperations.ClearBinding(border, ObservedProperty);
    }

    static void OnObservedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 自己换上的动画画刷也会走到这里
        if (applying || d is not Border border) return;
        if (e.OldValue is not SolidColorBrush from || e.NewValue is not SolidColorBrush to || from.Color == to.Color) return;
        var duration = Motion.Duration(border, GetDurationKey(border), fallback);
        if (duration <= TimeSpan.Zero) return;

        // 从当前显示的颜色开始（上一次过渡没播完时也连贯）
        var brush = new SolidColorBrush(from.Color);
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to.Color, duration) { EasingFunction = CubicBezierEase.Standard });
        applying = true;
        try
        {
            border.SetCurrentValue(Border.BackgroundProperty, brush);
        }
        finally
        {
            applying = false;
        }
    }
}
