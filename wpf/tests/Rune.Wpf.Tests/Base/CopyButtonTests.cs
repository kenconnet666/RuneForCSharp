using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>CopyButton：复制后换成对勾、读屏的项状态是"已复制"、到时复原；名称可改；取值出错时回调拿到原因；异步取值时转圈、不重复取。</summary>
public class CopyButtonTests
{
    static StackPanel Host(CopyButton button)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(button);
        return host;
    }

    static void Click(CopyButton button) => ((IInvokeProvider)button.Peer().GetPattern(PatternInterface.Invoke)).Invoke();

    static Icon Glyph(CopyButton button) => (Icon)button.Template.FindName("PART_Icon", button);

    /// <summary>测试写真实的系统剪贴板：先记下原来的文字，测完放回去。</summary>
    static void KeepingClipboard(Action body)
    {
        var saved = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        try
        {
            body();
        }
        finally
        {
            if (saved is null) Clipboard.Clear();
            else Clipboard.SetText(saved);
        }
    }

    [Fact]
    [Upstream("CopyButton", "复制后 data-state=")]
    public Task Copying_shows_a_check_announces_and_resets_after_the_timeout() => WpfUi.Run(() => KeepingClipboard(() =>
    {
        var button = new CopyButton { Value = "secret", Timeout = TimeSpan.FromMilliseconds(200) };
        WpfUi.Show(Host(button));
        CopiedEventArgs? result = null;
        button.Copied += (_, e) => result = e;

        Assert.Equal("复制", button.Peer().GetName());
        Assert.Same(Lucide.Copy, Glyph(button).Data);
        Click(button);
        WpfUi.Until(() => result is not null);

        Assert.True(result!.Success);
        Assert.Equal("secret", Clipboard.GetText());
        Assert.True(button.IsCopied);
        Assert.Same(Lucide.Check, Glyph(button).Data);
        Assert.Equal("已复制", button.Peer().GetItemStatus());

        WpfUi.Until(() => !button.IsCopied);
        Assert.Same(Lucide.Copy, Glyph(button).Data);
    }));

    [Fact]
    [Upstream("CopyButton", "aria-label 可以改；复制失败时回调拿到 false 与原因")]
    [Upstream("CopyButton", "异步取值出错")]
    public Task Name_can_change_and_failures_report_the_reason() => WpfUi.Run(() =>
    {
        var failed = new InvalidOperationException("offline");
        var button = new CopyButton { ValueFactory = () => Task.FromException<string>(failed) };
        AutomationProperties.SetName(button, "复制链接");
        WpfUi.Show(Host(button));
        CopiedEventArgs? result = null;
        button.Copied += (_, e) => result = e;

        Assert.Equal("复制链接", button.Peer().GetName());
        Click(button);
        WpfUi.Until(() => result is not null);
        Assert.Equal((false, failed), (result!.Success, result.Error));
        Assert.False(button.IsCopied);
        Assert.False(button.IsLoading); // 出错后不再转圈
    });

    [Fact]
    [Upstream("CopyButton", "异步取值：等待时转圈")]
    public Task Async_values_spin_while_waiting_and_ignore_repeated_clicks() => WpfUi.Run(() => KeepingClipboard(() =>
    {
        var pending = new TaskCompletionSource<string>();
        var calls = 0;
        var button = new CopyButton { ValueFactory = () => { calls++; return pending.Task; } };
        WpfUi.Show(Host(button));
        var copied = false;
        button.Copied += (_, e) => copied = e.Success;

        Click(button);
        WpfUi.Until(() => button.IsLoading);
        Click(button);
        WpfUi.Flush();
        Assert.Equal(1, calls);

        pending.SetResult("https://example.com/invite/abc");
        WpfUi.Until(() => copied);
        Assert.False(button.IsLoading);
        Assert.True(button.IsCopied);
        Assert.Equal("https://example.com/invite/abc", Clipboard.GetText());
    }));
}
