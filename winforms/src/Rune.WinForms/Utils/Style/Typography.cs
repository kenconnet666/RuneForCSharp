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

/// <summary>
/// 字重档位（vue-tsx-pro 的 FontWeight，对应主题的 fontWeights）。C# 里叫 TextWeight，免得与平台的字重类型重名。
/// </summary>
/// <param name="Value">取值（light、normal、medium、semibold、bold）。</param>
[TypeConverter(typeof(VariantConverter<TextWeight>))]
public readonly record struct TextWeight(string Value) : IVariant<TextWeight>
{
    /// <summary>300：细。</summary>
    public static readonly TextWeight Light = new("light");

    /// <summary>400：正文。</summary>
    public static readonly TextWeight Normal = new("normal");

    /// <summary>500：标签、表头。</summary>
    public static readonly TextWeight Medium = new("medium");

    /// <summary>600：按钮、标题。</summary>
    public static readonly TextWeight Semibold = new("semibold");

    /// <summary>700：粗。</summary>
    public static readonly TextWeight Bold = new("bold");

    /// <inheritdoc />
    public static IReadOnlyList<TextWeight> Known { get; } = [Light, Normal, Medium, Semibold, Bold];

    /// <summary>主题里的字重数值（<see cref="Tokens.FontWeight"/>）；default 或使用方扩展的取值为 null。</summary>
    public int? Number => Value switch
    {
        "light" => Tokens.FontWeight.Light,
        "normal" => Tokens.FontWeight.Normal,
        "medium" => Tokens.FontWeight.Medium,
        "semibold" => Tokens.FontWeight.Semibold,
        "bold" => Tokens.FontWeight.Bold,
        _ => null,
    };

    /// <inheritdoc />
    public static TextWeight Parse(string value)
    {
        var weight = new TextWeight(value);
        Variants.WarnIfUnknown(weight);
        return weight;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>文字的水平对齐（vue-tsx-pro 的 TextAlign），按书写方向。</summary>
public enum TextAlign
{
    /// <summary>对齐起始边：从左到右排版时靠左。</summary>
    Start,

    /// <summary>居中。</summary>
    Center,

    /// <summary>对齐结束边：从左到右排版时靠右。</summary>
    End,
}

/// <summary>换行方式（vue-tsx-pro 的 TextWrap）。balance、pretty 是浏览器的排版优化，桌面上按普通换行处理。</summary>
public enum TextWrap
{
    /// <summary>正常换行。</summary>
    Wrap,

    /// <summary>不换行，超出时溢出（配合 Truncate 省略）。</summary>
    NoWrap,

    /// <summary>各行长度尽量均衡（标题、短文本）；桌面上同 Wrap。</summary>
    Balance,

    /// <summary>避免最后一行只剩一个字（段落）；桌面上同 Wrap。</summary>
    Pretty,
}
