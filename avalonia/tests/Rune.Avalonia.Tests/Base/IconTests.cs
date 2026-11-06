using System.Reflection;
using Avalonia;

namespace Rune.Avalonia.Tests;

/// <summary>生成的 Lucide 全集（tools/icons-gen）：每个图标都能解析成几何，坐标在 24 格画布里，旧名转发到新名。</summary>
public class IconTests
{
    static IEnumerable<IconData> Icons() =>
        typeof(Lucide).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() is null)
            .Select(p => (IconData)p.GetValue(null)!);

    [Fact]
    public Task Every_icon_parses_into_a_geometry() => HeadlessUi.Run(() =>
    {
        var icons = Icons().ToList();
        Assert.True(icons.Count > 2000);
        foreach (var icon in icons)
        {
            Assert.NotNull(icon.Geometry);
            if (icon.FillPath is not null) Assert.NotNull(icon.FillGeometry);
        }
    });

    [Fact]
    public Task Shapes_land_on_the_24_grid() => HeadlessUi.Run(() =>
    {
        // circle-x：半径 10 的圆在中心 (12, 12)
        var bounds = Lucide.CircleX.Geometry.Bounds;
        Assert.Equal(new Rect(2, 2, 20, 20), new Rect(Math.Round(bounds.X, 2), Math.Round(bounds.Y, 2), Math.Round(bounds.Width, 2), Math.Round(bounds.Height, 2)));
        Assert.Same(Lucide.CircleX.Geometry, Lucide.CircleX.Geometry);
        Assert.NotNull(Lucide.Palette.FillGeometry);
        Assert.Null(Lucide.Check.FillGeometry);
    });

    [Fact]
    public void Old_names_forward_to_the_new_ones()
    {
#pragma warning disable CS0618 // 测的就是旧名
        Assert.Same(Lucide.CircleX, Lucide.XCircle);
#pragma warning restore CS0618
        Assert.Equal("circle-x", Lucide.CircleX.Name);
    }
}
