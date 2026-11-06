using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Tests;

public class EmSizeTests
{
    [Fact]
    public Task Values_follow_the_font_size() => WpfUi.Run(() =>
    {
        var probe = new ProbeButton();
        var host = new StackPanel { Children = { probe } };
        host.SetValue(TextElement.FontSizeProperty, 20d);
        WpfUi.Show(host);

        Assert.Equal(new Thickness(20, 5, 20, 5), probe.Padding);
        Assert.Equal(new CornerRadius(8), probe.CornerRadius);

        host.SetValue(TextElement.FontSizeProperty, 10d);
        WpfUi.Flush();

        Assert.Equal(new Thickness(10, 2.5, 10, 2.5), probe.Padding);
        Assert.Equal(new CornerRadius(4), probe.CornerRadius);
    });

    [Fact]
    public Task User_styles_and_local_values_are_kept() => WpfUi.Run(() =>
    {
        var styled = new ProbeButton();
        var local = new ProbeButton { Padding = new Thickness(7) };
        var host = new StackPanel { Children = { styled, local } };
        host.Resources[typeof(ProbeButton)] = new Style(typeof(ProbeButton)) { Setters = { new Setter(Control.PaddingProperty, new Thickness(3)) } };
        host.SetValue(TextElement.FontSizeProperty, 20d);
        WpfUi.Show(host);

        Assert.Equal(new Thickness(3), styled.Padding);
        Assert.Equal(new Thickness(7), local.Padding);

        // 字号变了也不会被 em 改写
        host.SetValue(TextElement.FontSizeProperty, 10d);
        WpfUi.Flush();
        Assert.Equal(new Thickness(3), styled.Padding);
    });
}
