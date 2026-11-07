using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;

namespace Rune.Avalonia.Tests;

/// <summary>
/// CodeBlock：高亮器异步替换纯文本（不认识、没给语言、关掉、失败时纯文本，慢结果不覆盖新结果）；复制按钮的位置；按行显示（行号、增删、强调）；
/// 长代码折叠与展开；部件。内置高亮器按语言分段。
/// </summary>
public class CodeBlockTests
{
    const string Source = "const a = 1;\nconst b = 2;\nconst c = 3;\nconst d = 4;";

    static T Part<T>(CodeBlock block, string name) where T : Control => block.GetLogicalDescendants().OfType<T>().First(part => part.Name == name);

    static SelectableTextBlock CodeText(CodeBlock block) => Part<SelectableTextBlock>(block, CodeBlockParts.Code);

    [Fact]
    [Upstream("CodeBlock", "没有高亮器时显示纯文本")]
    public Task Without_a_highlighter_the_code_is_plain_text_in_a_horizontal_scroll_area() => HeadlessUi.Run(() =>
    {
        var block = new CodeBlock { Code = Source, Language = "ts", Highlighter = null };
        HeadlessUi.Show(block);

        Assert.Equal(Source, CodeText(block).Text);
        Assert.Null(block.Tokens);
        Assert.Equal(ScrollAreaAxis.X, Part<ScrollArea>(block, CodeBlockParts.Scroll).Axis);
    });

    [Fact]
    [Upstream("CodeBlock", "Provider 注入的高亮器：先显示纯文本，结果到了再替换")]
    public Task An_injected_highlighter_replaces_the_plain_text_when_it_finishes() => HeadlessUi.Run(() =>
    {
        var pending = new TaskCompletionSource<IReadOnlyList<CodeToken>?>();
        var block = new CodeBlock { Code = Source, Language = "ts", Highlighter = (_, _) => pending.Task };
        HeadlessUi.Show(block);
        Assert.Equal(Source, CodeText(block).Text);

        pending.SetResult([new CodeToken(0, 5, CodeTokenKind.Keyword)]);
        HeadlessUi.Until(() => block.Tokens is not null);
        var runs = CodeText(block).Inlines!;
        Assert.Equal("const", ((global::Avalonia.Controls.Documents.Run)runs[0]).Text);
        Assert.Equal(Source, string.Concat(runs.OfType<global::Avalonia.Controls.Documents.Run>().Select(run => run.Text)));
    });

    [Fact]
    [Upstream("CodeBlock", "没给语言时不调用高亮器")]
    [Upstream("CodeBlock", "highlight={false} 时按纯文本显示、不调用高亮器；改成 true 再高亮")]
    public Task The_highlighter_runs_only_with_a_language_and_highlight_on() => HeadlessUi.Run(() =>
    {
        var calls = 0;
        CodeHighlighter counting = (code, language) =>
        {
            calls++;
            return CodeHighlighting.Builtin(code, language);
        };
        var block = new CodeBlock { Code = Source, Highlighter = counting };
        HeadlessUi.Show(block);
        Assert.Equal(0, calls);

        block.Highlight = false;
        block.Language = "ts";
        Assert.Equal(0, calls);
        Assert.Null(block.Tokens);
        block.Highlight = true;
        HeadlessUi.Until(() => block.Tokens is not null);
        Assert.Equal(1, calls);
    });

