namespace Rune.WinForms.Tests;

/// <summary>StatusDot：sm 8px、md 10px（按 DPI）；有名字时读屏当作图像，否则是装饰。</summary>
public class StatusDotTests
{
    [Fact]
    public Task Size_and_accessibility() => WinFormsUi.Run(() =>
    {
        var plain = new StatusDotControl();
        var labelled = new StatusDotControl { DotSize = StatusDotSize.Md, Tone = StatusTone.Neutral, AccessibleName = "离线" };
        using var form = WinFormsUi.Show(plain, labelled);
        var scale = plain.DeviceDpi / 96f;

        Assert.Equal((int)MathF.Ceiling(8 * scale), plain.Width - plain.Padding.Horizontal);
        Assert.Equal((int)MathF.Ceiling(10 * scale), labelled.Width - labelled.Padding.Horizontal);
        Assert.Equal(AccessibleRole.Graphic, labelled.AccessibilityObject.Role);
        Assert.Equal(AccessibleRole.None, plain.AccessibilityObject.Role);
    });
}
