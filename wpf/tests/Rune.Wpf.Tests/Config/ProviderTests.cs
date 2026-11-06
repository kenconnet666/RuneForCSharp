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

    [Fact]
    public Task Type_styles_reach_parts_of_that_type_under_the_part_styles() => WpfUi.Run(() =>
    {
        // 决定 19：给所有 rune:Icon 写的样式也进 Spinner、Badge 里的图标部件；同一层的部件键样式叠在它上面
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf">
              <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style TargetType="rune:Icon"><Setter Property="StrokeWidth" Value="3" /><Setter Property="Tone" Value="danger" /></Style>
                    <Style x:Key="{x:Static rune:BadgeParts.Icon}" TargetType="rune:Icon"><Setter Property="StrokeWidth" Value="1" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <StackPanel>
                  <rune:Icon x:Name="plain" Data="{x:Static rune:Lucide.Search}" />
                  <rune:Spinner x:Name="spinner" />
                  <rune:Badge x:Name="badge" Icon="{x:Static rune:Lucide.CircleCheck}">已通过</rune:Badge>
                </StackPanel>
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        WpfUi.Show(root);

        Assert.Equal(3, ((Icon)root.FindName("plain")).StrokeWidth);
        var spinnerIcon = (Icon)((Spinner)root.FindName("spinner")).Template.FindName("PART_Icon", (Spinner)root.FindName("spinner"));
        Assert.Equal((3d, TextTone.Danger), (spinnerIcon.StrokeWidth, spinnerIcon.Tone));
        Assert.Equal(Lucide.LoaderCircle, spinnerIcon.Data); // 主题里的部件样式还在最下面
        var badgeIcon = LogicalTreeHelper.GetChildren((Badge)root.FindName("badge")).OfType<Icon>().Single();
        Assert.Equal((1d, TextTone.Danger), (badgeIcon.StrokeWidth, badgeIcon.Tone));
    });

    [Fact]
    public Task Nested_part_styles_layer_over_the_same_key() => WpfUi.Run(() =>
    {
        // 卡片的按钮部件里再写按钮的标签部件：叠在这一层的标签样式（Light）与主题（Bold）之上，不整个替换
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf"
                        xmlns:t="clr-namespace:Rune.Wpf.Tests;assembly=Rune.Wpf.Tests">
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style x:Key="{x:Static t:ProbeParts.Label}" TargetType="TextBlock"><Setter Property="FontWeight" Value="Light" /></Style>
                    <Style x:Key="{x:Static t:ProbeCardParts.Button}" TargetType="t:ProbeButton">
                      <Style.Resources>
                        <Style x:Key="{x:Static t:ProbeParts.Label}" TargetType="TextBlock"><Setter Property="Foreground" Value="Red" /></Style>
                      </Style.Resources>
                    </Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <StackPanel>
                  <t:ProbeCard x:Name="card" />
                  <t:ProbeButton x:Name="plain" />
                </StackPanel>
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        WpfUi.Show(root);

        Assert.Equal($"Light/{Colors.Red}", Label(((ProbeCard)root.FindName("card")).Button));
        Assert.Equal($"Light/{Colors.Black}", Label((ProbeButton)root.FindName("plain")));
    });

    /// <summary>一层 Provider 的片段：普通值与条件值（条件是 ProbeButton.Variant 等于某个值，代替悬停、按下这类状态）。</summary>
    sealed record Layer(Color? Plain, (string State, Color Value)[] Conditional)
    {
        public Provider Wrap(UIElement child)
        {
            var fragment = new Style(typeof(ProbeButton));
            if (Plain is { } plain) fragment.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(plain)));
            foreach (var (state, value) in Conditional)
                fragment.Triggers.Add(new Trigger { Property = ProbeButton.VariantProperty, Value = state, Setters = { new Setter(Control.BackgroundProperty, new SolidColorBrush(value)) } });
            return new Provider { Styles = new ResourceDictionary { [typeof(ProbeButton)] = fragment }, Child = child };
        }
    }

    /// <summary>
    /// 参考模型（vue-tsx-pro 的分段语义）：从外到内，普通值整体替换前面的取值（连条件值），条件值与前面的合并、后写的优先；
    /// 某个状态下的取值是命中的条件值，没有时是普通值。各层都没写时为 null（取主题里的值，不比）。
    /// </summary>
    static Color? Expected(IEnumerable<Layer> layers, string state)
    {
        Color? plain = null;
        var conditional = new Dictionary<string, Color>();
        foreach (var layer in layers)
        {
            if (layer.Plain is { } value)
            {
                plain = value;
                conditional.Clear();
            }
            foreach (var (s, v) in layer.Conditional) conditional[s] = v;
        }
        return conditional.TryGetValue(state, out var hit) ? hit : plain;
    }

    [Fact]
    public Task Inner_plain_value_replaces_outer_conditions() => WpfUi.Run(() =>
    {
        // 外层：平时红、状态 a 时蓝；内层只写绿：状态 a 时也是绿（WPF 原生是蓝：样式触发器胜过样式 setter）
        Layer[] layers = [new(Colors.Red, [("a", Colors.Blue)]), new(Colors.Green, [])];
        var button = new ProbeButton { Variant = "a" };
        WpfUi.Show(layers[0].Wrap(layers[1].Wrap(button)));
        Assert.Equal(Colors.Green, ((SolidColorBrush)button.Background).Color);
    });

    [Fact]
    public Task Random_layers_match_the_merged_as_one_model() => WpfUi.Run(() =>
    {
        // 照搬上游的验证办法：随机生成若干层，与参考模型逐个状态比对
        var random = new Random(20261107);
        Color[] palette = [Colors.Red, Colors.Green, Colors.Blue, Colors.Gold, Colors.Teal, Colors.Purple];
        string[] states = ["a", "b", "c"];
        var host = new Border();
        WpfUi.Show(host);
        for (var round = 0; round < 150; round++)
        {
            var layers = Enumerable.Range(0, random.Next(1, 5)).Select(_ => new Layer(
                random.Next(2) == 0 ? palette[random.Next(palette.Length)] : null,
                [.. states.Take(2).Where(_ => random.Next(5) < 2).Select(state => (state, palette[random.Next(palette.Length)]))])).ToList();
            var button = new ProbeButton();
            UIElement tree = button;
            for (var i = layers.Count - 1; i >= 0; i--) tree = layers[i].Wrap(tree);
            host.Child = tree;
            foreach (var state in states)
            {
                button.Variant = state;
                WpfUi.Flush();
                if (Expected(layers, state) is { } expected)
                    Assert.True(expected == ((SolidColorBrush)button.Background).Color, $"第 {round} 轮、状态 {state}：期望 {expected}，实际 {((SolidColorBrush)button.Background).Color}；各层 {string.Join(" | ", layers)}");
            }
        }
    });
}
