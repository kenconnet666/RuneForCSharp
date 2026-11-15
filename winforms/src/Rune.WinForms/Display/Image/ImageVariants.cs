using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>图片在框里怎么放（vue-tsx-pro 的 ImageFits，即 CSS 的 object-fit）。</summary>
/// <param name="Value">取值（cover、contain、fill、none、scale-down）。</param>
[TypeConverter(typeof(VariantConverter<ImageFit>))]
public readonly record struct ImageFit(string Value) : IVariant<ImageFit>
{
    /// <summary>铺满、裁掉多余（默认）：缩略图、头像。</summary>
    public static readonly ImageFit Cover = new("cover");

    /// <summary>整张放进去、留白：商品图、图纸。</summary>
    public static readonly ImageFit Contain = new("contain");

    /// <summary>拉伸铺满。</summary>
    public static readonly ImageFit Fill = new("fill");

    /// <summary>原大，超出的裁掉。</summary>
    public static readonly ImageFit None = new("none");

    /// <summary>比框大时同 contain，否则原大。</summary>
    public static readonly ImageFit ScaleDown = new("scale-down");

    /// <inheritdoc />
    public static IReadOnlyList<ImageFit> Known { get; } = [Cover, Contain, Fill, None, ScaleDown];

    /// <inheritdoc />
    public static ImageFit Parse(string value)
    {
        var fit = new ImageFit(value);
        Variants.WarnIfUnknown(fit);
        return fit;
    }

    /// <inheritdoc />
    public override string ToString() => Value ?? "cover";
}

/// <summary>图片的加载状态。</summary>
public enum ImageStatus
{
    /// <summary>加载中：扫光的占位。</summary>
    Loading,

    /// <summary>加载完成。</summary>
    Loaded,

    /// <summary>加载失败（地址错了、解码不了）：显示占位与 alt。</summary>
    Error,
}

/// <summary>Lightbox 里的一张图（vue-tsx-pro 的 LightboxItem）。</summary>
/// <param name="Source">大图的地址。</param>
/// <param name="Alt">图片的说明（读屏念它）。</param>
public sealed record LightboxItem(Uri Source, string Alt)
{
    /// <summary>图片下方的说明文字（或任意内容）。</summary>
    public object? Caption { get; init; }

    /// <summary>下载时建议的文件名；不写时取地址里的文件名。</summary>
    public string? DownloadName { get; init; }

    /// <summary>显示下载按钮，默认 true（TS 的 download: false 时为 false）。</summary>
    public bool Downloadable { get; init; } = true;

    /// <summary>下载时建议的文件名：写了 <see cref="DownloadName"/> 用它，否则取地址最后一段。</summary>
    public string SuggestedFileName
    {
        get
        {
            if (DownloadName is { Length: > 0 } name) return name;
            var path = Source.IsAbsoluteUri ? Source.LocalPath : Source.OriginalString;
            var file = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
            return file.Length > 0 ? file : "image";
        }
    }
}
