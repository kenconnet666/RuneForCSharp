using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 字号档位（vue-tsx-pro 的 TextSize）：xs–xl 对应 <see cref="Tokens.FontSize"/>，也可以直接写像素数（设备无关像素）。
/// </summary>
/// <param name="Value">档位名（xs…xl）或像素数的文字。</param>
[TypeConverter(typeof(VariantConverter<TextSize>))]
public readonly record struct TextSize(string Value) : IVariant<TextSize>
{
    /// <summary>12px：辅助说明、标签。</summary>
    public static readonly TextSize Xs = new("xs");

    /// <summary>13px：紧凑控件。</summary>
    public static readonly TextSize Sm = new("sm");

    /// <summary>14px：正文与默认控件。</summary>
    public static readonly TextSize Md = new("md");

    /// <summary>16px：宽松控件、小标题。</summary>
    public static readonly TextSize Lg = new("lg");

    /// <summary>18px：强调文字、卡片标题。</summary>
    public static readonly TextSize Xl = new("xl");

    /// <inheritdoc />
    public static IReadOnlyList<TextSize> Known { get; } = [Xs, Sm, Md, Lg, Xl];

    /// <summary>设备无关像素：像素数原样，档位取主题字号；default 时为 null。</summary>
    public float? Pixels => Value switch
    {
        null => null,
        "xs" => Tokens.FontSize.Xs,
        "sm" => Tokens.FontSize.Sm,
        "md" => Tokens.FontSize.Md,
        "lg" => Tokens.FontSize.Lg,
        "xl" => Tokens.FontSize.Xl,
        _ => float.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels) ? pixels : null,
    };

    /// <inheritdoc />
    public static TextSize Parse(string value)
    {
        var size = new TextSize(value);
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) Variants.WarnIfUnknown(size);
        return size;
    }

    /// <summary>像素数。</summary>
    /// <param name="pixels">设备无关像素。</param>
    public static implicit operator TextSize(float pixels) => new(pixels.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// 文字色调（vue-tsx-pro 的 Tone）：图标、文字按状态着色；inherit 跟随外面的文字颜色。
/// </summary>
/// <param name="Value">取值（neutral、muted、accent……）。</param>
[TypeConverter(typeof(VariantConverter<Tone>))]
public readonly record struct Tone(string Value) : IVariant<Tone>
{
    /// <summary>正文色。</summary>
    public static readonly Tone Neutral = new("neutral");

    /// <summary>次要说明。</summary>
    public static readonly Tone Muted = new("muted");

    /// <summary>品牌色的文字。</summary>
    public static readonly Tone Accent = new("accent");

    /// <summary>危险、错误。</summary>
    public static readonly Tone Danger = new("danger");

    /// <summary>成功、正常。</summary>
    public static readonly Tone Success = new("success");

    /// <summary>警告、需要注意。</summary>
    public static readonly Tone Warning = new("warning");

    /// <summary>信息、提示。</summary>
    public static readonly Tone Info = new("info");

    /// <summary>跟随外面的文字颜色。</summary>
    public static readonly Tone Inherit = new("inherit");

    /// <inheritdoc />
    public static IReadOnlyList<Tone> Known { get; } = [Neutral, Muted, Accent, Danger, Success, Warning, Info, Inherit];

    /// <summary>
    /// 这个色调的颜色（同 vue-tsx-pro 的 toneStyles）：取当前主题；不写或 inherit 时是宿主的文字颜色，
    /// 这时不读主题，换主题也不重画。
    /// </summary>
    /// <param name="context">元素所在的环境。</param>
    public Color ColorIn(ElementContext context) => Value switch
    {
        "neutral" => context.Theme.Colors.Text,
        "muted" => context.Theme.Colors.TextMuted,
        "accent" => context.Theme.Colors.AccentText,
        "danger" => context.Theme.Colors.DangerText,
        "success" => context.Theme.Colors.SuccessText,
        "warning" => context.Theme.Colors.WarningText,
        "info" => context.Theme.Colors.InfoText,
        _ => context.ForeColor,
    };

    /// <inheritdoc />
    public static Tone Parse(string value)
    {
        var tone = new Tone(value);
        Variants.WarnIfUnknown(tone);
        return tone;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
