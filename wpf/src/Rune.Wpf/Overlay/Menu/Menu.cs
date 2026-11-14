using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;

namespace Rune.Wpf;

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
///   &lt;rune:MenuItem Shortcut="Ctrl+D" Command="{Binding Duplicate}"&gt;复制&lt;/rune:MenuItem&gt;
///   &lt;rune:Separator Decorative="False" /&gt;
///   &lt;rune:MenuItem Tone="danger" Click="OnRemove"&gt;删除&lt;/rune:MenuItem&gt;
/// &lt;/rune:Menu&gt;
/// </code>
/// 键盘：触发器上 Enter / 空格 / ↓ 打开并聚焦第一项，↑ 打开并聚焦最后一项；菜单里 ↑↓ 移动（循环）、Home / End、按字母跳到以它开头的项，
/// Enter / 空格执行，Esc 关闭并回到触发器，Tab 关闭并继续往后。执行普通项后关闭；复选项默认不关，单选项默认关闭；都可以用 CloseOnSelect 改。
/// 菜单项的快捷键（<see cref="MenuItemBase.Shortcut"/>）在窗口里随时有效。弹出层用 <see cref="Popover"/>：点外面关闭，位置、翻转同弹出框。
/// 读屏：面板是菜单，以触发器为名称（写了 AutomationProperties.Name 时用它）。
/// </summary>
[ContentProperty(nameof(Items))]
public class Menu : FrameworkElement, IMenuHost
{
    /// <summary>触发器（一般是按钮）：照常排在原处，点它开关。</summary>
    public static readonly DependencyProperty TriggerProperty = DependencyProperty.Register(nameof(Trigger), typeof(UIElement), typeof(Menu),
        new FrameworkPropertyMetadata(null, (d, _) => ((Menu)d).OnTriggerChanged()));

