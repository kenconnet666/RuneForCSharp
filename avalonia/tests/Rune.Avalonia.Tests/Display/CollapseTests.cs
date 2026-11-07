using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>Collapse：收起时内容隐藏、不占高度；展开后长到内容高；收起的过程中内容仍可见；lazy；内容层是部件。</summary>
public class CollapseTests
{
    static Border Holder(Collapse collapse) => collapse.GetLogicalChildren().OfType<Border>().Single(b => b.Name == CollapseParts.Content);

    [Fact]
    [Upstream("Collapse", "收起时隐藏内容")]
    public Task Content_is_hidden_once_fully_collapsed() => HeadlessUi.Run(() =>
    {
        // 时长 token 设成 0（同 Provider 局部关闭动效）：Avalonia 12 的无头平台推不动动画时钟，过渡的中间态测不了
        var collapse = new Collapse { Content = new Button { Content = "内容", Height = 40 }, Resources = { ["Rune.Duration.Md"] = TimeSpan.Zero } };
        var window = HeadlessUi.Show(new StackPanel { Children = { collapse } });

        Assert.False(Holder(collapse).IsVisible); // 不能 Tab 聚焦，读屏读不到
        Assert.Equal(0, collapse.Bounds.Height);
        Assert.DoesNotContain(":open", collapse.Classes);

        collapse.IsOpen = true;
        HeadlessUi.Flush(window);
        Assert.True(Holder(collapse).IsVisible);
        Assert.Contains(":open", collapse.Classes);
        Assert.Equal(40, collapse.Bounds.Height);

        collapse.IsOpen = false;
        HeadlessUi.Flush(window);
        Assert.False(Holder(collapse).IsVisible);
        Assert.Equal(0, collapse.Bounds.Height);
    });

    [Fact]
    public Task Content_stays_visible_while_collapsing() => HeadlessUi.Run(() =>
    {
        var collapse = new Collapse { IsOpen = true, Content = new Button { Content = "内容", Height = 40 } };
        var window = HeadlessUi.Show(new StackPanel { Children = { collapse } });

        collapse.IsOpen = false;
        HeadlessUi.Flush(window);
        Assert.Equal(Motion.Enabled, Holder(collapse).IsVisible); // 过渡结束才隐藏；系统关闭动画时立即隐藏
    });

    [Fact]
    public Task Starting_open_shows_the_content_at_full_height() => HeadlessUi.Run(() =>
    {
        var collapse = new Collapse { IsOpen = true, Content = new Button { Content = "内容", Height = 40 } };
        HeadlessUi.Show(new StackPanel { Children = { collapse } });

        Assert.True(Holder(collapse).IsVisible);
        Assert.Equal(40, collapse.Bounds.Height);
    });

    [Fact]
    [Upstream("Collapse", "属性落到外层，propsContent 落到内容层")]
    public Task Content_layer_is_a_named_part() => HeadlessUi.Run(() =>
    {
        var collapse = new Collapse { IsOpen = true, Content = new TextBlock { Text = "内容" } };
        collapse.Styles.Add(new Style(x => x.OfType<Border>().Name(CollapseParts.Content)) { Setters = { new Setter(Border.BackgroundProperty, Brushes.Red) } });
        HeadlessUi.Show(collapse);

        Assert.Equal(Brushes.Red, Holder(collapse).Background);
    });

    [Fact]
    [Upstream("Collapse", "lazy：第一次展开时才渲染内容")]
    public Task Lazy_content_is_added_on_first_open_and_kept() => HeadlessUi.Run(() =>
    {
        var content = new TextBlock { Text = "内容" };
        var collapse = new Collapse { Lazy = true, Content = content };
        HeadlessUi.Show(collapse);

        Assert.Null(Holder(collapse).Child);
        collapse.IsOpen = true;
        Assert.Same(content, Holder(collapse).Child);
        collapse.IsOpen = false;
        Assert.Same(content, Holder(collapse).Child);
    });
}
