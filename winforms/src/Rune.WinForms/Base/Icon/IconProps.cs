namespace Rune.WinForms;

/// <summary>
/// 图标当部件时的属性（TS 的 <c>Partial&lt;IconProps&gt;</c>，DESIGN §7）：字段可空，null 是"这一层不改"。
/// 组件给的默认在下，使用方的 <c>PropsIcon</c> 在上，逐字段合并（<see cref="Merge"/>）；合并完套到图标上（<see cref="ApplyTo"/>）。
/// <code>
/// new SpinnerControl { PropsIcon = new IconProps { StrokeWidth = 3 } };   // 只改线宽，图标与大小仍是 Spinner 给的
/// </code>
/// </summary>
public sealed record IconProps
{
    /// <summary>图标数据。</summary>
    public IconData? Data { get; init; }

    /// <summary>大小档位或像素数。</summary>
    public TextSize? Size { get; init; }

    /// <summary>色调。</summary>
    public Tone? Tone { get; init; }

    /// <summary>线宽（24 格画布上的单位）。</summary>
    public float? StrokeWidth { get; init; }

    /// <summary>从右到左排版时水平翻转。</summary>
    public bool? FlipInRtl { get; init; }

    /// <summary>上面一层盖住这一层：上层写了的字段用上层的，没写的沿用这一层。</summary>
    /// <param name="over">上面一层；null 时原样返回。</param>
    public IconProps Merge(IconProps? over) => over is null ? this : new()
    {
        Data = over.Data ?? Data,
        Size = over.Size ?? Size,
        Tone = over.Tone ?? Tone,
        StrokeWidth = over.StrokeWidth ?? StrokeWidth,
        FlipInRtl = over.FlipInRtl ?? FlipInRtl,
    };

    /// <summary>把写了的字段设到图标上，没写的保持图标自己的默认值。</summary>
    /// <param name="icon">图标元素。</param>
    public void ApplyTo(Icon icon)
    {
        if (Data is not null) icon.Data = Data;
        if (Size is { } size) icon.Size = size;
        if (Tone is { } tone) icon.Tone = tone;
        if (StrokeWidth is { } strokeWidth) icon.StrokeWidth = strokeWidth;
        if (FlipInRtl is { } flip) icon.FlipInRtl = flip;
    }
}
