namespace Rune.WinForms;

/// <summary>
/// 文字当部件时的属性（TS 的 <c>Partial&lt;TextProps&gt;</c>，DESIGN §7）：字段可空，null 是"这一层不改"。
/// 组件给的默认在下，使用方的 <c>PropsLabel</c> 这类属性在上，逐字段合并（<see cref="Merge"/>）；合并完套到文字上（<see cref="ApplyTo"/>）。
/// <code>
/// new ProgressControl { Value = 120, PropsValueLabel = new TextProps { Tone = TextTone.Danger } };   // 只改颜色，等宽数字仍是 Progress 给的
/// </code>
/// </summary>
public sealed record TextProps
{
    /// <summary>字号档位或像素数。</summary>
    public TextSize? Size { get; init; }

    /// <summary>字重。</summary>
    public TextWeight? Weight { get; init; }

    /// <summary>色调。</summary>
    public TextTone? Tone { get; init; }

    /// <summary>对齐。</summary>
    public TextAlign? Align { get; init; }

    /// <summary>等宽数字。</summary>
    public bool? Numeric { get; init; }

    /// <summary>上面一层盖住这一层：上层写了的字段用上层的，没写的沿用这一层。</summary>
    /// <param name="over">上面一层；null 时原样返回。</param>
    public TextProps Merge(TextProps? over) => over is null ? this : new()
    {
        Size = over.Size ?? Size,
        Weight = over.Weight ?? Weight,
        Tone = over.Tone ?? Tone,
        Align = over.Align ?? Align,
        Numeric = over.Numeric ?? Numeric,
    };

    /// <summary>把写了的字段设到文字上，没写的保持文字自己的默认值。</summary>
    /// <param name="text">文字元素。</param>
    public void ApplyTo(Text text)
    {
        if (Size is { } size) text.Size = size;
        if (Weight is { } weight) text.Weight = weight;
        if (Tone is { } tone) text.Tone = tone;
        if (Align is { } align) text.Align = align;
        if (Numeric is { } numeric) text.Numeric = numeric;
    }
}
