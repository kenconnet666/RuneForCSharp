using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>Skeleton：包着内容时与内容一样大、内容不可见不可交互；形状按 em；加载完显示内容；占位对读屏隐藏。</summary>
public class SkeletonTests
{
    static Border Holder(Skeleton skeleton) => skeleton.GetLogicalChildren().OfType<Border>().Single(b => b.Name == SkeletonParts.Content);

    [Fact]
    public Task Wrapping_keeps_the_content_size_but_hides_and_disables_it() => HeadlessUi.Run(() =>
    {
        var button = new Button { Content = "保存", Width = 80, Height = 30 };
        var skeleton = new Skeleton { Content = button, HorizontalAlignment = HorizontalAlignment.Left };
        HeadlessUi.Show(new StackPanel { Children = { skeleton } });

        Assert.Equal(new Size(80, 30), skeleton.Bounds.Size);
        Assert.Equal(0, Holder(skeleton).Opacity);
        Assert.False(button.IsEffectivelyEnabled); // 包着的按钮不能聚焦、点击
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(skeleton));

        skeleton.Loading = false;
        Assert.Equal(1, Holder(skeleton).Opacity);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.Equal(AccessibilityView.Default, AutomationProperties.GetAccessibilityView(skeleton));
    });

    [Fact]
    public Task Shapes_are_sized_in_em() => HeadlessUi.Run(() =>
    {
        var (text, heading, circle, image) = (new Skeleton { Shape = SkeletonShape.Text }, new Skeleton { Shape = SkeletonShape.Heading, HorizontalAlignment = HorizontalAlignment.Left },
            new Skeleton { Shape = SkeletonShape.Circle, HorizontalAlignment = HorizontalAlignment.Left }, new Skeleton { Shape = SkeletonShape.Image });
        HeadlessUi.Show(new StackPanel { Width = 320, Children = { text, heading, circle, image } });
        var em = text.FontSize;

        Assert.Equal(new Size(320, em), text.Bounds.Size);
        Assert.Equal(new Size(128, 1.5 * em), heading.Bounds.Size);
        Assert.Equal(new Size(2.5 * em, 2.5 * em), circle.Bounds.Size);
        Assert.Equal(new Size(320, 180), image.Bounds.Size);
    });
}
