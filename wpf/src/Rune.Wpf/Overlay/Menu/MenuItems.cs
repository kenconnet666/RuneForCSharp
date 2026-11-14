using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 按钮类菜单项共有的（vue-tsx-pro 的 useButtonItem、menuItemContent）：起始边的勾选标记（复选、单选项）、图标、文字（Content）、
/// 文字下方的说明（<see cref="Description"/>，弱化；读屏当作帮助文字，不算进名称）、结束边的快捷键提示。
/// 列表行的外观：控件高度、上下 space.xs、左右 space.sm、sm 圆角，悬停 fillHover；不可用时变淡、方向键跳过。
/// 执行（点击、Enter、空格、快捷键）时先触发 Click、Command，点击后按 <see cref="CloseOnSelect"/> 关闭菜单。
/// </summary>
[TemplatePart(Name = MenuItemBaseParts.DescriptionName, Type = typeof(ContentPresenter))]
[TemplatePart(Name = MenuItemBaseParts.Shortcut, Type = typeof(FrameworkElement))]
public abstract class MenuItemBase : ButtonBase
{
    /// <summary>文字前的图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(MenuItemBase));

    /// <summary>快捷键（'Ctrl+D'、'Mod+Shift+P'）：结束边显示，按下时同点了这一项（菜单开没开都一样）；读屏从 AcceleratorKey 念。</summary>
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(MenuItemBase),
        new FrameworkPropertyMetadata(null, (d, _) => ((MenuItemBase)d).OnShortcutChanged()));

    /// <summary>文字下方的说明（第二行，textMuted、sm）：读屏当作这一项的帮助文字，不算进名称。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(object), typeof(MenuItemBase));

    /// <summary>执行后是否关闭菜单：普通项、单选项默认关，复选项默认不关。</summary>
    public static readonly DependencyProperty CloseOnSelectProperty = DependencyProperty.Register(nameof(CloseOnSelect), typeof(bool), typeof(MenuItemBase), new FrameworkPropertyMetadata(true));

    static readonly DependencyPropertyKey MarkKey = DependencyProperty.RegisterReadOnly(nameof(Mark), typeof(IconData), typeof(MenuItemBase), new PropertyMetadata(null));

    /// <summary>勾选标记的图标（复选是对勾，单选是圆点；普通项没有）。主题的模板绑它。</summary>
    public static readonly DependencyProperty MarkProperty = MarkKey.DependencyProperty;

    static readonly DependencyPropertyKey IsMarkedKey = DependencyProperty.RegisterReadOnly(nameof(IsMarked), typeof(bool), typeof(MenuItemBase), new PropertyMetadata(false));

    /// <summary>勾上了（复选、单选项）：标记显示，没勾时只占位。主题的触发器按它。</summary>
    public static readonly DependencyProperty IsMarkedProperty = IsMarkedKey.DependencyProperty;

    static readonly DependencyPropertyKey ShortcutTextKey = DependencyProperty.RegisterReadOnly(nameof(ShortcutText), typeof(string), typeof(MenuItemBase), new PropertyMetadata(null));

    /// <summary>快捷键显示的文字（Ctrl+D）。主题的模板绑它。</summary>
    public static readonly DependencyProperty ShortcutTextProperty = ShortcutTextKey.DependencyProperty;

    static readonly DependencyPropertyKey IsDangerKey = DependencyProperty.RegisterReadOnly(nameof(IsDanger), typeof(bool), typeof(MenuItemBase), new PropertyMetadata(false));

    /// <summary>破坏性操作（MenuItem 的 Tone 为 danger）。主题的触发器按它。</summary>
    public static readonly DependencyProperty IsDangerProperty = IsDangerKey.DependencyProperty;

    static readonly DependencyPropertyKey IsSubmenuKey = DependencyProperty.RegisterReadOnly(nameof(IsSubmenu), typeof(bool), typeof(MenuItemBase), new PropertyMetadata(false));

    /// <summary>打开子菜单的那一项：结束边一个箭头。主题的触发器按它。</summary>
    public static readonly DependencyProperty IsSubmenuProperty = IsSubmenuKey.DependencyProperty;

    FrameworkElement? shortcutPart;
    ContentPresenter? descriptionPart;

    static MenuItemBase()
    {
        // 各种菜单项共用一份样式
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MenuItemBase), new FrameworkPropertyMetadata(typeof(MenuItemBase)));
        FontSizeProperty.OverrideMetadata(typeof(MenuItemBase), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((MenuItemBase)d).UpdateMetrics()));
    }

    /// <summary>新建：沿用所在菜单的波纹设置。</summary>
    protected MenuItemBase() => Loaded += (_, _) =>
    {
        if (Host is { Ripple: { } ripple }) SetCurrentValue(RippleProperty, ripple);
        UpdateMark();
    };

    /// <summary>文字前的图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>快捷键。</summary>
    public string? Shortcut { get => (string?)GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>执行后是否关闭菜单。</summary>
    public bool CloseOnSelect { get => (bool)GetValue(CloseOnSelectProperty); set => SetValue(CloseOnSelectProperty, value); }

    /// <summary>勾选标记的图标。</summary>
    public IconData? Mark => (IconData?)GetValue(MarkProperty);

    /// <summary>勾上了。</summary>
    public bool IsMarked => (bool)GetValue(IsMarkedProperty);

    /// <summary>快捷键显示的文字。</summary>
    public string? ShortcutText => (string?)GetValue(ShortcutTextProperty);

    /// <summary>破坏性操作。</summary>
    public bool IsDanger { get => (bool)GetValue(IsDangerProperty); private protected set => SetValue(IsDangerKey, value); }

    /// <summary>打开子菜单的那一项。</summary>
    public bool IsSubmenu { get => (bool)GetValue(IsSubmenuProperty); private protected set => SetValue(IsSubmenuKey, value); }

    /// <summary>勾选标记的图标（复选是对勾，单选是圆点）；普通项没有。</summary>
    private protected virtual IconData? Indicator => null;

    /// <summary>勾没勾上（复选、单选项）；普通项为 null。</summary>
    internal virtual bool? Checked => null;

    /// <summary>解析好的快捷键；没写时为 null。</summary>
    internal Hotkey? Hotkey { get; private set; }

    /// <summary>所在的一列菜单（菜单或子菜单）。</summary>
    internal virtual IMenuHost? Host => Ancestors(this).OfType<IMenuHost>().FirstOrDefault();

    /// <summary>文字（类型搜索、读屏名称）：Content 是字符串时就是它，否则是写的读屏名称。</summary>
    internal string LabelText => Content as string ?? AutomationProperties.GetName(this);

    /// <summary>逻辑树上往外的各层。</summary>
    internal static IEnumerable<DependencyObject> Ancestors(DependencyObject element)
    {
        for (var node = LogicalTreeHelper.GetParent(element); node is not null; node = LogicalTreeHelper.GetParent(node)) yield return node;
    }

    /// <summary>容器里（逻辑树上）的菜单项，含分组、子菜单里的。</summary>
    internal static IEnumerable<MenuItemBase> ItemsIn(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is MenuItemBase item) yield return item;
            foreach (var nested in ItemsIn(child)) yield return nested;
        }
    }

    void OnShortcutChanged()
    {
        Hotkey = Shortcut is { Length: > 0 } combo ? global::Rune.Wpf.Hotkey.Parse(combo) : null;
        SetValue(ShortcutTextKey, Hotkey?.Text);
        if (Hotkey is { } hotkey) AutomationProperties.SetAcceleratorKey(this, hotkey.AccessKey);
        else ClearValue(AutomationProperties.AcceleratorKeyProperty);
    }

    /// <summary>勾选标记：有标记的项占一列（勾没勾都对齐），没勾时只占位。</summary>
    private protected void UpdateMark()
    {
        SetValue(MarkKey, Indicator);
        SetValue(IsMarkedKey, Checked == true);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        shortcutPart = GetTemplateChild(MenuItemBaseParts.Shortcut) as FrameworkElement;
        descriptionPart = GetTemplateChild(MenuItemBaseParts.DescriptionName) as ContentPresenter;
        UpdateMark();
        UpdateMetrics();
    }

    /// <summary>
    /// 按字号的尺寸：快捷键、箭头离文字至少 space.lg；说明的行高按它自己的 sm 字号算（WPF 的行高按像素继承）。
    /// </summary>
    private protected virtual void UpdateMetrics()
    {
        var gap = this.Resource<double>(ThemeKeys.Space.Lg) * FontSize;
        if (shortcutPart is not null) shortcutPart.Margin = new Thickness(gap, 0, 0, 0);
        if (descriptionPart is not null) TextBlock.SetLineHeight(descriptionPart, Leading.GetFactor(this) * TextElement.GetFontSize(descriptionPart));
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
    protected override AutomationPeer OnCreateAutomationPeer() => new MenuItemPeer(this);

    /// <summary>读屏：菜单项；名称是文字，说明是帮助文字；复选、单选项能切换并报勾选状态。</summary>
    sealed class MenuItemPeer(MenuItemBase owner) : ButtonBaseAutomationPeer(owner), IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

        protected override string GetClassNameCore() => owner.GetType().Name;

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.LabelText;

        protected override string GetHelpTextCore() =>
            AutomationProperties.GetHelpText(owner) is { Length: > 0 } help ? help : owner.Description as string ?? "";

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Toggle ? owner.Checked is null ? null : this : base.GetPattern(patternInterface);

        public ToggleState ToggleState => owner.Checked == true ? ToggleState.On : ToggleState.Off;

        public void Toggle() => ((IInvokeProvider)this).Invoke();
    }
}

