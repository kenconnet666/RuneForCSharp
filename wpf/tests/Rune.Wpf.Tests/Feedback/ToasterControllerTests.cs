using Microsoft.Extensions.Time.Testing;

namespace Rune.Wpf.Tests;

/// <summary>ToasterController：到时关闭、暂停保留剩余时间、剩余时间条的计时、只给显示出来的计时、关闭全部、promise、快捷方法、同编号只留一条、关闭回调。</summary>
public class ToasterControllerTests
{
    static readonly TimeSpan Ms = TimeSpan.FromMilliseconds(1);

    /// <summary>不在界面线程上建：计时器的回调直接执行，快进假时钟就能看到结果。</summary>
    static ToasterController Create(FakeTimeProvider clock, int duration = 5000)
    {
        var saved = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return new ToasterController(TimeSpan.FromMilliseconds(duration), clock);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(saved);
        }
    }

    static bool Closing(ToasterController toaster, string id) => toaster.Toasts.Single(toast => toast.Id == id).Closing;

    [Fact]
    [Upstream("Toast", "到时自动关闭（进入离场，等显示区域移除）；duration 为 Infinity 时不关闭")]
    public void Closes_on_time_but_not_when_infinite()
    {
        var clock = new FakeTimeProvider();
        var toaster = Create(clock, 1000);
        var brief = toaster.Show(new() { Title = "已保存" });
        var sticky = toaster.Show(new() { Title = "一直在", Duration = Timeout.InfiniteTimeSpan });
        clock.Advance(999 * Ms);
        Assert.False(Closing(toaster, brief));
        clock.Advance(Ms);
        Assert.True(Closing(toaster, brief));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(Closing(toaster, sticky));
        toaster.Remove(brief);
        Assert.Equal([sticky], toaster.Toasts.Select(toast => toast.Id));
    }

    [Fact]
    [Upstream("Toast", "暂停时剩余时间保留，继续后接着计时")]
    public void Pause_keeps_the_remaining_time()
    {
        var clock = new FakeTimeProvider();
        var toaster = Create(clock, 1000);
        var id = toaster.Show(new() { Title = "x" });
        clock.Advance(600 * Ms);
        toaster.Pause();
        clock.Advance(5000 * Ms);
        Assert.False(Closing(toaster, id));
        toaster.Resume();
        clock.Advance(399 * Ms);
        Assert.False(Closing(toaster, id));
        clock.Advance(Ms);
        Assert.True(Closing(toaster, id));
    }

    [Fact]
    [Upstream("Toast", "countdown 给剩余时间条：重新计时换编号，加载中、关闭后为 null；paused 跟随暂停")]
    public void Countdown_feeds_the_progress_bar()
    {
        var toaster = Create(new FakeTimeProvider(), 1000);
        var id = toaster.Show(new() { Title = "x" });
        var record = toaster.Toasts.Single();
        var first = record.Countdown!.Value;
        Assert.Equal(TimeSpan.FromSeconds(1), first.Duration);
        toaster.Update(id, new() { Duration = TimeSpan.FromSeconds(3) });
        Assert.Equal(new ToastCountdown(TimeSpan.FromSeconds(3), first.Id + 1), record.Countdown);
        toaster.Update(id, new() { Loading = true });
        Assert.Null(record.Countdown);
        Assert.False(toaster.Paused);
        toaster.Pause();
        Assert.True(toaster.Paused);
        toaster.Update(id, new() { Loading = false });
        toaster.Dismiss(id);
        Assert.Null(record.Countdown);
    }

    [Fact]
    [Upstream("Toast", "setLimit：只给显示出来的（最新的几条）计时；前面的移除后，没轮到的才开始")]
    public void Only_the_shown_toasts_count_down()
    {
        var clock = new FakeTimeProvider();
        var toaster = Create(clock, 1000);
        toaster.Limit = 1;
        var older = toaster.Show(new() { Title = "旧" });
        var newer = toaster.Show(new() { Title = "新" });
        clock.Advance(1000 * Ms);
        // 旧的被挤到显示的条数之外：停在原处，没有看到就不该消失
        Assert.Equal((false, true), (Closing(toaster, older), Closing(toaster, newer)));
        toaster.Remove(newer);
        clock.Advance(999 * Ms);
        Assert.False(Closing(toaster, older));
        clock.Advance(Ms);
        Assert.True(Closing(toaster, older));
    }

    [Fact]
    [Upstream("Toast", "暂停期间新加的通知也等继续后才开始计时")]
    public void Toasts_added_while_paused_wait()
    {
        var clock = new FakeTimeProvider();
        var toaster = Create(clock, 1000);
        toaster.Pause();
        var id = toaster.Show(new() { Title = "x" });
        clock.Advance(5000 * Ms);
        Assert.False(Closing(toaster, id));
        toaster.Resume();
        clock.Advance(1000 * Ms);
        Assert.True(Closing(toaster, id));
    }

    [Fact]
    [Upstream("Toast", "dismiss 不给编号时关闭全部；关闭后 update 不再生效")]
    public void Dismiss_all_and_no_updates_after_closing()
    {
        var toaster = Create(new FakeTimeProvider());
        var a = toaster.Show(new() { Title = "a" });
        toaster.Show(new() { Title = "b" });
        toaster.Dismiss();
        Assert.All(toaster.Toasts, toast => Assert.True(toast.Closing));
        toaster.Update(a, new() { Title = "changed" });
        Assert.Equal("a", toaster.Toasts[0].Title);
    }

    [Fact]
    [Upstream("Toast", "promise：加载中不自动关闭；成功后换成结果并重新计时")]
    public async Task Promise_loads_then_shows_the_result()
    {
        var clock = new FakeTimeProvider();
        var toaster = Create(clock, 1000);
        var finish = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = toaster.Promise(finish.Task, new()
        {
            Loading = new() { Title = "上传中" },
            Success = count => new() { Title = $"已上传 {count} 个" },
            Error = _ => new() { Title = "失败" },
        });
        var toast = toaster.Toasts.Single();
        Assert.Equal((true, false), (toast.Loading, toast.Closable));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(toast.Closing);

        finish.SetResult(3);
        Assert.Equal(3, await result);
        Assert.Equal(("已上传 3 个", StatusTone.Success, false, true), (toast.Title, toast.Tone, toast.Loading, toast.Closable));
        clock.Advance(1000 * Ms);
        Assert.True(toast.Closing);
    }

    [Fact]
    [Upstream("Toast", "promise：失败时换成错误色调，并照样抛出")]
    public async Task Promise_shows_the_failure_and_rethrows()
    {
        var toaster = Create(new FakeTimeProvider());
        var failure = new InvalidOperationException("断网");
        var result = toaster.Promise(Task.FromException<int>(failure), new()
        {
            Loading = new() { Title = "上传中" },
            Success = _ => new() { Title = "完成" },
            Error = error => new() { Title = error.Message },
        });
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => result));
        Assert.Equal(("断网", StatusTone.Danger), (toaster.Toasts[0].Title, toaster.Toasts[0].Tone));
    }

    [Fact]
    [Upstream("Toast", "快捷方法：色调与默认值；error 读屏立即播报，loading 不自动关闭、不能手动关")]
    public void Shortcuts_set_the_tone_and_defaults()
    {
        var toaster = Create(new FakeTimeProvider(), 1000);
        var ok = toaster.Success("已保存", new() { Description = "3 条" });
        var failed = toaster.Error("保存失败");
        var busy = toaster.Loading("正在导出");
        ToastRecord ById(string id) => toaster.Toasts.Single(toast => toast.Id == id);
        Assert.Equal((StatusTone.Success, "已保存", "3 条"), (ById(ok).Tone, ById(ok).Title, ById(ok).Options.Description));
        Assert.Equal((StatusTone.Danger, true), (ById(failed).Tone, ById(failed).Important));
        Assert.Equal((true, false, null), (ById(busy).Loading, ById(busy).Closable, ById(busy).Countdown));
        Assert.NotEmpty(toaster.Warning("快到期了"));
        Assert.NotEmpty(toaster.Info("有新版本"));
    }

    [Fact]
    [Upstream("Toast", "同一个 id 只有一条：再 show 时换掉它（不合并），重新计时")]
    public void Same_id_replaces_the_toast()
    {
        var clock = new FakeTimeProvider();
        var toaster = Create(clock, 1000);
        toaster.Loading("正在导出", new() { Id = "export" });
        clock.Advance(5000 * Ms);
        var id = toaster.Success("导出完成", new() { Id = "export" });
        Assert.Equal("export", id);
        var toast = Assert.Single(toaster.Toasts);
        Assert.Equal((StatusTone.Success, "导出完成"), (toast.Tone, toast.Title));
        Assert.Equal((null, null), (toast.Options.Loading, toast.Options.Closable));
        clock.Advance(1000 * Ms);
        Assert.True(Closing(toaster, "export"));
        // 离场中的同一个编号：移走，新的一条接着出现
        toaster.Info("再来一次", new() { Id = "export" });
        Assert.Single(toaster.Toasts);
        Assert.False(Closing(toaster, "export"));
    }

    [Fact]
    [Upstream("Toast", "onClose：自动消失与被关闭的原因，只调用一次")]
    public void On_close_reports_the_reason_once()
    {
        var clock = new FakeTimeProvider();
        var toaster = Create(clock, 1000);
        var reasons = new List<string>();
        toaster.Show(new() { Title = "a", OnClose = reason => reasons.Add($"a:{reason}") });
        var b = toaster.Show(new() { Title = "b", OnClose = reason => reasons.Add($"b:{reason}") });
        toaster.Dismiss(b);
        toaster.Dismiss(b);
        clock.Advance(1000 * Ms);
        Assert.Equal(["b:Dismissed", "a:Timeout"], reasons);
    }
}
