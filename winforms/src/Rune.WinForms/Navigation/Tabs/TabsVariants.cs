using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>标签的排列方向（vue-tsx-pro 的 TabsOrientations）。</summary>
/// <param name="Value">取值（horizontal、vertical）。</param>
[TypeConverter(typeof(VariantConverter<TabsOrientation>))]
public readonly record struct TabsOrientation(string Value) : IVariant<TabsOrientation>
{
    /// <summary>横排（默认），左右键切换。</summary>
    public static readonly TabsOrientation Horizontal = new("horizontal");

    /// <summary>竖排（标签在左侧），上下键切换。</summary>
    public static readonly TabsOrientation Vertical = new("vertical");

    /// <inheritdoc />
    public static IReadOnlyList<TabsOrientation> Known { get; } = [Horizontal, Vertical];

    /// <inheritdoc />
    public static TabsOrientation Parse(string value)
    {
        var orientation = new TabsOrientation(value);
        Variants.WarnIfUnknown(orientation);
        return orientation;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>焦点移动时是否同时切换（vue-tsx-pro 的 TabsActivations）。</summary>
/// <param name="Value">取值（automatic、manual）。</param>
[TypeConverter(typeof(VariantConverter<TabsActivation>))]
public readonly record struct TabsActivation(string Value) : IVariant<TabsActivation>
{
    /// <summary>焦点移到哪个标签就显示哪个面板（默认）：面板内容轻量时用。</summary>
    public static readonly TabsActivation Automatic = new("automatic");

    /// <summary>方向键只移动焦点，按 Enter / 空格才切换：切换面板代价大时用。</summary>
    public static readonly TabsActivation Manual = new("manual");

    /// <inheritdoc />
    public static IReadOnlyList<TabsActivation> Known { get; } = [Automatic, Manual];

    /// <inheritdoc />
    public static TabsActivation Parse(string value)
    {
        var activation = new TabsActivation(value);
        Variants.WarnIfUnknown(activation);
        return activation;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>标签的样式（vue-tsx-pro 的 TabsVariants）。</summary>
/// <param name="Value">取值（line、soft）。</param>
[TypeConverter(typeof(VariantConverter<TabsVariant>))]
public readonly record struct TabsVariant(string Value) : IVariant<TabsVariant>
{
    /// <summary>下划线（默认）：当前标签下方一条品牌色的线。</summary>
    public static readonly TabsVariant Line = new("line");

    /// <summary>浅色底：当前标签有浅色底，像分段控件。</summary>
    public static readonly TabsVariant Soft = new("soft");

    /// <inheritdoc />
    public static IReadOnlyList<TabsVariant> Known { get; } = [Line, Soft];

    /// <inheritdoc />
    public static TabsVariant Parse(string value)
    {
        var variant = new TabsVariant(value);
        Variants.WarnIfUnknown(variant);
        return variant;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
