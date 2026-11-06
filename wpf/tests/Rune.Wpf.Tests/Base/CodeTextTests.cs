using System.Windows.Controls;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>CodeText：反引号里的部分是 Code，其余是普通文字。</summary>
public class CodeTextTests
{
    [Upstream("CodeText", "反引号里的渲染成 code")]
    [Upstream("CodeText", "没有反引号")]
    [Fact]
    public Task Backticks_become_code() => WpfUi.Run(() =>
    {
        var mixed = new CodeText { Source = "输入 `button` 就能找到 `Button`" };
        var plain = new CodeText { Source = "只有文字" };
        var host = new StackPanel { Children = { mixed, plain } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);

        Assert.Equal(["Run:输入 ", "Code:button", "Run: 就能找到 ", "Code:Button"],
            mixed.Inlines.Select(inline => inline is Code code ? $"Code:{((Run)code.Inlines.FirstInline).Text}" : $"Run:{((Run)inline).Text}"));
        Assert.IsType<Run>(Assert.Single(plain.Inlines));
    });

    [Fact]
    public Task Provider_part_style_reaches_the_code() => WpfUi.Run(() =>
    {
        const string Xaml = """
            <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf">
              <StackPanel.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><rune:RuneTheme /></ResourceDictionary.MergedDictionaries></ResourceDictionary></StackPanel.Resources>
              <rune:Provider>
                <rune:Provider.Styles>
                  <ResourceDictionary>
                    <Style x:Key="{x:Static rune:CodeTextParts.Code}" TargetType="rune:Code"><Setter Property="Background" Value="Red" /></Style>
                  </ResourceDictionary>
                </rune:Provider.Styles>
                <rune:CodeText x:Name="text" Source="输入 `button`" />
              </rune:Provider>
            </StackPanel>
            """;
        var root = (FrameworkElement)XamlReader.Parse(Xaml);
        WpfUi.Show(root);

        var code = ((CodeText)root.FindName("text")).Inlines.OfType<Code>().Single();
        Assert.Equal(Colors.Red, ((SolidColorBrush)code.Background).Color);
        Assert.Equal(root.FindResource("Rune.Font.Mono"), code.FontFamily); // 主题样式仍在
    });
}
