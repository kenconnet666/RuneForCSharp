using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>CopyButton：复制后换成对勾、读屏的项状态是"已复制"、到时复原；名称可改；取值或写入出错时回调拿到原因；异步取值时转圈、不重复取。</summary>
public class CopyButtonTests
{
    static void Click(Window window, Control button)
    {
        var at = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }

    static Icon Glyph(CopyButton button) => button.GetVisualDescendants().OfType<Icon>().Single(icon => icon.Name == IconButtonParts.Icon);

    [Fact]
    [Upstream("CopyButton", "复制后 data-state=")]
    public Task Copying_shows_a_check_announces_and_resets_after_the_timeout() => HeadlessUi.Run(() =>
    {
        var button = new CopyButton { Value = "secret", Timeout = TimeSpan.FromMilliseconds(800) };
        var window = HeadlessUi.Show(button);
        CopiedEventArgs? result = null;
        button.Copied += (_, e) => result = e;

        Assert.Equal("复制", button.Peer().GetName());
        Assert.Same(Lucide.Copy, Glyph(button).Data);
        Click(window, button);
        HeadlessUi.Until(() => result is not null);

        Assert.True(result!.Success);
        var clipboard = window.Clipboard!.TryGetTextAsync();
        HeadlessUi.Until(() => clipboard.IsCompleted);
        Assert.Equal("secret", clipboard.Result);
        Assert.True(button.IsCopied);
        Assert.Contains(":copied", button.Classes);
        Assert.Same(Lucide.Check, Glyph(button).Data);
        Assert.Same(Lucide.Copy, button.Icon); // 只换图标部件的图形，属性不动
        Assert.Equal("已复制", button.Peer().GetItemStatus());
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(button));

        HeadlessUi.Until(() => !button.IsCopied);
        Assert.Same(Lucide.Copy, Glyph(button).Data);
    });

    [Fact]
    [Upstream("CopyButton", "aria-label 可以改；复制失败时回调拿到 false 与原因")]
    [Upstream("CopyButton", "异步取值出错")]
    public Task Name_can_change_and_failures_report_the_reason() => HeadlessUi.Run(() =>
    {
        var failed = new InvalidOperationException("offline");
        var button = new CopyButton { ValueFactory = () => Task.FromException<string>(failed) };
        AutomationProperties.SetName(button, "复制链接");
        var window = HeadlessUi.Show(button);
        CopiedEventArgs? result = null;
        button.Copied += (_, e) => result = e;

        Assert.Equal("复制链接", button.Peer().GetName());
        Click(window, button);
        HeadlessUi.Until(() => result is not null);
        Assert.Equal((false, failed), (result!.Success, result.Error));
        Assert.False(button.IsCopied);
        Assert.False(button.IsLoading); // 出错后不再转圈
    });

    [Fact]
    [Upstream("CopyButton", "异步取值：等待时转圈")]
    public Task Async_values_spin_while_waiting_and_ignore_repeated_clicks() => HeadlessUi.Run(() =>
    {
        var pending = new TaskCompletionSource<string>();
        var calls = 0;
        var button = new CopyButton { ValueFactory = () => { calls++; return pending.Task; } };
        var window = HeadlessUi.Show(button);
        var copied = false;
        button.Copied += (_, e) => copied = e.Success;

        Click(window, button);
        HeadlessUi.Until(() => button.IsLoading);
        Click(window, button);
        Assert.Equal(1, calls);

        pending.SetResult("https://example.com/invite/abc");
        HeadlessUi.Until(() => copied);
        Assert.False(button.IsLoading);
        Assert.True(button.IsCopied);
    });
}