/// <summary>菜单项（vue-tsx-pro 的 MenuItem）：执行一个操作，Click、Command（TS 的 onAction）；<see cref="Tone"/> 为 danger 时是破坏性操作（删除）。</summary>
public class MenuItem : MenuItemBase
{
    /// <summary>色调：删除等破坏性操作用 danger，默认 neutral。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(MenuItemTone), typeof(MenuItem),
        new FrameworkPropertyMetadata(MenuItemTone.Neutral, (d, _) => ((MenuItem)d).IsDanger = ((MenuItem)d).Tone == MenuItemTone.Danger));

    /// <summary>色调。</summary>
    public MenuItemTone Tone { get => (MenuItemTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
}

/// <summary>链接菜单项（vue-tsx-pro 的 MenuLinkItem）：<see cref="Href"/> 是绝对地址，执行时用系统打开（桌面没有站内地址：应用里的跳转用 MenuItem 的 Command）。</summary>
public class MenuLinkItem : MenuItemBase
{
    /// <summary>地址（绝对地址）。</summary>
    public static readonly DependencyProperty HrefProperty = DependencyProperty.Register(nameof(Href), typeof(Uri), typeof(MenuLinkItem));

    /// <summary>地址。</summary>
    public Uri? Href { get => (Uri?)GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <inheritdoc />
    internal override void Run()
    {
        base.Run();
        if (Href is { IsAbsoluteUri: true } href) LinkLauncher.Open(href);
    }
}

/// <summary>复选菜单项（vue-tsx-pro 的 MenuCheckboxItem）：执行时切换 <see cref="IsChecked"/>；默认执行后不关闭菜单（常连续勾选几项）。</summary>
public class MenuCheckboxItem : MenuItemBase
{
    /// <summary>是否勾选（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(MenuCheckboxItem),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((MenuCheckboxItem)d).UpdateMark()));

    static MenuCheckboxItem() => CloseOnSelectProperty.OverrideMetadata(typeof(MenuCheckboxItem), new FrameworkPropertyMetadata(false));

    /// <summary>是否勾选。</summary>
    public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    private protected override IconData? Indicator => Lucide.Check;

    internal override bool? Checked => IsChecked;

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
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(MenuRadioItem),
        new FrameworkPropertyMetadata(null, (d, _) => ((MenuRadioItem)d).UpdateMark()));

    /// <summary>这一项的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    MenuRadioGroup? Group => Ancestors(this).OfType<MenuRadioGroup>().FirstOrDefault();

    private protected override IconData? Indicator => Lucide.Dot;

    internal override bool? Checked => Group?.Value is { } value && value == Value;

    /// <summary>组的值变了：换标记。</summary>
    internal void Sync() => UpdateMark();

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
[ContentProperty(nameof(Items))]
public class MenuGroup : FrameworkElement
{
    /// <summary>分组的标题。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(MenuGroup),
        new FrameworkPropertyMetadata(null, (d, _) => ((MenuGroup)d).OnLabelChanged()));

    readonly StackPanel root = new();
    readonly Text label = new() { Tone = TextTone.Muted, Size = TextSize.Sm, Weight = TextWeight.Medium, Visibility = Visibility.Collapsed };
    readonly StackPanel items = new();

    static MenuGroup() => TextElement.FontSizeProperty.OverrideMetadata(typeof(MenuGroup),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((MenuGroup)d).UpdateMetrics()));

    /// <summary>新建：标题与组里的项竖着排。</summary>
    public MenuGroup()
    {
        label.SetResourceReference(StyleProperty, MenuGroupParts.Label);
        root.Children.Add(label);
        root.Children.Add(items);
        AddLogicalChild(root);
        AddVisualChild(root);
        Loaded += (_, _) => UpdateMetrics();
    }

    /// <summary>分组的标题。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>组里的项。</summary>
    public UIElementCollection Items => items.Children;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    void OnLabelChanged()
    {
        label.Text = Label;
        label.Visibility = Label is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>标题的内边距：上下 space.xs、左右 space.sm（按标题自己的 sm 字号）。</summary>
    void UpdateMetrics()
    {
        var em = label.FontSize;
        label.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Sm) * em, this.Resource<double>(ThemeKeys.Space.Xs) * em,
            this.Resource<double>(ThemeKeys.Space.Sm) * em, this.Resource<double>(ThemeKeys.Space.Xs) * em);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    sealed class GroupPeer(MenuGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => owner.GetType().Name;

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Label ?? "";
    }
}

