using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>LightboxControl：铺满窗体、平的面板；← / → 切换（Loop 时绕回）、Esc 关闭；打开后整张放进视口；+ / − / 0 缩放与比例；按钮的读屏名称。</summary>
public class LightboxTests
{
    static LightboxItem[] Items(int count) => [.. Enumerable.Range(1, count).Select(i => new LightboxItem(ImageTests.Picture($"l{i}"), $"图纸 {i}"))];

    static void Escape(DialogControl dialog) =>
        dialog.Surface.Form.GetType().GetMethod("ProcessDialogKey", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(Keys)])!.Invoke(dialog.Surface.Form, [Keys.Escape]);

    [Fact]
    [Upstream("Lightbox", "←→ 切换，Esc 关闭并把焦点还回去")]
    public Task Arrows_switch_and_escape_closes() => WinFormsUi.Run(() =>
    {
        var opener = new ButtonControl { Text = "预览" };
        var lightbox = new LightboxControl { Items = Items(3) };
        var reasons = new List<OpenChangeReason>();
        lightbox.OpenChanged += (_, e) => reasons.Add(e.Reason);
        using var form = ImageTests.Open(opener, lightbox);
        opener.Focus();
        lightbox.IsOpen = true;
        WinFormsUi.Pump();

        Assert.Equal(form.RectangleToScreen(form.ClientRectangle), lightbox.Surface.Backdrop!.Panel);
        Assert.Equal(lightbox.Surface.Backdrop.Panel, lightbox.Surface.Form.Bounds);
        Assert.Equal("图片预览", lightbox.Surface.Form.AccessibleName);
        Assert.Equal(AccessibleRole.Graphic, lightbox.Parts.Stage.AccessibilityObject.Role);
        Assert.Equal("图纸 1", lightbox.Parts.Stage.AccessibilityObject.Name);
        Assert.False(lightbox.Parts.Previous.Visible);
        lightbox.Press(Keys.Right);
        lightbox.Press(Keys.Right);
        Assert.Equal(2, lightbox.Index);
        lightbox.Press(Keys.Left);
        Assert.Equal(1, lightbox.Index);
        Assert.Equal("放大", lightbox.Parts.ZoomIn.AccessibleName);
        Assert.Equal("下载", lightbox.Parts.Download.AccessibleName);

        Escape(lightbox);
        WinFormsUi.Until(() => !lightbox.IsOpen);
        Assert.Equal([OpenChangeReason.Escape], reasons);
        // 焦点回到打开它的按钮
        WinFormsUi.Until(() => opener.Focused);
    });

    [Fact]
    public Task Loop_wraps_around_and_download_can_be_hidden() => WinFormsUi.Run(() =>
    {
        var items = Items(2);
        items[1] = items[1] with { Downloadable = false };
        var lightbox = new LightboxControl { Items = items, Loop = true };
        using var form = ImageTests.Open(lightbox);
        lightbox.IsOpen = true;
        WinFormsUi.Pump();

        Assert.True(lightbox.Parts.Previous.Visible);
        Assert.True(lightbox.Parts.Download.Visible);
        lightbox.Parts.Previous.PerformClick();
        Assert.Equal(1, lightbox.Index);
        Assert.False(lightbox.Parts.Download.Visible);
        lightbox.Press(Keys.Right);
        Assert.Equal(0, lightbox.Index);
        lightbox.Close();
    });

    [Fact]
    public Task Opening_fits_the_picture_and_keys_zoom() => WinFormsUi.Run(() =>
    {
        var lightbox = new LightboxControl { Items = Items(1) };
        using var form = ImageTests.Open(lightbox);
        lightbox.IsOpen = true;
        WinFormsUi.Until(() => lightbox.Parts.Zoom.Text is { Length: > 0 });

        // 小图不放大：原大居中
        var stage = lightbox.Parts.Stage;
        Assert.Equal(1, lightbox.View.State.Scale);
        Assert.Equal((stage.Width - stage.LogicalToDeviceUnits(40)) / 2d, lightbox.View.State.X, 3);
        Assert.Equal("100%", lightbox.Parts.Zoom.Text);
        stage.Focus();
        Assert.True(lightbox.Press(Keys.Oemplus));
        Assert.Equal(1.25, lightbox.View.State.Scale);
        Assert.Equal("125%", lightbox.Parts.Zoom.Text);
        lightbox.Press(Keys.OemMinus);
        lightbox.Press(Keys.OemMinus);
        Assert.Equal(0.8, lightbox.View.State.Scale, 6);
        lightbox.Press(Keys.D0);
        Assert.Equal(1, lightbox.View.State.Scale);
        lightbox.Close();
    });
}
