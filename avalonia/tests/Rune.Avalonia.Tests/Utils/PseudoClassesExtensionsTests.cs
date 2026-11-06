using Avalonia.Media;

namespace Rune.Avalonia.Tests;

public class PseudoClassesExtensionsTests
{
    [Theory]
    [InlineData("solid", "soft")]
    [InlineData("soft", "solid")]
    public Task Switching_a_variant_changes_the_value_once(string from, string to) => HeadlessUi.Run(() =>
    {
        var probe = new ProbeControl { Theme = ProbeControl.CreateTheme(), Variant = from };
        var window = HeadlessUi.Show(probe);
        probe.BackgroundChanges = 0;

        probe.Variant = to;
        HeadlessUi.Flush(window);

        Assert.Equal(1, probe.BackgroundChanges);
        Assert.Equal(to == "solid" ? Colors.Blue : Colors.LightBlue, ((ISolidColorBrush)probe.Background!).Color);
    });
}
