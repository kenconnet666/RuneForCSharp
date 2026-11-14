using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>ThemeResources：查不到资源时取生成的默认值；Render 里查资源的控件在换主题、作用域改 token 时重画。</summary>
public class ThemeResourcesTests
{
    [Fact]
    public Task Missing_resources_fall_back_to_the_generated_token_values() => HeadlessUi.Run(() =>
    {
        var loose = new Border(); // 不在窗口里：查不到应用的主题以外的东西，但应用主题里有

        Assert.Equal(0.75, loose.Resource<double>(ThemeKeys.Space.Md));
        Assert.Equal(0.75, ThemeKeys.Fallback(ThemeKeys.Space.Md));
        Assert.Equal(TimeSpan.FromMilliseconds(200), ThemeKeys.Fallback(ThemeKeys.Duration.Md));
        Assert.Null(ThemeKeys.Fallback(ThemeKeys.Brush.Border)); // 颜色没有默认值
        Assert.Null(ThemeKeys.Fallback("Rune.Unknown.Key"));
    });

    [Fact]
    public Task Render_time_lookups_redraw_when_the_scope_or_theme_changes() => HeadlessUi.Run(() =>
    {
        var probe = new RenderProbe();
        var host = new Border { Child = probe };
        probe.RedrawOnResourceChanges(host);
        var window = HeadlessUi.Show(new StackPanel { Children = { host } });
        Redraw(window);
        var before = probe.Renders;

        host.Resources[ThemeKeys.Brush.Fill] = Brushes.Red; // 作用域里改 token
        Redraw(window);
        Assert.True(probe.Renders > before);

        before = probe.Renders;
        window.RequestedThemeVariant = ThemeVariant.Dark;
        Redraw(window);
        Assert.True(probe.Renders > before);
    });

    static void Redraw(Window window)
    {
        HeadlessUi.Flush(window);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        HeadlessUi.Flush(window);
    }

    sealed class RenderProbe : Control
    {
        public int Renders;

        protected override Size MeasureOverride(Size availableSize) => new(10, 10);

        public override void Render(DrawingContext context) => Renders++;
    }
}
