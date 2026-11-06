using System.ComponentModel;

namespace Rune.Avalonia;

/// <summary>
/// 状态色调（vue-tsx-pro 的 StatusTone）：Badge、Tag、Alert、Toast 等"带颜色的标记与提示"共用，按钮家族、Progress 也用这一套。
/// </summary>
/// <param name="Value">取值（neutral、accent、info、success、warning、danger）。</param>
[TypeConverter(typeof(VariantConverter<StatusTone>))]
public readonly record struct StatusTone(string Value) : IVariant<StatusTone>
{
    /// <summary>中性：普通的标签、计数，不需要强调的操作。</summary>
    public static readonly StatusTone Neutral = new("neutral");

    /// <summary>品牌色：新功能、选中、主要操作。</summary>
    public static readonly StatusTone Accent = new("accent");

    /// <summary>信息：提示、说明。</summary>
    public static readonly StatusTone Info = new("info");

    /// <summary>成功：已完成、正常。</summary>
    public static readonly StatusTone Success = new("success");

    /// <summary>警告：需要注意、即将过期。</summary>
    public static readonly StatusTone Warning = new("warning");

    /// <summary>危险：错误、失败、删除等破坏性操作。</summary>
    public static readonly StatusTone Danger = new("danger");

    /// <inheritdoc />
    public static IReadOnlyList<StatusTone> Known { get; } = [Neutral, Accent, Info, Success, Warning, Danger];

    /// <summary>调色板的资源键（<c>Rune.Palette.Danger</c>，token-gen 按 palette.mjs 生成）；没写时为 null。</summary>
    public string? PaletteKey => Value is { Length: > 0 } tone ? $"Rune.Palette.{char.ToUpperInvariant(tone[0])}{tone[1..]}" : null;

    /// <inheritdoc />
    public static StatusTone Parse(string value)
    {
        var tone = new StatusTone(value);
        Variants.WarnIfUnknown(tone);
        return tone;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>标记与提示的样式（vue-tsx-pro 的 StatusVariant）：外观 = 样式 × 色调。</summary>
/// <param name="Value">取值（solid、soft、surface、outline）。</param>
[TypeConverter(typeof(VariantConverter<StatusVariant>))]
public readonly record struct StatusVariant(string Value) : IVariant<StatusVariant>
{
    /// <summary>实底：最醒目，计数、状态点。</summary>
    public static readonly StatusVariant Solid = new("solid");

    /// <summary>浅色底。</summary>
    public static readonly StatusVariant Soft = new("soft");

    /// <summary>浅色底加描边。</summary>
    public static readonly StatusVariant Surface = new("surface");

    /// <summary>只有描边。</summary>
    public static readonly StatusVariant Outline = new("outline");

    /// <inheritdoc />
    public static IReadOnlyList<StatusVariant> Known { get; } = [Solid, Soft, Surface, Outline];

    /// <inheritdoc />
    public static StatusVariant Parse(string value)
    {
        var variant = new StatusVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>圆角档位（vue-tsx-pro 的 Radius，对应主题的 radii，em，随字号缩放）。</summary>
/// <param name="Value">取值（none、xs、sm、md、lg、xl、full）。</param>
[TypeConverter(typeof(VariantConverter<Radius>))]
public readonly record struct Radius(string Value) : IVariant<Radius>
{
    /// <summary>直角：表格、密集列表里与直线对齐。</summary>
    public static readonly Radius None = new("none");

    /// <summary>0.15em。</summary>
    public static readonly Radius Xs = new("xs");

    /// <summary>0.25em。</summary>
    public static readonly Radius Sm = new("sm");

    /// <summary>0.4em：控件与按钮的默认。</summary>
    public static readonly Radius Md = new("md");

    /// <summary>0.7em：卡片、面板。</summary>
    public static readonly Radius Lg = new("lg");

    /// <summary>1em。</summary>
    public static readonly Radius Xl = new("xl");

    /// <summary>胶囊形（宽高相等时是圆形）。</summary>
    public static readonly Radius Full = new("full");

    /// <inheritdoc />
    public static IReadOnlyList<Radius> Known { get; } = [None, Xs, Sm, Md, Lg, Xl, Full];

    /// <summary>主题里的 em 系数的资源键（<c>Rune.Radius.Sm</c>）；default 时为 null。</summary>
    public string? ResourceKey => Value is null ? null : $"Rune.Radius.{char.ToUpperInvariant(Value[0])}{Value[1..]}";

    /// <inheritdoc />
    public static Radius Parse(string value)
    {
        var radius = new Radius(value);
        Variants.WarnIfUnknown(radius);
        return radius;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
