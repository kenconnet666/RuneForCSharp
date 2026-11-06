using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>Icon 组件：大小跟随字号或档位、色调取主题、线宽取 token、从右到左默认不翻转、没有名称时对读屏隐藏。</summary>
public class IconTests
{
    static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    [Fact]
    public Task Size_follows_the_font_size_a_step_or_pixels() => HeadlessUi.Run(() =>
    {
        var inherit = new Icon { Data = Lucide.Search };
        var step = new Icon { Data = Lucide.Search, Size = TextSize.Lg };
        var pixels = new Icon { Data = Lucide.Search, Size = 24 };
        var host = new StackPanel { Children = { inherit, step, pixels } };
        host.SetValue(TextElement.FontSizeProperty, 20d);
        HeadlessUi.Show(host);

        Assert.Equal(new Size(20, 20), inherit.DesiredSize);
        Assert.Equal(new Size(16, 16), step.DesiredSize); // fontSizes.lg
        Assert.Equal(new Size(24, 24), pixels.DesiredSize);
    });

    [Fact]
    public Task Tone_colors_come_from_the_theme_and_default_follows_the_text() => HeadlessUi.Run(() =>
    {
        var plain = new Icon { Data = Lucide.Info };
        var danger = new Icon { Data = Lucide.Info, Tone = TextTone.Danger };
        var host = new StackPanel { Children = { plain, danger } };
        host.SetValue(TextElement.ForegroundProperty, Brushes.Purple);
        HeadlessUi.Show(host);

        Assert.Equal(Colors.Purple, BrushColor(plain.Foreground));
        Application.Current!.TryGetResource("Rune.Brush.DangerText", ThemeVariant.Light, out var dangerText);
        Assert.Equal(BrushColor((IBrush)dangerText!), BrushColor(danger.Foreground));
        // 线宽取 strokeWidths.md
        Assert.Equal(2, plain.StrokeWidth);
    });

    [Fact]
    public Task Icons_do_not_flip_in_right_to_left_unless_asked() => HeadlessUi.Run(() =>
    {
        var plain = new Icon { Data = Lucide.Search };
        var arrow = new Icon { Data = Lucide.ArrowRight, FlipInRtl = true };
        var host = new StackPanel { FlowDirection = FlowDirection.RightToLeft, Children = { plain, arrow } };
        HeadlessUi.Show(host);

        // 外面整棵子树镜像：普通图标再镜像一次抵消掉，有方向的图标跟着翻
        Assert.True(plain.HasMirrorTransform);
        Assert.False(arrow.HasMirrorTransform);
    });

    [Fact]
    public Task Decorative_icons_are_hidden_from_screen_readers() => HeadlessUi.Run(() =>
    {
        var decorative = new Icon { Data = Lucide.Search };
        var named = new Icon { Data = Lucide.Trash };
        AutomationProperties.SetName(named, "删除");
        HeadlessUi.Show(new StackPanel { Children = { decorative, named } });

        var decorativePeer = ControlAutomationPeer.CreatePeerForElement(decorative);
        var namedPeer = ControlAutomationPeer.CreatePeerForElement(named);
        Assert.False(decorativePeer.IsControlElement());
        Assert.True(namedPeer.IsControlElement());
        Assert.Equal(AutomationControlType.Image, namedPeer.GetAutomationControlType());
        Assert.Equal("删除", namedPeer.GetName());
    });
}
