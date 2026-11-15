using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>裁剪框的形状（vue-tsx-pro 的 ImageCropShapes）。</summary>
/// <param name="Value">取值（rect、round）。</param>
[TypeConverter(typeof(VariantConverter<ImageCropShape>))]
public readonly record struct ImageCropShape(string Value) : IVariant<ImageCropShape>
{
    /// <summary>矩形（默认）。</summary>
    public static readonly ImageCropShape Rect = new("rect");

    /// <summary>圆形（头像）：输出的图四角透明。</summary>
    public static readonly ImageCropShape Round = new("round");

    /// <inheritdoc />
    public static IReadOnlyList<ImageCropShape> Known { get; } = [Rect, Round];

    /// <summary>是圆形。</summary>
    public bool IsRound => Value == "round";

    /// <inheritdoc />
    public static ImageCropShape Parse(string value)
    {
        var shape = new ImageCropShape(value);
        Variants.WarnIfUnknown(shape);
        return shape;
    }

    /// <inheritdoc />
    public override string ToString() => Value ?? "rect";
}

/// <summary>导出的图片格式（TS 的 type：image/png、image/jpeg）。</summary>
public enum ImageCropFileType
{
    /// <summary>PNG（默认；圆形的四角透明）。</summary>
    Png,

    /// <summary>JPEG（没有透明：圆形的四角是白的）。</summary>
    Jpeg,
}

/// <summary>输出的像素大小（vue-tsx-pro 的 ImageCropOutput）：只写一边时按宽高比算另一边；都不写时按图片原本的像素。</summary>
/// <param name="Width">输出的宽（像素）。</param>
/// <param name="Height">输出的高（像素）。</param>
public readonly record struct ImageCropOutput(int? Width = null, int? Height = null);

/// <summary>导出成图片的选项（vue-tsx-pro 的 ImageCropFormat）：像素大小加上格式与质量。</summary>
public sealed record ImageCropFormat
{
    /// <summary>输出的像素大小。</summary>
    public ImageCropOutput Size { get; init; }

    /// <summary>格式，默认 PNG。</summary>
    public ImageCropFileType Type { get; init; }

    /// <summary>质量（JPEG 用），0–1；不写时是编码器的默认。</summary>
    public double? Quality { get; init; }
}

/// <summary>
/// 裁剪图片的几何（vue-tsx-pro 的 ImageCropper 里的计算，三个库共用一份）：裁剪框按宽高比放进区域中间、四周留边；
/// 正好盖满裁剪框的缩放；初始状态（盖满、居中）；裁剪框对应的图片像素区域与输出的像素大小。
/// </summary>
public static class ImageCropGeometry
{
    /// <summary>裁剪框：按宽高比（宽 ÷ 高）放进区域中间，四周至少留 padding。</summary>
    public static PanZoomRegion Frame(double width, double height, double aspect, double padding)
    {
        var roomWidth = Math.Max(width - padding * 2, 1);
        var roomHeight = Math.Max(height - padding * 2, 1);
        var frameWidth = Math.Min(roomWidth, roomHeight * aspect);
        var frameHeight = frameWidth / aspect;
        return new PanZoomRegion((width - frameWidth) / 2, (height - frameHeight) / 2, frameWidth, frameHeight);
    }

    /// <summary>正好盖满裁剪框的缩放；还不知道图片大小时 1。</summary>
    public static double CoverScale(PanZoomRegion frame, (double Width, double Height)? natural) =>
        natural is { Width: > 0, Height: > 0 } size ? Math.Max(frame.Width / size.Width, frame.Height / size.Height) : 1;

    /// <summary>初始状态：图片盖满裁剪框、居中。</summary>
    public static PanZoomState Initial(PanZoomRegion frame, (double Width, double Height)? natural)
    {
        var scale = CoverScale(frame, natural);
        var (width, height) = natural is { } size ? (size.Width * scale, size.Height * scale) : (0, 0);
        return new PanZoomState(frame.X + (frame.Width - width) / 2, frame.Y + (frame.Height - height) / 2, scale);
    }

    /// <summary>裁剪框里是图片的哪一块（图片像素）。</summary>
    public static PanZoomRegion Source(PanZoomRegion frame, PanZoomState state) =>
        new((frame.X - state.X) / state.Scale, (frame.Y - state.Y) / state.Scale, frame.Width / state.Scale, frame.Height / state.Scale);

    /// <summary>输出的像素大小：只写一边时按宽高比算另一边；都不写时按图片里那一块原本的像素。</summary>
    public static (int Width, int Height) OutputSize(PanZoomRegion source, double aspect, ImageCropOutput output)
    {
        var width = (int)Math.Round(output.Width ?? (output.Height is { } height ? height * aspect : source.Width));
        var outputHeight = (int)Math.Round(output.Height ?? width / aspect);
        return (Math.Max(1, width), Math.Max(1, outputHeight));
    }
}
