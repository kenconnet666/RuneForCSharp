using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>通知为什么关闭（vue-tsx-pro 的 ToastCloseReasons）。</summary>
public enum ToastCloseReason
{
    /// <summary>到时间自动消失。</summary>
    Timeout,

    /// <summary>用户点了关闭、滑走、按了 Esc，或代码调用了 Dismiss。</summary>
    Dismissed,
}

/// <summary>通知显示在窗口的哪个位置（vue-tsx-pro 的 ToastPlacements）。</summary>
/// <param name="Value">取值（top-start、top、top-end、bottom-start、bottom、bottom-end）。</param>
[TypeConverter(typeof(VariantConverter<ToastPlacement>))]
public readonly record struct ToastPlacement(string Value) : IVariant<ToastPlacement>
{
    /// <summary>左上（从右到左排版时右上）。</summary>
    public static readonly ToastPlacement TopStart = new("top-start");

    /// <summary>顶部居中。</summary>
    public static readonly ToastPlacement Top = new("top");

    /// <summary>右上（从右到左排版时左上）。</summary>
    public static readonly ToastPlacement TopEnd = new("top-end");

    /// <summary>左下（从右到左排版时右下）。</summary>
    public static readonly ToastPlacement BottomStart = new("bottom-start");

    /// <summary>底部居中。</summary>
    public static readonly ToastPlacement Bottom = new("bottom");

    /// <summary>右下（默认；从右到左排版时左下）。</summary>
    public static readonly ToastPlacement BottomEnd = new("bottom-end");

    /// <inheritdoc />
    public static IReadOnlyList<ToastPlacement> Known { get; } = [TopStart, Top, TopEnd, BottomStart, Bottom, BottomEnd];

    /// <summary>在顶部：新的通知在最上面，进场从上方滑进来。</summary>
    public bool FromTop => (Value ?? "bottom-end").StartsWith("top", StringComparison.Ordinal);

    /// <summary>横向：-1 起始边、0 居中、1 结束边。</summary>
    public int Inline => (Value ?? "bottom-end") switch
    {
        "top-start" or "bottom-start" => -1,
        "top" or "bottom" => 0,
        _ => 1,
    };

    /// <inheritdoc />
    public static ToastPlacement Parse(string value)
    {
        var placement = new ToastPlacement(value);
        Variants.WarnIfUnknown(placement);
        return placement;
    }

    /// <inheritdoc />
    public override string ToString() => Value ?? "bottom-end";
}

/// <summary>
/// 一条通知的内容与行为（vue-tsx-pro 的 ToastOptions）。不写的（null）用默认值；<see cref="ToasterController.Update"/> 时只换写了的。
/// 标题是文字，正文、操作同 Alert 的 Description、Action（WinForms 的正文是文字、操作是控件）。
/// </summary>
public sealed record ToastOptions
{
    /// <summary>标题（中等字重的第一行）。</summary>
    public string? Title { get; init; }

    /// <summary>正文。</summary>
    public object? Description { get; init; }

    /// <summary>色调，默认 neutral。</summary>
    public StatusTone Tone { get; init; }

    /// <summary>图标；不写时按色调选。</summary>
    public IconData? Icon { get; init; }

    /// <summary>false 时不显示图标（TS 的 icon: false）。</summary>
    public bool? ShowIcon { get; init; }

    /// <summary>进行中：图标换成加载指示，不自动消失。</summary>
    public bool? Loading { get; init; }

    /// <summary>结束边的操作（如“撤销”按钮）。</summary>
    public object? Action { get; init; }

    /// <summary>显示关闭按钮，默认 true。</summary>
    public bool? Closable { get; init; }

    /// <summary>显示多久后自动消失；<see cref="Timeout.InfiniteTimeSpan"/> 不自动消失；不写时用控制器的默认值。</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>重要：读屏立即打断播报（出错、需要马上处理的通知）；默认按礼貌的顺序念。</summary>
    public bool? Important { get; init; }

    /// <summary>编号：同一个编号的通知只有一条，再显示时换掉它；不写时自动生成。</summary>
    public string? Id { get; init; }

