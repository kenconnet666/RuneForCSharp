using System.Diagnostics;

namespace Rune.WinForms.Tests;

/// <summary>CopyButton：复制后换成对勾并弹出、到时复原；名称可改；取值出错时回调拿到原因；异步取值时转圈、不重复取。</summary>
public class CopyButtonTests
{
    /// <summary>边处理消息边等，直到条件成立；超过 3 秒算失败。</summary>
    static void Until(Func<bool> condition)
    {
        for (var clock = Stopwatch.StartNew(); !condition();)
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("等了 3 秒条件仍不成立");
            WinFormsUi.Wait(TimeSpan.FromMilliseconds(10));
        }
    }

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
    public Task Copying_shows_a_check_and_resets_after_the_timeout() => WinFormsUi.Run(() => KeepingClipboard(() =>
    {
        var button = new CopyButtonControl { Value = "secret", Timeout = TimeSpan.FromMilliseconds(800) };
        using var form = WinFormsUi.Show(button);
        CopiedEventArgs? result = null;
        button.Copied += (_, e) => result = e;

        Assert.Equal("复制", button.AccessibilityObject.Name);
        Assert.Same(Lucide.Copy, button.Icon);
        button.PerformClick();
        Until(() => result is not null);

        Assert.True(result!.Success);
        Assert.Equal("secret", Clipboard.GetText());
        Assert.True(button.IsCopied);
        Assert.Same(Lucide.Check, button.Icon);
        Assert.Equal(Motion.Enabled, button.Element.PopProgress < 1); // 对勾正在弹出；系统关闭动画时直接弹完

        Until(() => !button.IsCopied);
        Assert.Same(Lucide.Copy, button.Icon);
        Assert.Equal(1, button.Element.PopProgress);
    }));

    [Fact]
    [Upstream("CopyButton", "aria-label 可以改；复制失败时回调拿到 false 与原因")]
    [Upstream("CopyButton", "异步取值出错")]
    public Task Name_can_change_and_failures_report_the_reason() => WinFormsUi.Run(() =>
    {
        var failed = new InvalidOperationException("offline");
        var button = new CopyButtonControl { ValueFactory = () => Task.FromException<string>(failed), AccessibleName = "复制链接" };
        using var form = WinFormsUi.Show(button);
        CopiedEventArgs? result = null;
        button.Copied += (_, e) => result = e;

        Assert.Equal("复制链接", button.AccessibilityObject.Name);
        button.PerformClick();
        Until(() => result is not null);
        Assert.Equal((false, failed), (result!.Success, result.Error));
        Assert.False(button.IsCopied);
        Assert.False(button.IsLoading); // 出错后不再转圈
    });

    [Fact]
    [Upstream("CopyButton", "异步取值：等待时转圈")]
    public Task Async_values_spin_while_waiting_and_ignore_repeated_clicks() => WinFormsUi.Run(() => KeepingClipboard(() =>
    {
        var pending = new TaskCompletionSource<string>();
        var calls = 0;
        var button = new CopyButtonControl { ValueFactory = () => { calls++; return pending.Task; } };
        using var form = WinFormsUi.Show(button);
        var copied = false;
        button.Copied += (_, e) => copied = e.Success;

        button.PerformClick();
        Assert.True(button.IsLoading);
        Assert.True(button.AccessibilityObject.State.HasFlag(AccessibleStates.Busy));
        button.PerformClick();
        Assert.Equal(1, calls);

        pending.SetResult("https://example.com/invite/abc");
        Until(() => copied);
        Assert.False(button.IsLoading);
        Assert.True(button.IsCopied);
        Assert.Equal("https://example.com/invite/abc", Clipboard.GetText());
    }));
}
