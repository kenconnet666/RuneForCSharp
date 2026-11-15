using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>AsideLayout：主栏是子元素、侧栏带名称；够宽时侧栏定宽在右、贴住，不到 56rem 时叠成一列。</summary>
public class AsideLayoutTests
{
    static AsideLayout Sample() => new()
    {
        AsideLabel = "收益与关闭",
        Aside = { new TextBlock { Text = "收益" } },
        Children = { new TextBlock { Text = "概览" } },
    };

    static Flex Part(AsideLayout layout, string name) => layout.GetLogicalDescendants().OfType<Flex>().Single(flex => flex.Name == name);

    [Fact]
    [Upstream("AsideLayout", "主栏是子元素、侧栏是 aside（带名称）；按容器宽度排")]
    public Task Main_and_named_aside_side_by_side_when_wide() => HeadlessUi.Run(() =>
    {
        var layout = Sample();
        var window = HeadlessUi.Show(new Border { Width = 1200, Child = layout });
        HeadlessUi.Flush(window);
        var aside = Part(layout, AsideLayoutParts.Aside);
        var main = Part(layout, AsideLayoutParts.Main);
        Assert.Equal("收益与关闭", AutomationProperties.GetName(aside));
        Assert.Equal("收益", ((TextBlock)aside.Children.Single()).Text);
        Assert.Equal("概览", ((TextBlock)main.Children.Single()).Text);
        Assert.True(layout.Wide);
        // 侧栏 26rem（416）、在右边，主栏占满剩下的（隔 md）
        Assert.Equal(416, aside.Bounds.Width);
        Assert.True(aside.TranslatePoint(default, layout)!.Value.X > main.Bounds.Width);
        Assert.NotNull(aside.FindAncestorOfType<Affix>());
    });

    [Fact]
    public Task Stacks_below_56rem_and_sticky_can_be_turned_off() => HeadlessUi.Run(() =>
    {
        var layout = Sample();
        var host = new Border { Width = 600, Child = layout };
        var window = HeadlessUi.Show(host);
        HeadlessUi.Flush(window);
        var aside = Part(layout, AsideLayoutParts.Aside);
        var main = Part(layout, AsideLayoutParts.Main);
        Assert.False(layout.Wide);
        Assert.Equal(600, aside.Bounds.Width);
        Assert.True(aside.TranslatePoint(default, layout)!.Value.Y >= main.Bounds.Height);
        // 叠成一列时不贴
        Assert.Null(aside.FindAncestorOfType<Affix>());
        host.Width = 1200;
        layout.Sticky = false;
        HeadlessUi.Flush(window);
        Assert.True(layout.Wide);
        Assert.Null(aside.FindAncestorOfType<Affix>());
    });
}
