using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>
/// 按钮类菜单项共有的（vue-tsx-pro 的 useButtonItem、menuItemContent）：起始边的勾选标记（复选、单选项）、图标、文字（Content）、
/// 文字下方的说明（<see cref="Description"/>，弱化；读屏当作描述，不算进名称）、结束边的快捷键提示。
/// 列表行的外观：控件高度、上下 space.xs、左右 space.sm、sm 圆角，悬停 fillHover；不可用时变淡、方向键跳过。
/// 执行（点击、Enter、空格、快捷键）时先触发 Click、Command，点击后按 <see cref="CloseOnSelect"/> 关闭菜单。部件名在 <see cref="MenuItemParts"/>。
/// </summary>
[PseudoClasses(":has-icon", ":has-indicator", ":checked", ":described", ":has-shortcut", ":danger", ":submenu")]
public abstract class MenuItemBase : ButtonBase
{
    /// <summary>文字前的图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<MenuItemBase, IconData?>(nameof(Icon));

    /// <summary>快捷键（'Ctrl+D'、'Mod+Shift+P'）：结束边显示，按下时同点了这一项（菜单开没开都一样）；读屏从 AcceleratorKey 念。</summary>
    public static readonly StyledProperty<string?> ShortcutProperty = AvaloniaProperty.Register<MenuItemBase, string?>(nameof(Shortcut));

    /// <summary>文字下方的说明（第二行，textMuted、sm）：读屏当作这一项的描述（帮助文字），不算进名称。</summary>
    public static readonly StyledProperty<object?> DescriptionProperty = AvaloniaProperty.Register<MenuItemBase, object?>(nameof(Description));

    /// <summary>执行后是否关闭菜单：普通项、单选项默认关，复选项默认不关。</summary>
    public static readonly StyledProperty<bool> CloseOnSelectProperty = AvaloniaProperty.Register<MenuItemBase, bool>(nameof(CloseOnSelect), true);

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>快捷键。</summary>
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>执行后是否关闭菜单。</summary>
    public bool CloseOnSelect { get => GetValue(CloseOnSelectProperty); set => SetValue(CloseOnSelectProperty, value); }

    /// <summary>勾选标记的图标（复选是对勾，单选是圆点）；普通项没有。</summary>
    internal virtual IconData? Indicator => null;

    /// <summary>勾没勾上（复选、单选项）；普通项为 null。</summary>
    internal virtual bool? Checked => null;

    /// <summary>解析好的快捷键；没写时为 null。</summary>
    internal Hotkey? Hotkey { get; private set; }

    Icon? indicatorPart;
    Text? shortcutPart;

    /// <summary>所在的一列菜单（菜单或子菜单）。</summary>
    internal virtual IMenuHost? Host => this.GetLogicalAncestors().OfType<IMenuHost>().FirstOrDefault();