    /// <summary>开着（默认双向绑定）：Esc、点外面、执行菜单项都会关闭并写回。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Menu),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(nameof(Placement), typeof(FloatingPlacement), typeof(Menu),
        new FrameworkPropertyMetadata(FloatingPlacement.BottomStart, (d, _) => ((Menu)d).popover.Placement = ((Menu)d).Placement));

    /// <summary>字号档位，菜单项随之缩放；不写时继承外面的字号。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Menu),
        new FrameworkPropertyMetadata(default(TextSize), (d, _) => ((Menu)d).ApplySize()));

    /// <summary>菜单项按下时的波纹；不写时沿用全局设置。</summary>
    public static readonly DependencyProperty RippleProperty = DependencyProperty.Register(nameof(Ripple), typeof(bool?), typeof(Menu));

    readonly MenuPopover popover;
    readonly StackPanel items = new();
    readonly ScrollArea scroll = new() { IsViewportFocusable = false };
    Window? window;

    /// <summary>新建：触发器与弹出的一列菜单项。</summary>
    public Menu()
    {
        popover = new MenuPopover(this) { PanelRole = AutomationControlType.Menu };
        scroll.Content = items;
        popover.Content = scroll;
        popover.SetBinding(Popover.IsOpenProperty, new Binding(nameof(IsOpen)) { Source = this, Mode = BindingMode.TwoWay });
        popover.OpenChanged += (_, e) => OpenChanged?.Invoke(this, e);
        AddLogicalChild(popover);
        AddVisualChild(popover);
        // 弹出框的内容呈现器不给内容逻辑父元素：菜单项沿逻辑树找所在的菜单，滚动区域挂在菜单下
        AddLogicalChild(scroll);
        // 方向键在项之间移动（循环），按字母跳到以它开头的项
        _ = new RovingFocus(items, element => element is MenuItemBase) { Orientation = "vertical", Typeahead = true, TextOf = element => ((MenuItemBase)element).LabelText };
        Loaded += (_, _) =>
        {
            window = Window.GetWindow(this);
            // 菜单项的快捷键在窗口上预览：菜单没打开、焦点在别处时也有效
            if (window is not null) window.PreviewKeyDown += OnWindowKeyDown;
        };
        Unloaded += (_, _) =>
        {
            if (window is not null) window.PreviewKeyDown -= OnWindowKeyDown;
            window = null;
        };
    }

    /// <summary>触发器。</summary>
    public UIElement? Trigger { get => (UIElement?)GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>菜单项：MenuItem、MenuLinkItem、MenuCheckboxItem、MenuRadioGroup、MenuGroup、MenuSub、Separator。</summary>
    public UIElementCollection Items => items.Children;

    /// <summary>开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>弹出位置。</summary>
    public FloatingPlacement Placement { get => (FloatingPlacement)GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>菜单项按下时的波纹。</summary>
    public bool? Ripple { get => (bool?)GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    /// <summary>用户开、关了（触发器、点外面、Esc、执行了一项、Tab 移走）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>弹出层的面板（测试用）。</summary>
    internal Border Panel => popover.Panel;

    /// <summary>这一列里能执行的项（不含子菜单里的；跳过不可用的）。</summary>
    internal IEnumerable<MenuItemBase> EnabledItems => MenuItemBase.ItemsIn(items).Where(item => item.Host == this && item.IsEnabled && !item.IsDisabled);

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { popover, scroll }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override System.Windows.Media.Visual GetVisualChild(int index) => popover;

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == AutomationProperties.NameProperty) UpdateName();
    }

    void OnTriggerChanged()
    {
        popover.Trigger = Trigger;
        UpdateName();
    }

    /// <summary>面板的名称：写了 AutomationProperties.Name 时用它，不再以触发器为名称（同上游：aria-labelledby 会盖过 aria-label）。</summary>
    void UpdateName()
    {
        if (AutomationProperties.GetName(this) is not { Length: > 0 } name) return;
        AutomationProperties.SetName(popover.Panel, name);
        popover.Panel.ClearValue(AutomationProperties.LabeledByProperty);
    }

    /// <summary>字号档位：设在菜单的内容上（触发器不跟着变）。</summary>
    void ApplySize()
    {
        if (Size.Value is null) scroll.ClearValue(TextElement.FontSizeProperty);
        else if (Size.Pixels is { } pixels) scroll.SetValue(TextElement.FontSizeProperty, pixels);
        else scroll.SetResourceReference(TextElement.FontSizeProperty, Size.ResourceKey);
    }

    /// <summary>按下了某一项的快捷键：同点了那一项执行（不关心菜单开没开）；不可用的项不执行。</summary>
    void OnWindowKeyDown(object sender, KeyEventArgs e) => e.Handled = !e.Handled && OnHotkey(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);

    /// <summary>按了键：是哪一项的快捷键时执行它（测试直接调，键盘的修饰键状态造不出来）。</summary>
    /// <returns>执行了一项。</returns>
    internal bool OnHotkey(Key key, ModifierKeys modifiers)
    {
        var item = MenuItemBase.ItemsIn(items).FirstOrDefault(item => item.Hotkey is { } hotkey && hotkey.Matches(key, modifiers) && item.IsEnabled && !item.IsDisabled);
        item?.Run();
        return item is not null;
    }

    /// <inheritdoc />
    public void CloseFromItem() => popover.Close(OpenChangeReason.Select);

    /// <summary>以别的元素为锚点打开（右键菜单）：在指针所在处，或在元素的下方。</summary>
    /// <param name="target">锚点。</param>
    /// <param name="atPointer">在指针所在处。</param>
    internal void OpenAt(UIElement target, bool atPointer) => popover.OpenAt(target, atPointer ? PlacementMode.MousePoint : PlacementMode.Bottom);

    /// <inheritdoc />
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        popover.Measure(availableSize);
        return popover.DesiredSize;
    }

    /// <inheritdoc />
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
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

        public void Close(OpenChangeReason reason) => SetOpen(false, reason);

        public void OpenAt(UIElement target, PlacementMode mode) => OpenAt(target, mode, OpenChangeReason.Trigger);

        private protected override Thickness PanelPadding(double em)
        {
            Panel.MinWidth = 12 * em;
            owner.items.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * em);
            return default;
        }

        private protected override UIElement? FocusTarget()
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
            if (e.Key != Key.Tab)
            {
                base.OnPanelKeyDown(e);
                return;
            }
            // Tab：关上，焦点从触发器往后（Shift+Tab 往前）走一步
            CloseKeepingFocus(OpenChangeReason.Focus);
            var direction = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next;
            if (owner.Trigger is { } trigger && trigger.Focus()) trigger.MoveFocus(new TraversalRequest(direction));
            e.Handled = true;
        }
    }
}
