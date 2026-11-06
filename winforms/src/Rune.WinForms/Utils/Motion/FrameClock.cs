using System.Diagnostics;

namespace Rune.WinForms;

/// <summary>
/// 逐帧的时钟：一直在动的东西（Spinner 的旋转）绘制时读 <see cref="Now"/>，下一帧到时让读过它的绘制 effect 失效。
/// 订阅只管一帧（<see cref="ReactiveSource"/> 的 NotifySubscribersOnce）：隐藏、移出视口的控件不再重画，也就不再订阅；
/// 没有订阅时定时器停下，空闲时不占 CPU。只在 UI 线程上使用（WinForms 的 Timer 依赖消息循环）。
/// </summary>
public sealed class FrameClock : ReactiveSource
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
    readonly Stopwatch clock = Stopwatch.StartNew();

    FrameClock() => timer.Tick += (_, _) =>
    {
        if (SubscriberCount == 0) timer.Stop();
        else NotifySubscribersOnce();
    };

    /// <summary>全局共用的时钟。</summary>
    public static FrameClock Shared { get; } = new();

    /// <summary>从时钟创建起过了多久；读的同时订阅下一帧。</summary>
    public TimeSpan Now
    {
        get
        {
            Track();
            if (Tracker.Current is not null) timer.Start();
            return clock.Elapsed;
        }
    }

    /// <summary>定时器在走（还有订阅，或刚失去订阅、下一拍停下）。</summary>
    public bool IsRunning => timer.Enabled;
}
