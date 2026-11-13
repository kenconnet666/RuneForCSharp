namespace Rune.WinForms.Tests;

/// <summary>
/// CodeBlockControl：高亮器异步替换纯文本（没给语言、关掉、失败时纯文本，慢结果不覆盖新结果）；复制按钮的位置；长代码折叠与展开；
/// 结尾的换行不多出一行。
/// </summary>
public class CodeBlockTests
{
    const string Source = "const a = 1;\nconst b = 2;\nconst c = 3;\nconst d = 4;";

    [Fact]
    [Upstream("CodeBlock", "没有高亮器时显示纯文本")]
    [Upstream("CodeBlock", "Provider 注入的高亮器：先显示纯文本，结果到了再替换")]
    [Upstream("CodeBlock", "代码变了先回到纯文本；先发出的慢结果晚到时不覆盖新结果")]
    public Task An_injected_highlighter_replaces_the_text_and_stale_results_are_dropped() => WinFormsUi.Run(() =>
    {
        var first = new TaskCompletionSource<IReadOnlyList<CodeToken>?>();
        var second = new TaskCompletionSource<IReadOnlyList<CodeToken>?>();
        var results = new Queue<TaskCompletionSource<IReadOnlyList<CodeToken>?>>([first, second]);
        var block = new CodeBlockControl { Width = 400, Code = "a", Language = "ts", Highlighter = (_, _) => results.Dequeue().Task };
        using var form = WinFormsUi.Show(block);
        WinFormsUi.Pump();
        Assert.Null(block.Tokens);

        block.Code = "const";
        WinFormsUi.Pump();
        second.SetResult([new CodeToken(0, 5, CodeTokenKind.Keyword)]);
        WinFormsUi.Until(() => block.Tokens is not null);
        first.SetResult([new CodeToken(0, 1, CodeTokenKind.String)]);
        WinFormsUi.Pump();
        Assert.Equal(CodeTokenKind.Keyword, Assert.Single(block.Tokens!).Kind);
    });

    [Fact]
    [Upstream("CodeBlock", "没给语言时不调用高亮器")]
    [Upstream("CodeBlock", "highlight={false} 时按纯文本显示、不调用高亮器；改成 true 再高亮")]
    [Upstream("CodeBlock", "高亮失败时保留纯文本并报错")]
    public Task The_highlighter_runs_only_with_a_language_and_highlight_on() => WinFormsUi.Run(() =>
    {
        var calls = 0;
        var block = new CodeBlockControl { Width = 400, Code = Source, Highlighter = (code, language) => { calls++; return CodeHighlighting.Builtin(code, language); } };
        using var form = WinFormsUi.Show(block);
        WinFormsUi.Pump();
        Assert.Equal(0, calls);

        block.Highlight = false;
        block.Language = "ts";
        WinFormsUi.Pump();
        Assert.Equal(0, calls);
        block.Highlight = true;
        WinFormsUi.Until(() => block.Tokens is not null);
        Assert.Equal(1, calls);

        block.Highlighter = (_, _) => Task.FromException<IReadOnlyList<CodeToken>?>(new InvalidOperationException("boom"));
        WinFormsUi.Pump();
        Assert.Null(block.Tokens);
    });

    [Fact]
    [Upstream("CodeBlock", "复制按钮：写入剪贴板，图标变成对勾并播报“已复制”；copyable={false} 时没有")]
    [Upstream("CodeBlock", "有标题时复制按钮在标题栏里；超过 maxLines 时折叠，按钮展开与收起")]
    [Upstream("CodeBlock", "结尾的换行不多出一行空的行号")]
    public Task The_copy_button_moves_into_the_title_bar_and_long_code_folds() => WinFormsUi.Run(() =>
    {
        var block = new CodeBlockControl { Width = 400, Code = Source + "\n" };
        using var form = WinFormsUi.Show(block);
        var (copy, scroll, expand) = block.Parts;
        Assert.Equal(Source + "\n", copy.Value);
        Assert.Equal(4, block.LineCount);
        Assert.True(copy.Top >= scroll.Top); // 没有标题：在代码区的右上角

        block.Title = "main.ts";
        WinFormsUi.Pump();
        Assert.True(copy.Bottom <= scroll.Top); // 有标题：在标题栏里
        block.Copyable = false;
        Assert.False(copy.Visible);

        var full = block.Height;
        block.MaxLines = 2;
        WinFormsUi.Pump();
        Assert.True(expand.Visible);
        Assert.Equal(2, block.VisibleLines);
        Assert.Equal("展开全部", block.ExpandText);
        block.IsExpanded = true;
        WinFormsUi.Pump();
        Assert.Null(block.VisibleLines);
        Assert.Equal("收起", block.ExpandText);
        Assert.True(block.Height > full); // 展开后多了底部的按钮
    });

    [Fact]
    [Upstream("CodeBlock", "按行渲染：行号不进复制的文本，新增与删除的行带 data-diff")]
    public Task Added_and_removed_lines_get_their_backgrounds() => WinFormsUi.Run(() =>
    {
        var block = new CodeBlockControl { Width = 400, Code = Source, LineNumbers = true, AddedLines = "2", RemovedLines = "3", Copyable = false };
        using var form = WinFormsUi.Show(block);
        using var bitmap = new Bitmap(block.Width, block.Height);
        block.DrawToBitmap(bitmap, new Rectangle(Point.Empty, block.Size));
        var colors = RuneTheme.Current.Value.Colors;
        var em = 0.9f * block.Font.SizeInPoints * block.DeviceDpi / 72f;
        int Row(int index) => block.LogicalToDeviceUnits(1) + (int)MathF.Round(0.75f * em) + (int)(MathF.Round(1.5f * em) * (index + 0.5f));
        var x = block.Width - block.LogicalToDeviceUnits(8);
        Assert.Equal(colors.CodeInserted.ToArgb(), bitmap.GetPixel(x, Row(1)).ToArgb());
        Assert.Equal(colors.CodeDeleted.ToArgb(), bitmap.GetPixel(x, Row(2)).ToArgb());
        Assert.NotEqual(colors.CodeInserted.ToArgb(), bitmap.GetPixel(x, Row(0)).ToArgb());
    });

    [Fact]
    [Upstream("CodeBlock", "行号写法：逗号分隔、区间、数组")]
    public void Line_specs_accept_lists_and_ranges() => Assert.Equal([1, 3, 4], CodeHighlighting.ParseLines("1,3-4").Order());

    [Fact]
    public Task Scope_defaults_fill_unwritten_options() => WinFormsUi.Run(() =>
    {
        var plain = new CodeBlockControl { Code = string.Join('\n', "a", "b", "c") };
        var noCopy = new CodeBlockControl { Code = "a", Copyable = true, LineNumbers = false }; // 显式写成组件默认值也要赢过作用域
        var scope = new ProviderControl { Dock = DockStyle.Fill };
        scope.Defaults.Set(new CodeBlockDefaults { LineNumbers = true, Copyable = false, MaxLines = 2, Wrap = true });
        scope.Controls.AddRange([plain, noCopy]);
        using var form = WinFormsUi.Show(scope);

        Assert.Equal((true, false, 2, true, false), (plain.LineNumbers, plain.Copyable, plain.MaxLines, plain.Wrap, plain.Flush)); // 没写的取作用域，Flush 是组件默认
        Assert.Equal((false, true), (noCopy.LineNumbers, noCopy.Copyable));
        plain.MaxLines = null; // 显式写成不折叠
        Assert.Null(plain.MaxLines);
    });
}
