using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 标签（role="tab"）：中等字重，平时 textMuted、悬停与选中时 text；控件高度、左右 space.md、sm 圆角。点击切换（使用方的 Click 先执行，标记 Handled 可以拦下）；
/// 自动切换时键盘聚焦就切换。读屏里是选项卡项，选中状态是选择项的 IsSelected。
/// </summary>
public class Tab : ButtonBase
{
    /// <summary>这个标签的值（同对应面板的 Value）。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Tab));

    static readonly DependencyPropertyKey IsSelectedKey = DependencyProperty.RegisterReadOnly(nameof(IsSelected), typeof(bool), typeof(Tab), new PropertyMetadata(false));

    /// <summary>选中着。</summary>
    public static readonly DependencyProperty IsSelectedProperty = IsSelectedKey.DependencyProperty;

    static readonly DependencyPropertyKey VariantKey = DependencyProperty.RegisterReadOnly(nameof(Variant), typeof(TabsVariant), typeof(Tab), new PropertyMetadata(TabsVariant.Line));

    /// <summary>所在 Tabs 的样式（主题的触发器按它切换外观）。</summary>
    public static readonly DependencyProperty VariantProperty = VariantKey.DependencyProperty;

    static readonly DependencyPropertyKey OrientationKey = DependencyProperty.RegisterReadOnly(nameof(Orientation), typeof(TabsOrientation), typeof(Tab), new PropertyMetadata(TabsOrientation.Horizontal));

    /// <summary>所在 Tabs 的方向。</summary>
    public static readonly DependencyProperty OrientationProperty = OrientationKey.DependencyProperty;

    Tabs? owner;

    static Tab() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Tab), new FrameworkPropertyMetadata(typeof(Tab)));

    /// <summary>新建：挂上来时告诉所在的 Tabs。</summary>
    public Tab() => Loaded += (_, _) => Tabs.Owner(this)?.Sync();

    /// <summary>这个标签的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选中着。</summary>
    public bool IsSelected => (bool)GetValue(IsSelectedProperty);

    /// <summary>所在 Tabs 的样式。</summary>
    public TabsVariant Variant => (TabsVariant)GetValue(VariantProperty);

    /// <summary>所在 Tabs 的方向。</summary>
    public TabsOrientation Orientation => (TabsOrientation)GetValue(OrientationProperty);

    /// <summary>跟着 Tabs 的选中值、样式、方向与波纹。</summary>
    internal void Apply(Tabs tabs, bool selected)
    {
        owner = tabs;
        SetValue(IsSelectedKey, selected);
        SetValue(VariantKey, tabs.Variant);
        SetValue(OrientationKey, tabs.Orientation);
        if (tabs.Ripple is { } ripple) Ripple = ripple;
        else ClearValue(RippleProperty);
    }

    /// <summary>点击：先发 Click（使用方的处理先执行，标记 Handled 可以拦下切换），再切到这个标签。</summary>
    protected override void OnClick()
    {
        if (IsLoading || IsDisabled) return;
        var click = new RoutedEventArgs(ClickEvent, this);
        RaiseEvent(click);
        if (click.Handled) return;
        if (Command is { } command && command.CanExecute(CommandParameter)) command.Execute(CommandParameter);
        if (Value is { } value) owner?.Select(value);
    }

    /// <summary>自动切换：键盘把焦点移到哪个标签就切到哪个。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (owner?.Activation == TabsActivation.Automatic && InputManager.Current.MostRecentInputDevice is KeyboardDevice && !IsDisabled && Value is { } value)
            owner.Select(value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    sealed class TabPeer(Tab owner) : ButtonBaseAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override string GetClassNameCore() => nameof(Tab);

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);

        public bool IsSelected => owner.IsSelected;

        public IRawElementProviderSimple? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void Select()
        {
            if (!owner.IsDisabled && owner.Value is { } value) owner.owner?.Select(value);
        }
    }
}
