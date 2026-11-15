using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>SelectionBar：有选择时显示数目和取消选择；数目变成 0 后淡出藏起来，淡出时不闪成“已选 0 项”；Label 换掉默认的文字。</summary>
public class SelectionBarTests
{
    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static string CountText(SelectionBar bar) => bar.CountContent is Text text ? text.Text ?? "" : bar.CountContent?.ToString() ?? "";

    [Fact]
    [Upstream("SelectionBar", "显示已选数目；点取消选择调用 onClear；数目为 0 时卸载")]
    public Task Shows_the_count_clears_and_hides_at_zero() => WpfUi.Run(() =>
    {
        var export = new System.Windows.Controls.Button { Content = "批量导出" };
        var bar = new SelectionBar { Count = 2, Actions = { export } };
        var clears = 0;
        bar.ClearRequested += (_, _) =>
        {
            clears++;
            bar.Count = 0;
        };
        WpfUi.Show(Host(bar));

        Assert.Equal(Visibility.Visible, bar.Visibility);
        Assert.Equal("已选 2 项", CountText(bar));
        Assert.Same(export, bar.Actions[0]);
        bar.Count = 5;
        Assert.Equal("已选 5 项", CountText(bar));
        Assert.Equal("已选 5 项", bar.Announced);

        Assert.Equal("取消选择", bar.ClearButton.Content);
        ((IInvokeProvider)bar.ClearButton.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.Equal(1, clears);
        // 淡出时仍显示最后的数目
        Assert.Equal("已选 5 项", CountText(bar));
        Assert.Equal("已选 0 项", bar.Announced);
        WpfUi.Until(() => bar.Visibility == Visibility.Collapsed);
    });

    [Fact]
    [Upstream("SelectionBar", "label 换掉默认的数目文字")]
    public Task Label_replaces_the_count_text() => WpfUi.Run(() =>
    {
        var bar = new SelectionBar { Count = 1286, Label = "已选全部 1,286 项" };
        var empty = new SelectionBar();
        WpfUi.Show(Host(bar, empty));

        Assert.Equal("已选全部 1,286 项", CountText(bar));
        Assert.Equal(Visibility.Collapsed, empty.Visibility);
    });
}
