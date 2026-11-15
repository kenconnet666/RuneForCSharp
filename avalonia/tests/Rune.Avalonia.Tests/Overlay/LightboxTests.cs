using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>Lightbox：铺满窗口；← / → 切换（Loop 时绕回）、Esc 关闭并还回焦点；打开后整张放进视口；+ / − / 0 缩放与比例；名称与按钮的读屏名称。</summary>
public class LightboxTests
{
    static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    static LightboxItem[] Items(int count) => [.. Enumerable.Range(1, count).Select(i => new LightboxItem(ImageTests.Picture($"l{i}"), $"图纸 {i}"))];

    [Fact]
    [Upstream("Lightbox", "←→ 切换，Esc 关闭并把焦点还回去")]
    public Task Arrows_switch_and_escape_closes_returning_focus() => HeadlessUi.Run(() =>
    {
        var opener = new global::Avalonia.Controls.Button { Content = "预览" };
        var lightbox = new Lightbox { Items = Items(3) };
        var window = HeadlessUi.Show(new StackPanel { Children = { opener, lightbox } });
        opener.Focus();
        lightbox.IsOpen = true;
        HeadlessUi.Until(() => lightbox.Viewport.IsFocused);

        Assert.Equal(new Rect(0, 0, 800, 600), lightbox.Panel.Bounds);
        Assert.Equal("图片预览", lightbox.Panel.Peer().GetName());
        Assert.Equal(AutomationControlType.Image, lightbox.Viewport.Peer().GetAutomationControlType());
        Assert.Equal("图纸 1", lightbox.Viewport.Peer().GetName());
        Assert.False(lightbox.Sides.Previous.IsVisible);
        Press(lightbox.Viewport, Key.Right);
        Press(lightbox.Viewport, Key.Right);
        Assert.Equal(2, lightbox.Index);
        Press(lightbox.Viewport, Key.Left);
        Assert.Equal(1, lightbox.Index);
        Assert.Equal("放大", lightbox.Tools.ZoomIn.Peer().GetName());
        Assert.Equal("下载", lightbox.Tools.Download.Peer().GetName());

        Press(lightbox.Viewport, Key.Escape);
        HeadlessUi.Until(() => !lightbox.IsOpen);
        Assert.True(opener.IsFocused);
        HeadlessUi.Flush(window);
    });

    [Fact]
    public Task Loop_wraps_around_and_download_can_be_hidden() => HeadlessUi.Run(() =>
    {
        var items = Items(2);
        items[1] = items[1] with { Downloadable = false };
        var lightbox = new Lightbox { Items = items, Loop = true };
        var window = HeadlessUi.Show(lightbox);
        lightbox.IsOpen = true;
        HeadlessUi.Until(() => lightbox.Viewport.Bounds.Width > 0);

        Assert.True(lightbox.Sides.Previous.IsVisible);
        Assert.True(lightbox.Tools.Download.IsVisible);
        ((IInvokeProvider)lightbox.Sides.Previous.Peer()).Invoke();
        Assert.Equal(1, lightbox.Index);
        Assert.False(lightbox.Tools.Download.IsVisible);
        Press(lightbox.Viewport, Key.Right);
        Assert.Equal(0, lightbox.Index);
        lightbox.Close();
        HeadlessUi.Flush(window);
    });

    [Fact]
    public Task Opening_fits_the_picture_and_keys_zoom() => HeadlessUi.Run(() =>
    {
        var lightbox = new Lightbox { Items = Items(1) };
        var window = HeadlessUi.Show(lightbox);
        lightbox.IsOpen = true;
        HeadlessUi.Until(() => lightbox.Labels.Zoom.Text is { Length: > 0 });

        // 小图不放大：原大居中
        Assert.Equal(1, lightbox.View.State.Scale);
        Assert.Equal("100%", lightbox.Labels.Zoom.Text);
        Press(lightbox.Viewport, Key.OemPlus);
        Assert.Equal(1.25, lightbox.View.State.Scale);
        Assert.Equal("125%", lightbox.Labels.Zoom.Text);
        Press(lightbox.Viewport, Key.OemMinus);
        Press(lightbox.Viewport, Key.OemMinus);
        Assert.Equal(0.8, lightbox.View.State.Scale, 6);
        Press(lightbox.Viewport, Key.D0);
        Assert.Equal(1, lightbox.View.State.Scale);
        lightbox.Close();
        HeadlessUi.Flush(window);
    });
}
