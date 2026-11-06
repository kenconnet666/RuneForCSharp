using global::Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 加载指示防闪烁（vue-tsx-pro 的 useDelayedLoading）：很快完成的加载不出现指示，出现了就至少显示一段时间
/// （同 Ant Design Spin 的 delay）。只管视觉：禁止点击、读屏的"忙"仍按真实的加载状态立即生效。
/// 创建时已经在加载就立即显示（没有内容可看，空白比指示更糟）。
/// </summary>
/// <param name="changed">显示与否变了时调用。</param>
/// <param name="loading">创建时是否已经在加载。</param>
public sealed class DelayedLoading(Action<bool> changed, bool loading = false)
{
    readonly DispatcherTimer timer = new();
    DateTime shownAt = loading ? DateTime.UtcNow : default;

    /// <summary>开始加载后过多久才显示，默认 200 毫秒。</summary>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>显示后至少保持多久，默认 500 毫秒。</summary>
    public TimeSpan MinDuration { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>加载指示是否显示。</summary>
    public bool Shown { get; private set; } = loading;

    /// <summary>真实的加载状态变了。</summary>
    /// <param name="value">是否在加载。</param>
    public void Update(bool value)
    {
        timer.Stop();
        if (value)
        {
            // 正在等着隐藏时又开始加载：取消隐藏，继续显示
            if (!Shown) Schedule(Delay, true);
            return;
        }
        if (Shown) Schedule(shownAt + MinDuration - DateTime.UtcNow, false);
    }

    void Schedule(TimeSpan wait, bool next)
    {
        timer.Interval = wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(1);
        timer.Tick -= OnTick;
        timer.Tick += OnTick;
        timer.Start();

        void OnTick(object? sender, EventArgs e)
        {
            timer.Stop();
            timer.Tick -= OnTick;
            Shown = next;
            if (next) shownAt = DateTime.UtcNow;
            changed(next);
        }
    }

    /// <summary>停掉计时（控件卸载时）。</summary>
    public void Stop() => timer.Stop();
}