/// <summary>一组单选菜单项（vue-tsx-pro 的 MenuRadioGroup）：同一时间只选中一项，<see cref="Value"/> 是选中项的值（默认双向绑定）。</summary>
public class MenuRadioGroup : MenuGroup
{
    /// <summary>选中项的值。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(MenuRadioGroup),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((MenuRadioGroup)d).Sync()));

    /// <summary>选中项的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    void Sync()
    {
        foreach (var item in Items.OfType<MenuRadioItem>()) item.Sync();
    }
}

/// <summary>菜单项的部件（各种菜单项共用，TS 的 menuRecipe）：说明有部件样式（DESIGN §7），其余是模板里的元素名。</summary>
public static class MenuItemBaseParts
{
    /// <summary>复选、单选的标记（TS 的 indicator）。</summary>
    public const string Indicator = "PART_Indicator";

    /// <summary>文字前的图标（TS 的 icon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>文字（TS 的 label）。</summary>
    public const string Label = "PART_Label";

    /// <summary>说明的部件样式（TS 的 description：textMuted、sm）。</summary>
    public static readonly PartKey Description = new(typeof(MenuItemBase), nameof(Description), typeof(ContentPresenter));

    /// <summary>说明在模板里的元素名。</summary>
    public const string DescriptionName = "PART_Description";

