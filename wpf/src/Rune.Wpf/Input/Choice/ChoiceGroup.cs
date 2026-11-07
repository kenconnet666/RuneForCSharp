using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 复选组、单选组共用的部分（vue-tsx-pro 的 choiceRecipe 的 group）：竖排（gap xs）或横排换行（列距 lg、行距 xs），
/// 整组只读、出错，项可以直接写在里面，也可以来自 ItemsSource（ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>）。
/// 整组不可用写 IsEnabled。排列由主题样式里的 ItemsPanel 给（Choice.xaml）。
/// 项在加载时按可视树（其次逻辑树）往上找到最近的组并登记，所以项可以放在组里的任意布局容器中。
/// </summary>
public abstract class ChoiceGroup : ItemsControl, IFieldControl
{
    /// <summary>排列方向；默认竖排。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(ChoiceGroup), new PropertyMetadata(Orientation.Vertical));

    /// <summary>整组只读：能聚焦、读屏念"只读"，选择不能改（查看详情、审批时）；外观照常。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(ChoiceGroup), new PropertyMetadata(false, (d, _) => ((ChoiceGroup)d).Sync()));

    /// <summary>整组校验未通过（如"至少选一项"）。</summary>
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(ChoiceGroup), new PropertyMetadata(false, (d, _) => ((ChoiceGroup)d).Sync()));

    /// <summary>ItemsSource 的数据 → 选项（值、文字、说明、不可用）；数据是 ListboxOption、字符串时不用写。</summary>
    public static readonly DependencyProperty ToOptionProperty = DependencyProperty.Register(
        nameof(ToOption), typeof(Func<object?, ListboxOption>), typeof(ChoiceGroup), new PropertyMetadata(null));

    readonly HashSet<ChoiceBase> members = [];

    static ChoiceGroup() => FocusableProperty.OverrideMetadata(typeof(ChoiceGroup), new FrameworkPropertyMetadata(false));

    /// <summary>整组只读：自己写了的优先，其次所在字段的。</summary>
    internal bool ReadOnly => Field.IsSet(this, IsReadOnlyProperty) ? IsReadOnly : Field.Of(this)?.IsReadOnly ?? false;

    /// <summary>整组出错：自己写了的优先，其次所在字段的。</summary>
    internal bool Invalid => Field.IsSet(this, IsInvalidProperty) ? IsInvalid : Field.Of(this)?.Invalid ?? false;

    void IFieldControl.FieldChanged() => Sync();

    /// <summary>排列方向。</summary>
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>整组只读。</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>整组校验未通过。</summary>
    public bool IsInvalid { get => (bool)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => (Func<object?, ListboxOption>?)GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>直接写在里面的勾选项就是它自己的容器。</summary>
    protected override bool IsItemItsOwnContainerOverride(object item) => item is ChoiceBase;

    /// <summary>数据生成的项：值、文字、说明、不可用取自选项。</summary>
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (item is ChoiceBase || element is not ChoiceBase choice) return;
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

    /// <summary>项加载时登记、卸载时注销。</summary>
    internal void Join(ChoiceBase item)
    {
        members.Add(item);
        Sync();
    }

    /// <inheritdoc cref="Join" />
    internal void Leave(ChoiceBase item)
    {
        members.Remove(item);
        Sync();
    }

    /// <summary>属于这一组的项，按可视树的先后。</summary>
    /// <typeparam name="T">项的类型。</typeparam>
    protected List<T> Choices<T>() where T : ChoiceBase
    {
        var found = new List<T>();
        Walk(this);
        // 还没进可视树的（刚登记、模板还没生成）排在最后
        found.AddRange(members.OfType<T>().Where(item => !found.Contains(item)));
        return found;

        void Walk(DependencyObject node)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is T item && members.Contains(item)) found.Add(item);
                else Walk(child);
            }
        }
    }

    /// <summary>元素所在的最近一个组：先沿可视树，再沿逻辑树往上。</summary>
    /// <typeparam name="T">组的类型。</typeparam>
    internal static T? Of<T>(DependencyObject element) where T : ChoiceGroup
    {
        for (var node = Parent(element); node is not null; node = Parent(node))
            if (node is ChoiceGroup group) return group as T;
        return null;

        static DependencyObject? Parent(DependencyObject node) =>
            (node is Visual ? VisualTreeHelper.GetParent(node) : null) ?? LogicalTreeHelper.GetParent(node);
    }

    /// <summary>读屏：一组（role="group"），名称写 AutomationProperties.Name，没写时是所在字段的标签；帮助文字是字段的说明与错误。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new ChoiceGroupAutomationPeer(this);

    sealed class ChoiceGroupAutomationPeer(ChoiceGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : (Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null) ?? "";

        protected override string GetHelpTextCore() => base.GetHelpTextCore() is { Length: > 0 } help ? help : Field.Of(owner)?.HelpText ?? "";

        protected override string GetClassNameCore() => owner.GetType().Name;
    }
}
