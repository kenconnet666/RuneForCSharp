using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>CompositeControl：继承来的 Padding、BorderThickness 留在根元素四周；组件自己接了的不重复留。</summary>
public class CompositeControlTests
{
    static StackPanel Show(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        WpfUi.Show(host);
        return host;
    }

    [Fact]
    public Task Padding_and_border_wrap_the_root() => WpfUi.Run(() =>
    {
        var plain = new ValueChange { Before = "10", After = "12" };
        var padded = new ValueChange { Before = "10", After = "12", Padding = new Thickness(8, 4, 8, 4), BorderThickness = new Thickness(1), BorderBrush = Brushes.Red, Background = Brushes.Yellow };
        Show(plain, padded);

        Assert.Equal(plain.DesiredSize.Width + 18, padded.DesiredSize.Width, 3);
        Assert.Equal(plain.DesiredSize.Height + 10, padded.DesiredSize.Height, 3);
        var root = (UIElement)VisualTreeHelper.GetChild(padded, 0);
        Assert.Equal(new Point(9, 5), root.TranslatePoint(default, padded));
    });

    [Fact]
    public Task Components_that_own_the_chrome_do_not_get_it_twice() => WpfUi.Run(() =>
    {
        var alert = new Alert { Content = "x", Padding = new Thickness(20) };
        Show(alert);

        // Alert 把 Padding 接到自己的根元素上：根元素贴着 Alert 的边
        var root = (UIElement)VisualTreeHelper.GetChild(alert, 0);
        Assert.Equal(new Point(0, 0), root.TranslatePoint(default, alert));
    });
}
