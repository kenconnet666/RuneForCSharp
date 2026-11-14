namespace Rune.Avalonia;

/// <summary>
/// 随时间更新的"现在"（vue-tsx-pro 的 useNow，RelativeTime、Countdown 共用）：组件每次刷新后给出下一跳的间隔，
/// 时钟对齐到间隔的整数倍再跳——同一窗口里的几个计时器同一刻跳，倒计时的秒数不会一会儿快一会儿慢。
/// 回调回到排下一跳时的线程（界面线程）上执行。组件不在界面上时停下（TS 是页面隐藏时停下）。三个平台各有一份逐字相同的副本。
/// </summary>
/// <param name="tick">到点时调用（组件在里面刷新并排下一跳）。</param>
sealed class NowClock(Action tick)
{
    ITimer? timer;
    // 每次排、停换一代：已经投递出去的旧回调看到代数不对就不做
    int generation;

    /// <summary>时钟（测试里换成假的）。</summary>
    public TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>现在。</summary>
    public DateTimeOffset Now => Time.GetUtcNow();

    /// <summary>过 <paramref name="every"/> 的整数倍时跳一次；null 时停下。</summary>
    public void Schedule(TimeSpan? every)
    {
        Stop();
        if (every is not { } period || period <= TimeSpan.Zero) return;
        var ms = (long)period.TotalMilliseconds;
        var due = TimeSpan.FromMilliseconds(ms - Now.ToUnixTimeMilliseconds() % ms);
        var current = generation;
        var context = SynchronizationContext.Current;
        timer = Time.CreateTimer(_ =>
        {
            if (context is null) Run();
            else context.Post(_ => Run(), null);
        }, null, due, Timeout.InfiniteTimeSpan);

        void Run()
        {
            if (current == generation) tick();
        }
    }

    /// <summary>停下。</summary>
    public void Stop()
    {
        generation++;
        timer?.Dispose();
        timer = null;
    }
}
