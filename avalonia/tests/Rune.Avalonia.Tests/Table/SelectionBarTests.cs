using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>SelectionBar：有选择时显示数目和取消选择；数目变成 0 后淡出藏起来，淡出时不闪成“已选 0 项”；Label 换掉默认的文字。</summary>
public class SelectionBarTests
{
    static string CountText(SelectionBar bar) => bar.CountContent is Text text ? text.Text ?? "" : bar.CountContent?.ToString() ?? "";

    [Fact]
    [Upstream("SelectionBar", "显示已选数目；点取消选择调用 onClear；数目为 0 时卸载")]
    public Task Shows_the_count_clears_and_hides_at_zero() => HeadlessUi.Run(() =>
    {
        var export = new global::Avalonia.Controls.Button { Content = "批量导出" };
        var bar = new SelectionBar { Count = 2, Actions = { export } };
        var clears = 0;
        bar.ClearRequested += (_, _) =>
        {
            clears++;
            bar.Count = 0;
        };
        HeadlessUi.Show(bar);

        Assert.True(bar.IsVisible);
        Assert.Equal("已选 2 项", CountText(bar));
        Assert.Same(export, bar.Actions[0]);
        bar.Count = 5;
        Assert.Equal("已选 5 项", CountText(bar));
        Assert.Equal("已选 5 项", bar.Announced);

        Assert.Equal("取消选择", bar.ClearButton.Content);
        ((IInvokeProvider)bar.ClearButton.Peer()).Invoke();
        Assert.Equal(1, clears);
        // 淡出时仍显示最后的数目
        Assert.Equal("已选 5 项", CountText(bar));
        Assert.Equal("已选 0 项", bar.Announced);
        HeadlessUi.Until(() => !bar.IsVisible);
    });

    [Fact]
    [Upstream("SelectionBar", "label 换掉默认的数目文字")]
    public Task Label_replaces_the_count_text() => HeadlessUi.Run(() =>
    {
        var bar = new SelectionBar { Count = 1286, Label = "已选全部 1,286 项" };
        var empty = new SelectionBar();
        HeadlessUi.Show(new StackPanel { Children = { bar, empty } });

        Assert.Equal("已选全部 1,286 项", CountText(bar));
        Assert.False(empty.IsVisible);
    });
}
