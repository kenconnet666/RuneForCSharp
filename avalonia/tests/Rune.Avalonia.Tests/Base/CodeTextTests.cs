using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>CodeText：反引号里的部分是 Code，其余是普通文字。</summary>
public class CodeTextTests
{
    [Upstream("CodeText", "反引号里的渲染成 code")]
    [Upstream("CodeText", "没有反引号")]
    [Fact]
    public Task Backticks_become_code() => HeadlessUi.Run(() =>
    {
        var mixed = new CodeText { Source = "输入 `button` 就能找到 `Button`" };
        var plain = new CodeText { Source = "只有文字" };
        HeadlessUi.Show(new StackPanel { Children = { mixed, plain } });

        Assert.Equal(["Run:输入 ", "Code:button", "Run: 就能找到 ", "Code:Button"],
            mixed.Inlines!.Select(inline => inline is Code code ? $"Code:{((Run)code.Inlines[0]).Text}" : $"Run:{((Run)inline).Text}"));
        Assert.IsType<Run>(Assert.Single(plain.Inlines!));
    });

    [Fact]
    public Task Code_part_is_reachable_by_name() => HeadlessUi.Run(() =>
    {
        var text = new CodeText { Source = "输入 `button`" };
        var host = new StackPanel { Children = { text } };
        host.Styles.Add(new Style(x => x.OfType<CodeText>().Descendant().OfType<Code>().Name(CodeTextParts.Code)) { Setters = { new Setter(Inline.BackgroundProperty, Brushes.Red) } });
        HeadlessUi.Show(host);

        Assert.Equal(Brushes.Red, text.Inlines!.OfType<Code>().Single().Background);
    });
}
