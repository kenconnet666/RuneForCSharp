using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>ErrorBoundary：里面出错时只换掉这一块、上报一次、不往上传；重试与 ResetKey 重新建里面的内容；自定义出错时的内容；代码交过来的错误。</summary>
public class ErrorBoundaryTests
{
    /// <summary>前 failures 次建出来的控件在排版时出错，之后正常（同上游的 flaky）。</summary>
    sealed class Flaky(int attempt, int failures) : TextBlock
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            if (attempt <= failures) throw new InvalidOperationException($"第 {attempt} 次出错");
            Text = $"正常（第 {attempt} 次）";
            return base.MeasureOverride(availableSize);
        }
    }

    static IDataTemplate FlakyTemplate(int failures)
    {
        var created = 0;
        return new FuncDataTemplate<object?>((_, _) => new Flaky(++created, failures));
    }

    static IEnumerable<string> Texts(Control root) => root.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? "");

    static Button RetryButton(ErrorBoundary boundary) => boundary.GetLogicalDescendants().OfType<Button>().Single();

    [Fact]
    [Upstream("ErrorBoundary", "渲染出错：只把这一块换成出错提示，旁边的照常；上报一次、带出错位置")]
    public Task Layout_error_replaces_only_this_block() => HeadlessUi.Run(() =>
    {
        var boundary = new ErrorBoundary { ContentTemplate = FlakyTemplate(1) };
        Provider.SetLang(boundary, Lang.ZhCn);
        var caught = new List<ErrorCaughtEventArgs>();
        boundary.ErrorCaught += (_, e) => caught.Add(e);
        var root = new StackPanel { Children = { boundary, new TextBlock { Text = "旁边的内容" } } };
        var window = HeadlessUi.Show(root);
        HeadlessUi.Flush(window);
        Assert.Contains("这块内容出错了", Texts(boundary));
        Assert.Contains("第 1 次出错", Texts(boundary));
        Assert.Contains("旁边的内容", Texts(root));
        Assert.Single(caught);
        Assert.IsType<InvalidOperationException>(caught[0].Error);
        Assert.Equal("layout", caught[0].Where);
    });

    [Fact]
    [Upstream("ErrorBoundary", "重试：里面的组件重新创建")]
    public Task Retry_creates_the_content_again() => HeadlessUi.Run(() =>
    {
        var boundary = new ErrorBoundary { ContentTemplate = FlakyTemplate(1) };
        var window = HeadlessUi.Show(boundary);
        HeadlessUi.Flush(window);
        RetryButton(boundary).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        HeadlessUi.Flush(window);
        Assert.Null(boundary.Error);
        Assert.Equal(["正常（第 2 次）"], Texts(boundary));
    });

    [Fact]
    [Upstream("ErrorBoundary", "fallback 自己画出错时的内容，拿到错误与重试函数")]
    public Task Custom_fallback_gets_the_error_and_retry() => HeadlessUi.Run(() =>
    {
        Action? again = null;
        var boundary = new ErrorBoundary
        {
            ContentTemplate = FlakyTemplate(1),
            Fallback = (error, retry) =>
            {
                again = retry;
                return $"{error.Message}，再试一次";
            },
        };
        var window = HeadlessUi.Show(boundary);
        HeadlessUi.Flush(window);
        Assert.Equal(["第 1 次出错，再试一次"], Texts(boundary));
        again!();
        HeadlessUi.Flush(window);
        Assert.Equal(["正常（第 2 次）"], Texts(boundary));
    });

    [Fact]
    [Upstream("ErrorBoundary", "事件处理里出错也接住；resetKeys 变了就清掉错误")]
    public Task Reported_errors_show_and_a_new_reset_key_clears_them() => HeadlessUi.Run(() =>
    {
        var source = "a";
        var boundary = new ErrorBoundary { ResetKey = source, ContentTemplate = new FuncDataTemplate<object?>((_, _) => new Button { Content = $"数据源 {source}" }) };
        var window = HeadlessUi.Show(boundary);
        HeadlessUi.Flush(window);
        var button = boundary.GetLogicalDescendants().OfType<Button>().Single();
        // 事件处理里接到的错误交给最近的边界
        Assert.True(ErrorBoundary.Show(button, new InvalidOperationException("点了就出错")));
        HeadlessUi.Flush(window);
        Assert.Contains("点了就出错", Texts(boundary));
        source = "b";
        boundary.ResetKey = source;
        HeadlessUi.Flush(window);
        Assert.Equal("数据源 b", boundary.GetLogicalDescendants().OfType<Button>().Single().Content);
        // 不在边界里的控件：交不出去
        Assert.False(ErrorBoundary.Show(new Button(), new InvalidOperationException()));
    });

    [Fact]
    public Task Errors_while_creating_the_content_are_caught() => HeadlessUi.Run(() =>
    {
        var boundary = new ErrorBoundary { ContentTemplate = new FuncDataTemplate<object?>((_, _) => throw new InvalidOperationException("建不出来")) };
        string? where = null;
        boundary.ErrorCaught += (_, e) => where = e.Where;
        var window = HeadlessUi.Show(boundary);
        HeadlessUi.Flush(window);
        Assert.Equal("create", where);
        Assert.Contains("建不出来", Texts(boundary));
    });
}
