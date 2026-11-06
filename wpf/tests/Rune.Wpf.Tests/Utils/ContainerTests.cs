using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

public class ContainerTests
{
    [Theory]
    [InlineData(639.9, ContainerSize.Xs)]
    [InlineData(640, ContainerSize.Sm)]
    [InlineData(768, ContainerSize.Md)]
    [InlineData(1024, ContainerSize.Lg)]
    [InlineData(1280, ContainerSize.Xl)]
    public void Width_maps_to_breakpoints(double width, ContainerSize expected) =>
        Assert.Equal(expected, Container.SizeFor(width));

    [Fact]
    public Task Descendants_follow_the_watched_width() => WpfUi.Run(() =>
    {
        var probe = new ProbeButton();
        var box = new Border { Width = 700, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Child = probe };
        Container.SetWatch(box, true);
        WpfUi.Show(box);

        Assert.Equal(ContainerSize.Sm, Container.GetSize(probe));
        Assert.Equal(Colors.Blue, ((SolidColorBrush)probe.Background).Color);

        box.Width = 300;
        WpfUi.Flush();

        // 继承的 Size 变成 Xs，主题样式里按它写的触发器生效
        Assert.Equal(ContainerSize.Xs, Container.GetSize(probe));
        Assert.Equal(Colors.Orange, ((SolidColorBrush)probe.Background).Color);
    });
}