    /// <summary>文字（类型搜索、读屏名称）：Content 是字符串时就是它，否则是写的读屏名称。</summary>
    internal string LabelText => Content as string ?? AutomationProperties.GetName(this) ?? "";

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(MenuItemBase);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty) PseudoClasses.Set(":has-icon", Icon is not null);
        else if (change.Property == DescriptionProperty) PseudoClasses.Set(":described", Description is not null);
        else if (change.Property == ShortcutProperty)
        {
            Hotkey = Shortcut is { Length: > 0 } combo ? global::Rune.Avalonia.Hotkey.Parse(combo) : null;
            PseudoClasses.Set(":has-shortcut", Hotkey is not null);
            if (Hotkey is { } hotkey) AutomationProperties.SetAcceleratorKey(this, hotkey.AccessKey);
            else ClearValue(AutomationProperties.AcceleratorKeyProperty);
            UpdateParts();
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        indicatorPart = e.NameScope.Find<Icon>(MenuItemParts.Indicator);
        shortcutPart = e.NameScope.Find<Text>(MenuItemParts.Shortcut);
        UpdateParts();
    }

    /// <summary>标记的图标、快捷键显示的文字（Ctrl+D）：不是样式属性，模板绑不了，在这里设。</summary>
    void UpdateParts()
    {
        if (indicatorPart is not null) indicatorPart.Data = Indicator;
        if (shortcutPart is not null) shortcutPart.Text = Hotkey?.Text;
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        UpdateMark();
        if (Host is { Ripple: { } ripple }) SetValue(RippleProperty, ripple, BindingPriority.Template);
    }

    /// <summary>勾选标记：有标记的项占一列（勾没勾都对齐），没勾时只占位。</summary>
    private protected void UpdateMark()
    {
        PseudoClasses.Set(":has-indicator", Indicator is not null);
        PseudoClasses.Set(":checked", Checked == true);
    }

    /// <summary>执行这一项（点击、Enter、空格、快捷键）：先触发 Click、Command。</summary>
    internal virtual void Run() => base.OnClick();

    /// <summary>点了：执行，按 <see cref="CloseOnSelect"/> 关闭菜单；加载中、不可用时都不执行。</summary>
    protected override void OnClick()
    {
        if (IsLoading || IsDisabled) return;
        Run();
        if (CloseOnSelect) Host?.CloseFromItem();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => Checked is null ? new MenuItemPeer(this) : new CheckableMenuItemPeer(this);

    /// <summary>读屏：菜单项；名称是文字，说明是帮助文字。</summary>
    class MenuItemPeer(MenuItemBase owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

        protected override string GetClassNameCore() => owner.GetType().Name;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.LabelText;

        protected override string? GetHelpTextCore() =>
            AutomationProperties.GetHelpText(owner) is { Length: > 0 } help ? help : owner.Description as string;

    }

    /// <summary>复选、单选项：能切换，报勾选状态。</summary>
    sealed class CheckableMenuItemPeer(MenuItemBase owner) : MenuItemPeer(owner), IToggleProvider
    {
        public ToggleState ToggleState => ((MenuItemBase)Owner).Checked == true ? ToggleState.On : ToggleState.Off;

        public void Toggle() => ((IInvokeProvider)this).Invoke();
    }
}

/// <summary>菜单项（vue-tsx-pro 的 MenuItem）：执行一个操作，Click、Command（TS 的 onAction）；<see cref="Tone"/> 为 danger 时是破坏性操作（删除）。</summary>
public class MenuItem : MenuItemBase
{
    /// <summary>色调：删除等破坏性操作用 danger，默认 neutral。</summary>
    public static readonly StyledProperty<MenuItemTone> ToneProperty = AvaloniaProperty.Register<MenuItem, MenuItemTone>(nameof(Tone), MenuItemTone.Neutral);

    /// <summary>色调。</summary>
    public MenuItemTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty) PseudoClasses.Set(":danger", Tone == MenuItemTone.Danger);
    }
}

/// <summary>链接菜单项（vue-tsx-pro 的 MenuLinkItem）：<see cref="Href"/> 是绝对地址，执行时用系统打开（桌面没有站内地址：应用里的跳转用 MenuItem 的 Command）。</summary>
public class MenuLinkItem : MenuItemBase
{
    /// <summary>地址（绝对地址）。</summary>
    public static readonly StyledProperty<Uri?> HrefProperty = LinkButton.HrefProperty.AddOwner<MenuLinkItem>();

    /// <summary>地址。</summary>
    public Uri? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <inheritdoc />
    internal override void Run()
    {
        base.Run();
        if (Href is { IsAbsoluteUri: true } href) LinkLauncher.Open(this, href);
    }
}

/// <summary>复选菜单项（vue-tsx-pro 的 MenuCheckboxItem）：执行时切换 <see cref="IsChecked"/>；默认执行后不关闭菜单（常连续勾选几项）。</summary>
public class MenuCheckboxItem : MenuItemBase
{
    /// <summary>是否勾选（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty = AvaloniaProperty.Register<MenuCheckboxItem, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    static MenuCheckboxItem() => CloseOnSelectProperty.OverrideDefaultValue<MenuCheckboxItem>(false);

    /// <summary>是否勾选。</summary>
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    internal override IconData? Indicator => Lucide.Check;

