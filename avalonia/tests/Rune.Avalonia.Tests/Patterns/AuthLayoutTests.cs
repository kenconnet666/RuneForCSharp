using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>AuthLayout：品牌区在前、表单在后，工具在整页结束边的上角；不写品牌区时只有表单区；够 64rem 宽时左右分栏，否则上下叠。</summary>
public class AuthLayoutTests
{
    static Control Part(AuthLayout layout, string name) => layout.GetVisualChildren().OfType<Control>().Single(control => control.Name == name);

    [Fact]
    [Upstream("AuthLayout", "品牌区、工具和表单各在各的位置")]
    public Task Hero_tools_and_form_in_place() => HeadlessUi.Run(() =>
    {
        var layout = new AuthLayout { Hero = new TextBlock { Text = "联合降本" }, Tools = new Button { Content = "主题" }, Content = new TextBlock { Text = "登录" } };
        var window = HeadlessUi.Show(new Border { Width = 1200, Height = 600, Child = layout });
        HeadlessUi.Flush(window);
        var hero = Part(layout, AuthLayoutParts.Hero);
        var main = Part(layout, AuthLayoutParts.Main);
        var tools = Part(layout, AuthLayoutParts.Tools);
        Assert.Equal("联合降本", hero.GetLogicalDescendants().OfType<TextBlock>().Single().Text);
        Assert.Contains(main.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "登录");
        Assert.True(layout.Wide);
        // 品牌区在开始边、占 1.15 份；表单在结束边；工具在整页结束边的上角
        Assert.Equal(0, hero.Bounds.X);
        Assert.Equal(1200 * 1.15 / 2.15, hero.Bounds.Width, tolerance: 1);
        Assert.Equal(hero.Bounds.Right, main.Bounds.X, tolerance: 1);
        Assert.True(tools.Bounds.Right < 1200 && tools.Bounds.X > main.Bounds.X && tools.Bounds.Y < 40);
    });

    [Fact]
    [Upstream("AuthLayout", "不写 hero 时只有表单区，不写 tools 时没有工具区")]
    public Task Without_hero_only_the_form() => HeadlessUi.Run(() =>
    {
        var layout = new AuthLayout { Content = new TextBlock { Text = "登录" } };
        var window = HeadlessUi.Show(new Border { Width = 1200, Height = 600, Child = layout });
        HeadlessUi.Flush(window);
        Assert.False(Part(layout, AuthLayoutParts.Hero).IsVisible);
        Assert.False(Part(layout, AuthLayoutParts.Tools).IsVisible);
        Assert.Equal(1200, Part(layout, AuthLayoutParts.Main).Bounds.Width);
    });

    [Fact]
    public Task Narrow_layout_puts_the_hero_on_top() => HeadlessUi.Run(() =>
    {
        var layout = new AuthLayout { Hero = new TextBlock { Text = "联合降本" }, Content = new TextBlock { Text = "登录" } };
        var window = HeadlessUi.Show(new Border { Width = 600, Height = 800, Child = layout });
        HeadlessUi.Flush(window);
        var hero = Part(layout, AuthLayoutParts.Hero);
        var main = Part(layout, AuthLayoutParts.Main);
        Assert.False(layout.Wide);
        Assert.Equal(600, hero.Bounds.Width);
        Assert.Equal(hero.Bounds.Bottom, main.Bounds.Y, 3);
        // 窄时表单贴近顶部（顶部 xl 内边距）
        var form = main.GetLogicalDescendants().OfType<TextBlock>().Single();
        Assert.True(form.TranslatePoint(default, main)!.Value.Y < 40);
    });
}
