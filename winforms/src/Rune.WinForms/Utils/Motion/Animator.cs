using System.Diagnostics;

namespace Rune.WinForms;

/// <summary>
/// 共享的动画时钟：一个定时器驱动所有补间，没有补间时停下（空闲时不占 CPU）。补间每一帧写信号，
/// 读到这个信号的绘制 effect 只让对应的控件或区域失效（实测 100 个按钮里动其中 1 个，其余 99 个零重绘）。
/// <code>
/// Animator.Animate(shown, Color.Silver, TimeSpan.FromMilliseconds(120));       // 颜色过渡
/// Animator.Animate(offset, 0d, TimeSpan.FromMilliseconds(200), Easing.Enter);  // 数值过渡
/// </code>
/// 同一个信号上再开一个补间会接替旧的（从当前值继续，不跳）。系统关闭动画、时长为 0 时直接写终值。
/// 只在 UI 线程上使用：定时器是 WinForms 的 Timer，依赖消息循环。
/// </summary>
public static class Animator
{
    /// <summary>一帧的间隔：WinForms 定时器的精度约 15.6ms，再小也没有意义。</summary>
    static readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };

    static readonly List<Tween> active = [];

    static Animator() => timer.Tick += (_, _) => Step();

    /// <summary>还有补间在播。</summary>
    public static bool IsRunning => active.Count > 0;

    /// <summary>颜色补间（逐通道插值，含不透明度）。</summary>
    /// <param name="target">要写的信号。</param>
    /// <param name="to">终值。</param>
    /// <param name="duration">时长。</param>
    /// <param name="easing">缓动，默认 <see cref="Easing.Standard"/>。</param>
    public static void Animate(Signal<Color> target, Color to, TimeSpan duration, Easing? easing = null)
    {
        var from = target.Peek();
        Start(target, duration, easing, p => target.Value = Lerp(from, to, p), () => target.Value = to);
    }

    /// <summary>数值补间。</summary>
    /// <param name="target">要写的信号。</param>
    /// <param name="to">终值。</param>
    /// <param name="duration">时长。</param>
    /// <param name="easing">缓动，默认 <see cref="Easing.Standard"/>。</param>
    public static void Animate(Signal<double> target, double to, TimeSpan duration, Easing? easing = null)
    {
        var from = target.Peek();
        Start(target, duration, easing, p => target.Value = from + (to - from) * p, () => target.Value = to);
    }

    /// <summary>停掉某个信号上的补间，值停在当前帧。</summary>
    public static void Stop(ReactiveSource target) => active.RemoveAll(t => t.Target == target);

    static void Start(ReactiveSource target, TimeSpan duration, Easing? easing, Action<double> apply, Action finish)
    {
        Stop(target);
        if (duration <= TimeSpan.Zero || !Motion.Enabled)
        {
            finish();
            return;
        }
        active.Add(new Tween(target, Stopwatch.StartNew(), duration, easing ?? Easing.Standard, apply, finish));
        timer.Start();
    }

    static void Step()
    {
        foreach (var tween in active.ToArray())
        {
            var progress = tween.Clock.Elapsed / tween.Duration;
            if (progress >= 1)
            {
                active.Remove(tween);
                tween.Finish();
            }
            else
            {
                tween.Apply(tween.Easing.Ease(progress));
            }
        }
        if (active.Count == 0) timer.Stop();
    }

    static Color Lerp(Color from, Color to, double p) => Color.FromArgb(
        Channel(from.A, to.A, p), Channel(from.R, to.R, p), Channel(from.G, to.G, p), Channel(from.B, to.B, p));

    static int Channel(int from, int to, double p) => (int)Math.Round(from + (to - from) * p);

    sealed record Tween(ReactiveSource Target, Stopwatch Clock, TimeSpan Duration, Easing Easing, Action<double> Apply, Action Finish);
}
