using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 标签（role="tab"）：中等字重，平时 textMuted、悬停与选中时 text；控件高度、左右 space.md、sm 圆角。点击切换；自动切换时键盘聚焦就切换。
/// 读屏里是选项卡项，选中状态是选择项的 IsSelected。伪类：<c>:selected</c>、<c>:line</c>、<c>:soft</c>、<c>:vertical</c>。
/// </summary>
[PseudoClasses(":selected", ":line", ":soft", ":vertical")]
public class Tab : ButtonBase
{
    /// <summary>这个标签的值（同对应面板的 Value）。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Tab, string?>(nameof(Value));

    /// <summary>选中着。</summary>
    public static readonly DirectProperty<Tab, bool> IsSelectedProperty = AvaloniaProperty.RegisterDirect<Tab, bool>(nameof(IsSelected), tab => tab.IsSelected);

    Tabs? owner;

    /// <summary>这个标签的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选中着。</summary>
    public bool IsSelected { get; private set => SetAndRaise(IsSelectedProperty, ref field, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Tab);

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        this.FindLogicalAncestorOfType<Tabs>()?.Sync();
    }

    /// <summary>跟着 Tabs 的选中值、样式、方向与波纹。</summary>
    internal void Apply(Tabs tabs, bool selected)
    {
        owner = tabs;
        IsSelected = selected;
        PseudoClasses.Set(":selected", selected);
        PseudoClasses.Set(":line", tabs.Variant != TabsVariant.Soft);
        PseudoClasses.Set(":soft", tabs.Variant == TabsVariant.Soft);
        PseudoClasses.Set(":vertical", tabs.Orientation == TabsOrientation.Vertical);
        if (tabs.Ripple is { } ripple) Ripple = ripple;
        else ClearValue(RippleProperty);
    }

    /// <summary>点击：先发 Click（使用方的处理先执行，标记 Handled 可以拦下切换），再切到这个标签。</summary>
    protected override void OnClick()
    {
        if (IsDisabled || IsLoading) return;
        var click = new RoutedEventArgs(ClickEvent);
        RaiseEvent(click);
        if (click.Handled) return;
        if (Command is { } command && command.CanExecute(CommandParameter)) command.Execute(CommandParameter);
        if (Value is { } value) owner?.Select(value);
    }

    /// <summary>自动切换：键盘把焦点移到哪个标签就切到哪个（指针按下得到的焦点由点击切换）。</summary>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (owner?.Activation == TabsActivation.Automatic && e.NavigationMethod != NavigationMethod.Pointer && !IsDisabled && Value is { } value) owner.Select(value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    sealed class TabPeer(Tab owner) : ButtonBaseAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        public bool IsSelected => owner.IsSelected;

        public ISelectionProvider? SelectionContainer => null;

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