    /// <summary>开始关闭时调用（自动消失或被关闭）；同一条通知只调用一次。</summary>
    public Action<ToastCloseReason>? OnClose { get; init; }

    /// <summary>叠上另一份：写了的（不是 null）换掉。</summary>
    public ToastOptions Merge(ToastOptions over) => new()
    {
        Title = over.Title ?? Title,
        Description = over.Description ?? Description,
        Tone = over.Tone.Value is null ? Tone : over.Tone,
        Icon = over.Icon ?? Icon,
        ShowIcon = over.ShowIcon ?? ShowIcon,
        Loading = over.Loading ?? Loading,
        Action = over.Action ?? Action,
        Closable = over.Closable ?? Closable,
        Duration = over.Duration ?? Duration,
        Important = over.Important ?? Important,
        Id = over.Id ?? Id,
        OnClose = over.OnClose ?? OnClose,
    };
}

/// <summary>剩余时间条的这一轮计时：总时长与编号（重新计时时换一个）。</summary>
/// <param name="Duration">总时长。</param>
/// <param name="Id">编号。</param>
public readonly record struct ToastCountdown(TimeSpan Duration, int Id);

/// <summary>列表里的一条通知（vue-tsx-pro 的 ToastRecord）。</summary>
public sealed class ToastRecord
{
    internal ToastRecord(string id, ToastOptions options)
    {
        Id = id;
        Options = options;
    }

    /// <summary>编号（Show 的返回值）。</summary>
    public string Id { get; }

    /// <summary>当前的内容。</summary>
    public ToastOptions Options { get; internal set; }

    /// <summary>正在离场（已关闭，等显示区域的离场过渡结束后移除）。</summary>
    public bool Closing { get; internal set; }

    /// <summary>本轮自动关闭的计时；不自动关闭（加载中、不限时、已关闭）时 null。</summary>
    public ToastCountdown? Countdown { get; internal set; }

    /// <summary>标题。</summary>
    public string? Title => Options.Title;

    /// <summary>色调（不写时 neutral）。</summary>
    public StatusTone Tone => Options.Tone.Value is null ? StatusTone.Neutral : Options.Tone;

    /// <summary>进行中。</summary>
    public bool Loading => Options.Loading == true;

    /// <summary>能关闭（显示关闭按钮、Esc 与滑动能关）。</summary>
    public bool Closable => Options.Closable != false;

    /// <summary>重要（读屏立即播报）。</summary>
    public bool Important => Options.Important == true;
}

/// <summary>
/// <see cref="ToasterController.Promise{T}"/> 的三个阶段：进行中（自动带上 Loading）、成功（默认色调 success）、失败（默认色调 danger）。
/// </summary>
/// <typeparam name="T">结果的类型。</typeparam>
public sealed record ToastPromiseMessages<T>
{
    /// <summary>进行中。</summary>
    public required ToastOptions Loading { get; init; }

    /// <summary>成功：由结果算出。</summary>
    public required Func<T, ToastOptions> Success { get; init; }

    /// <summary>失败：由异常算出。</summary>
    public required Func<Exception, ToastOptions> Error { get; init; }
}

/// <summary>
/// 通知控制器（vue-tsx-pro 的 createToaster，三个库共用一份）：应用里建一个，交给显示区域 Toaster，在任何地方调用。
/// <code>
/// public static readonly ToasterController Toaster = new();
/// Toaster.Success("已保存");
/// var id = Toaster.Show(new ToastOptions { Title = "已删除", Action = undoButton });
/// Toaster.Loading("正在导出", new() { Id = "export" }); …; Toaster.Success("导出完成", new() { Id = "export" });
/// await Toaster.Promise(UploadAsync(file), new() { Loading = new() { Title = "正在上传" }, Success = _ => new() { Title = "上传完成" }, Error = e => new() { Title = e.Message } });
/// </code>
/// 写了编号的通知不重复：同一个编号再显示时换掉那一条（重新计时），不再加一条。计时：默认 5 秒；显示区域在指针停在上面、焦点在里面、窗口不在前台时暂停；
/// 加载中的、不限时的不自动消失；只给显示出来的（最新的 <see cref="Limit"/> 条）计时。列表与每条的变化经 <see cref="Changed"/> 在建它的线程上通知。
/// </summary>
public sealed class ToasterController
{
    static int nextId;

