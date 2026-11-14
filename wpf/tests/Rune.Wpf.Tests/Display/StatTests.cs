using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Stat：读屏里是一组（名称是指标名）；数值按语言格式化、单位、图标；变化量的方向与色调；加载骨架与淡入；StatGroup 的竖线。</summary>
public class StatTests
{
    static double Top(UIElement part, Stat stat) => part.TranslatePoint(default, stat).Y;

    static AutomationPeer Peer(Stat stat) => UIElementAutomationPeer.CreatePeerForElement(stat);

    static void Show(double width, params UIElement[] children)
    {
        var host = new StackPanel { Width = width };
        foreach (var child in children) host.Children.Add(child);
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
    }

    [Fact]
    [Upstream("Stat", "指标名是 dt、数值与单位是 dd；数字按语言格式化，图标对读屏隐藏")]
    public Task Label_names_the_group_and_the_number_is_formatted() => WpfUi.Run(() =>
    {
        var stat = new Stat { Label = "预期销售额", Value = 1087.5m, Format = new NumericFormat { MinimumFractionDigits = 2 }, Unit = "万元", Icon = Lucide.ClipboardList };
        Show(300, stat);

        var peer = Peer(stat);
        Assert.Equal((AutomationControlType.Group, "预期销售额"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal("1,087.50", stat.Parts.Number.ShownText);
        Assert.Equal("万元", stat.Parts.Unit.Text);
        Assert.Equal(Visibility.Visible, stat.Parts.Icon.Visibility);
        Assert.DoesNotContain(peer.GetChildren() ?? [], child => child.GetName() == "预期销售额"); // 指标名一行（含图标）不另念
    });

    [Fact]
    [Upstream("Stat", "图标在指标名前；children 是数值下面的补充内容，footer 是底栏，都是 dd")]
    public Task Content_sits_below_the_value_and_the_footer_is_last() => WpfUi.Run(() =>
    {
        var progress = new Progress { Value = 81 };
        var footer = new Flex { Justify = FlexJustify.Between, Children = { new Text { Text = "年度目标 100 万元" }, new Text { Text = "完成 81%" } } };
        var stat = new Stat { Label = "测算销售", Value = 81, Unit = "万元", Icon = Lucide.ClipboardList, Content = progress, Footer = footer };
        Show(300, stat);

        Assert.True(Top(progress, stat) > Top(stat.Parts.Number, stat));
        Assert.True(Top(stat.Parts.Footer, stat) > Top(progress, stat));
        Assert.True(stat.Parts.Footer.BorderThickness.Top > 0); // 底栏上方一条线
    });

    [Fact]
    [Upstream("Stat", "字符串值原样显示")]
    public Task Text_values_show_as_is() => WpfUi.Run(() =>
    {
        var stat = new Stat { Label = "状态", Text = "—" };
        Show(double.NaN, stat);

        Assert.Equal("—", stat.Parts.Text.Text);
        Assert.Equal(Visibility.Visible, stat.Parts.Text.Visibility);
        Assert.Equal(Visibility.Collapsed, stat.Parts.Number.Visibility);
    });

    [Fact]
    [Upstream("Stat", "变化量：默认百分比带符号；上升为成功色、下降为危险色，invertChange 反过来")]
    public Task Change_is_a_signed_percent_colored_by_direction() => WpfUi.Run(() =>
    {
        Stat[] stats =
        [
            new() { Label = "a", Value = 1, Change = 0.123m, ChangeLabel = "较上月" },
            new() { Label = "b", Value = 1, Change = -0.05m },
            new() { Label = "c", Value = 1, Change = -0.05m, InvertChange = true },
            new() { Label = "d", Value = 1, Change = 0 },
        ];
        Show(double.NaN, stats[0], stats[1], stats[2], stats[3]);

        Assert.Equal(["up", "down", "down", "flat"], stats.Select(stat => stat.Direction));
        Assert.Equal("+12.3%", stats[0].Parts.ChangeNumber.ShownText);
        Assert.Equal("较上月", stats[0].Parts.ChangeLabel.Text);
        Assert.Equal([TextTone.Success, TextTone.Danger, TextTone.Success, TextTone.Muted], stats.Select(stat => stat.ChangeTone));
    });

    [Fact]
    [Upstream("Stat", "loading：指标名照常显示，数值与变化量换成对读屏隐藏的骨架，dl 带 aria-busy")]
    public Task Loading_swaps_the_value_and_change_for_skeletons() => WpfUi.Run(() =>
    {
        var stat = new Stat { Label = "营收", Value = 1200, Unit = "万元", Change = 0.1m, IsLoading = true };
        Show(double.NaN, stat);

        Assert.True(stat.SkeletonShown);
        Assert.Equal(Visibility.Collapsed, stat.Parts.Number.Visibility);
        Assert.Equal(Visibility.Collapsed, stat.Parts.Unit.Visibility);
        Assert.Equal("加载中", Peer(stat).GetItemStatus());
        Assert.Equal("营收", Peer(stat).GetName());
    });

    [Fact]
    [Upstream("Stat", "骨架换回数值时数值与变化量淡入；一开始就有数值时不淡入")]
    public Task Values_fade_in_only_after_a_skeleton() => WpfUi.Run(() =>
    {
        var stat = new Stat { Label = "营收", Value = 1200, Change = 0.1m };
        Show(double.NaN, stat);
        Assert.False(stat.Revealing);

        stat.IsLoading = true;
        WpfUi.Until(() => stat.SkeletonShown);
        stat.IsLoading = false;
        WpfUi.Until(() => !stat.SkeletonShown);
        Assert.True(stat.Revealing);
    });

    [Fact]
    [Upstream("Stat", "每个节点都有部件属性：图标、数字、单位、变化与说明、补充说明")]
    public Task Every_node_is_a_named_part() => WpfUi.Run(() =>
    {
        var stat = new Stat { Label = "营收", Value = 100, Unit = "万元", Icon = Lucide.ClipboardList, Change = 0.1m, ChangeLabel = "环比", Description = "截至昨天" };
        Show(double.NaN, stat);

        var names = WpfUi.Descendants(stat).OfType<FrameworkElement>().Select(element => element.Name).ToHashSet();
        Assert.Superset(new HashSet<string> { "PART_Label", "PART_Icon", "PART_Value", "PART_Number", "PART_Unit", "PART_Change", "PART_ChangeLabel", "PART_Description" }, names);
    });

    [Fact]
    [Upstream("Stat", "tone 给数值着色；StatGroup 里的每个 Stat 都带分隔样式")]
    public Task Tone_colors_the_number_and_grouped_stats_get_a_divider() => WpfUi.Run(() =>
    {
        var danger = new Stat { Label = "超时", Text = "10", Tone = TextTone.Danger };
        var plain = new Stat { Label = "待验收", Text = "5" };
        var solo = new Stat { Label = "超时", Text = "10" };
        var group = new StatGroup { Children = { danger, plain } };
        Show(600, group, solo);

        Assert.Equal(TextTone.Danger, danger.Parts.Text.Tone);
        Assert.Equal(default, plain.Parts.Text.Tone);
        Assert.True(plain.BorderThickness.Left > 0); // 组里有竖线
        Assert.Equal(0, solo.BorderThickness.Left);
    });
}
