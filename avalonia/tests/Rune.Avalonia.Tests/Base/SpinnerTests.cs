using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Spinner：大小转给图标部件、使用方的部件样式盖过主题、图标在转、有 Label 时读屏当作进度条。</summary>
public class SpinnerTests
{
    static Icon IconOf(Spinner spinner) => spinner.GetVisualDescendants().OfType<Icon>().Single();

    [Fact]
    public Task Size_goes_to_the_icon_part_and_defaults_to_1em() => HeadlessUi.Run(() =>
    {
        var inherit = new Spinner();
        var step = new Spinner { Size = TextSize.Lg };
        var host = new StackPanel { Children = { inherit, step } };
        host.SetValue(TextElement.FontSizeProperty, 20d);
        HeadlessUi.Show(host);

        Assert.Equal(Lucide.LoaderCircle, IconOf(inherit).Data);
        Assert.Equal(new Size(20, 20), inherit.DesiredSize);
        Assert.Equal(TextSize.Lg, IconOf(step).Size);
        Assert.Equal(new Size(16, 16), step.DesiredSize);
    });

    [Fact]
    public Task User_part_styles_override_the_theme_property_by_property() => HeadlessUi.Run(() =>
    {
        var spinner = new Spinner { Size = TextSize.Xl };
        var plain = new Icon { Data = Lucide.Search };
        var host = new StackPanel { Children = { spinner, plain } };
        host.Styles.Add(new Style(x => x.OfType<Spinner>().Template().OfType<Icon>().Name(SpinnerParts.Icon))
        {
            Setters = { new Setter(Icon.StrokeWidthProperty, 3d), new Setter(Icon.DataProperty, Lucide.Loader) },
        });
        HeadlessUi.Show(host);

        var icon = IconOf(spinner);
        Assert.Equal(Lucide.Loader, icon.Data);
        Assert.Equal(3, icon.StrokeWidth);
        Assert.Equal(TextSize.Xl, icon.Size); // 主题里转过来的大小还在
        Assert.Equal(2, plain.StrokeWidth); // 只改了 Spinner 里的图标
    });

    [Fact]
    public Task The_icon_rotates() => HeadlessUi.Run(() =>
    {
        var spinner = new Spinner();
        var window = HeadlessUi.Show(new StackPanel { Children = { spinner } });
        HeadlessUi.Flush(window);

        Assert.IsAssignableFrom<Transform>(IconOf(spinner).RenderTransform);
    });

    [Fact]
    public Task Labelled_spinners_are_progress_bars_and_plain_ones_are_hidden() => HeadlessUi.Run(() =>
    {
        var decorative = new Spinner();
        var labelled = new Spinner { Label = "正在加载订单" };
        HeadlessUi.Show(new StackPanel { Children = { decorative, labelled } });

        Assert.False(ControlAutomationPeer.CreatePeerForElement(decorative).IsControlElement());
        var peer = ControlAutomationPeer.CreatePeerForElement(labelled);
        Assert.True(peer.IsControlElement());
        Assert.Equal(AutomationControlType.ProgressBar, peer.GetAutomationControlType());
        Assert.Equal("正在加载订单", peer.GetName());
    });
}
