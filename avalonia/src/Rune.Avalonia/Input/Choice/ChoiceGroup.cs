using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Layout;
using Avalonia.LogicalTree;

namespace Rune.Avalonia;

/// <summary>
/// 复选组、单选组共用的部分（vue-tsx-pro 的 choiceRecipe 的 group）：竖排（gap xs）或横排换行（列距 lg、行距 xs），
/// 整组只读、出错，项可以直接写在里面，也可以来自 ItemsSource（ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>）。
/// 整组不可用写 IsEnabled。排列由主题里的 ItemsPanel 给（ChoiceGroup.axaml）。
/// </summary>
[PseudoClasses(":horizontal", ":readonly", ":invalid")]
public abstract class ChoiceGroup : ItemsControl
{
    /// <summary>排列方向；默认竖排。</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<ChoiceGroup, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>整组只读：能聚焦、读屏念"只读"，选择不能改（查看详情、审批时）；外观照常。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<ChoiceGroup, bool>(nameof(IsReadOnly));

    /// <summary>整组校验未通过（如"至少选一项"）。</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AvaloniaProperty.Register<ChoiceGroup, bool>(nameof(IsInvalid));

    /// <summary>ItemsSource 的数据 → 选项（值、文字、说明、不可用）；数据是 ListboxOption、字符串时不用写。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<ChoiceGroup, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>排列方向。</summary>
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>整组只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>整组校验未通过。</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>新建：竖排。</summary>
    protected ChoiceGroup() => PseudoClasses.Set(":horizontal", false);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty) PseudoClasses.Set(":horizontal", Orientation == Orientation.Horizontal);
        else if (change.Property == IsReadOnlyProperty || change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(":readonly", IsReadOnly);
            PseudoClasses.Set(":invalid", IsInvalid);
            Sync();
        }
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

    /// <summary>读屏：一组（role="group"），名称写 AutomationProperties.Name。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new ChoiceGroupAutomationPeer(this);

    sealed class ChoiceGroupAutomationPeer(ChoiceGroup owner) : ItemsControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
