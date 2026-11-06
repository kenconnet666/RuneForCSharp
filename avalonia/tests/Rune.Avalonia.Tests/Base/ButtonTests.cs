using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Button：调色板给颜色（含悬停）、默认值在主题里、尺寸按 em、加载中点击无效且指示防闪烁、按下出波纹、部件能用 /template/ 样式改。
/// </summary>
public class ButtonTests
{
    static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    static Color Token(string key)
    {
        Application.Current!.TryGetResource(key, ThemeVariant.Light, out var value);
        return BrushColor((IBrush?)value);
    }

    static T Part<T>(Button button, string name) where T : Control =>
        button.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    static Point Center(Visual visual, Visual root) => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    [Fact]
    public Task Palette_gives_variant_colors_and_hover() => HeadlessUi.Run(() =>
    {
        var solid = new Button { Content = "确定" };
        var outline = new Button { Content = "取消", Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral };
        var window = HeadlessUi.Show(new StackPanel { Children = { solid, outline } });

        Assert.Equal((ButtonVariant.Solid, StatusTone.Accent, Radius.Md), (solid.Variant, solid.Tone, solid.Radius));
        Assert.Equal(Token("Rune.Brush.Accent"), BrushColor(solid.Background));
        Assert.Equal(Token("Rune.Brush.AccentContrast"), BrushColor(solid.Foreground));
        Assert.Equal(Token("Rune.Brush.Border"), BrushColor(outline.BorderBrush));

        window.MouseMove(Center(solid, window));
        HeadlessUi.Flush(window);
        // 基础值是悬停色（不含颜色过渡的中间值）
        Assert.Equal(Token("Rune.Brush.AccentHover"), BrushColor(solid.GetBaseValue(Button.BackgroundProperty).Value));
    });

    [Fact]
    public Task Sizes_are_em_and_styles_can_change_defaults() => HeadlessUi.Run(() =>
    {
        var large = new Button { Content = "lg", Size = TextSize.Lg };
        var styled = new Button { Content = "默认" };
        var host = new StackPanel { Children = { large, new Border { Child = styled } } };
        host.Children[1].Styles.Add(new Style(x => x.OfType<Button>()) { Setters = { new Setter(Button.VariantProperty, ButtonVariant.Ghost) } });
        HeadlessUi.Show(host);

        Assert.Equal(16, large.FontSize);
        Assert.Equal(new Thickness(16, 4), large.Padding);           // 1em × 0.25em
        Assert.Equal(36, large.MinHeight);                           // max(2.25em, 24px)
        Assert.Equal(new CornerRadius(0.4 * 16), large.CornerRadius); // radii.md = 0.4em
        Assert.Equal(ButtonVariant.Ghost, styled.Variant);
    });

    [Fact]
    public Task Loading_blocks_clicks_and_shows_the_spinner_after_a_delay() => HeadlessUi.Run(() =>
    {
        var clicks = 0;
        var button = new Button { Content = "保存" };
        button.Click += (_, _) => clicks++;
        var window = HeadlessUi.Show(new StackPanel { Children = { button } });
        var at = Center(button, window);

        button.IsLoading = true;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Assert.Equal(0, clicks);
        Assert.DoesNotContain(":spinner", button.Classes); // 200 毫秒内不出现

        Pump(TimeSpan.FromMilliseconds(350));
        Assert.Contains(":spinner", button.Classes);
        Assert.True(Part<Spinner>(button, ButtonParts.Spinner).IsVisible);
        Assert.Equal(0, Part<StackPanel>(button, ButtonParts.Content).Opacity); // 内容透明，宽度不变

        button.IsLoading = false;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Assert.Equal(1, clicks);
        Assert.Contains(":spinner", button.Classes); // 出现了至少显示 0.5 秒
        Pump(TimeSpan.FromMilliseconds(600));
        Assert.DoesNotContain(":spinner", button.Classes);
    });

    /// <summary>边等真实时间边处理投递回 UI 线程的任务（DelayedLoading 用 Task.Delay 等待）。</summary>
    static void Pump(TimeSpan duration)
    {
        for (var clock = System.Diagnostics.Stopwatch.StartNew(); clock.Elapsed < duration;)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public Task Pressing_starts_a_ripple_of_the_text_color() => HeadlessUi.Run(() =>
    {
        var button = new Button { Content = "发布", Width = 120 };
        var window = HeadlessUi.Show(new StackPanel { Children = { button } });
        var layer = Part<Canvas>(button, "PART_Ripples");

        window.MouseDown(layer.TranslatePoint(new Point(10, 10), window)!.Value, MouseButton.Left);
        var ripple = Assert.IsType<Ellipse>(Assert.Single(layer.Children));
        // 圆心到最远的角：扩散完正好盖满整个按钮
        var radius = Math.Sqrt(Math.Pow(layer.Bounds.Width - 10, 2) + Math.Pow(layer.Bounds.Height - 10, 2));
        Assert.Equal(2 * radius, ripple.Width, 3);
        var fill = Assert.IsAssignableFrom<ISolidColorBrush>(ripple.Fill);
        Assert.Equal((BrushColor(button.Foreground), 0.22), (fill.Color, fill.Opacity));

        // 关掉波纹的按钮不出
        button.Ripple = false;
        window.MouseUp(layer.TranslatePoint(new Point(10, 10), window)!.Value, MouseButton.Left);
        window.MouseDown(layer.TranslatePoint(new Point(10, 10), window)!.Value, MouseButton.Left);
        Assert.Single(layer.Children);
    });

    [Fact]
    public Task Parts_take_template_styles() => HeadlessUi.Run(() =>
    {
        var button = new Button { Content = "新建", Icon = Lucide.Plus, Size = TextSize.Xl };
        var host = new StackPanel { Children = { button } };
        host.SetValue(TextElement.FontSizeProperty, 14d);
        host.Styles.Add(new Style(x => x.OfType<Button>().Template().OfType<Icon>().Name(ButtonParts.Icon)) { Setters = { new Setter(Icon.StrokeWidthProperty, 3d) } });
        HeadlessUi.Show(host);

        var icon = Part<Icon>(button, ButtonParts.Icon);
        Assert.Equal((3d, Lucide.Plus, true), (icon.StrokeWidth, icon.Data, icon.IsVisible));
        Assert.Equal(18, icon.Bounds.Width); // 1em，跟随按钮的字号
        Assert.False(Part<Icon>(button, ButtonParts.IconEnd).IsVisible);
    });
}