    /// <summary>快捷键提示（TS 的 shortcut）。</summary>
    public const string Shortcut = "PART_Shortcut";

    /// <summary>子菜单的箭头（TS 的 chevron）。</summary>
    public const string Chevron = "PART_Chevron";
}

/// <summary>分组的部件。</summary>
public static class MenuGroupParts
{
    /// <summary>分组标题的部件样式（TS 的 groupLabel、propsLabel）。</summary>
    public static readonly PartKey Label = new(typeof(MenuGroup), nameof(Label), typeof(Text));
}

/// <summary>子菜单的部件：那一项的部件同菜单项（<see cref="MenuItemBaseParts"/>），面板的样式同弹出框的面板（<see cref="PopoverParts.Panel"/>）。</summary>
public static class MenuSubParts
{
    /// <summary>子菜单的面板（TS 的 propsPanel）。</summary>
    public static readonly PartKey Panel = PopoverParts.Panel;

    /// <summary>文字前的图标。</summary>
    public const string Icon = MenuItemBaseParts.Icon;

    /// <summary>文字。</summary>
    public const string Label = MenuItemBaseParts.Label;

    /// <summary>说明。</summary>
    public static readonly PartKey Description = MenuItemBaseParts.Description;

    /// <summary>右侧的箭头。</summary>
    public const string Chevron = MenuItemBaseParts.Chevron;
}
