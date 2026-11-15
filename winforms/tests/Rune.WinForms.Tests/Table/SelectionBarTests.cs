namespace Rune.WinForms.Tests;

/// <summary>SelectionBarControl：有选择时显示数目和取消选择；数目变成 0 后不显示；Label 换掉默认的文字。</summary>
public class SelectionBarTests
{
    static Form Show(params Control[] controls)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        var stack = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Top };
        stack.Controls.AddRange(controls);
        host.Controls.Add(stack);
        return WinFormsUi.Show(host);
    }

    [Fact]
    [Upstream("SelectionBar", "显示已选数目；点取消选择调用 onClear；数目为 0 时卸载")]
    public Task Shows_the_count_clears_and_hides_at_zero() => WinFormsUi.Run(() =>
    {
        var export = new ButtonControl { Text = "批量导出" };
        var bar = new SelectionBarControl { Count = 2 };
        bar.Actions.Add(export);
        var clears = 0;
        bar.ClearRequested += (_, _) =>
        {
            clears++;
            bar.Count = 0;
        };
        using var form = Show(bar);

        Assert.True(bar.Visible);
        Assert.Equal("已选 2 项", bar.CountText);
        Assert.Same(export, bar.Actions[0]);
        bar.Count = 5;
        Assert.Equal("已选 5 项", bar.CountText);
        Assert.Equal("已选 5 项", bar.Announced);

        Assert.Equal("取消选择", bar.ClearButton.Text);
        bar.ClearButton.PerformClick();
        Assert.Equal(1, clears);
        Assert.False(bar.Visible);
        // 藏起来时数目停在最后一次，下次出现前不闪成“已选 0 项”
        Assert.Equal("已选 5 项", bar.CountText);
        Assert.Equal("已选 0 项", bar.Announced);
    });

    [Fact]
    [Upstream("SelectionBar", "label 换掉默认的数目文字")]
    public Task Label_replaces_the_count_text() => WinFormsUi.Run(() =>
    {
        var bar = new SelectionBarControl { Count = 1286, Label = "已选全部 1,286 项" };
        var empty = new SelectionBarControl();
        using var form = Show(bar, empty);

        Assert.Equal("已选全部 1,286 项", bar.CountText);
        Assert.False(empty.Visible);
    });
}
