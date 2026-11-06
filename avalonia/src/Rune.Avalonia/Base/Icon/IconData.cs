using global::Avalonia.Media;

namespace Rune.Avalonia;

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

    /// <summary>描边的几何：第一次用到时解析，之后各处共用同一个（不要修改它）。</summary>
    public Geometry Geometry => field ??= Geometry.Parse(Path);

    /// <summary>填充的几何；没有填充的部分时为 null。</summary>
    public Geometry? FillGeometry => FillPath is null ? null : field ??= Geometry.Parse(FillPath);
}
