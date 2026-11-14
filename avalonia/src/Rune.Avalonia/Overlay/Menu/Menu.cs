using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>菜单项所在的一列菜单（<see cref="Menu"/> 或子菜单）：执行了一项后关闭，波纹的设置。</summary>
interface IMenuHost
{
    /// <summary>菜单项按下时的波纹；null 时沿用全局设置。</summary>
    bool? Ripple { get; }

    /// <summary>执行了一项：关闭（整串菜单），原因是 select，焦点回到触发器。</summary>
    void CloseFromItem();
}

/// <summary>
/// 菜单（vue-tsx-pro 的 Menu，WAI-ARIA 的 Menu Button 模式）：按钮弹出一列操作。
/// <code>
/// &lt;rune:Menu&gt;
///   &lt;rune:Menu.Trigger&gt;&lt;rune:Button IconEnd="{x:Static rune:Lucide.ChevronDown}"&gt;操作&lt;/rune:Button&gt;&lt;/rune:Menu.Trigger&gt;
///   &lt;rune:MenuItem Icon="{x:Static rune:Lucide.Pencil}" Click="OnEdit"&gt;编辑&lt;/rune:MenuItem&gt;
///   &lt;rune:MenuItem Icon="{x:Static rune:Lucide.Copy}" Shortcut="Ctrl+D" Command="{Binding Duplicate}"&gt;复制&lt;/rune:MenuItem&gt;
///   &lt;rune:Separator /&gt;
///   &lt;rune:MenuGroup Label="视图"&gt;&lt;rune:MenuCheckboxItem IsChecked="{Binding ShowGrid}"&gt;显示网格&lt;/rune:MenuCheckboxItem&gt;&lt;/rune:MenuGroup&gt;
///   &lt;rune:MenuRadioGroup Label="排序" Value="{Binding Sort}"&gt;
///     &lt;rune:MenuRadioItem Value="name"&gt;名称&lt;/rune:MenuRadioItem&gt;&lt;rune:MenuRadioItem Value="date"&gt;日期&lt;/rune:MenuRadioItem&gt;
///   &lt;/rune:MenuRadioGroup&gt;
///   &lt;rune:MenuItem Icon="{x:Static rune:Lucide.Trash}" Tone="danger" Click="OnRemove"&gt;删除&lt;/rune:MenuItem&gt;
/// &lt;/rune:Menu&gt;
/// </code>
/// 键盘：触发器上 Enter / 空格 / ↓ 打开并聚焦第一项，↑ 打开并聚焦最后一项；菜单里 ↑↓ 移动（循环）、Home / End、按字母跳到以它开头的项，
/// Enter / 空格执行，Esc 关闭并回到触发器，Tab 关闭并继续往后。执行普通项后关闭；复选项默认不关（常连续勾选几项），单选项默认关闭；都可以用 CloseOnSelect 改。
/// 菜单项的快捷键（<see cref="MenuItemBase.Shortcut"/>）在窗口里随时有效，菜单开没开都一样。弹出层用 <see cref="Popover"/>：点外面关闭，位置、翻转同弹出框。
/// 读屏：面板是菜单，以触发器为名称（写了 AutomationProperties.Name 时用它）。
/// </summary>
public class Menu : Control, IMenuHost
{
    /// <summary>触发器（一般是按钮）：照常排在原处，点它开关。</summary>
    public static readonly StyledProperty<Control?> TriggerProperty = Popover.TriggerProperty.AddOwner<Menu>();

    /// <summary>开着（默认双向绑定）：Esc、点外面、执行菜单项都会关闭并写回。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = Popover.IsOpenProperty.AddOwner<Menu>();

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    public static readonly StyledProperty<FloatingPlacement> PlacementProperty = Popover.PlacementProperty.AddOwner<Menu>();

    /// <summary>字号档位，菜单项随之缩放；不写时继承外面的字号。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Menu, TextSize>(nameof(Size));

    /// <summary>菜单项按下时的波纹；不写时沿用全局设置。</summary>
    public static readonly StyledProperty<bool?> RippleProperty = AvaloniaProperty.Register<Menu, bool?>(nameof(Ripple));

    readonly MenuPopover popover;
    readonly StackPanel items = new() { Name = MenuParts.Content };
    readonly ScrollArea scroll = new() { IsViewportFocusable = false };
    readonly TemplateResources resources;
    TopLevel? host;

    /// <summary>新建：触发器与弹出的一列菜单项。</summary>
    public Menu()
    {
        popover = new MenuPopover(this);
        scroll.Content = items;
        popover.Content = scroll;
        AutomationProperties.SetControlTypeOverride(popover.Panel, AutomationControlType.Menu);
        resources = new TemplateResources(scroll);
        LogicalChildren.Add(popover);
        VisualChildren.Add(popover);
        popover.OpenChanged += (_, e) => OpenChanged?.Invoke(this, e);
        popover.PropertyChanged += (_, e) =>
        {
            if (e.Property == Popover.IsOpenProperty) SetCurrentValue(IsOpenProperty, popover.IsOpen);
        };
        // 方向键在项之间移动（循环），按字母跳到以它开头的项
        _ = new RovingFocus(items, control => control is MenuItemBase) { Orientation = "vertical", Typeahead = true, TextOf = control => ((MenuItemBase)control).LabelText };
    }

