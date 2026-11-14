using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>步骤条的方向（vue-tsx-pro 的 StepsOrientations）。</summary>
/// <param name="Value">取值（horizontal、vertical）。</param>
[TypeConverter(typeof(VariantConverter<StepsOrientation>))]
public readonly record struct StepsOrientation(string Value) : IVariant<StepsOrientation>
{
    /// <summary>横排（默认）：各步等宽，连接线在标题右侧延伸。</summary>
    public static readonly StepsOrientation Horizontal = new("horizontal");

    /// <summary>竖排：步骤多、说明长时；步骤之间有竖的连接线。</summary>
    public static readonly StepsOrientation Vertical = new("vertical");

    /// <inheritdoc />
    public static IReadOnlyList<StepsOrientation> Known { get; } = [Horizontal, Vertical];

    /// <inheritdoc />
    public static StepsOrientation Parse(string value)
    {
        var orientation = new StepsOrientation(value);
        Variants.WarnIfUnknown(orientation);
        return orientation;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>步骤的状态（vue-tsx-pro 的 StepStates）。</summary>
/// <param name="Value">取值（complete、current、upcoming、error）。</param>
[TypeConverter(typeof(VariantConverter<StepState>))]
public readonly record struct StepState(string Value) : IVariant<StepState>
{
    /// <summary>已完成：对勾、品牌色。</summary>
    public static readonly StepState Complete = new("complete");

    /// <summary>当前步骤。</summary>
    public static readonly StepState Current = new("current");

    /// <summary>未开始。</summary>
    public static readonly StepState Upcoming = new("upcoming");

    /// <summary>出错：警示图标、危险色。</summary>
    public static readonly StepState Error = new("error");

    /// <inheritdoc />
    public static IReadOnlyList<StepState> Known { get; } = [Complete, Current, Upcoming, Error];

    /// <inheritdoc />
    public static StepState Parse(string value)
    {
        var state = new StepState(value);
        Variants.WarnIfUnknown(state);
        return state;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>一个步骤（vue-tsx-pro 的 StepItem）。</summary>
/// <param name="Title">标题。</param>
public sealed record StepItem(string Title)
{
    /// <summary>说明（标题下面的小字）。</summary>
    public string? Description { get; init; }

    /// <summary>代替序号的图标；已完成时仍显示对勾，出错时仍显示警示图标。</summary>
    public IconData? Icon { get; init; }

    /// <summary>状态；不写时按当前步骤推出（之前已完成、之后未开始）。</summary>
    public StepState? Status { get; init; }

    /// <summary>地址：已完成的这一步是链接，点了打开它。</summary>
    public string? Href { get; init; }

    /// <summary>不可点回（即使已完成、有地址或步骤条可以点回）。</summary>
    public bool IsDisabled { get; init; }
}

/// <summary>步骤的状态与能不能点回（vue-tsx-pro 的 Steps 里推状态的那几行）。</summary>
static class StepRules
{
    /// <summary>这一步的状态：写了 Status 用它，否则之前的已完成、当前的是当前、之后的未开始。</summary>
    public static StepState StateAt(StepItem item, int index, int current) =>
        item.Status ?? (index < current ? StepState.Complete : index == current ? StepState.Current : StepState.Upcoming);

    /// <summary>能点回：已完成或出错（出错的步骤也要能点回去改），而且没有标不可用。</summary>
    public static bool Reachable(StepItem item, StepState state) => (state == StepState.Complete || state == StepState.Error) && !item.IsDisabled;
}
