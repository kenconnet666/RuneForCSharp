using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 加载条的控制器（vue-tsx-pro 的 createLoadingBar）：应用里建一个，换页、整页刷新数据时 <see cref="Start"/>，数据回来 <see cref="Finish"/>，
/// 失败 <see cref="Error"/>；窗口顶部的 LoadingBar 显示它。
/// <code>
/// public static readonly LoadingBarController Loading = new();
/// Loading.Start(); await LoadAsync(); Loading.Finish();
/// </code>
/// 不知道真实进度：开始后先快后慢地涨，停在 90% 前等完成；完成走满后淡出，出错变成危险色走满后淡出。<see cref="Delay"/>（默认 200 毫秒）之内就完成的不显示，
/// 免得每次换页都闪一下。属性变了在建它的线程上通知（界面线程上建，绑定直接能用）。
/// </summary>
public sealed class LoadingBarController : INotifyPropertyChanged
{
    /// <summary>每隔多久涨一次。</summary>
    static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(300);

    /// <summary>走满的过渡、淡出各等多久。</summary>
    static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);

    readonly TimeProvider time;
    readonly SynchronizationContext? context;
    readonly List<ITimer> timers = [];
    // 每次开始、结束换一代：已经投递出去的旧回调看到代数不对就不做
    int generation;

    /// <summary>新建。</summary>
    /// <param name="delay">多久之后才显示，默认 200 毫秒：很快完成的不显示。</param>
    /// <param name="time">时钟（测试里换成假的）。</param>
    public LoadingBarController(TimeSpan? delay = null, TimeProvider? time = null)
    {
        Delay = delay ?? TimeSpan.FromMilliseconds(200);
        this.time = time ?? TimeProvider.System;
        context = SynchronizationContext.Current;
    }

    /// <summary>多久之后才显示。</summary>
    public TimeSpan Delay { get; }

    /// <summary>进度，0–100。</summary>
    public double Progress { get; private set => Set(ref field, value, nameof(Progress)); }

    /// <summary>显示着（开始一会儿之后、结束淡出之前）。</summary>
    public bool IsVisible { get; private set => Set(ref field, value, nameof(IsVisible)); }

    /// <summary>出错了：危险色。</summary>
    public bool IsFailed { get; private set => Set(ref field, value, nameof(IsFailed)); }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>每次涨多少：离 90% 越近涨得越慢。</summary>
    internal static double Trickle(double progress) => (90 - progress) * 0.12 + 0.5;

    /// <summary>开始（已经在走时接着走，重新计时）。</summary>
    public void Start()
    {
        Stop();
        IsFailed = false;
        // 还在显示时接着走；上一次已经走完（100%，正在淡出）就从头来，不然下一拍会从 100% 退回 90%
        Progress = IsVisible && Progress < 100 ? Progress : 0;
        After(Delay, () =>
        {
            IsVisible = true;
            Progress = Math.Max(Progress, 8);
        });
        Every(Tick, () =>
        {
            if (IsVisible) Progress = Math.Min(Progress + Trickle(Progress), 90);
        });
    }

    /// <summary>完成：走满后淡出。</summary>
    public void Finish() => End(failed: false);

    /// <summary>出错：变成危险色、走满后淡出。</summary>
    public void Error() => End(failed: true);

    void End(bool failed)
    {
        Stop();
        if (!IsVisible)
        {
            Progress = 0;
            return;
        }
        IsFailed = failed;
        Progress = 100;
        // 走满的过渡结束后淡出，淡出后归零
        After(Settle, () =>
        {
            IsVisible = false;
            After(Settle, () => Progress = 0);
        });
    }

    void Stop()
    {
        generation++;
        foreach (var timer in timers) timer.Dispose();
        timers.Clear();
    }

    void After(TimeSpan due, Action action) => timers.Add(time.CreateTimer(Callback(action), null, due, Timeout.InfiniteTimeSpan));

    void Every(TimeSpan period, Action action) => timers.Add(time.CreateTimer(Callback(action), null, period, period));

    /// <summary>计时器的回调回到建控制器的线程上执行；那时已经换了一代（又开始或结束过）就不做。</summary>
    TimerCallback Callback(Action action)
    {
        var current = generation;
        void Run()
        {
            if (current == generation) action();
        }
        return _ =>
        {
            if (context is null) Run();
            else context.Post(_ => Run(), null);
        };
    }

    void Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
