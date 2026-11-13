using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 图标数据：24×24 画布上的路径，按 Lucide 的画法描边（圆头、圆角）。路径用 SVG 的路径语法；
/// <see cref="Lucide"/> 里的已归一成绝对坐标的 M、L、C、Z。自定义图标照同样的格式：<c>new IconData("logo", "M4 4L20 20")</c>。
/// </summary>
/// <param name="name">名字（Lucide 的图标名）。</param>
/// <param name="path">描边的路径。</param>
/// <param name="fillPath">要填充的部分（少数 Lucide 图标的实心点），没有时为 null。</param>
public sealed class IconData(string name, string path, string? fillPath = null)
{
    /// <summary>画布边长：路径的坐标在 0–24 之间。</summary>
    public const double Size = 24;

    /// <summary>名字（Lucide 的图标名）。</summary>
    public string Name { get; } = name;

    /// <summary>描边的路径。</summary>
    public string Path { get; } = path;

    /// <summary>要填充的部分，没有时为 null。</summary>
    public string? FillPath { get; } = fillPath;

    /// <summary>描边的几何：第一次用到时解析并冻结，之后各处（包括别的线程）共用同一个。</summary>
    public Geometry Geometry => field ??= Frozen(Geometry.Parse(Path));

    /// <summary>描线用的一笔一笔的折线（Icon 的 Draw）：第一次描线时解析。</summary>
    internal IconStrokes Strokes => field ??= IconStrokes.Parse(Path);

    /// <summary>填充的几何（已冻结）；没有填充的部分时为 null。</summary>
    public Geometry? FillGeometry => FillPath is null ? null : field ??= Frozen(Geometry.Parse(FillPath));

    static Geometry Frozen(Geometry geometry)
    {
        geometry.Freeze();
        return geometry;
    }
}
