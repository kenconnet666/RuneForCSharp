using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia.Tests;

/// <summary>ColorSwatch：名称与取值（颜色本身、改写、不显示）、色块是装饰；主题画刷的取值随主题变。</summary>
public class ColorSwatchTests
{
    static IEnumerable<string> Texts(ColorSwatch swatch) =>
        swatch.GetLogicalDescendants().OfType<Text>().Where(text => text.IsVisible).Select(text => text.Text ?? "");

    [Fact]
    [Upstream("ColorSwatch", "显示名称与取值：普通颜色显示它本身，value 可以改写，false 不显示")]
    public Task Shows_label_and_value() => HeadlessUi.Run(() =>
    {
        var brand = new ColorSwatch { Color = Brush.Parse("#3e63dd"), Label = "品牌色" };
        var mask = new ColorSwatch { Color = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), Label = "遮罩", Value = "50% 黑" };
        var red = new ColorSwatch { Color = Brushes.Red, ShowValue = false };
        var window = HeadlessUi.Show(new StackPanel { Children = { brand, mask, red } });
        HeadlessUi.Flush(window);
        Assert.Equal(["品牌色", "#3e63dd"], Texts(brand));
        Assert.Equal(["遮罩", "50% 黑"], Texts(mask));
        Assert.Empty(Texts(red));
        var swatch = brand.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == ColorSwatchParts.Swatch);
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(swatch));
        Assert.Equal("rgba(0, 0, 0, 0.5)", ColorSwatch.Format(mask.Color));
    });

    [Fact]
    public Task Theme_brushes_show_the_current_value() => HeadlessUi.Run(() =>
    {
        var surface = new ColorSwatch { Label = "surface" };
        surface[!ColorSwatch.ColorProperty] = new DynamicResourceExtension("Rune.Brush.Surface");
        var window = HeadlessUi.Show(new StackPanel { Children = { surface } });
        try
        {
            HeadlessUi.Flush(window);
            Application.Current!.TryGetResource("Rune.Brush.Surface", ThemeVariant.Light, out var light);
            Assert.Equal(ColorSwatch.Format((IBrush?)light), Texts(surface).Last());
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            HeadlessUi.Flush(window);
            Application.Current!.TryGetResource("Rune.Brush.Surface", ThemeVariant.Dark, out var dark);
            Assert.Equal(ColorSwatch.Format((IBrush?)dark), Texts(surface).Last());
            Assert.NotEqual(ColorSwatch.Format((IBrush?)light), ColorSwatch.Format((IBrush?)dark));
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    });
}
