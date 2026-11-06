using System.ComponentModel;

namespace Rune.Avalonia;

/// <summary>
/// 间距档位（vue-tsx-pro 的 Space，对应主题的 space）：列表、Flex、Grid 的 gap。值是 em 系数（<c>Rune.Space.Md</c>），按所在元素的字号算。
/// </summary>
/// <param name="Value">取值（none、xs、sm、md、lg、xl）。</param>
[TypeConverter(typeof(VariantConverter<Space>))]
public readonly record struct Space(string Value) : IVariant<Space>
{
    /// <summary>0。</summary>
    public static readonly Space None = new("none");

    /// <summary>0.25em：图标与文字之间、列表项之间。</summary>
    public static readonly Space Xs = new("xs");

    /// <summary>0.5em：控件内部元素之间。</summary>
    public static readonly Space Sm = new("sm");

    /// <summary>0.75em：输入框水平内边距、浮层内边距。</summary>
    public static readonly Space Md = new("md");

    /// <summary>1em：按钮水平内边距、表单项之间。</summary>
    public static readonly Space Lg = new("lg");

    /// <summary>1.5em：卡片、对话框内边距。</summary>
    public static readonly Space Xl = new("xl");

    /// <inheritdoc />
    public static IReadOnlyList<Space> Known { get; } = [None, Xs, Sm, Md, Lg, Xl];

    /// <summary>em 系数的资源键（<c>Rune.Space.Md</c>）；default 时为 null。</summary>
    public string? ResourceKey => Value is null ? null : $"Rune.Space.{char.ToUpperInvariant(Value[0])}{Value[1..]}";

    /// <inheritdoc />
    public static Space Parse(string value)
    {
        var space = new Space(value);
        Variants.WarnIfUnknown(space);
        return space;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
