using Microsoft.Extensions.Time.Testing;

namespace Rune.WinForms.Tests;

/// <summary>LoadingBar：控制器延迟后显示、先快后慢停在 90 前，完成走满淡出归零，出错变色；显示是读屏里的进度条，空闲时是看不见的。</summary>
public class LoadingBarTests
{
    static readonly TimeSpan Ms = TimeSpan.FromMilliseconds(1);

    /// <summary>不在界面线程上建：计时器的回调直接执行，快进假时钟就能看到结果。</summary>
    static LoadingBarController Create(FakeTimeProvider clock)
    {
        var saved = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return new LoadingBarController(time: clock);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(saved);
        }
    }

    [Fact]
    [Upstream("LoadingBar", "延迟后显示并逐步前进")]
    public void Shows_after_the_delay_creeps_below_90_and_fades_after_finishing()
    {
        var clock = new FakeTimeProvider();
        var bar = Create(clock);
        bar.Start();
        clock.Advance(199 * Ms);
        Assert.False(bar.IsVisible);
        clock.Advance(Ms);
        Assert.True(bar.IsVisible);
        Assert.Equal(8, bar.Progress);

        clock.Advance(300 * Ms);
        Assert.Equal(8 + LoadingBarController.Trickle(8), bar.Progress, 6);
        clock.Advance(60_000 * Ms);
        Assert.InRange(bar.Progress, 85, 90);

        bar.Finish();
        Assert.Equal(100, bar.Progress);
        clock.Advance(300 * Ms);
        Assert.False(bar.IsVisible);
        clock.Advance(300 * Ms);
        Assert.Equal(0, bar.Progress);
    }

    [Fact]
    [Upstream("LoadingBar", "延迟之内就完成")]
    public void Finishing_within_the_delay_never_shows()
    {
        var clock = new FakeTimeProvider();
        var bar = Create(clock);
        var changes = new List<string?>();
        bar.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        bar.Start();
        clock.Advance(120 * Ms);
        bar.Finish();
        clock.Advance(2000 * Ms);
        Assert.False(bar.IsVisible);
        Assert.DoesNotContain(nameof(LoadingBarController.IsVisible), changes);
    }

    [Fact]
    [Upstream("LoadingBar", "error：标记失败并走满")]
    public void Error_marks_failure_and_starting_again_clears_it()
    {
        var clock = new FakeTimeProvider();
        var bar = Create(clock);
        bar.Start();
        clock.Advance(500 * Ms);
        bar.Error();
        Assert.True(bar.IsFailed);
        Assert.Equal(100, bar.Progress);

        // 淡出中又开始：从头来，不从 100 退回 90
        bar.Start();
        Assert.False(bar.IsFailed);
        Assert.Equal(0, bar.Progress);
    }

    [Fact]
    [Upstream("LoadingBar", "progressbar 的数值跟随控制器")]
    public Task Display_is_a_progress_bar_invisible_to_screen_readers_when_idle() => WinFormsUi.Run(() =>
    {
        var clock = new FakeTimeProvider();
        var controller = Create(clock);
        var bar = new LoadingBarControl { Controller = controller, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(bar);

        Assert.Equal(bar.LogicalToDeviceUnits(3), bar.Height); // borderWidths.md
        Assert.Equal(AccessibleRole.ProgressBar, bar.AccessibilityObject.Role);
        Assert.NotEqual(0, (int)(bar.AccessibilityObject.State & AccessibleStates.Invisible));

        controller.Start();
        clock.Advance(200 * Ms);
        Assert.Equal(0, (int)(bar.AccessibilityObject.State & AccessibleStates.Invisible));
        Assert.Equal("8%", bar.AccessibilityObject.Value);
        Assert.Equal("加载中", bar.AccessibilityObject.Name);
    });
}