    /// <summary>触发器。</summary>
    public Control? Trigger { get => GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>菜单项：MenuItem、MenuLinkItem、MenuCheckboxItem、MenuRadioGroup、MenuGroup、MenuSub、Separator。</summary>
    [Content]
    public Controls Items => items.Children;

    /// <summary>开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>弹出位置。</summary>
    public FloatingPlacement Placement { get => GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>菜单项按下时的波纹。</summary>
    public bool? Ripple { get => GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>用户开、关了（触发器、点外面、Esc、执行了一项、Tab 移走）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>弹出层的面板（测试用）。</summary>
    internal Border Panel => popover.Panel;

    /// <summary>这一列里能执行的项（不含子菜单里的；跳过不可用的）。</summary>
    internal IEnumerable<MenuItemBase> EnabledItems =>
        items.GetLogicalDescendants().OfType<MenuItemBase>().Where(item => item.Host == this && item.IsEffectivelyEnabled && !item.IsDisabled);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TriggerProperty)
        {
            popover.Trigger = Trigger;
            UpdateName();
        }
        else if (change.Property == IsOpenProperty) popover.IsOpen = IsOpen;
        else if (change.Property == PlacementProperty) popover.Placement = Placement;
        else if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(TextElement.FontSizeProperty, pixels);
            else resources.Bind(TextElement.FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == AutomationProperties.NameProperty) UpdateName();
    }

    /// <summary>面板的名称：写了 AutomationProperties.Name 时用它，不再以触发器为名称（同上游：aria-labelledby 会盖过 aria-label）。</summary>
    void UpdateName()
    {
        if (AutomationProperties.GetName(this) is not { Length: > 0 } name) return;
        AutomationProperties.SetName(popover.Panel, name);
        popover.Panel.ClearValue(AutomationProperties.LabeledByProperty);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        host = TopLevel.GetTopLevel(this);
        // 菜单项的快捷键在窗口上隧道接：菜单没打开、焦点在别处时也有效
        host?.AddHandler(KeyDownEvent, OnHostKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        host?.RemoveHandler(KeyDownEvent, OnHostKeyDown);
        host = null;
    }

    /// <summary>按下了某一项的快捷键：同点了那一项执行（不关心菜单开没开，也不开关菜单）；不可用的项不执行。</summary>
    void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        var item = items.GetLogicalDescendants().OfType<MenuItemBase>()
            .FirstOrDefault(item => item.Hotkey is { } hotkey && hotkey.Matches(e.Key, e.KeyModifiers) && item.IsEffectivelyEnabled && !item.IsDisabled);
        if (item is null) return;
        item.Run();
        e.Handled = true;
    }

    /// <inheritdoc />
    public void CloseFromItem() => popover.Close(OpenChangeReason.Select);

    /// <summary>以别的元素为锚点打开（右键菜单）：在指针所在处，或在元素的左下角。</summary>
    /// <param name="target">锚点。</param>
    /// <param name="atPointer">在指针所在处。</param>
    internal void OpenAt(Control target, bool atPointer) => popover.OpenAt(target, atPointer ? global::Avalonia.Controls.PlacementMode.Pointer : global::Avalonia.Controls.PlacementMode.BottomEdgeAlignedLeft);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        popover.Measure(availableSize);
        return popover.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        popover.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>
    /// 菜单的弹出层：面板不留内边距（内容层 space.xs，滚动条贴着边缘），至少 12em 宽；打开时聚焦第一项（↑ 打开时最后一项）；
    /// 触发器上 ↓ / ↑ 打开；Tab 关闭，焦点照常往后走。
    /// </summary>
    sealed class MenuPopover(Menu owner) : Popover
    {
        bool focusLast;

        // 面板的外观写在 rune|Popover 的部件样式里：类型选择器只认样式键
        protected override Type StyleKeyOverride => typeof(Popover);

        public void Close(OpenChangeReason reason) => SetOpen(false, reason);

        public void OpenAt(Control target, global::Avalonia.Controls.PlacementMode mode) => OpenAt(target, mode, OpenChangeReason.Trigger);

        private protected override Thickness PanelPadding(double em)
        {
            Panel.MinWidth = 12 * em;
            owner.items.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * em);
            return default;
        }

        private protected override InputElement? FocusTarget()
        {
            var items = owner.EnabledItems.ToList();
            var target = focusLast ? items.LastOrDefault() : items.FirstOrDefault();
            focusLast = false;
            return target;
        }

        private protected override void OnTriggerKeyDown(KeyEventArgs e)
        {
            if (e.Key is not (Key.Down or Key.Up)) return;
            focusLast = e.Key == Key.Up;
            SetOpen(true, OpenChangeReason.Trigger);
            e.Handled = true;
        }

        private protected override void OnPanelKeyDown(KeyEventArgs e)
        {
            if (e.Key != Key.Tab || e.Handled)
            {
                base.OnPanelKeyDown(e);
                return;
            }
            // Tab：关上，焦点从触发器往后（Shift+Tab 往前）走一步
            CloseKeepingFocus(OpenChangeReason.Focus);
            var direction = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? NavigationDirection.Previous : NavigationDirection.Next;
            if (owner.Trigger is { } trigger && trigger.Focus(NavigationMethod.Tab)) TopLevel.GetTopLevel(trigger)?.FocusManager?.TryMoveFocus(direction);
            e.Handled = true;
        }
    }
}

/// <summary>菜单的部件名：样式里用 <c>rune|Menu Border#PART_Panel</c> 选中。</summary>
public static class MenuParts
{
    /// <summary>面板（TS 的 panel，role="menu"）。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>滚动区域里的内容层（TS 的 content：space.xs 内边距）。</summary>
    public const string Content = "PART_Content";
}