    [Fact]
    [Upstream("CodeBlock", "代码变了先回到纯文本；先发出的慢结果晚到时不覆盖新结果")]
    public Task Changing_the_code_resets_and_stale_results_are_dropped() => HeadlessUi.Run(() =>
    {
        var first = new TaskCompletionSource<IReadOnlyList<CodeToken>?>();
        var second = new TaskCompletionSource<IReadOnlyList<CodeToken>?>();
        var results = new Queue<TaskCompletionSource<IReadOnlyList<CodeToken>?>>([first, second]);
        var block = new CodeBlock { Code = "a", Language = "ts", Highlighter = (_, _) => results.Dequeue().Task };
        HeadlessUi.Show(block);

        block.Code = "b";
        Assert.Equal("b", CodeText(block).Text);
        second.SetResult([new CodeToken(0, 1, CodeTokenKind.Keyword)]);
        HeadlessUi.Until(() => block.Tokens is not null);
        first.SetResult([new CodeToken(0, 1, CodeTokenKind.String)]);
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(50));
        Assert.Equal(CodeTokenKind.Keyword, Assert.Single(block.Tokens!).Kind);
    });

    [Fact]
    [Upstream("CodeBlock", "复制按钮：写入剪贴板，图标变成对勾并播报“已复制”；copyable={false} 时没有")]
    [Upstream("CodeBlock", "有标题时复制按钮在标题栏里；超过 maxLines 时折叠，按钮展开与收起")]
    public Task The_copy_button_copies_the_code_and_moves_into_the_title_bar() => HeadlessUi.Run(() =>
    {
        var block = new CodeBlock { Code = Source };
        HeadlessUi.Show(block);
        var copy = Part<CopyButton>(block, CodeBlockParts.Copy);
        Assert.Equal(Source, copy.Value);
        Assert.Same(Part<Panel>(block, CodeBlockParts.Body), copy.Parent);
        Assert.False(Part<Border>(block, CodeBlockParts.Header).IsVisible);

        block.Title = "main.ts";
        Assert.True(Part<Border>(block, CodeBlockParts.Header).IsVisible);
        Assert.NotSame(Part<Panel>(block, CodeBlockParts.Body), copy.Parent);
        Assert.Equal("main.ts", Part<ContentPresenter>(block, CodeBlockParts.Title).Content);

        block.Copyable = false;
        Assert.False(copy.IsVisible);

        // 超过两行时折叠：展开全部 → 收起
        block.MaxLines = 2;
        var expand = Part<ButtonBase>(block, CodeBlockParts.Expand);
        Assert.True(expand.IsVisible);
        var pre = Part<Border>(block, CodeBlockParts.Pre);
        Assert.True(pre.MaxHeight < double.PositiveInfinity);
        Assert.Contains("展开全部", expand.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text));
        block.IsExpanded = true;
        Assert.Equal(double.PositiveInfinity, pre.MaxHeight);
        Assert.Contains("收起", expand.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text));
    });

    [Fact]
    [Upstream("CodeBlock", "高亮失败时保留纯文本并报错")]
    public Task A_failing_highlighter_keeps_the_plain_text() => HeadlessUi.Run(() =>
    {
        var block = new CodeBlock { Code = Source, Language = "ts", Highlighter = (_, _) => Task.FromException<IReadOnlyList<CodeToken>?>(new InvalidOperationException("boom")) };
        HeadlessUi.Show(block);
        HeadlessUi.Pump(TimeSpan.FromMilliseconds(50));
        Assert.Null(block.Tokens);
        Assert.Equal(Source, CodeText(block).Text);
    });

    [Fact]
    [Upstream("CodeBlock", "行号写法：逗号分隔、区间、数组")]
    public void Line_specs_accept_lists_and_ranges()
    {
        Assert.Equal([1, 3, 4], CodeHighlighting.ParseLines("1,3-4").Order());
        Assert.Empty(CodeHighlighting.ParseLines(null));
        Assert.Equal([2, 5], CodeHighlighting.ParseLines(" 2 , x, 5").Order());
    }

    [Fact]
    [Upstream("CodeBlock", "按行渲染：行号不进复制的文本，新增与删除的行带 data-diff")]
    [Upstream("CodeBlock", "结尾的换行不多出一行空的行号")]
    [Upstream("CodeBlock", "部件属性：标题、代码区、每一行与行号；行号始终对读屏隐藏")]
    public Task Lines_carry_numbers_and_diff_marks() => HeadlessUi.Run(() =>
    {
        var block = new CodeBlock { Code = Source + "\n", LineNumbers = true, AddedLines = "2", RemovedLines = "3", HighlightLines = "4" };
        HeadlessUi.Show(block);

        Assert.Equal(4, block.Rows.Count);
        var numbers = block.GetLogicalDescendants().OfType<TextBlock>().Where(text => text.Name == CodeBlockParts.LineNumber).ToArray();
        Assert.Equal(["1", "2", "3", "4"], numbers.Select(number => number.Text));
        Assert.All(numbers, number => Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(number)));
        Assert.Equal((false, true, true, true), (block.Rows[0].Classes.Contains("added"), block.Rows[1].Classes.Contains("added"),
            block.Rows[2].Classes.Contains("removed"), block.Rows[3].Classes.Contains("highlighted")));
        Assert.Equal(Source + "\n", Part<CopyButton>(block, CodeBlockParts.Copy).Value); // 复制的是代码本身，没有行号
    });

    [Fact]
    [Upstream("CodeBlock", "部件属性：展开按钮里的箭头图标；展开后的翻转仍由组件加上")]
    public Task The_expand_chevron_flips_when_expanded() => HeadlessUi.Run(() =>
    {
        var block = new CodeBlock { Code = Source, MaxLines = 2 };
        HeadlessUi.Show(block);
        var icon = Part<Icon>(block, CodeBlockParts.ExpandIcon);
        Assert.Equal(TransformOperations.Identity, icon.RenderTransform);
        block.IsExpanded = true;
        Assert.NotEqual(TransformOperations.Identity, icon.RenderTransform);
    });

    [Fact]
    public void The_builtin_highlighter_splits_common_languages()
    {
        var ts = CodeHighlighting.Tokenize("import { a } from 'vue'; // 注释", "ts")!;
        Assert.Contains(ts, token => token is { Start: 0, Length: 6, Kind: CodeTokenKind.Keyword });
        Assert.Contains(ts, token => token.Kind == CodeTokenKind.String && token.Length == 5);
        Assert.Contains(ts, token => token.Kind == CodeTokenKind.Comment);

        var xml = CodeHighlighting.Tokenize("<rune:Button Content=\"保存\" />", "xaml")!;
        Assert.Contains(xml, token => token is { Start: 1, Kind: CodeTokenKind.Tag });
        Assert.Contains(xml, token => token.Kind == CodeTokenKind.Attribute);

        Assert.Null(CodeHighlighting.Tokenize("x", "cobol"));
    }
}
