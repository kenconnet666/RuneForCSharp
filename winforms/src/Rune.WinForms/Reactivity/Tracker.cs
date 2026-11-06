namespace Rune.WinForms;

/// <summary>
/// 依赖追踪的基础：<see cref="Signal{T}"/>、<see cref="Computed{T}"/> 被读时登记到当前的追踪者，被写时通知登记过的追踪者。
/// 整个运行时只在 UI 线程上使用（WinForms 控件本来也只能在创建它的线程上访问），不做加锁。
/// </summary>
public abstract class ReactiveSource
{
    readonly HashSet<Tracker> subscribers = [];

    /// <summary>当前有几个追踪者依赖它（测试与泄漏排查用）。</summary>
    public int SubscriberCount => subscribers.Count;

    /// <summary>被读：登记到当前的追踪者（没有追踪者时什么也不做）。</summary>
    protected void Track() => Tracker.Current?.Depend(this);

    /// <summary>值变了：通知依赖它的追踪者。先拷贝一份：通知过程中追踪者可能重新登记依赖。</summary>
    protected void NotifySubscribers()
    {
        if (subscribers.Count == 0) return;
        foreach (var subscriber in subscribers.ToArray()) subscriber.OnSourceChanged();
    }

    /// <summary>
    /// 通知后清空订阅：只管一次的源（逐帧的时钟）用它。下一次读时重新登记，不再读的（隐藏、移出视口而没重画的控件）自然退出。
    /// </summary>
    protected void NotifySubscribersOnce()
    {
        if (subscribers.Count == 0) return;
        var notified = subscribers.ToArray();
        subscribers.Clear();
        foreach (var subscriber in notified) subscriber.OnSourceChanged();
    }

    internal void Subscribe(Tracker tracker) => subscribers.Add(tracker);

    internal void Unsubscribe(Tracker tracker) => subscribers.Remove(tracker);
}

/// <summary>
/// 追踪者：一次追踪期间读到的信号就是它的依赖，任何一个变了就收到 <see cref="OnSourceChanged"/>。
/// 每次追踪都先清掉旧依赖（条件分支变了，依赖也跟着变）。
/// </summary>
public abstract class Tracker : IDisposable
{
    [ThreadStatic] static Tracker? current;

    readonly List<ReactiveSource> dependencies = [];

    /// <summary>正在追踪的追踪者；不在追踪中时为 null。</summary>
    internal static Tracker? Current => current;

    /// <summary>当前的依赖个数（测试用）。</summary>
    public int DependencyCount => dependencies.Count;

    /// <summary>某个依赖变了。</summary>
    protected internal abstract void OnSourceChanged();

    /// <summary>开始一次追踪；释放返回值时结束（恢复外层的追踪者，追踪可以嵌套）。</summary>
    protected Scope BeginTracking()
    {
        Clear();
        var previous = current;
        current = this;
        return new Scope(previous);
    }

    internal void Depend(ReactiveSource source)
    {
        if (dependencies.Contains(source)) return;
        dependencies.Add(source);
        source.Subscribe(this);
    }

    /// <summary>退订所有依赖：控件释放时调用，免得长寿的信号（主题 token）一直引用着它。</summary>
    public virtual void Dispose()
    {
        Clear();
        GC.SuppressFinalize(this);
    }

    void Clear()
    {
        foreach (var dependency in dependencies) dependency.Unsubscribe(this);
        dependencies.Clear();
    }

    /// <summary>一次追踪的范围：用 using 包住要追踪的读取。</summary>
    public readonly struct Scope : IDisposable
    {
        readonly Tracker? previous;

        internal Scope(Tracker? previous) => this.previous = previous;

        /// <summary>结束追踪，恢复外层的追踪者。</summary>
        public void Dispose() => current = previous;
    }
}