    internal override bool? Checked => IsChecked;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty) UpdateMark();
    }

    /// <inheritdoc />
    internal override void Run()
    {
        SetCurrentValue(IsCheckedProperty, !IsChecked);
        base.Run();
    }
}

/// <summary>单选菜单项（vue-tsx-pro 的 MenuRadioItem）：放在 <see cref="MenuRadioGroup"/> 里，组的 Value 等于 <see cref="Value"/> 时选中；执行时选中它。</summary>
public class MenuRadioItem : MenuItemBase
{
    /// <summary>这一项的值。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<MenuRadioItem, string?>(nameof(Value));

    /// <summary>这一项的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    MenuRadioGroup? Group => this.GetLogicalAncestors().OfType<MenuRadioGroup>().FirstOrDefault();

    internal override IconData? Indicator => Lucide.Dot;

    internal override bool? Checked => Group?.Value is { } value && value == Value;

    /// <summary>组的值变了：换标记。</summary>
    internal void Sync() => UpdateMark();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) UpdateMark();
    }

    /// <inheritdoc />
    internal override void Run()
    {
        Group?.SetCurrentValue(MenuRadioGroup.ValueProperty, Value);
        base.Run();
    }
}

/// <summary>
/// 菜单里的一组项（vue-tsx-pro 的 MenuGroup）：<see cref="Label"/> 是组内第一行的标题（textMuted、sm、中等字重，上下 space.xs、左右 space.sm），
/// 同时是分组的读屏名称。
/// </summary>
public class MenuGroup : Control
{
    /// <summary>分组的标题。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<MenuGroup, string?>(nameof(Label));

    readonly StackPanel root = new();
    readonly Text label = new() { Name = MenuItemParts.GroupLabel, Tone = TextTone.Muted, Size = TextSize.Sm, Weight = TextWeight.Medium, IsVisible = false };
    readonly StackPanel items = new();

    /// <summary>新建：标题与组里的项竖着排。</summary>
    public MenuGroup()
    {
        root.Children.AddRange([label, items]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
    }

    /// <summary>分组的标题。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>组里的项。</summary>
    [Content]
    public Controls Items => items.Children;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            label.Text = Label;
            label.IsVisible = Label is { Length: > 0 };
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == global::Avalonia.Controls.Documents.TextElement.FontSizeProperty) UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateMetrics();
    }

    /// <summary>标题的内边距：上下 space.xs、左右 space.sm（按标题自己的 sm 字号）。</summary>
    void UpdateMetrics()
    {
        var em = label.FontSize;
        label.Padding = new Thickness(this.Resource("Rune.Space.Sm", 0.5) * em, this.Resource("Rune.Space.Xs", 0.25) * em);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    sealed class GroupPeer(MenuGroup owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => owner.Label;
    }
}

/// <summary>一组单选菜单项（vue-tsx-pro 的 MenuRadioGroup）：同一时间只选中一项，<see cref="Value"/> 是选中项的值（默认双向绑定）。</summary>
public class MenuRadioGroup : MenuGroup
{
    /// <summary>选中项的值。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<MenuRadioGroup, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>选中项的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
            foreach (var item in Items.OfType<MenuRadioItem>()) item.Sync();
    }
}

/// <summary>菜单项、分组的部件名（模板里的元素名；分组标题是 MenuGroup 里的 Text）。</summary>
public static class MenuItemParts
{
    /// <summary>复选、单选的标记（TS 的 indicator）：占一列，没勾时只占位。</summary>
    public const string Indicator = "PART_Indicator";

    /// <summary>文字前的图标（TS 的 icon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>文字（TS 的 label）。</summary>
    public const string Label = "PART_Label";

    /// <summary>说明（TS 的 description）。</summary>
    public const string Description = "PART_Description";

    /// <summary>快捷键提示（TS 的 shortcut）。</summary>
    public const string Shortcut = "PART_Shortcut";

    /// <summary>子菜单的箭头（TS 的 chevron）。</summary>
    public const string Chevron = "PART_Chevron";

    /// <summary>分组的标题（TS 的 groupLabel）。</summary>
    public const string GroupLabel = "PART_GroupLabel";
}
