using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>提示条的样式（vue-tsx-pro 的 AlertVariants；实底不用于大块文字，不提供）。</summary>
/// <param name="Value">取值（soft、surface、outline）。</param>
[TypeConverter(typeof(VariantConverter<AlertVariant>))]
public readonly record struct AlertVariant(string Value) : IVariant<AlertVariant>
{
    /// <summary>浅色底（默认）。</summary>
    public static readonly AlertVariant Soft = new("soft");

    /// <summary>浅色底加描边：放在白色卡片里也看得清边界。</summary>
    public static readonly AlertVariant Surface = new("surface");

    /// <summary>只有描边：信息不那么紧急时用。</summary>
    public static readonly AlertVariant Outline = new("outline");

    /// <inheritdoc />
    public static IReadOnlyList<AlertVariant> Known { get; } = [Soft, Surface, Outline];

    /// <inheritdoc />
    public static AlertVariant Parse(string value)
    {
        var variant = new AlertVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>读屏怎样播报出现的提示（vue-tsx-pro 的 AlertLives）：Alert、Banner 共用。</summary>
/// <param name="Value">取值（off、polite、assertive）。</param>
[TypeConverter(typeof(VariantConverter<AlertLive>))]
public readonly record struct AlertLive(string Value) : IVariant<AlertLive>
{
    /// <summary>不主动播报（默认）：页面加载时就在的提示。</summary>
    public static readonly AlertLive Off = new("off");

    /// <summary>礼貌播报（TS 的 role="status"）：等读屏念完当前内容再念，如"已保存"。</summary>
    public static readonly AlertLive Polite = new("polite");

    /// <summary>立即播报（TS 的 role="alert"）：打断当前朗读，只用于错误等紧急信息。</summary>
    public static readonly AlertLive Assertive = new("assertive");

    /// <inheritdoc />
    public static IReadOnlyList<AlertLive> Known { get; } = [Off, Polite, Assertive];

    /// <inheritdoc />
    public static AlertLive Parse(string value)
    {
        var live = new AlertLive(value);
        Variants.WarnIfUnknown(live);
        return live;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>横幅的样式（vue-tsx-pro 的 BannerVariants）。</summary>
/// <param name="Value">取值（soft、solid）。</param>
[TypeConverter(typeof(VariantConverter<BannerVariant>))]
public readonly record struct BannerVariant(string Value) : IVariant<BannerVariant>
{
    /// <summary>浅色底（默认）。</summary>
    public static readonly BannerVariant Soft = new("soft");

    /// <summary>实底：最醒目，必须先看到的消息。</summary>
    public static readonly BannerVariant Solid = new("solid");

    /// <inheritdoc />
    public static IReadOnlyList<BannerVariant> Known { get; } = [Soft, Solid];

    /// <inheritdoc />
    public static BannerVariant Parse(string value)
    {
        var variant = new BannerVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
