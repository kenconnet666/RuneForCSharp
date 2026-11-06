using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Skeleton：包着内容时与内容一样大、内容不可见不可交互；形状按 em；加载完显示内容；占位对读屏隐藏。</summary>
public class SkeletonTests
{
    static StackPanel Themed(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    [Fact]
    public Task Wrapping_keeps_the_content_size_but_hides_and_disables_it() => WpfUi.Run(() =>
    {
        var button = new Button { Content = "保存", Width = 80, Height = 30 };
        var skeleton = new Skeleton { Content = button, HorizontalAlignment = HorizontalAlignment.Left };
        WpfUi.Show(Themed(skeleton));

        Assert.Equal(new Size(80, 30), skeleton.RenderSize);
        Assert.False(button.IsEnabled); // 包着的按钮不能聚焦、点击
        Assert.Null(UIElementAutomationPeer.CreatePeerForElement(skeleton).GetChildren());

        skeleton.IsLoading = false;
        Assert.True(button.IsEnabled);
        var peer = UIElementAutomationPeer.CreatePeerForElement(skeleton);
        peer.ResetChildrenCache();
        Assert.NotNull(peer.GetChildren());
    });

    [Fact]
    public Task Shapes_are_sized_in_em() => WpfUi.Run(() =>
    {
        var (text, heading, circle, image) = (new Skeleton { Shape = SkeletonShape.Text }, new Skeleton { Shape = SkeletonShape.Heading, HorizontalAlignment = HorizontalAlignment.Left },
            new Skeleton { Shape = SkeletonShape.Circle, HorizontalAlignment = HorizontalAlignment.Left }, new Skeleton { Shape = SkeletonShape.Image });
        var host = Themed(text, heading, circle, image);
        host.Width = 320;
        WpfUi.Show(host);
        var em = System.Windows.Documents.TextElement.GetFontSize(text);

        Assert.Equal(new Size(320, em), text.RenderSize);
        Assert.Equal(new Size(128, 1.5 * em), heading.RenderSize);
        Assert.Equal(new Size(2.5 * em, 2.5 * em), circle.RenderSize);
        Assert.Equal(new Size(320, 180), image.RenderSize);
    });
}
