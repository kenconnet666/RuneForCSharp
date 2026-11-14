namespace Rune.Wpf;

/// <summary>
/// 一组悬停浮层共用的状态（vue-tsx-pro 的 createHoverGroup）：同一时间只显示一个；刚关掉一个（<see cref="SkipDelay"/> 之内）时下一个直接打开，
/// 在一排触发器上移动不用每次都等。
/// </summary>
/// <param name="skipDelay">刚关掉一个后多久之内，下一个直接打开；默认 300 毫秒（同 Radix 的 skipDelayDuration）。</param>
/// <param name="time">时钟（测试里换成假的）。</param>
sealed class HoverGroup(TimeSpan? skipDelay = null, TimeProvider? time = null)
{
    /// <summary>刚关掉一个后多久之内，下一个直接打开。</summary>
    public TimeSpan SkipDelay { get; } = skipDelay ?? TimeSpan.FromMilliseconds(300);

    /// <summary>时钟。</summary>
    public TimeProvider Time { get; } = time ?? TimeProvider.System;

    /// <summary>正在显示的那个。</summary>
    internal HoverIntent? Shown { get; set; }

    /// <summary>上次关闭的时间。</summary>
    internal DateTimeOffset LastClosed { get; set; } = DateTimeOffset.MinValue;

    /// <summary>"热"着：有一个正显示着，或刚关掉不久。</summary>
    internal bool Warm => Shown is not null || Time.GetUtcNow() - LastClosed < SkipDelay;
}

/// <summary>
/// 悬停浮层的开关时机（vue-tsx-pro 的 useHoverIntent，HoverCard 用）：指针停留 delay 后打开，离开 closeDelay 后关闭（可以移到浮层上），
/// 键盘聚焦立即打开，按下触发器、失焦时关闭。同组里同一时间只开一个。计时器的回调回到建它的线程上执行。
/// </summary>
/// <param name="group">所在的组。</param>
/// <param name="read">每次读的时机：停留多久打开、离开多久关闭、是否不打开。</param>
/// <param name="setOpen">写开关并带上原因。</param>
sealed class HoverIntent(HoverGroup group, Func<(TimeSpan Delay, TimeSpan CloseDelay, bool Disabled)> read, Action<bool, OpenChangeReason> setOpen) : IDisposable
{
    readonly SynchronizationContext? context = SynchronizationContext.Current;
    readonly Action<bool, OpenChangeReason> write = setOpen;
    ITimer? timer;
    // 每次排期换一代：已经投递出去的旧回调不做
    int generation;
    bool visible;

    /// <summary>指针进入触发器：停留 delay 后打开（组里"热"着时直接开）。</summary>
    public void PointerEntered()
    {
        var (delay, _, disabled) = read();
        if (disabled) return;
        Schedule(true, group.Warm ? TimeSpan.Zero : delay, OpenChangeReason.Hover);
    }

    /// <summary>指针离开触发器或浮层：closeDelay 后关闭（留出移到浮层上的时间）。</summary>
    public void PointerExited() => Schedule(false, read().CloseDelay, OpenChangeReason.Hover);

    /// <summary>指针进了浮层：不关。</summary>
    public void PanelEntered() => Cancel();

    /// <summary>键盘聚焦触发器：立即打开（点击聚焦时不调）。</summary>
    public void KeyboardFocused()
    {
        if (!read().Disabled) Schedule(true, TimeSpan.Zero, OpenChangeReason.Focus);
    }

    /// <summary>立即关闭：按下触发器、Esc、焦点离开。</summary>
    /// <param name="reason">为什么。</param>
    public void CloseNow(OpenChangeReason reason) => Schedule(false, TimeSpan.Zero, reason);

    /// <summary>浮层显示了：关掉同组正显示的另一个。</summary>
    public void Opened()
    {
        visible = true;
        if (group.Shown is { } other && other != this) other.write(false, OpenChangeReason.Hover);
        group.Shown = this;
    }

    /// <summary>浮层隐藏了：记下时间，供下一个跳过延迟。</summary>
    public void Closed()
    {
        if (visible) group.LastClosed = group.Time.GetUtcNow();
        visible = false;
        if (group.Shown == this) group.Shown = null;
    }

    void Schedule(bool open, TimeSpan wait, OpenChangeReason reason)
    {
        Cancel();
        if (wait <= TimeSpan.Zero)
        {
            write(open, reason);
            return;
        }
        var current = generation;
        void Run()
        {
            if (current == generation) write(open, reason);
        }
        timer = group.Time.CreateTimer(_ =>
        {
            if (context is null) Run();
            else context.Post(_ => Run(), null);
        }, null, wait, Timeout.InfiniteTimeSpan);
    }

    void Cancel()
    {
        generation++;
        timer?.Dispose();
        timer = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Cancel();
        if (group.Shown == this) group.Shown = null;
    }
}
