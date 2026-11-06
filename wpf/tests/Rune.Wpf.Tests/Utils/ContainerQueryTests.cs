using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

public class ContainerQueryTests
{
    [Theory]
    [InlineData(639.9, Breakpoint.Xs)]
    [InlineData(640, Breakpoint.Sm)]
    [InlineData(768, Breakpoint.Md)]
    [InlineData(1024, Breakpoint.Lg)]
    [InlineData(1280, Breakpoint.Xl)]
    public void Width_maps_to_breakpoints(double width, Breakpoint expected) =>
        Assert.Equal(expected, ContainerQuery.SizeFor(width));

    [Fact]
    public Task Descendants_follow_the_watched_width() => WpfUi.Run(() =>
    {
        var probe = new ProbeButton();
        var box = new Border { Width = 700, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Child = probe };
        ContainerQuery.SetWatch(box, true);
        WpfUi.Show(box);

        Assert.Equal(Breakpoint.Sm, ContainerQuery.GetSize(probe));
        Assert.Equal(Colors.Blue, ((SolidColorBrush)probe.Background).Color);

        box.Width = 300;
        WpfUi.Flush();

        // 继承的 Size 变成 Xs，主题样式里按它写的触发器生效
        Assert.Equal(Breakpoint.Xs, ContainerQuery.GetSize(probe));
        Assert.Equal(Colors.Orange, ((SolidColorBrush)probe.Background).Color);
    });
}
