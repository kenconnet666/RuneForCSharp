namespace Rune.WinForms.Tests;

/// <summary>Kbd：字号是宿主的 0.86 倍，最小宽度 1.6em，高度是行高 snug 加上、下描边（底边 2px）；读屏当作静态文字。</summary>
public class KbdTests
{
    [Upstream("Kbd", "渲染 <kbd>")]
    [Fact]
    public Task Size_follows_the_scaled_font_and_screen_readers_read_the_key() => WinFormsUi.Run(() =>
    {
        var kbd = new KbdControl { Text = "K" };
        using var form = WinFormsUi.Show(kbd);
        var em = form.Font.SizeInPoints * 0.86f * kbd.DeviceDpi / 72f;
        var scale = kbd.DeviceDpi / 96f;

        Assert.True(kbd.Width - kbd.Padding.Horizontal >= (int)(1.6f * em));
        Assert.Equal((int)MathF.Ceiling(Tokens.LineHeight.Snug * em + 3 * scale), kbd.Height - kbd.Padding.Vertical);
        Assert.Equal((AccessibleRole.StaticText, "K"), (kbd.AccessibilityObject.Role, kbd.AccessibilityObject.Name));
    });
}
