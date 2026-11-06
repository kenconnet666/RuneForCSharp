using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

public class BrushTransitionTests
{
    static Color Shown(ProbeButton b) => ((SolidColorBrush)b.Part<Border>("PART_Root").Background).Color;

    [Fact]
    public Task Background_interpolates_and_keeps_its_value_source() => WpfUi.Run(() =>
    {
        Assert.SkipUnless(Motion.Enabled, "系统关闭了动画效果：过渡直接到终值");
        var probe = new ProbeButton();
        var host = new StackPanel { Children = { probe } };
        host.Resources[BrushTransition.DefaultDurationKey] = TimeSpan.FromMilliseconds(400);
        WpfUi.Show(host);

        // 逐帧取样，只要见到过中间色就算插值了：机器忙（CI、并行构建）时单次取样可能正好落在动画结束之后
        probe.Variant = "ghost";
        var sawMiddle = false;
        for (var clock = System.Diagnostics.Stopwatch.StartNew(); clock.ElapsedMilliseconds < 400 && !sawMiddle;)
        {
            WpfUi.Wait(TimeSpan.FromMilliseconds(15));
            sawMiddle = Shown(probe) is var shown && shown != Colors.Blue && shown != Colors.Silver;
        }
        WpfUi.Wait(TimeSpan.FromMilliseconds(450));

        Assert.True(sawMiddle);
        Assert.Equal(Colors.Silver, Shown(probe));
        // 只换了当前值：TemplateBinding 还在，之后的档位变化照常生效
        var root = probe.Part<Border>("PART_Root");
        Assert.Equal(BaseValueSource.ParentTemplate, DependencyPropertyHelper.GetValueSource(root, Border.BackgroundProperty).BaseValueSource);
        probe.Variant = "solid";
        WpfUi.Wait(TimeSpan.FromMilliseconds(450));
        Assert.Equal(Colors.Blue, Shown(probe));
    });

    [Fact]
    public Task Zero_duration_in_scope_switches_immediately() => WpfUi.Run(() =>
    {
        var probe = new ProbeButton();
        var host = new StackPanel { Children = { probe } };
        host.Resources[BrushTransition.DefaultDurationKey] = TimeSpan.Zero;
        WpfUi.Show(host);

        probe.Variant = "ghost";
        WpfUi.Flush();

        Assert.Equal(Colors.Silver, Shown(probe));
    });
}
