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
        host.Resources[BrushTransition.DefaultDurationKey] = TimeSpan.FromMilliseconds(200);
        WpfUi.Show(host);

        probe.Variant = "ghost";
        WpfUi.Wait(TimeSpan.FromMilliseconds(80));
        var middle = Shown(probe);
        WpfUi.Wait(TimeSpan.FromMilliseconds(250));

        Assert.NotEqual(Colors.Blue, middle);
        Assert.NotEqual(Colors.Silver, middle);
        Assert.Equal(Colors.Silver, Shown(probe));
        // 只换了当前值：TemplateBinding 还在，之后的档位变化照常生效
        var root = probe.Part<Border>("PART_Root");
        Assert.Equal(BaseValueSource.ParentTemplate, DependencyPropertyHelper.GetValueSource(root, Border.BackgroundProperty).BaseValueSource);
        probe.Variant = "solid";
        WpfUi.Wait(TimeSpan.FromMilliseconds(300));
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
