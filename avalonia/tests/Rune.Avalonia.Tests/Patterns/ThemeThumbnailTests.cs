using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Tests;

/// <summary>ThemeThumbnail：按字号 6em × 3.75em 的定宽一块，放在竖排里也不被拉宽；纯装饰，读屏跳过。</summary>
public class ThemeThumbnailTests
{
    [Fact]
    public Task Fixed_size_by_font_and_hidden_from_readers() => HeadlessUi.Run(() =>
    {
        var thumbnail = new ThemeThumbnail { ThemeVariant = RuneThemeVariants.Forest };
        var window = HeadlessUi.Show(new StackPanel { Width = 400, Children = { thumbnail } });
        HeadlessUi.Flush(window);
        var em = global::Avalonia.Controls.Documents.TextElement.GetFontSize(thumbnail);
        Assert.Equal((6 * em, 3.75 * em), (thumbnail.Bounds.Width, thumbnail.Bounds.Height));
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(thumbnail));
    });
}
