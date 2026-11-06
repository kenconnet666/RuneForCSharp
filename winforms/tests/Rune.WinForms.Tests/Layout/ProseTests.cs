namespace Rune.WinForms.Tests;

/// <summary>ProseControl：竖排、块之间 space.lg、最宽约 70ch；读屏里是文档。</summary>
public class ProseTests
{
    [Fact]
    public Task Blocks_are_spaced_and_measured() => WinFormsUi.Run(() =>
    {
        var (first, second) = (new ParagraphControl { Text = "一" }, new ParagraphControl { Text = "二" });
        var prose = new ProseControl();
        prose.Controls.AddRange([first, second]);
        using var form = WinFormsUi.Show(prose);
        var em = prose.Font.SizeInPoints * prose.DeviceDpi / 72f;

        Assert.Equal(first.Bottom + (int)Math.Round(em), second.Top); // space.lg = 1em
        Assert.Equal(70 * TextRenderer.MeasureText("0", prose.Font, Size.Empty, TextFormatFlags.NoPadding).Width, prose.MaximumSize.Width);
        Assert.Equal(0, new ProseControl { LimitLineLength = false }.MaximumSize.Width);
        Assert.Equal(AccessibleRole.Document, prose.AccessibilityObject.Role);
    });
}
