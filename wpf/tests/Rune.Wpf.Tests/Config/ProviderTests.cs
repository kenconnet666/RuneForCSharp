using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

public class ProviderTests
{
    /// <summary>
    /// 用 XAML 搭嵌套的 Provider：走真实的构造顺序（自上而下构造、子元素先于父元素 Initialized）。
    /// 外层改 Variant 与 Padding、部件字重；内层只改 Variant 与部件前景色。
    /// </summary>
    const string Nested = """
        <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf"
                    xmlns:t="clr-namespace:Rune.Wpf.Tests;assembly=Rune.Wpf.Tests">
          <rune:Provider>
            <rune:Provider.Styles>
              <ResourceDictionary>
                <Style TargetType="t:ProbeButton"><Setter Property="Variant" Value="outline" /><Setter Property="Padding" Value="3" /></Style>
                <Style x:Key="{x:Static t:ProbeParts.Label}" TargetType="TextBlock"><Setter Property="FontWeight" Value="Light" /></Style>
              </ResourceDictionary>
            </rune:Provider.Styles>
            <StackPanel>
              <t:ProbeButton x:Name="outer" />
              <t:ProbeButton x:Name="local" Variant="ghost" />
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style TargetType="t:ProbeButton"><Setter Property="Variant" Value="ghost" /></Style>
                    <Style x:Key="{x:Static t:ProbeParts.Label}" TargetType="TextBlock"><Setter Property="Foreground" Value="Red" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <t:ProbeButton x:Name="inner" />
              </rune:Provider>
            </StackPanel>
          </rune:Provider>
          <t:ProbeButton x:Name="outside" />
        </StackPanel>
        """;

    static (FrameworkElement Root, Func<string, ProbeButton> Find) Load()
    {
        var root = (FrameworkElement)XamlReader.Parse(Nested);
        WpfUi.Show(root);
        return (root, name => (ProbeButton)root.FindName(name));
    }

    static string Label(ProbeButton b)
    {
        var label = b.Part<TextBlock>("PART_Label");
        return $"{label.FontWeight}/{((SolidColorBrush)label.Foreground).Color}";
    }

    [Fact]
    public Task Inner_layer_merges_onto_outer_layer() => WpfUi.Run(() =>
    {
        var (_, find) = Load();

        Assert.Equal(("outline", new Thickness(3)), (find("outer").Variant, find("outer").Padding));
        // 内层只写了 Variant：Padding 沿用外层（WPF 原生的嵌套隐式样式会整个替换，Padding 就丢了）
        Assert.Equal(("ghost", new Thickness(3)), (find("inner").Variant, find("inner").Padding));
    });

    [Fact]
    public Task Explicit_values_win_over_providers() => WpfUi.Run(() =>
    {
        var (_, find) = Load();

        Assert.Equal("ghost", find("local").Variant);
        Assert.Equal("solid", find("outside").Variant);
    });

    [Fact]
    public Task Part_styles_merge_by_key() => WpfUi.Run(() =>
    {
        var (_, find) = Load();

        Assert.Equal($"Light/{Colors.Black}", Label(find("outer")));
        Assert.Equal($"Light/{Colors.Red}", Label(find("inner")));
        // Provider 之外取主题里的部件样式
        Assert.Equal($"Bold/{Colors.Black}", Label(find("outside")));
    });

    [Fact]
    public Task Each_element_gets_its_style_once() => WpfUi.Run(() =>
    {
        var (_, find) = Load();

        // 合成的时序处理不当时，内层的子元素会先拿到外层的结果、再换一次 Style
        Assert.Equal(1, find("outer").StyleChanges);
        Assert.Equal(1, find("inner").StyleChanges);
        Assert.Equal(0, find("outside").StyleChanges);
    });

    [Fact]
    public Task Same_fragment_on_the_same_outer_layer_is_composed_once() => WpfUi.Run(() =>
    {
        // 列表里每一项各包一个 Provider、用同一个样式片段时，共用同一份合成结果
        var fragment = new Style(typeof(ProbeButton)) { Setters = { new Setter(ProbeButton.VariantProperty, "ghost") } };
        ProbeButton first = new(), second = new();
        var host = new StackPanel
        {
            Children =
            {
                new Provider { Styles = new ResourceDictionary { [typeof(ProbeButton)] = fragment }, Child = first },
                new Provider { Styles = new ResourceDictionary { [typeof(ProbeButton)] = fragment }, Child = second },
            },
        };
        WpfUi.Show(host);

        Assert.Equal("ghost", first.Variant);
        Assert.Same(first.Style, second.Style);
    });
}
