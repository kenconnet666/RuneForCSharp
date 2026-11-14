using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Progress 的量表区间：ResolvedTone 跟着值所在的那一段，主题的触发器按它着色。</summary>
public class ProgressBandsTests
{
    [Fact]
    public Task Bands_pick_the_tone_from_the_value() => WpfUi.Run(() =>
    {
        var progress = new Progress { Value = 912, Max = 1000, Bands = [new(800, StatusTone.Accent), new(1000, StatusTone.Warning), new(decimal.MaxValue, StatusTone.Danger)] };
        var host = new StackPanel { Width = 300, Children = { progress } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);

        Assert.Equal(StatusTone.Warning, progress.ResolvedTone);
        progress.Value = 1200;
        Assert.Equal(StatusTone.Danger, progress.ResolvedTone);
        progress.Bands = null;
        Assert.Equal(progress.Tone, progress.ResolvedTone);
    });
}