    readonly TimeProvider time;
    readonly SynchronizationContext? context;
    readonly List<ToastRecord> toasts = [];
    readonly Dictionary<string, Timer> timers = [];
    int countdowns;
    int limit = int.MaxValue;

    /// <summary>新建。</summary>
    /// <param name="duration">默认显示多久，默认 5 秒。</param>
    /// <param name="time">时钟（测试里换成假的）。</param>
    public ToasterController(TimeSpan? duration = null, TimeProvider? time = null)
    {
        Duration = duration ?? TimeSpan.FromSeconds(5);
        this.time = time ?? TimeProvider.System;
        context = SynchronizationContext.Current;
    }

    /// <summary>默认显示多久。</summary>
    public TimeSpan Duration { get; }

    /// <summary>当前的通知，旧的在前。</summary>
    public IReadOnlyList<ToastRecord> Toasts => toasts;

    /// <summary>计时暂停着（悬停、聚焦、窗口不在前台时）。</summary>
    public bool Paused { get; private set; }

    /// <summary>最多显示几条（显示区域的 Max）：只给显示出来的通知计时，没轮到的等它出现再开始。</summary>
    public int Limit
    {
        get => limit;
        set
        {
            limit = Math.Max(1, value);
            Sync();
        }
    }

    /// <summary>列表或某条通知变了。</summary>
    public event EventHandler? Changed;

    /// <summary>显示一条通知，返回它的编号；编号已有时换掉那一条（不与它合并）。</summary>
    public string Show(ToastOptions options)
    {
        var existing = options.Id is { } wanted ? Find(wanted) : null;
        if (existing is { Closing: false })
        {
            existing.Options = options;
            Schedule(existing);
            Notify();
            return existing.Id;
        }
        // 同一个编号的那条正在离场：先移走，新的接着出现
        if (existing is not null) Remove(existing.Id, notify: false);
        var toast = new ToastRecord(options.Id ?? $"toast-{Interlocked.Increment(ref nextId)}", options);
        toasts.Add(toast);
        Schedule(toast);
        Notify();
        return toast.Id;
    }

    /// <summary>成功的通知：<c>Success("已保存")</c>。</summary>
    public string Success(string title, ToastOptions? options = null) => Show((options ?? new()) with { Title = title, Tone = StatusTone.Success });

    /// <summary>出错的通知（读屏立即播报）。</summary>
    public string Error(string title, ToastOptions? options = null) =>
        Show((options ?? new()) with { Title = title, Tone = StatusTone.Danger, Important = options?.Important ?? true });

    /// <summary>警告的通知。</summary>
    public string Warning(string title, ToastOptions? options = null) => Show((options ?? new()) with { Title = title, Tone = StatusTone.Warning });

    /// <summary>提示的通知。</summary>
    public string Info(string title, ToastOptions? options = null) => Show((options ?? new()) with { Title = title, Tone = StatusTone.Info });

    /// <summary>进行中的通知（不自动消失、不能手动关）：完成后用同一个编号调 Success / Error 换掉它，或 Dismiss。</summary>
    public string Loading(string title, ToastOptions? options = null) =>
        Show((options ?? new()) with { Title = title, Tone = StatusTone.Neutral, Loading = true, Closable = options?.Closable ?? false });

    /// <summary>改一条通知的内容（写了的换掉）；计时按新的时长重新开始。已关闭的不再改。</summary>
    public void Update(string id, ToastOptions options)
    {
        if (Find(id) is not { Closing: false } toast) return;
        toast.Options = toast.Options.Merge(options);
        Schedule(toast);
        Notify();
    }

    /// <summary>关闭一条通知；不给编号时关闭全部。</summary>
    public void Dismiss(string? id = null)
    {
        foreach (var toast in id is null ? [.. toasts] : Find(id) is { } found ? [found] : Array.Empty<ToastRecord>()) Close(toast, ToastCloseReason.Dismissed);
        Notify();
    }

