using System.ComponentModel;
using System.Globalization;

namespace Rune.Wpf;

/// <summary>
/// 字号档位（vue-tsx-pro 的 TextSize）：xs–xl 对应主题的 fontSizes（<c>Rune.FontSize.Md</c>），也可以直接写像素数（<c>Size="18"</c>）。
/// </summary>
/// <param name="Value">档位名（xs…xxxl）或像素数的文字。</param>
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

    /// <summary>24px：二级标题（H2 的默认）。</summary>
    public static readonly TextSize Xxl = new("xxl");

    /// <summary>30px：页面标题（H1 的默认）。</summary>
    public static readonly TextSize Xxxl = new("xxxl");

    /// <inheritdoc />
    public static IReadOnlyList<TextSize> Known { get; } = [Xs, Sm, Md, Lg, Xl, Xxl, Xxxl];

    /// <summary>直接给像素数时的值；是档位名时为 null。</summary>
    public double? Pixels => double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixels) ? pixels : null;

    /// <summary>档位名对应的字号资源键（<c>Rune.FontSize.Md</c>）；是像素数时为 null。</summary>
    public string? ResourceKey => Value is null || Pixels is not null ? null : $"Rune.FontSize.{char.ToUpperInvariant(Value[0])}{Value[1..]}";

    /// <inheritdoc />
    public static TextSize Parse(string value)
    {
        var size = new TextSize(value);
        if (size.Pixels is null) Variants.WarnIfUnknown(size);
        return size;
    }

    /// <summary>像素数。</summary>
    /// <param name="pixels">设备无关像素。</param>
    public static implicit operator TextSize(double pixels) => new(pixels.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// 文字色调（vue-tsx-pro 的 TextTone）：图标、文字按状态着色；inherit 跟随外面的文字颜色。
/// </summary>
/// <param name="Value">取值（neutral、muted、accent……）。</param>
[TypeConverter(typeof(VariantConverter<TextTone>))]
public readonly record struct TextTone(string Value) : IVariant<TextTone>
{
    /// <summary>正文色。</summary>
    public static readonly TextTone Neutral = new("neutral");

    /// <summary>次要说明。</summary>
    public static readonly TextTone Muted = new("muted");

    /// <summary>品牌色的文字。</summary>
    public static readonly TextTone Accent = new("accent");

    /// <summary>危险、错误。</summary>
    public static readonly TextTone Danger = new("danger");

    /// <summary>成功、正常。</summary>
    public static readonly TextTone Success = new("success");

    /// <summary>警告、需要注意。</summary>
    public static readonly TextTone Warning = new("warning");

    /// <summary>信息、提示。</summary>
    public static readonly TextTone Info = new("info");

    /// <summary>跟随外面的文字颜色。</summary>
    public static readonly TextTone Inherit = new("inherit");

    /// <inheritdoc />
    public static IReadOnlyList<TextTone> Known { get; } = [Neutral, Muted, Accent, Danger, Success, Warning, Info, Inherit];

    /// <summary>文字颜色的画刷资源键（同 vue-tsx-pro 的 toneStyles）；不写、inherit 或使用方扩展的取值为 null，跟随外面的文字颜色。</summary>
    public string? BrushKey => Value switch
    {
        "neutral" => "Rune.Brush.Text",
        "muted" => "Rune.Brush.TextMuted",
        "accent" => "Rune.Brush.AccentText",
        "danger" => "Rune.Brush.DangerText",
        "success" => "Rune.Brush.SuccessText",
        "warning" => "Rune.Brush.WarningText",
        "info" => "Rune.Brush.InfoText",
        _ => null,
    };

    /// <inheritdoc />
    public static TextTone Parse(string value)
    {
        var tone = new TextTone(value);
        Variants.WarnIfUnknown(tone);
        return tone;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// 字重档位（vue-tsx-pro 的 FontWeight，对应主题的 fontWeights）。C# 里叫 TextWeight，免得与平台的 FontWeight 重名。
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

    /// <summary>字重资源键（<c>Rune.FontWeight.Semibold</c>）；default 时为 null。</summary>
    public string? ResourceKey => Value is null ? null : $"Rune.FontWeight.{char.ToUpperInvariant(Value[0])}{Value[1..]}";

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
