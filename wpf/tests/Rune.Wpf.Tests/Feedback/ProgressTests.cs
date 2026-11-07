using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>Progress：读屏的进度条与取值文字；不确定进度；decimal 按原值夹住；目标线；环形；标签行；部件样式。</summary>
public class ProgressTests
{
    /// <summary>放进中文的作用域（"60%，目标 80%"），条形宽 300。</summary>
    static Progress Show(Progress progress)
    {
        var host = new StackPanel { Width = 300 };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        Provider.SetLang(host, Lang.ZhCn);
        host.Children.Add(progress);
        WpfUi.Show(host);
        return progress;
    }

    static AutomationPeer Peer(Progress progress) => UIElementAutomationPeer.CreatePeerForElement(progress);

    static string Spoken(Progress progress) => ((IValueProvider)Peer(progress).GetPattern(PatternInterface.Value)).Value;

    static IRangeValueProvider Range(Progress progress) => (IRangeValueProvider)Peer(progress).GetPattern(PatternInterface.RangeValue);

    static IEnumerable<Text> Texts(Progress progress) => WpfUi.Descendants(progress).OfType<Text>().Where(t => t.IsVisible);

    [Fact]
    [Upstream("Progress", "有值：role=")]
    public Task Value_is_clamped_and_spoken_as_a_percentage() => WpfUi.Run(() =>
    {
        var progress = Show(new Progress { Value = 150, Max = 200 });

        Assert.Equal(AutomationControlType.ProgressBar, Peer(progress).GetAutomationControlType());
        Assert.Equal((150d, 0d, 200d), (Range(progress).Value, Range(progress).Minimum, Range(progress).Maximum));
        Assert.Equal("75%", Spoken(progress));
        Assert.Equal(ProgressState.Loading, progress.State);
        Assert.Equal(ProgressState.Complete, Show(new Progress { Value = 999 }).State);
    });

    [Fact]
    [Upstream("Progress", "没有值：不确定进度")]
    public Task Without_value_it_is_indeterminate_and_value_text_is_custom() => WpfUi.Run(() =>
    {
        var progress = Show(new Progress());
        Assert.Equal(ProgressState.Indeterminate, progress.State);
        Assert.Equal("", Spoken(progress));

        var custom = Show(new Progress { Value = 2, Max = 5, ValueText = (value, max) => $"{value} / {max} 个文件" });
        Assert.Equal("2 / 5 个文件", Spoken(custom));
    });

    [Fact]
    [Upstream("Progress", "bigint、Decimal：按原值比较与夹住")]
    public Task Decimal_values_compare_and_clamp_exactly() => WpfUi.Run(() =>
    {
        var big = Show(new Progress { Value = 9_007_199_254_740_993m, Max = 9_007_199_254_740_993m, ValueText = (value, _) => $"{value} 字节" });
        Assert.Equal(ProgressState.Complete, big.State);
        Assert.Equal("9007199254740993 字节", Spoken(big));

        var money = Show(new Progress { Value = 150.5m, Max = 100m });
        Assert.Equal(100d, Range(money).Value);
        Assert.Equal("100%", Spoken(money));
    });

    [Fact]
    [Upstream("Progress", "target：条上画目标线")]
    public Task Target_draws_a_line_and_is_spoken_after_the_value() => WpfUi.Run(() =>
    {
        var progress = Show(new Progress { Value = 60, Target = 80 });
        Assert.Equal("60%，目标 80%", Spoken(progress));
        var borders = WpfUi.Descendants(progress).OfType<Border>().ToList();
        var (indicator, line) = (borders[0], borders[1]);
        Assert.True(line.IsVisible);
        Assert.Equal(0.8 * 300, line.TranslatePoint(new Point(line.ActualWidth / 2, 0), progress).X, 0.5); // 按比例定位
        Assert.Equal(0.6 * 300, indicator.ActualWidth, 0.5);
    });

    [Fact]
    [Upstream("Progress", "环形：根元素是 progressbar")]
    public Task Circle_shows_the_percentage_in_the_middle_and_the_label_below() => WpfUi.Run(() =>
    {
        var ring = Show(new Progress { Shape = ProgressShape.Circle, Value = 72 });
        var em = ring.FontSize;
        Assert.Equal(new Size(5 * em, 5 * em), ring.RenderSize); // 直径 5em，按内容宽
        Assert.Equal(["72%"], Texts(ring).Select(t => t.Text));
        Assert.Null(Peer(ring).GetChildren()); // 中间的数值对读屏隐藏，读屏从取值得知
        Assert.Equal(FontWeights.SemiBold, Texts(ring).Single().FontWeight);

        var labelled = Show(new Progress { Shape = ProgressShape.Circle, Value = 40, Target = 50, Label = "本月完成率" });
        Assert.Equal("本月完成率", Peer(labelled).GetName());
        Assert.Equal("40%，目标 50%", Spoken(labelled));
        var label = Texts(labelled).Single(t => t.Text == "本月完成率");
        Assert.True(label.TranslatePoint(default, labelled).Y >= 5 * em); // 标签在环的下方

        Assert.Equal(ProgressState.Indeterminate, Show(new Progress { Shape = ProgressShape.Circle }).State);
    });

    [Fact]
    [Upstream("Progress", "label：上方的标签是名称")]
    public Task Label_row_names_the_bar_and_shows_the_value() => WpfUi.Run(() =>
    {
        var storage = Show(new Progress { Value = 38, Max = 50, Label = "存储空间", ValueLabel = "38 / 50 GB" });
        Assert.Equal("存储空间", Peer(storage).GetName());
        Assert.Equal(["存储空间", "38 / 50 GB"], Texts(storage).Select(t => t.Text));
        Assert.Equal("76%", Spoken(storage)); // 读屏念的仍是取值文字
        Assert.Equal(0.93 * storage.FontSize, Texts(storage).First().FontSize, 3);

        Assert.Equal(["资料完善度", "64%"], Texts(Show(new Progress { Value = 64, Label = "资料完善度" })).Select(t => t.Text));
        Assert.Equal(["完善度"], Texts(Show(new Progress { Value = 64, Label = "完善度", ShowValueLabel = false })).Select(t => t.Text));
        // 不确定进度没有数值；没有标签时只有进度条
        Assert.Equal(["加载"], Texts(Show(new Progress { Label = "加载" })).Select(t => t.Text));
        Assert.Empty(Texts(Show(new Progress { Value = 1 })));
    });

    [Fact]
    public Task Indicator_follows_the_tone_and_part_styles_override_it() => WpfUi.Run(() =>
    {
        var danger = Show(new Progress { Value = 50, Tone = StatusTone.Danger });
        var indicator = WpfUi.Descendants(danger).OfType<Border>().First();
        Assert.Equal(((SolidColorBrush)danger.FindResource("Rune.Brush.DangerText")).Color, ((SolidColorBrush)indicator.Background).Color);

        // TS 的 propsValueLabel：Provider 同键的部件样式叠在主题的上面（等宽数字仍在）
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf" Width="300">
              <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style x:Key="{x:Static rune:ProgressParts.ValueLabel}" TargetType="rune:Text"><Setter Property="Tone" Value="danger" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <rune:Progress x:Name="custom" Value="120" Label="API 调用" />
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)System.Windows.Markup.XamlReader.Parse(Xaml);
        WpfUi.Show(root);
        var value = Texts((Progress)root.FindName("custom")).Last();
        Assert.Equal(TextTone.Danger, value.Tone);
        Assert.True(value.Numeric);
    });
}
