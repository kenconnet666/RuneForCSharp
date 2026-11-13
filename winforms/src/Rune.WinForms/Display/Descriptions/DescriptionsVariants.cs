using System.ComponentModel;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>描述列表里标签与值的排列（vue-tsx-pro 的 DescriptionsLayouts）。</summary>
/// <param name="Value">取值（horizontal、vertical、inline）。</param>
[TypeConverter(typeof(VariantConverter<DescriptionsLayout>))]
public readonly record struct DescriptionsLayout(string Value) : IVariant<DescriptionsLayout>
{
    /// <summary>横排（默认）：标签在左、值在右。</summary>
    public static readonly DescriptionsLayout Horizontal = new("horizontal");

    /// <summary>竖排：标签在上、值在下。</summary>
    public static readonly DescriptionsLayout Vertical = new("vertical");

    /// <summary>行内：各项一行排开、放不下换行（页头里的编号、团队、主办这类关键信息）；栏数不起作用。</summary>
    public static readonly DescriptionsLayout Inline = new("inline");

    /// <inheritdoc />
    public static IReadOnlyList<DescriptionsLayout> Known { get; } = [Horizontal, Vertical, Inline];

    /// <inheritdoc />
    public static DescriptionsLayout Parse(string value)
    {
        var layout = new DescriptionsLayout(value);
        Variants.WarnIfUnknown(layout);
        return layout;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// 描述列表分几栏（vue-tsx-pro 的 columns：number | 'auto'）。XAML 里写数字或 auto；C# 里 int 能直接赋值。
/// auto 时按宽度定：每栏不窄于 MinItemWidth，最多 MaxColumns 栏。
/// </summary>
/// <param name="Value">取值（数字或 auto）。</param>
[TypeConverter(typeof(VariantConverter<DescriptionsColumns>))]
public readonly record struct DescriptionsColumns(string Value) : IVariant<DescriptionsColumns>
{
    /// <summary>按宽度定栏数。</summary>
    public static readonly DescriptionsColumns Auto = new("auto");

    /// <inheritdoc />
    public static IReadOnlyList<DescriptionsColumns> Known { get; } = [Auto];

    /// <summary>按宽度定吗。</summary>
    public bool IsAuto => Value == "auto";

    /// <summary>
    /// 实际的栏数。auto：宽度 ÷ 每栏最小宽度（向下取整），在 1 到 <paramref name="maxColumns"/> 之间；
    /// 固定栏数：容器窄于 32em 时排成一列（TS 的容器查询 @container (max-width: 32em)）。没写时 1。
    /// </summary>
    /// <param name="width">列表的宽度。</param>
    /// <param name="em">字号（32em 按它算）。</param>
    /// <param name="minItemWidth">auto 时每栏的最小宽度。</param>
    /// <param name="maxColumns">auto 时最多几栏。</param>
    internal int Resolve(double width, double em, double minItemWidth, int maxColumns)
    {
        if (IsAuto) return double.IsInfinity(width) ? Math.Max(1, maxColumns) : Math.Clamp((int)Math.Floor(width / Math.Max(1, minItemWidth)), 1, Math.Max(1, maxColumns));
        var count = Value is null ? 1 : int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? Math.Max(1, n) : 1;
        return width <= 32 * em ? 1 : count;
    }

    /// <summary>栏数。</summary>
    /// <param name="count">分几栏。</param>
    public static implicit operator DescriptionsColumns(int count) => new(count.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public static DescriptionsColumns Parse(string value)
    {
        var columns = new DescriptionsColumns(value);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) Variants.WarnIfUnknown(columns);
        return columns;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
