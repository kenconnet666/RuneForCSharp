using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

public class EmExtensionTests
{
    [Fact]
    public Task Values_follow_the_font_size() => HeadlessUi.Run(() =>
    {
        var probe = new ProbeControl { Theme = ProbeControl.CreateTheme() };
        var host = new StackPanel { Children = { probe } };
        host.SetValue(TextElement.FontSizeProperty, 20d);
        var window = HeadlessUi.Show(host);

        Assert.Equal(new Thickness(20, 5), probe.Padding);
        Assert.Equal(new CornerRadius(8), probe.Root.CornerRadius);

        host.SetValue(TextElement.FontSizeProperty, 10d);
        HeadlessUi.Flush(window);

        Assert.Equal(new Thickness(10, 2.5), probe.Padding);
        Assert.Equal(new CornerRadius(4), probe.Root.CornerRadius);
    });

    [Fact]
    public Task User_styles_and_local_values_win() => HeadlessUi.Run(() =>
    {
        var styled = new ProbeControl { Theme = ProbeControl.CreateTheme() };
        var local = new ProbeControl { Theme = ProbeControl.CreateTheme(), Padding = new Thickness(7) };
        var host = new StackPanel { Children = { styled, local } };
        host.Styles.Add(new Style(x => x.OfType<ProbeControl>()) { Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(3)) } });
        HeadlessUi.Show(host);

        Assert.Equal(new Thickness(3), styled.Padding);
        Assert.Equal(new Thickness(7), local.Padding);
    });
}
