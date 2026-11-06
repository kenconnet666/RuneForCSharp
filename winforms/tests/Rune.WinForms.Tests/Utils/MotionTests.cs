namespace Rune.WinForms.Tests;

public class EasingTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void Linear_is_identity(double t) => Assert.Equal(t, Easing.Linear.Ease(t), 1e-6);

    [Fact]
    public void Matches_css_cubic_bezier()
    {
        // CSS 的 ease：cubic-bezier(0.25, 0.1, 0.25, 1) 在 0.5 处约为 0.8024
        Assert.Equal(0.8024, new Easing(0.25, 0.1, 0.25, 1).Ease(0.5), 1e-3);
    }

    [Fact]
    public void Builtin_curves_are_monotonic_from_0_to_1()
    {
        foreach (var easing in new[] { Easing.Standard, Easing.Enter, Easing.Exit })
        {
            Assert.Equal(0, easing.Ease(0));
            Assert.Equal(1, easing.Ease(1));
            var previous = 0.0;
            for (var t = 0.05; t < 1; t += 0.05)
            {
                var value = easing.Ease(t);
                Assert.True(value >= previous - 1e-9, $"在 {t:0.00} 处回落");
                previous = value;
            }
        }
    }
}

public class AnimatorTests
{
    [Fact]
    public Task Tween_reaches_the_target_and_the_clock_stops() => WinFormsUi.Run(() =>
    {
        Assert.SkipUnless(Motion.Enabled, "系统关闭了动画效果：补间直接到终值");
        var shown = new Signal<Color>(Color.FromArgb(255, 0, 0, 255));
        var target = Color.FromArgb(255, 192, 192, 192);

        // 预热：第一次使用时定时器要建隐藏窗口、代码要 JIT，第一帧可能比整个补间还晚
        var warmup = new Signal<double>(0);
        Animator.Animate(warmup, 1, TimeSpan.FromMilliseconds(30));
        for (var i = 0; i < 100 && Animator.IsRunning; i++) WinFormsUi.Wait(TimeSpan.FromMilliseconds(10));

        Animator.Animate(shown, target, TimeSpan.FromMilliseconds(300));
        // 持续采样，不赌某一个时刻
        var samples = new List<Color>();
        for (var i = 0; i < 60 && Animator.IsRunning; i++)
        {
            WinFormsUi.Wait(TimeSpan.FromMilliseconds(10));
            samples.Add(shown.Peek());
        }

        Assert.Contains(samples, c => c != Color.FromArgb(255, 0, 0, 255) && c != target);
        Assert.Equal(target, shown.Peek());
        Assert.False(Animator.IsRunning);
    });

    [Fact]
    public Task Zero_duration_writes_the_target_immediately() => WinFormsUi.Run(() =>
    {
        var value = new Signal<double>(0);

        Animator.Animate(value, 10, TimeSpan.Zero);

        Assert.Equal(10, value.Peek());
        Assert.False(Animator.IsRunning);
    });

    [Fact]
    public Task Only_the_animated_control_is_invalidated() => WinFormsUi.Run(() =>
    {
        var animated = new PaintProbe(new Signal<Color>(Color.Blue));
        var still = new PaintProbe(new Signal<Color>(Color.Blue)) { Left = 50 };
        using var form = WinFormsUi.Show(animated, still);
        animated.Invalidations = still.Invalidations = 0;

        Animator.Animate(animated.Color, Color.Silver, TimeSpan.FromMilliseconds(120));
        for (var i = 0; i < 20 && Animator.IsRunning; i++)
        {
            WinFormsUi.Wait(TimeSpan.FromMilliseconds(15));
            WinFormsUi.Paint(animated); // 离屏没有 WM_PAINT：手动重画，让依赖跟上
        }

        Assert.True(animated.Invalidations >= 1);
        Assert.Equal(0, still.Invalidations);
    });
}
