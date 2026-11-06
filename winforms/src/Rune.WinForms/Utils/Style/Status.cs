using System.ComponentModel;

namespace Rune.WinForms;

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

/// <summary>样式 × 色调的颜色（vue-tsx-pro 的 badgeRecipe 的 24 条组合；中性色调用反色、填充等另一组 token）。</summary>
/// <param name="Foreground">文字与图标。</param>
/// <param name="Background">底色；没有时为 null。</param>
/// <param name="Border">描边；没有时为 null。</param>
public readonly record struct StatusColors(Color Foreground, Color? Background, Color? Border)
{
    /// <summary>取一种组合在主题里的颜色；使用方扩展的取值按浅色底、中性处理。</summary>
    /// <param name="variant">样式。</param>
    /// <param name="tone">色调。</param>
    /// <param name="colors">当前主题的颜色。</param>
    public static StatusColors Of(StatusVariant variant, StatusTone tone, ThemeColors colors)
    {
        (Color Solid, Color Contrast, Color Soft, Color Text, Color Border)? shades = tone.Value switch
        {
            "accent" => (colors.Accent, colors.AccentContrast, colors.AccentSoft, colors.AccentText, colors.AccentBorder),
            "info" => (colors.Info, colors.InfoContrast, colors.InfoSoft, colors.InfoText, colors.InfoBorder),
            "success" => (colors.Success, colors.SuccessContrast, colors.SuccessSoft, colors.SuccessText, colors.SuccessBorder),
            "warning" => (colors.Warning, colors.WarningContrast, colors.WarningSoft, colors.WarningText, colors.WarningBorder),
            "danger" => (colors.Danger, colors.DangerContrast, colors.DangerSoft, colors.DangerText, colors.DangerBorder),
            _ => null,
        };
        return (variant.Value, shades) switch
        {
            ("solid", null) => new(colors.InverseText, colors.InverseSurface, null),
            ("solid", { } s) => new(s.Contrast, s.Solid, null),
            ("surface", null) => new(colors.Text, colors.Surface, colors.Border),
            ("surface", { } s) => new(s.Text, s.Soft, s.Border),
            ("outline", null) => new(colors.Text, null, colors.Border),
            ("outline", { } s) => new(s.Text, null, s.Border),
            (_, null) => new(colors.Text, colors.Fill, null),
            (_, { } s) => new(s.Text, s.Soft, null),
        };
    }
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

    /// <summary>主题里的圆角：em 系数（full 是 9999 像素，按高度的一半画成胶囊）；default 或使用方扩展的取值为 null。</summary>
    /// <param name="radii">当前主题的圆角。</param>
    public float? In(ThemeRadii radii) => Value switch
    {
        "none" => radii.None,
        "xs" => radii.Xs,
        "sm" => radii.Sm,
        "md" => radii.Md,
        "lg" => radii.Lg,
        "xl" => radii.Xl,
        "full" => radii.Full,
        _ => null,
    };

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
