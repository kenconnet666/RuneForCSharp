using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>
/// CodeBlock：高亮器异步替换纯文本（没给语言、关掉、失败时纯文本，慢结果不覆盖新结果）；复制按钮的位置；按行显示（行号、增删、强调）；
/// 长代码折叠与展开。
/// </summary>
public class CodeBlockTests
{
    const string Source = "const a = 1;\nconst b = 2;\nconst c = 3;\nconst d = 4;";

    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(child);
        return host;
    }

    [Fact]
    [Upstream("CodeBlock", "没有高亮器时显示纯文本")]
    public Task Without_a_highlighter_the_code_is_plain_text() => WpfUi.Run(() =>
    {
        var block = new CodeBlock { Code = Source, CodeLanguage = "ts", Highlighter = null };
        WpfUi.Show(Host(block));
        Assert.Equal(Source, block.CodeText.Text);
        Assert.Null(block.Tokens);
    });

    [Fact]
    [Upstream("CodeBlock", "Provider 注入的高亮器：先显示纯文本，结果到了再替换")]
    [Upstream("CodeBlock", "代码变了先回到纯文本；先发出的慢结果晚到时不覆盖新结果")]
    public Task An_injected_highlighter_replaces_the_text_and_stale_results_are_dropped() => WpfUi.Run(() =>
    {
        var first = new TaskCompletionSource<IReadOnlyList<CodeToken>?>();
        var second = new TaskCompletionSource<IReadOnlyList<CodeToken>?>();
        var results = new Queue<TaskCompletionSource<IReadOnlyList<CodeToken>?>>([first, second]);
        var block = new CodeBlock { Code = "a", CodeLanguage = "ts", Highlighter = (_, _) => results.Dequeue().Task };
        WpfUi.Show(Host(block));

        block.Code = "const";
        WpfUi.Flush();
        Assert.Equal("const", block.CodeText.Text);
        second.SetResult([new CodeToken(0, 5, CodeTokenKind.Keyword)]);
        WpfUi.Until(() => block.Tokens is not null);
        first.SetResult([new CodeToken(0, 1, CodeTokenKind.String)]);
        WpfUi.Flush();
        Assert.Equal(CodeTokenKind.Keyword, Assert.Single(block.Tokens!).Kind);
        Assert.Equal("const", ((Run)block.CodeText.Inlines.FirstInline).Text);
    });

    [Fact]
    [Upstream("CodeBlock", "没给语言时不调用高亮器")]
    [Upstream("CodeBlock", "highlight={false} 时按纯文本显示、不调用高亮器；改成 true 再高亮")]
    [Upstream("CodeBlock", "高亮失败时保留纯文本并报错")]
    public Task The_highlighter_runs_only_with_a_language_and_highlight_on() => WpfUi.Run(() =>
    {
        var calls = 0;
        var block = new CodeBlock { Code = Source, Highlighter = (code, language) => { calls++; return CodeHighlighting.Builtin(code, language); } };
        WpfUi.Show(Host(block));
        WpfUi.Flush();
        Assert.Equal(0, calls);

        block.Highlight = false;
        block.CodeLanguage = "ts";
        WpfUi.Flush();
        Assert.Equal(0, calls);
        block.Highlight = true;
        WpfUi.Until(() => block.Tokens is not null);
        Assert.Equal(1, calls);

        block.Highlighter = (_, _) => Task.FromException<IReadOnlyList<CodeToken>?>(new InvalidOperationException("boom"));
        WpfUi.Flush();
        Assert.Null(block.Tokens);
        Assert.Equal(Source, block.CodeText.Text);
    });

    [Fact]
    [Upstream("CodeBlock", "复制按钮：写入剪贴板，图标变成对勾并播报“已复制”；copyable={false} 时没有")]
    [Upstream("CodeBlock", "有标题时复制按钮在标题栏里；超过 maxLines 时折叠，按钮展开与收起")]
    [Upstream("CodeBlock", "部件属性：展开按钮里的箭头图标；展开后的翻转仍由组件加上")]
    public Task The_copy_button_moves_into_the_title_bar_and_long_code_folds() => WpfUi.Run(() =>
    {
        var block = new CodeBlock { Code = Source };
        WpfUi.Show(Host(block));
        var (copy, header, title, body, pre, expand, expandText, expandIcon) = block.Parts;
        Assert.Equal(Source, copy.Value);
        Assert.Same(body, copy.Parent);
        Assert.Equal(Visibility.Collapsed, header.Visibility);

        block.Title = "main.ts";
        Assert.Equal(Visibility.Visible, header.Visibility);
        Assert.NotSame(body, copy.Parent);
        Assert.Equal("main.ts", title.Content);
        block.Copyable = false;
        Assert.Equal(Visibility.Collapsed, copy.Visibility);

        block.MaxLines = 2;
        Assert.Equal(Visibility.Visible, expand.Visibility);
        Assert.True(pre.MaxHeight < double.PositiveInfinity);
        Assert.Equal("展开全部", expandText.Text);
        Assert.Same(Transform.Identity, expandIcon.RenderTransform);
        block.IsExpanded = true;
        Assert.Equal(double.PositiveInfinity, pre.MaxHeight);
        Assert.Equal("收起", expandText.Text);
        Assert.NotSame(Transform.Identity, expandIcon.RenderTransform);
    });

    [Fact]
    [Upstream("CodeBlock", "行号写法：逗号分隔、区间、数组")]
    public void Line_specs_accept_lists_and_ranges() => Assert.Equal([1, 3, 4], CodeHighlighting.ParseLines("1,3-4").Order());

    [Fact]
    [Upstream("CodeBlock", "按行渲染：行号不进复制的文本，新增与删除的行带 data-diff")]
    [Upstream("CodeBlock", "结尾的换行不多出一行空的行号")]
    [Upstream("CodeBlock", "部件属性：标题、代码区、每一行与行号；行号始终对读屏隐藏")]
    public Task Lines_carry_numbers_and_diff_marks() => WpfUi.Run(() =>
    {
        var block = new CodeBlock { Code = Source + "\n", LineNumbers = true, AddedLines = "2", RemovedLines = "3", HighlightLines = "4" };
        WpfUi.Show(Host(block));

        Assert.Equal(4, block.Rows.Count);
        var numbers = block.Rows.Select(row => (TextBlock)row.Children[0]).ToArray();
        Assert.Equal(["1", "2", "3", "4"], numbers.Select(number => number.Text));
        Assert.All(numbers, number => Assert.False(UIElementAutomationPeer.CreatePeerForElement(number).IsControlElement()));
        Assert.Equal([null, "added", "removed", "highlighted"], block.Rows.Select(row => row.Tag as string));
        Assert.Equal(block.ThemeColor("Rune.Brush.CodeInserted"), Lookup.ColorOf(block.Rows[1].Background));
        Assert.Equal(Source + "\n", block.Parts.Copy.Value);
    });
}
