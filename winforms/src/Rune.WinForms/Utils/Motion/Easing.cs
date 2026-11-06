namespace Rune.WinForms;

/// <summary>
/// 缓动曲线：CSS 的 cubic-bezier(x1, y1, x2, y2)。内置的 Standard、Enter、Exit 由 tools/token-gen 从 vue-tsx-pro 的
/// easings token 生成（Easing.g.cs）。
/// </summary>
/// <param name="x1">第一个控制点的 x（0–1）。</param>
/// <param name="y1">第一个控制点的 y。</param>
/// <param name="x2">第二个控制点的 x（0–1）。</param>
/// <param name="y2">第二个控制点的 y。</param>
public sealed partial class Easing(double x1, double y1, double x2, double y2)
{
    /// <summary>匀速。</summary>
    public static readonly Easing Linear = new(0, 0, 1, 1);

    /// <summary>时间进度（0–1）对应的值进度。</summary>
    /// <param name="progress">时间进度，超出 0–1 时截断。</param>
    public double Ease(double progress)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;
        return Bezier(SolveT(progress), y1, y2);
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

    /// <summary>由 x 反求参数 t：先用牛顿法，斜率太小（曲线很平）时改二分，保证收敛。</summary>
    double SolveT(double x)
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
