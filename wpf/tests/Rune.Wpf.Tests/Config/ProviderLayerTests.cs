using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>
/// <c>Provider.Styles</c> 的附加属性形式：任何元素都能当一层（单个实例的 propsXxx 不必再包一层 Provider），
/// 合成规则与 Provider 一样，叠在最近的外层（Provider 或别的元素的这一层）上。
/// </summary>
public class ProviderLayerTests
{
    const string Ns = """
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf"
        xmlns:t="clr-namespace:Rune.Wpf.Tests;assembly=Rune.Wpf.Tests"
        """;

    static FrameworkElement Load(string body)
    {
        var root = (FrameworkElement)XamlReader.Parse($"<StackPanel {Ns}><StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>{body}</StackPanel>");
        WpfUi.Show(root);
        return root;
    }

    static Text ValueLabel(Progress progress) => WpfUi.Descendants(progress).OfType<Text>().Last(text => text.IsVisible);

    static string Label(ProbeButton b)
    {
        var label = b.Part<TextBlock>("PART_Label");
        return $"{label.FontWeight}/{((SolidColorBrush)label.Foreground).Color}";
    }

    [Fact]
    public Task A_single_component_restyles_its_own_part() => WpfUi.Run(() =>
    {
        var root = Load("""
            <rune:Progress x:Name="styled" Value="120" Label="API 调用">
              <rune:Provider.Styles>
                <ResourceDictionary>
                  <Style x:Key="{x:Static rune:ProgressParts.ValueLabel}" TargetType="rune:Text"><Setter Property="Tone" Value="danger" /></Style>
                </ResourceDictionary>
              </rune:Provider.Styles>
            </rune:Progress>
            <rune:Progress x:Name="plain" Value="40" Label="存储" />
            """);

        var styled = ValueLabel((Progress)root.FindName("styled"));
        Assert.Equal((TextTone.Danger, true), (styled.Tone, styled.Numeric)); // 叠在主题的部件样式上：等宽数字仍在
        Assert.Equal(TextTone.Muted, ValueLabel((Progress)root.FindName("plain")).Tone); // 别的实例不受影响
    });

    [Fact]
    public Task An_element_layer_merges_onto_an_outer_provider_setter_by_setter() => WpfUi.Run(() =>
    {
        var root = Load("""
            <rune:Provider>
              <rune:Provider.Styles>
                <ResourceDictionary>
                  <Style TargetType="t:ProbeButton"><Setter Property="Variant" Value="outline" /><Setter Property="Padding" Value="3" /></Style>
                  <Style x:Key="{x:Static t:ProbeParts.Label}" TargetType="TextBlock"><Setter Property="FontWeight" Value="Light" /></Style>
                </ResourceDictionary>
              </rune:Provider.Styles>
              <StackPanel>
                <t:ProbeButton x:Name="sibling" />
                <t:ProbeButton x:Name="styled">
                  <rune:Provider.Styles>
                    <ResourceDictionary>
                      <Style TargetType="t:ProbeButton"><Setter Property="Variant" Value="ghost" /></Style>
                      <Style x:Key="{x:Static t:ProbeParts.Label}" TargetType="TextBlock"><Setter Property="Foreground" Value="Red" /></Style>
                    </ResourceDictionary>
                  </rune:Provider.Styles>
                </t:ProbeButton>
              </StackPanel>
            </rune:Provider>
            """);
        var (sibling, styled) = ((ProbeButton)root.FindName("sibling"), (ProbeButton)root.FindName("styled"));

        // 元素自己这一层的类型键样式作用到它自己：只改了 Variant，Padding 沿用外层
        Assert.Equal(("ghost", new Thickness(3)), (styled.Variant, styled.Padding));
        Assert.Equal($"Light/{Colors.Red}", Label(styled)); // 部件：外层的字重 + 这一层的颜色
        Assert.Equal(("outline", $"Light/{Colors.Black}"), (sibling.Variant, Label(sibling)));
        Assert.Equal(1, styled.StyleChanges); // 时序：只套一次 Style
    });

    [Fact]
    public Task A_type_style_on_an_element_layer_reaches_parts_of_that_type() => WpfUi.Run(() =>
    {
        var root = Load("""
            <StackPanel x:Name="scope">
              <rune:Provider.Styles>
                <ResourceDictionary>
                  <Style TargetType="rune:Text"><Setter Property="Weight" Value="semibold" /></Style>
                </ResourceDictionary>
              </rune:Provider.Styles>
              <rune:Progress x:Name="progress" Value="40" Label="存储" />
            </StackPanel>
            """);

        var value = ValueLabel((Progress)root.FindName("progress"));
        Assert.Equal(TextWeight.Semibold, value.Weight); // 部件键 ProgressParts.ValueLabel 也叠上了这一层的 Text 样式
        Assert.Equal(TextTone.Muted, value.Tone); // 主题的部件样式仍在下面
    });

    [Fact]
    public Task Changing_an_outer_layer_recomposes_the_inner_element_layer() => WpfUi.Run(() =>
    {
        var root = Load("""
            <rune:Provider x:Name="outer">
              <rune:Provider.Styles>
                <ResourceDictionary>
                  <Style x:Key="{x:Static t:ProbeParts.Label}" TargetType="TextBlock"><Setter Property="FontWeight" Value="Light" /></Style>
                </ResourceDictionary>
              </rune:Provider.Styles>
              <t:ProbeButton x:Name="styled">
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style x:Key="{x:Static t:ProbeParts.Label}" TargetType="TextBlock"><Setter Property="Foreground" Value="Red" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
              </t:ProbeButton>
            </rune:Provider>
            """);
        var outer = (Provider)root.FindName("outer");
        var styled = (ProbeButton)root.FindName("styled");

        outer.Styles = new ResourceDictionary { [ProbeParts.Label] = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.FontWeightProperty, FontWeights.Black) } } };
        WpfUi.Flush();
        Assert.Equal($"Black/{Colors.Red}", Label(styled));
    });

    [Fact]
    public Task Code_can_set_and_replace_an_element_layer() => WpfUi.Run(() =>
    {
        var progress = new Progress { Value = 120, Label = "API 调用" };
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(progress);
        WpfUi.Show(host);

        Provider.SetStyles(progress, new ResourceDictionary { [ProgressParts.ValueLabel] = new Style(typeof(Text)) { Setters = { new Setter(Text.ToneProperty, TextTone.Danger) } } });
        Assert.Equal(TextTone.Danger, ValueLabel(progress).Tone);

        // 换成更少的键：旧的结果不留在 Resources 里
        Provider.SetStyles(progress, new ResourceDictionary());
        Assert.Equal(TextTone.Muted, ValueLabel(progress).Tone);
    });
}
