using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 复选组、单选组共用的部分（vue-tsx-pro 的 choiceRecipe 的 group）：竖排（gap xs）或横排换行（列距 lg、行距 xs），
/// 整组只读、出错，项可以直接写在里面，也可以来自 ItemsSource（ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>）。
/// 整组不可用写 IsEnabled。排列由主题里的 ItemsPanel 给（ChoiceGroup.axaml）。卡片外观（<see cref="Card"/>）时每项是一张卡片、组排成栅格：
/// 竖排一列（gap sm），横排按 <see cref="MinItemWidth"/>（默认 14em）auto-fill 分列（列距 lg、行距 xs）。
/// </summary>
[PseudoClasses(":horizontal", ":readonly", ":invalid", ":card")]
public abstract class ChoiceGroup : ItemsControl, IFieldControl
{
    /// <summary>排列方向；默认竖排。</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<ChoiceGroup, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>整组只读：能聚焦、读屏念"只读"，选择不能改（查看详情、审批时）；外观照常。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<ChoiceGroup, bool>(nameof(IsReadOnly));

    /// <summary>整组校验未通过（如"至少选一项"）。</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AvaloniaProperty.Register<ChoiceGroup, bool>(nameof(IsInvalid));

    /// <summary>ItemsSource 的数据 → 选项（值、文字、说明、不可用）；数据是 ListboxOption、字符串时不用写。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<ChoiceGroup, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>卡片外观：每项是一张带边框的卡片，勾选时边框与底色换成色调色；组里的项都跟着（项自己写了 Card 的除外）。</summary>
    public static readonly StyledProperty<bool> CardProperty = AvaloniaProperty.Register<ChoiceGroup, bool>(nameof(Card));

    /// <summary>卡片外观横排时每张的最小宽度，放得下几张就几列（auto-fill）；默认 14em。</summary>
    public static readonly StyledProperty<Length> MinItemWidthProperty = AvaloniaProperty.Register<ChoiceGroup, Length>(nameof(MinItemWidth), Length.Parse("14em"));

    /// <summary>卡片外观。</summary>
    public bool Card { get => GetValue(CardProperty); set => SetValue(CardProperty, value); }

    /// <summary>卡片外观横排时每张的最小宽度。</summary>
    public Length MinItemWidth { get => GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }

    /// <summary>排列方向。</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>整组只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>整组校验未通过。</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>整组只读：自己写了的优先，其次所在字段的。</summary>
    internal bool ReadOnly => IsSet(IsReadOnlyProperty) ? IsReadOnly : Field.Of(this)?.IsReadOnly ?? false;

    /// <summary>整组出错：自己写了的优先，其次所在字段的。</summary>
    internal bool Invalid => IsSet(IsInvalidProperty) ? IsInvalid : Field.Of(this)?.Invalid ?? false;

    /// <summary>新建：竖排。</summary>
    protected ChoiceGroup() => PseudoClasses.Set(":horizontal", false);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty || change.Property == CardProperty || change.Property == MinItemWidthProperty)
        {
            PseudoClasses.Set(":horizontal", Orientation == Orientation.Horizontal);
            PseudoClasses.Set(":card", Card);
            UpdatePanel();
            if (change.Property == CardProperty) Sync();
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == IsInvalidProperty || change.Property == Field.OwnerProperty) Refresh();
    }

    void IFieldControl.FieldChanged() => Refresh();

    /// <summary>卡片外观时换成栅格（gap sm；横排按最小宽度 auto-fill，竖排一列）；不是卡片时用主题里的排列。</summary>
    void UpdatePanel()
    {
        if (!Card)
        {
            ClearValue(ItemsPanelProperty);
            return;
        }
        var (horizontal, minWidth) = (Orientation == Orientation.Horizontal, MinItemWidth);
        // 横排时列距 lg、行距 xs（上游排列方向的 columnGap、rowGap 是长写，盖过卡片的 gap: sm），竖排 gap sm
        ItemsPanel = new FuncTemplate<Panel?>(() => horizontal
            ? new Grid { ColumnGap = Space.Lg, RowGap = Space.Xs, MinItemWidth = minWidth, AutoFill = true }
            : new Grid { Gap = Space.Sm, Columns = 1 });
    }

    void Refresh()
    {
        PseudoClasses.Set(":readonly", ReadOnly);
        PseudoClasses.Set(":invalid", Invalid);
        Sync();
    }

    /// <summary>直接写在里面的勾选项就是它自己的容器；数据生成一个。</summary>
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return item is not ChoiceBase;
    }

    /// <summary>数据生成的项：值、文字、说明、不可用取自选项。</summary>
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (item is ChoiceBase || container is not ChoiceBase choice) return;
        var option = ListboxOption.From(item, ToOption);
        choice.Content = option.Label;
        choice.IsEnabled = !option.IsDisabled;
        Prepare(choice, option);
    }

    /// <summary>派生类写入这一项的值与说明。</summary>
    /// <param name="item">生成的项。</param>
    /// <param name="option">选项。</param>
    protected abstract void Prepare(ChoiceBase item, ListboxOption option);

    /// <summary>组的值、只读、出错变了：让里面的项跟上。</summary>
    internal abstract void Sync();

    /// <summary>属于这一组的项（嵌套的组里的不算）。</summary>
    /// <typeparam name="T">项的类型。</typeparam>
    protected IEnumerable<T> Choices<T>() where T : ChoiceBase =>
        this.GetLogicalDescendants().OfType<T>().Where(item => item.FindLogicalAncestorOfType<ChoiceGroup>() == this);

    /// <summary>读屏：一组（role="group"），名称写 AutomationProperties.Name，没写时是所在字段的标签；帮助文字是字段的说明与错误。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new ChoiceGroupAutomationPeer(this);

    sealed class ChoiceGroupAutomationPeer(ChoiceGroup owner) : ItemsControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;

        protected override string? GetHelpTextCore() => base.GetHelpTextCore() is { Length: > 0 } help ? help : Field.Of(owner)?.HelpText;
    }
}
