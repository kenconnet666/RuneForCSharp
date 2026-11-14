namespace Rune.WinForms.Tests;

/// <summary>Progress 的量表区间：ResolvedTone 跟着值所在的那一段。</summary>
public class ProgressBandsTests
{
    [Fact]
    public void Bands_pick_the_tone_from_the_value()
    {
        var progress = new Progress { Value = 912, Max = 1000, Bands = [new(800, StatusTone.Accent), new(1000, StatusTone.Warning), new(decimal.MaxValue, StatusTone.Danger)] };
        Assert.Equal(StatusTone.Warning, progress.ResolvedTone);
        progress.Value = 1200;
        Assert.Equal(StatusTone.Danger, progress.ResolvedTone);
        progress.Bands = null;
        Assert.Equal(progress.Tone, progress.ResolvedTone);
    }
}
