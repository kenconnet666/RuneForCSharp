namespace Rune.WinForms.Tests;

/// <summary>StatControl：读屏里是一组（名称是指标名、描述是数值）；数值按语言格式化、单位、图标；变化量的方向与色调；加载骨架；StatGroupControl 的竖线。</summary>
public class StatTests
{
    [Fact]
    [Upstream("Stat", "指标名是 dt、数值与单位是 dd；数字按语言格式化，图标对读屏隐藏")]
    public Task Label_names_the_group_and_the_number_is_formatted() => WinFormsUi.Run(() =>
    {
        var stat = new StatControl { Label = "预期销售额", Value = 1087.5m, Format = new NumericFormat { MinimumFractionDigits = 2 }, Unit = "万元", Icon = Lucide.ClipboardList, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(stat);

        Assert.Equal((AccessibleRole.Grouping, "预期销售额", "1,087.50 万元"), (stat.AccessibilityObject.Role, stat.AccessibilityObject.Name, stat.AccessibilityObject.Description));
        Assert.Equal("1,087.50", stat.Numbers.Number.ShownText);
        Assert.True(stat.Elements.Icon.Bounds.Right < stat.Elements.Unit.Bounds.Right);
    });

    [Fact]
    [Upstream("Stat", "图标在指标名前；children 是数值下面的补充内容，footer 是底栏，都是 dd")]
    public Task Content_sits_below_the_value_and_the_footer_is_last() => WinFormsUi.Run(() =>
    {
        var progress = new ProgressControl { Value = 81 };
        var footer = new TextControl { Text = "年度目标 100 万元" };
        var stat = new StatControl { Label = "测算销售", Value = 81, Unit = "万元", Icon = Lucide.ClipboardList, Content = progress, Footer = footer, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(stat);

        Assert.True(progress.Top > stat.Numbers.Number.Bottom);
        Assert.True(footer.Top > progress.Bottom);
    });

    [Fact]
    [Upstream("Stat", "字符串值原样显示")]
    public Task Text_values_show_as_is() => WinFormsUi.Run(() =>
    {
        var stat = new StatControl { Label = "状态", Text = "—", Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(stat);

        Assert.Equal("—", stat.Elements.Text.Content);
        Assert.False(stat.Numbers.Number.Visible);
        Assert.Equal("—", stat.AccessibilityObject.Description);
    });

    [Fact]
    [Upstream("Stat", "变化量：默认百分比带符号；上升为成功色、下降为危险色，invertChange 反过来")]
    public Task Change_is_a_signed_percent_colored_by_direction() => WinFormsUi.Run(() =>
    {
        StatControl[] stats =
        [
            new() { Label = "a", Value = 1, Change = 0.123m, ChangeLabel = "较上月" },
            new() { Label = "b", Value = 1, Change = -0.05m },
            new() { Label = "c", Value = 1, Change = -0.05m, InvertChange = true },
            new() { Label = "d", Value = 1, Change = 0 },
        ];
        using var form = WinFormsUi.Show(stats);

        Assert.Equal(["up", "down", "down", "flat"], stats.Select(stat => stat.Direction));
        Assert.Equal("+12.3%", stats[0].Numbers.Change.ShownText);
        Assert.Equal("较上月", stats[0].Elements.ChangeLabel.Content);
        Assert.Equal([TextTone.Success, TextTone.Danger, TextTone.Success, TextTone.Muted], stats.Select(stat => stat.ChangeTone));
    });

    [Fact]
    [Upstream("Stat", "loading：指标名照常显示，数值与变化量换成对读屏隐藏的骨架，dl 带 aria-busy")]
    public Task Loading_swaps_the_value_and_change_for_skeletons() => WinFormsUi.Run(() =>
    {
        var stat = new StatControl { Label = "营收", Value = 1200, Unit = "万元", Change = 0.1m, IsLoading = true, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(stat);

        Assert.True(stat.SkeletonShown);
        Assert.False(stat.Numbers.Number.Visible);
        Assert.False(stat.Numbers.Change.Visible);
        Assert.True((stat.AccessibilityObject.State & AccessibleStates.Busy) != 0);
        Assert.Equal("营收", stat.AccessibilityObject.Name);
    });

    [Fact]
    [Upstream("Stat", "tone 给数值着色；StatGroup 里的每个 Stat 都带分隔样式")]
    public Task Tone_colors_the_number_and_grouped_stats_are_padded() => WinFormsUi.Run(() =>
    {
        var danger = new StatControl { Label = "超时", Value = 10, Tone = TextTone.Danger };
        var plain = new StatControl { Label = "待验收", Value = 5 };
        var group = new StatGroupControl { Dock = DockStyle.Top };
        group.Controls.AddRange([danger, plain]);
        var solo = new StatControl { Label = "超时", Value = 10, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(solo, group);

        Assert.Equal(TextTone.Danger, danger.Numbers.Number.Tone);
        Assert.Equal(default, plain.Numbers.Number.Tone);
        Assert.True(plain.Numbers.Number.Left > 0); // 组里左右空 space.md
        Assert.Equal(0, solo.Numbers.Number.Left);
    });
}
