using Avalonia;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>ActionBar：起始边的说明在前、按钮在后；放在滚动区域的最后时贴着底边（盖着下面的内容时 :stuck），滚到底与内容平齐；Sticky=False 不挪。</summary>
public class ActionBarTests
{
    [Fact]
    [Upstream("ActionBar", "start 在前、按钮在后")]
    public Task Start_comes_before_the_actions_and_is_hidden_without_text() => HeadlessUi.Run(() =>
    {
        var save = new Button { Content = "保存" };
        var bar = new ActionBar { Start = "有 3 处改动还没保存", Children = { save } };
        HeadlessUi.Show(bar);

        var start = bar.Part<ContentControl>(ActionBarParts.Start);
        Assert.True(start.IsVisible);
        Assert.True(start.TranslatePoint(default, bar)!.Value.X < save.TranslatePoint(default, bar)!.Value.X);
        bar.Start = null;
        Assert.False(start.IsVisible);
    });

    [Fact]
    public Task Sticks_to_the_bottom_of_the_scroll_area_until_scrolled_to_the_end() => HeadlessUi.Run(() =>
    {
        var bar = new ActionBar { Start = "改动", Children = { new Button { Content = "保存" } } };
        var scroll = new ScrollArea { Height = 300, Content = new StackPanel { Children = { new Border { Height = 1000 }, bar } } };
        var window = HeadlessUi.Show(scroll);

        Assert.True(bar.IsStuck);
        Assert.True(bar.Lift > 0);
        // 挪上来的底边正好在视口的底边
        Assert.Equal(300, bar.TranslatePoint(new global::Avalonia.Point(0, bar.Bounds.Height), scroll)!.Value.Y, 1);

        scroll.Offset = new global::Avalonia.Vector(0, 10_000);
        HeadlessUi.Flush(window);
        Assert.False(bar.IsStuck);
        Assert.Equal(0, bar.Lift, 1);

        scroll.Offset = default;
        bar.Sticky = false;
        HeadlessUi.Flush(window);
        Assert.False(bar.IsStuck);
        Assert.Equal(0, bar.Lift, 1);
    });
}
