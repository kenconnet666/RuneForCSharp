using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>ActionBar：起始边的说明在前、按钮在后；放在滚动区域的最后时贴着底边（盖着下面的内容时 IsStuck），滚到底与内容平齐；Sticky=False 不挪。</summary>
public class ActionBarTests
{
    static FrameworkElement Themed(FrameworkElement child)
    {
        child.Resources.MergedDictionaries.Add(new RuneTheme());
        return child;
    }

    [Fact]
    [Upstream("ActionBar", "start 在前、按钮在后")]
    public Task Start_comes_before_the_actions_and_is_hidden_without_text() => WpfUi.Run(() =>
    {
        var save = new Button { Content = "保存" };
        var bar = new ActionBar { Start = "有 3 处改动还没保存", Children = { save } };
        WpfUi.Show(Themed(new StackPanel { Width = 400, Children = { bar } }));

        Assert.Equal(Visibility.Visible, bar.StartPart.Visibility);
        Assert.True(bar.StartPart.TranslatePoint(default, bar).X < save.TranslatePoint(default, bar).X);
        bar.Start = null;
        Assert.Equal(Visibility.Collapsed, bar.StartPart.Visibility);
    });

    [Fact]
    public Task Sticks_to_the_bottom_of_the_scroll_area_until_scrolled_to_the_end() => WpfUi.Run(() =>
    {
        var bar = new ActionBar { Start = "改动", Children = { new Button { Content = "保存" } } };
        var scroll = new ScrollArea { Height = 300, Width = 400, Content = new StackPanel { Children = { new Border { Height = 1000 }, bar } } };
        WpfUi.Show(Themed(new StackPanel { Children = { scroll } }));
        WpfUi.Flush();

        Assert.True(bar.IsStuck);
        Assert.True(bar.Lift > 0);
        // 挪上来的底边正好在视口的底边
        Assert.Equal(scroll.ViewportHeight, bar.TransformToAncestor(scroll).Transform(new Point(0, bar.ActualHeight)).Y, 1);

        scroll.ScrollToEnd();
        WpfUi.Flush();
        Assert.False(bar.IsStuck);
        Assert.Equal(0, bar.Lift, 1);

        scroll.ScrollToHome();
        bar.Sticky = false;
        WpfUi.Flush();
        Assert.False(bar.IsStuck);
        Assert.Equal(0, bar.Lift, 1);
    });
}
