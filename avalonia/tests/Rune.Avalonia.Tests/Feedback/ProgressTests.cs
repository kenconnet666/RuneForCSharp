using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Progress：读屏的进度条与取值文字；不确定进度；decimal 按原值夹住；目标线；环形；标签行。</summary>
public class ProgressTests
{
    /// <summary>放进中文的作用域（"60%，目标 80%"），条形宽 300。</summary>
    static Progress Show(Progress progress)
    {
        var host = new StackPanel { Width = 300, Children = { progress } };
        Provider.SetLang(host, Lang.ZhCn);
        HeadlessUi.Show(host);
        return progress;
    }

    static AutomationPeer Peer(Progress progress) => ControlAutomationPeer.CreatePeerForElement(progress);

    static string? Spoken(Progress progress) => ((IValueProvider)Peer(progress)).Value;

    static IEnumerable<Text> Texts(Progress progress) => progress.GetVisualDescendants().OfType<Text>().Where(t => t.IsEffectivelyVisible);

    [Fact]
    [Upstream("Progress", "有值：role=")]
    public Task Value_is_clamped_and_spoken_as_a_percentage() => HeadlessUi.Run(() =>
    {
        var progress = Show(new Progress { Value = 150, Max = 200 });
        var range = (IRangeValueProvider)Peer(progress);

        Assert.Equal(AutomationControlType.ProgressBar, Peer(progress).GetAutomationControlType());
        Assert.Equal((150d, 0d, 200d), (range.Value, range.Minimum, range.Maximum));
        Assert.Equal("75%", Spoken(progress));
        Assert.Equal(ProgressState.Loading, progress.State);
        Assert.Equal(ProgressState.Complete, Show(new Progress { Value = 999 }).State);
        Assert.Contains(":complete", Show(new Progress { Value = 999 }).Classes);
    });

    [Fact]
    [Upstream("Progress", "没有值：不确定进度")]
    public Task Without_value_it_is_indeterminate_and_value_text_is_custom() => HeadlessUi.Run(() =>
    {
        var progress = Show(new Progress());
        Assert.Equal(ProgressState.Indeterminate, progress.State);
        Assert.Contains(":indeterminate", progress.Classes);
        Assert.Null(Spoken(progress));

        var custom = Show(new Progress { Value = 2, Max = 5, ValueText = (value, max) => $"{value} / {max} 个文件" });
        Assert.Equal("2 / 5 个文件", Spoken(custom));
    });

    [Fact]
    [Upstream("Progress", "bigint、Decimal：按原值比较与夹住")]
    public Task Decimal_values_compare_and_clamp_exactly() => HeadlessUi.Run(() =>
    {
        var big = Show(new Progress { Value = 9_007_199_254_740_993m, Max = 9_007_199_254_740_993m, ValueText = (value, _) => $"{value} 字节" });
        Assert.Equal(ProgressState.Complete, big.State);
        Assert.Equal("9007199254740993 字节", Spoken(big));

        var money = Show(new Progress { Value = 150.5m, Max = 100m });
        Assert.Equal(100d, ((IRangeValueProvider)Peer(money)).Value);
        Assert.Equal("100%", Spoken(money));
    });

    [Fact]
    [Upstream("Progress", "target：条上画目标线")]
    public Task Target_draws_a_line_and_is_spoken_after_the_value() => HeadlessUi.Run(() =>
    {
        var progress = Show(new Progress { Value = 60, Target = 80 });
        Assert.Equal("60%，目标 80%", Spoken(progress));
        var line = progress.GetVisualDescendants().OfType<Border>().Single(b => b.Name != ProgressParts.Indicator);
        Assert.True(line.IsVisible);
        Assert.Equal(0.8 * 300, line.Bounds.Center.X, 0.5); // 按比例定位
        var indicator = progress.GetVisualDescendants().OfType<Border>().Single(b => b.Name == ProgressParts.Indicator);
        Assert.Equal(0.6 * 300, indicator.Bounds.Width, 0.5);
    });

    [Fact]
    [Upstream("Progress", "环形：根元素是 progressbar")]
    public Task Circle_shows_the_percentage_in_the_middle_and_the_label_below() => HeadlessUi.Run(() =>
    {
        var ring = Show(new Progress { Shape = ProgressShape.Circle, Value = 72 });
        var em = ring.FontSize;
        Assert.Equal(new Size(5 * em, 5 * em), ring.Bounds.Size); // 直径 5em
        Assert.Equal(["72%"], Texts(ring).Select(t => t.Text));
        Assert.DoesNotContain(Peer(ring).GetChildren(), child => child.IsControlElement()); // 中间的数值对读屏隐藏，读屏从取值得知

        var labelled = Show(new Progress { Shape = ProgressShape.Circle, Value = 40, Target = 50, Label = "本月完成率" });
        Assert.Equal("本月完成率", Peer(labelled).GetName());
        Assert.Equal("40%，目标 50%", Spoken(labelled));
        var label = Texts(labelled).Single(t => t.Text == "本月完成率");
        Assert.True(label.Bounds.Top >= 5 * em); // 标签在环的下方

        Assert.Equal(ProgressState.Indeterminate, Show(new Progress { Shape = ProgressShape.Circle }).State);
    });

    [Fact]
    [Upstream("Progress", "label：上方的标签是名称")]
    public Task Label_row_names_the_bar_and_shows_the_value() => HeadlessUi.Run(() =>
    {
        var storage = Show(new Progress { Value = 38, Max = 50, Label = "存储空间", ValueLabel = "38 / 50 GB" });
        Assert.Equal("存储空间", Peer(storage).GetName());
        Assert.Equal(["存储空间", "38 / 50 GB"], Texts(storage).Select(t => t.Text));
        Assert.Equal("76%", Spoken(storage)); // 读屏念的仍是取值文字
        var header = storage.GetLogicalChildren().OfType<Flex>().Single(f => f.Name == ProgressParts.Header);
        Assert.Equal(0.93 * storage.FontSize, header.GetValue(global::Avalonia.Controls.Documents.TextElement.FontSizeProperty), 3);

        Assert.Equal(["资料完善度", "64%"], Texts(Show(new Progress { Value = 64, Label = "资料完善度" })).Select(t => t.Text));
        Assert.Equal(["完善度"], Texts(Show(new Progress { Value = 64, Label = "完善度", ShowValueLabel = false })).Select(t => t.Text));
        // 不确定进度没有数值；没有标签时只有进度条
        Assert.Equal(["加载"], Texts(Show(new Progress { Label = "加载" })).Select(t => t.Text));
        Assert.Empty(Texts(Show(new Progress { Value = 1 })));
    });
}
