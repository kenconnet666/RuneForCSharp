using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Collapse：收起时内容隐藏、不占高度；展开后长到内容高；收起的过程中内容仍可见；lazy；内容层是部件。</summary>
public class CollapseTests
{
    static StackPanel Themed(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static Border Holder(Collapse collapse) => LogicalTreeHelper.GetChildren(collapse).OfType<Border>().Single();

    [Fact]
    [Upstream("Collapse", "收起时隐藏内容")]
    public Task Content_is_hidden_once_fully_collapsed() => WpfUi.Run(() =>
    {
        // 时长 token 设成 0（同 Provider 局部关闭动效），只看终态
        var collapse = new Collapse { Content = new Button { Content = "内容", Height = 40 } };
        collapse.Resources["Rune.Duration.Md"] = TimeSpan.Zero;
        var host = Themed(collapse);
        WpfUi.Show(host);

        Assert.Equal(Visibility.Collapsed, Holder(collapse).Visibility); // 不能 Tab 聚焦，读屏读不到
        Assert.Equal(0, collapse.RenderSize.Height);

        collapse.IsOpen = true;
        host.UpdateLayout();
        Assert.Equal(Visibility.Visible, Holder(collapse).Visibility);
        Assert.Equal(40, collapse.RenderSize.Height);

        collapse.IsOpen = false;
        host.UpdateLayout();
        Assert.Equal(Visibility.Collapsed, Holder(collapse).Visibility);
        Assert.Equal(0, collapse.RenderSize.Height);
    });

    [Fact]
    public Task Content_stays_visible_while_collapsing() => WpfUi.Run(() =>
    {
        var collapse = new Collapse { IsOpen = true, Content = new Button { Content = "内容", Height = 40 } };
        WpfUi.Show(Themed(collapse));

        Assert.Equal(40, collapse.RenderSize.Height); // 一开始就展开：直接到位
        collapse.IsOpen = false;
        Assert.Equal(Motion.Enabled, Holder(collapse).Visibility == Visibility.Visible); // 过渡结束才隐藏；系统关闭动画时立即隐藏
        WpfUi.Wait(TimeSpan.FromMilliseconds(400));
        Assert.Equal(Visibility.Collapsed, Holder(collapse).Visibility);
        Assert.Equal(0, collapse.RenderSize.Height);
    });

    [Fact]
    [Upstream("Collapse", "属性落到外层，propsContent 落到内容层")]
    public Task Content_layer_uses_the_part_style() => WpfUi.Run(() =>
    {
        var collapse = new Collapse { IsOpen = true, Content = new TextBlock { Text = "内容" } };
        var host = Themed(collapse);
        host.Resources.Add(CollapseParts.Content, new Style(typeof(Border)) { Setters = { new Setter(Border.BackgroundProperty, Brushes.Red) } });
        WpfUi.Show(host);

        Assert.Equal(Brushes.Red, Holder(collapse).Background);
    });

    [Fact]
    [Upstream("Collapse", "lazy：第一次展开时才渲染内容")]
    public Task Lazy_content_is_added_on_first_open_and_kept() => WpfUi.Run(() =>
    {
        var content = new TextBlock { Text = "内容" };
        var collapse = new Collapse { Lazy = true, Content = content };
        WpfUi.Show(Themed(collapse));

        Assert.Null(Holder(collapse).Child);
        collapse.IsOpen = true;
        Assert.Same(content, Holder(collapse).Child);
        collapse.IsOpen = false;
        Assert.Same(content, Holder(collapse).Child);
    });
}
