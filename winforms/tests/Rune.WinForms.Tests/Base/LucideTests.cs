using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>生成的 Lucide 全集（tools/icons-gen）：每个图标都能建成 GraphicsPath，坐标在 24 格画布里，旧名转发到新名。</summary>
public class LucideTests
{
    static IEnumerable<IconData> Icons() =>
        typeof(Lucide).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() is null)
            .Select(p => (IconData)p.GetValue(null)!);

    [Fact]
    public void Every_icon_builds_a_path_inside_the_canvas()
    {
        var icons = Icons().ToList();
        Assert.True(icons.Count > 2000);
        var canvas = RectangleF.FromLTRB(-1, -1, 25, 25);
        foreach (var icon in icons)
        {
            using var path = icon.CreatePath();
            Assert.True(path.PointCount > 0, icon.Name);
            Assert.True(canvas.Contains(path.GetBounds()), icon.Name);
            using var fill = icon.CreateFillPath();
            Assert.Equal(icon.FillPath is not null, fill is not null);
        }
    }

    [Fact]
    public void Shapes_land_on_the_24_grid()
    {
        // circle-x：半径 10 的圆在中心 (12, 12)，后面是两条斜线
        using var path = Lucide.CircleX.CreatePath();
        var bounds = path.GetBounds();
        Assert.Equal(new RectangleF(2, 2, 20, 20), new RectangleF(MathF.Round(bounds.X, 2), MathF.Round(bounds.Y, 2), MathF.Round(bounds.Width, 2), MathF.Round(bounds.Height, 2)));
        // 三个子路径：圆、两条线
        using var iterator = new System.Drawing.Drawing2D.GraphicsPathIterator(path);
        Assert.Equal(3, iterator.SubpathCount);
        using var fill = Lucide.Palette.CreateFillPath();
        Assert.NotNull(fill);
    }

    [Fact]
    public void Old_names_forward_to_the_new_ones()
    {
#pragma warning disable CS0618 // 测的就是旧名
        Assert.Same(Lucide.CircleX, Lucide.XCircle);
#pragma warning restore CS0618
        Assert.Equal("circle-x", Lucide.CircleX.Name);
    }
}
