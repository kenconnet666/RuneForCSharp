using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>分栏两栏的排法（vue-tsx-pro 的 SplitterOrientations）。</summary>
/// <param name="Value">取值（horizontal、vertical）。</param>
[TypeConverter(typeof(VariantConverter<SplitterOrientation>))]
public readonly record struct SplitterOrientation(string Value) : IVariant<SplitterOrientation>
{
    /// <summary>左右并排（默认），分隔条是一条竖线。</summary>
    public static readonly SplitterOrientation Horizontal = new("horizontal");

    /// <summary>上下排列，分隔条是一条横线。</summary>
    public static readonly SplitterOrientation Vertical = new("vertical");

    /// <inheritdoc />
    public static IReadOnlyList<SplitterOrientation> Known { get; } = [Horizontal, Vertical];

    /// <inheritdoc />
    public static SplitterOrientation Parse(string value)
    {
        var orientation = new SplitterOrientation(value);
        Variants.WarnIfUnknown(orientation);
        return orientation;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>起始一侧（左或上，从右到左排版时是右）或结束一侧：分栏能收起的那一栏、宽度调整手柄贴着的那条边。</summary>
public enum PaneSide
{
    /// <summary>起始一侧。</summary>
    Start,

    /// <summary>结束一侧。</summary>
    End,
}

/// <summary>分栏的分隔条上调比例的键（平台的键先按方向、从右到左排版换成这里的动作）。</summary>
public enum SplitterKey
{
    /// <summary>起始一栏变小一步（← 或 ↑）。</summary>
    Shrink,

    /// <summary>起始一栏变大一步（→ 或 ↓）。</summary>
    Grow,

    /// <summary>到最小比例。</summary>
    Home,

    /// <summary>到最大比例。</summary>
    End,
}

/// <summary>分栏与宽度调整手柄的计算（三个库共用一份）。</summary>
public static class SplitterScale
{
    /// <summary>分栏的键盘每按一次调多少（百分比）。</summary>
    public const double Step = 5;

    /// <summary>比例夹在最小、最大之间，保留一位小数（拖动时不出现一长串小数）。</summary>
    public static double Fit(double size, double min, double max) =>
        Math.Round(Math.Min(Math.Max(size, min), max) * 10, MidpointRounding.AwayFromZero) / 10;

    /// <summary>拖动：起点的比例加上移动量占整个分栏长度的比例。</summary>
    public static double Drag(double from, double delta, double total, double min, double max) =>
        Fit(total > 0 ? from + delta / total * 100 : from, min, max);

    /// <summary>键盘：起始一栏变大、变小一步，或到最小、最大。</summary>
    public static double Target(SplitterKey key, double size, double min, double max) => Fit(key switch
    {
        SplitterKey.Grow => size + Step,
        SplitterKey.Shrink => size - Step,
        SplitterKey.Home => min,
        _ => max,
    }, min, max);

    /// <summary>读屏念的当前值：收起起始一栏时是 0、收起结束一栏时是 100。</summary>
    public static double ValueNow(double size, PaneSide? hidden) => hidden switch
    {
        PaneSide.Start => 0,
        PaneSide.End => 100,
        _ => size,
    };

    /// <summary>
    /// 两栏的长度：去掉分隔条之后按比例分（TS 的 <c>minmax(0, size fr) auto minmax(0, 100 - size fr)</c>）；
    /// 收起的那一栏不占位置，另一栏占满。
    /// </summary>
    public static (double Start, double End) Panes(double available, double size, PaneSide? hidden)
    {
        available = Math.Max(0, available);
        return hidden switch
        {
            PaneSide.Start => (0, available),
            PaneSide.End => (available, 0),
            _ => (available * size / 100, available * (100 - size) / 100),
        };
    }

    /// <summary>收起、展开按钮的箭头指向起始一侧吗：起始一侧能收起且展开着，或结束一侧能收起且收着（箭头指向收起的方向）。</summary>
    public static bool TogglePointsToStart(PaneSide collapsible, bool collapsed) => (collapsible == PaneSide.Start) != collapsed;

    /// <summary>宽度调整手柄的新长度：夹在最小、最大之间，取整像素。</summary>
    public static double FitLength(double length, double min, double max) =>
        Math.Round(Math.Min(Math.Max(length, min), max), MidpointRounding.AwayFromZero);

    /// <summary>
    /// 手柄的移动量换成长度的变化：贴着起始边的手柄（从结束边滑入的抽屉）往起始方向拖是变长，方向与结束边相反；
    /// 竖线在从右到左排版下也反过来（横线不受影响）。
    /// </summary>
    public static double LengthDelta(double delta, PaneSide edge, bool rightToLeft) =>
        delta * (edge == PaneSide.Start ? -1 : 1) * (rightToLeft ? -1 : 1);
}
