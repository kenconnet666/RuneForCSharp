using System.Windows;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// CSS 的 cubic-bezier(x1, y1, x2, y2) 缓动：WPF 自带的缓动没有三次贝塞尔，vue-tsx-pro 的 easings token 用的都是它。
/// 内置的 <c>Standard</c>、<c>Enter</c>、<c>Exit</c> 由 tools/token-gen 生成（CubicBezierEase.g.cs），XAML 里用资源
/// <c>{DynamicResource Rune.Easing.Standard}</c>。
/// <see cref="EasingFunctionBase.EasingMode"/> 固定为 EaseIn：曲线本身已经是完整的形状，不要再被 WPF 翻转。
/// </summary>
public sealed partial class CubicBezierEase : EasingFunctionBase
{
    /// <summary>第一个控制点的 x（0–1）。</summary>
    public static readonly DependencyProperty X1Property = DependencyProperty.Register(nameof(X1), typeof(double), typeof(CubicBezierEase));

    /// <summary>第一个控制点的 y。</summary>
    public static readonly DependencyProperty Y1Property = DependencyProperty.Register(nameof(Y1), typeof(double), typeof(CubicBezierEase));

    /// <summary>第二个控制点的 x（0–1）。</summary>
    public static readonly DependencyProperty X2Property = DependencyProperty.Register(nameof(X2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1d));

    /// <summary>第二个控制点的 y。</summary>
    public static readonly DependencyProperty Y2Property = DependencyProperty.Register(nameof(Y2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1d));

    /// <summary>匀速（XAML 里逐个设控制点）。</summary>
    public CubicBezierEase() => EasingMode = EasingMode.EaseIn;

    /// <summary>用四个控制点坐标创建。</summary>
    /// <param name="x1">第一个控制点的 x（0–1）。</param>
    /// <param name="y1">第一个控制点的 y。</param>
    /// <param name="x2">第二个控制点的 x（0–1）。</param>
    /// <param name="y2">第二个控制点的 y。</param>
    public CubicBezierEase(double x1, double y1, double x2, double y2) : this()
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    /// <summary>第一个控制点的 x（0–1）。</summary>
    public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }

    /// <summary>第一个控制点的 y。</summary>
    public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }

    /// <summary>第二个控制点的 x（0–1）。</summary>
    public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }

    /// <summary>第二个控制点的 y。</summary>
    public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }

    /// <inheritdoc />
    protected override double EaseInCore(double normalizedTime)
    {
        if (normalizedTime <= 0) return 0;
        if (normalizedTime >= 1) return 1;
        return Bezier(SolveT(normalizedTime, X1, X2), Y1, Y2);
    }

    /// <inheritdoc />
    protected override Freezable CreateInstanceCore() => new CubicBezierEase();

    static CubicBezierEase Frozen(double x1, double y1, double x2, double y2)
    {
        var ease = new CubicBezierEase(x1, y1, x2, y2);
        ease.Freeze();
        return ease;
    }

    /// <summary>贝塞尔曲线在参数 t 处的一个分量（起点 0、终点 1）。</summary>
    static double Bezier(double t, double p1, double p2)
    {
        var u = 1 - t;
        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }

    static double BezierSlope(double t, double p1, double p2)
    {
        var u = 1 - t;
        return 3 * u * u * p1 + 6 * u * t * (p2 - p1) + 3 * t * t * (1 - p2);
    }

    /// <summary>由 x 反求参数 t：先用牛顿法，斜率太小（曲线很平）时改二分，保证收敛（同 Rune.WinForms 的 Easing）。</summary>
    static double SolveT(double x, double x1, double x2)
    {
        var t = x;
        for (var i = 0; i < 8; i++)
        {
            var error = Bezier(t, x1, x2) - x;
            if (Math.Abs(error) < 1e-7) return t;
            var slope = BezierSlope(t, x1, x2);
            if (Math.Abs(slope) < 1e-6) break;
            t -= error / slope;
        }
        double low = 0, high = 1;
        t = x;
        for (var i = 0; i < 40; i++)
        {
            var value = Bezier(t, x1, x2);
            if (Math.Abs(value - x) < 1e-7) break;
            if (value < x) low = t;
            else high = t;
            t = (low + high) / 2;
        }
        return t;
    }
}
