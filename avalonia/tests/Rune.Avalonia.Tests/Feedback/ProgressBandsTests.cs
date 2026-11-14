namespace Rune.Avalonia.Tests;

/// <summary>Progress 的量表区间：值落在第一段终点不小于它的那段，超过最后一段算最后一段；没有值、没有区间时用 Tone。</summary>
public class ProgressBandsTests
{
    static readonly ProgressBand[] Bands = [new(80, StatusTone.Accent), new(100, StatusTone.Warning), new(120, StatusTone.Danger)];

    [Theory]
    [InlineData(10, "accent")]
    [InlineData(80, "accent")]
    [InlineData(80.5, "warning")]
    [InlineData(100, "warning")]
    [InlineData(500, "danger")]
    public void Value_picks_its_band(double value, string tone) => Assert.Equal(tone, ProgressBands.ToneAt((decimal)value, Bands)?.Value);

    [Fact]
    public void No_value_or_no_bands_falls_back_to_the_tone()
    {
        Assert.Null(ProgressBands.ToneAt(null, Bands));
        Assert.Null(ProgressBands.ToneAt(50, []));
    }

    [Fact]
    public Task Bands_color_the_bar() => HeadlessUi.Run(() =>
    {
        var progress = new Progress { Value = 912, Max = 1000, Bands = [new(800, StatusTone.Accent), new(1000, StatusTone.Warning)] };
        HeadlessUi.Show(progress);
        Assert.Equal(StatusTone.Warning, progress.ResolvedTone);

        progress.Value = 100;
        Assert.Equal(StatusTone.Accent, progress.ResolvedTone);
    });
}