    /// <summary>进行中 → 成功 / 失败：同一条通知依次更新；返回任务的结果（失败时照样抛出）。</summary>
    public async Task<T> Promise<T>(Task<T> task, ToastPromiseMessages<T> messages)
    {
        var id = Show((messages.Loading with { Closable = messages.Loading.Closable ?? false }) with { Loading = true });
        try
        {
            var value = await task;
            Update(id, new ToastOptions { Tone = StatusTone.Success, Closable = true }.Merge(messages.Success(value)) with { Loading = false });
            return value;
        }
        catch (Exception error)
        {
            Update(id, new ToastOptions { Tone = StatusTone.Danger, Closable = true }.Merge(messages.Error(error)) with { Loading = false });
            throw;
        }
    }

    /// <summary>暂停所有通知的计时：显示区域在悬停、聚焦、窗口不在前台时调用。</summary>
    public void Pause()
    {
        if (Paused) return;
        Paused = true;
        Sync();
        Notify();
    }

    /// <summary>继续计时，见 <see cref="Pause"/>。</summary>
    public void Resume()
    {
        if (!Paused) return;
        Paused = false;
        Sync();
        Notify();
    }

    /// <summary>从列表里移除（显示区域在离场过渡结束后调用；直接调用则没有离场动画）。</summary>
    public void Remove(string id) => Remove(id, notify: true);

    void Remove(string id, bool notify)
    {
        toasts.RemoveAll(toast => toast.Id == id);
        StopTimer(id);
        // 腾出一个位置：下一条没轮到的通知出现，开始计时
        Sync();
        if (notify) Notify();
    }

    ToastRecord? Find(string id) => toasts.Find(toast => toast.Id == id);

    void Close(ToastRecord toast, ToastCloseReason reason)
    {
        if (toast.Closing) return;
        toast.Closing = true;
        toast.Countdown = null;
        StopTimer(toast.Id);
        toast.Options.OnClose?.Invoke(reason);
    }

    void StopTimer(string id)
    {
        if (!timers.Remove(id, out var timer)) return;
        timer.Handle?.Dispose();
    }

    /// <summary>重新计时：加载中、不限时的不计时。</summary>
    void Schedule(ToastRecord toast)
    {
        StopTimer(toast.Id);
        var duration = toast.Loading ? Timeout.InfiniteTimeSpan : toast.Options.Duration ?? Duration;
        toast.Countdown = duration == Timeout.InfiniteTimeSpan ? null : new ToastCountdown(duration, ++countdowns);
        if (toast.Countdown is null) return;
        timers[toast.Id] = new Timer(duration);
        Sync();
    }

    /// <summary>计时与显示对齐：显示出来的（最新的 Limit 条）在走，其余停着；暂停时都停。</summary>
    void Sync()
    {
        var shown = toasts.Skip(Math.Max(0, toasts.Count - limit)).Select(toast => toast.Id).ToHashSet();
        foreach (var (id, timer) in timers)
        {
            var running = timer.Handle is not null;
            if (!Paused && shown.Contains(id))
            {
                if (!running) Start(id, timer);
            }
            else if (running)
            {
                timer.Handle!.Dispose();
                timer.Handle = null;
                timer.Remaining -= time.GetElapsedTime(timer.StartedAt);
            }
        }
    }

    void Start(string id, Timer timer)
    {
        timer.StartedAt = time.GetTimestamp();
        var due = timer.Remaining < TimeSpan.Zero ? TimeSpan.Zero : timer.Remaining;
        timer.Handle = time.CreateTimer(_ => Post(() =>
        {
            // 已经换了一轮计时（重新计时、停下）就不做
            if (!timers.TryGetValue(id, out var current) || current != timer || Find(id) is not { } toast) return;
            Close(toast, ToastCloseReason.Timeout);
            Notify();
        }), null, due, Timeout.InfiniteTimeSpan);
    }

    /// <summary>计时器的回调回到建控制器的线程上执行。</summary>
    void Post(Action action)
    {
        if (context is null) action();
        else context.Post(_ => action(), null);
    }

    void Notify() => Changed?.Invoke(this, EventArgs.Empty);

    sealed class Timer(TimeSpan remaining)
    {
        public TimeSpan Remaining { get; set; } = remaining;

        public long StartedAt { get; set; }

        public ITimer? Handle { get; set; }
    }
}
