using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 图标数据：24×24 画布上的路径，按 Lucide 的画法描边（圆头、圆角）。<see cref="Lucide"/> 里的路径已归一成绝对坐标的
/// M、L、C、Z；自定义图标照同样的格式写：<c>new IconData("logo", "M4 4L20 20")</c>。
/// 绘制时 <see cref="CreatePath"/> 新建一条 GraphicsPath（24×24 的坐标），用 Matrix 缩放到图标尺寸再描边。
/// </summary>
/// <param name="name">名字（Lucide 的图标名）。</param>
/// <param name="path">描边的路径（只含绝对坐标的 M、L、C、Z）。</param>
/// <param name="fillPath">要填充的部分（少数 Lucide 图标的实心点），没有时为 null。</param>
[TypeConverter(typeof(IconDataConverter))]
public sealed class IconData(string name, string path, string? fillPath = null)
{
    /// <summary>画布边长：路径的坐标在 0–24 之间。</summary>
    public const float Size = 24;

    /// <summary>名字（Lucide 的图标名）。</summary>
    public string Name { get; } = name;

    /// <summary>描边的路径。</summary>
    public string Path { get; } = path;

    /// <summary>要填充的部分，没有时为 null。</summary>
    public string? FillPath { get; } = fillPath;

    Outline StrokeOutline => field ??= Outline.Parse(Path);

    Outline? FillOutline => FillPath is null ? null : field ??= Outline.Parse(FillPath);

    /// <summary>新建描边的 GraphicsPath；用完要 Dispose。解析结果缓存着，每次只是按点建一条新路径。</summary>
    public GraphicsPath CreatePath() => StrokeOutline.Create();

    /// <summary>新建填充部分的 GraphicsPath；没有填充的部分时为 null。用完要 Dispose。</summary>
    public GraphicsPath? CreateFillPath() => FillOutline?.Create();

    /// <summary>解析好的路径：GraphicsPath 的点与点的类型（起点、直线、贝塞尔、闭合）。</summary>
    sealed class Outline(PointF[] points, byte[] types)
    {
        public GraphicsPath Create() => new(points, types);

        public static Outline Parse(string path)
        {
            var points = new List<PointF>();
            var types = new List<byte>();
            var i = 0;
            while (i < path.Length)
            {
                var command = path[i++];
                switch (command)
                {
                    case 'M':
                        Add(PathPointType.Start, 1);
                        break;
                    case 'L':
                        Add(PathPointType.Line, 1);
                        break;
                    case 'C':
                        Add(PathPointType.Bezier, 3);
                        break;
                    case 'Z':
                        types[^1] |= (byte)PathPointType.CloseSubpath;
                        break;
                    default:
                        throw new FormatException($"图标路径只认绝对坐标的 M、L、C、Z，位置 {i - 1} 是“{command}”：{path}");
                }
            }
            return new Outline([.. points], [.. types]);

            void Add(PathPointType type, int count)
            {
                for (var n = 0; n < count; n++)
                {
                    points.Add(new PointF(Number(), Number()));
                    types.Add((byte)type);
                }
            }

            float Number()
            {
                while (i < path.Length && path[i] == ' ') i++;
                var start = i;
                if (i < path.Length && path[i] == '-') i++;
                while (i < path.Length && (char.IsAsciiDigit(path[i]) || path[i] == '.')) i++;
                return float.Parse(path.AsSpan(start, i - start), CultureInfo.InvariantCulture);
            }
        }
    }
}
