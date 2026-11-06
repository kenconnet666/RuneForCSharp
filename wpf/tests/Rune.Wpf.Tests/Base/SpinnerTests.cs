using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Spinner：大小经部件样式转给图标、Provider 的部件样式叠在主题之上、图标在转、有 Label 时读屏当作进度条。</summary>
public class SpinnerTests
{
    static Icon IconOf(Spinner spinner) => (Icon)spinner.Template.FindName("PART_Icon", spinner);

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Fact]
    public Task Size_goes_to_the_icon_part_and_defaults_to_1em() => WpfUi.Run(() =>
    {
        var inherit = new Spinner();
        var step = new Spinner { Size = TextSize.Lg };
        var host = Host(inherit, step);
        host.SetValue(TextElement.FontSizeProperty, 20d);
        WpfUi.Show(host);

        Assert.Equal(Lucide.LoaderCircle, IconOf(inherit).Data);
        Assert.Equal(new Size(20, 20), inherit.DesiredSize);
        Assert.Equal(TextSize.Lg, IconOf(step).Size);
        Assert.Equal(new Size(16, 16), step.DesiredSize);
    });

    [Fact]
    public Task Provider_part_styles_layer_over_the_theme() => WpfUi.Run(() =>
    {
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf">
              <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style x:Key="{x:Static rune:SpinnerParts.Icon}" TargetType="rune:Icon"><Setter Property="StrokeWidth" Value="3" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <rune:Spinner x:Name="spinner" Size="xl" />
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        WpfUi.Show(root);

        var icon = IconOf((Spinner)root.FindName("spinner"));
        Assert.Equal(3, icon.StrokeWidth);
        Assert.Equal(Lucide.LoaderCircle, icon.Data); // 主题部件样式里的图标还在
        Assert.Equal(TextSize.Xl, icon.Size);
    });

    [Fact]
    public Task The_icon_rotates_forever() => WpfUi.Run(() =>
    {
        var spinner = new Spinner();
        WpfUi.Show(Host(spinner));

        var rotation = Assert.IsType<RotateTransform>(IconOf(spinner).RenderTransform);
        Assert.True(rotation.HasAnimatedProperties);
    });

    [Fact]
    public Task Labelled_spinners_are_progress_bars_and_plain_ones_are_hidden() => WpfUi.Run(() =>
    {
        var decorative = new Spinner();
        var labelled = new Spinner { Label = "正在加载订单" };
        WpfUi.Show(Host(decorative, labelled));

        Assert.False(UIElementAutomationPeer.CreatePeerForElement(decorative).IsControlElement());
        var peer = UIElementAutomationPeer.CreatePeerForElement(labelled);
        Assert.True(peer.IsControlElement());
        Assert.Equal(AutomationControlType.ProgressBar, peer.GetAutomationControlType());
        Assert.Equal("正在加载订单", peer.GetName());
    });
}
