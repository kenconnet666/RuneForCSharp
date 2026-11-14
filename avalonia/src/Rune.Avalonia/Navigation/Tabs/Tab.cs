using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using System.Windows.Input;

namespace Rune.Avalonia;

/// <summary>
/// 标签（role="tab"）：中等字重，平时 textMuted、悬停与选中时 text；控件高度、左右 space.md、sm 圆角。点击切换；自动切换时键盘聚焦就切换。
/// 读屏里是选项卡项，选中状态是选择项的 IsSelected。伪类：<c>:selected</c>、<c>:line</c>、<c>:soft</c>、<c>:vertical</c>、<c>:closable</c>、<c>:dragging</c>。
/// 订阅了 <see cref="CloseRequested"/> 才能关闭（上游给了 onClose 才有）：末尾一个圆形的关闭按钮（部件 <see cref="TabParts.Close"/>，只给指针用），
/// 标签上按 Delete、鼠标中键点也关闭。使用方移除这个标签；关掉的是当前标签时 Tabs 选它后面（没有时前面）的一个，焦点也跟过去。
/// </summary>
[PseudoClasses(":selected", ":line", ":soft", ":vertical", ":closable", ":dragging")]
public class Tab : ButtonBase
{
    /// <summary>这个标签的值（同对应面板的 Value）。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Tab, string?>(nameof(Value));

    /// <summary>选中着。</summary>
    public static readonly DirectProperty<Tab, bool> IsSelectedProperty = AvaloniaProperty.RegisterDirect<Tab, bool>(nameof(IsSelected), tab => tab.IsSelected);

    Tabs? owner;
    /// <summary>请求关闭时调用的命令（MVVM）：设了也算订阅（有关闭按钮）；在 <see cref="CloseRequested"/> 之后调用，参数是 <see cref="CloseCommandParameter"/>。</summary>
    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<Tab, ICommand?>(nameof(CloseCommand));

    /// <summary><see cref="CloseCommand"/> 的参数。</summary>
    public static readonly StyledProperty<object?> CloseCommandParameterProperty = AvaloniaProperty.Register<Tab, object?>(nameof(CloseCommandParameter));

    EventHandler? closeRequested;
    IconButton? close;
    ContentPresenter? closeHost;
    bool closePressed;
    bool middlePressed;

    /// <summary>这个标签的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选中着。</summary>
    public bool IsSelected { get; private set => SetAndRaise(IsSelectedProperty, ref field, value); }

    /// <summary>请求关闭时调用的命令。</summary>
    public ICommand? CloseCommand { get => GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }

    /// <summary>关闭命令的参数。</summary>
    public object? CloseCommandParameter { get => GetValue(CloseCommandParameterProperty); set => SetValue(CloseCommandParameterProperty, value); }

    /// <summary>发 <see cref="CloseRequested"/>，再调 <see cref="CloseCommand"/>（不能执行时不调）。</summary>
    void RaiseCloseRequested()
    {
        closeRequested?.Invoke(this, EventArgs.Empty);
        if (CloseCommand is { } command && command.CanExecute(CloseCommandParameter)) command.Execute(CloseCommandParameter);
    }

    /// <summary>请求关闭（关闭按钮、Delete、鼠标中键；TS 的 onClose）：使用方移除这个标签。订阅了才有关闭按钮。</summary>
    public event EventHandler? CloseRequested
    {
        add
        {
            closeRequested += value;
            SyncClose();
        }
        remove
        {
            closeRequested -= value;
            SyncClose();
        }
    }

    /// <summary>能关闭：订阅了 <see cref="CloseRequested"/> 或设了 <see cref="CloseCommand"/>。</summary>
    internal bool IsClosable => closeRequested is not null || CloseCommand is not null;

    /// <summary>读屏、菜单里的文字：内容的文字，没有时是值。</summary>
    internal string Label => Field.PlainText(Content) is { Length: > 0 } text ? text : Value ?? "";

    /// <summary>被移除时有焦点（TabList 把焦点交给接着选中的标签）。</summary>
    internal bool HadFocusWhenRemoved { get; private set; }

    /// <summary>拖动排序中：半透明。</summary>
    internal bool IsDragging { set => PseudoClasses.Set(":dragging", value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(Tab);

    /// <summary>焦点环画在里面：放不下时标签列表横向滚动，画在外面会被裁掉。</summary>
    private protected override double FocusRingOutset => 0;

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        this.FindLogicalAncestorOfType<Tabs>()?.Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        HadFocusWhenRemoved = false;
    }

    /// <summary>记下被移除时有没有焦点：基类随后把焦点挪走。</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        HadFocusWhenRemoved = IsFocused;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        closeHost = e.NameScope.Find<ContentPresenter>("PART_CloseHost");
        if (closeHost is not null) closeHost.Content = close;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CloseCommandProperty) SyncClose();
        if (change.Property == ContentProperty || change.Property == ValueProperty || change.Property == IsEnabledProperty)
        {
            UpdateCloseLabel();
            // "全部标签页"菜单里是标签的文字
            this.FindLogicalAncestorOfType<TabList>()?.SyncMenuItems();
        }
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

    /// <summary>订阅了关闭就建关闭按钮，退订了就拿掉。</summary>
    void SyncClose()
    {
        PseudoClasses.Set(":closable", IsClosable);
        if (!IsClosable)
        {
            ClearValue(AutomationProperties.AcceleratorKeyProperty);
            close = null;
            if (closeHost is not null) closeHost.Content = null;
            return;
        }
        if (close is not null) return;
        close = new IconButton
        {
            Name = TabParts.Close,
            Icon = Lucide.X,
            Size = TextSize.Xs,
            Variant = ButtonVariant.Ghost,
            Radius = Radius.Full,
            // 只给指针用：标签列表里只能有标签；键盘、读屏用标签上的 Delete
            Focusable = false,
        };
        AutomationProperties.SetAccessibilityView(close, AccessibilityView.Raw);
        AutomationProperties.SetAcceleratorKey(this, "Delete");
        // 按下时不把焦点挪到标签上（上游 pointerdown 时 preventDefault）：焦点管理器在按下的源元素上（隧道的最后一站）挪焦点，
        // 在按钮这一层先拦下，点击自己认
        close.AddHandler(PointerPressedEvent, OnClosePressed, RoutingStrategies.Tunnel);
        close.AddHandler(PointerReleasedEvent, OnCloseReleased, RoutingStrategies.Tunnel);
        UpdateCloseLabel();
        if (closeHost is not null) closeHost.Content = close;
    }

    void UpdateCloseLabel()
    {
        if (close is not null) AutomationProperties.SetName(close, Provider.GetLang(this).TabClose(Label));
    }

    void OnClosePressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (IsDisabled || !e.GetCurrentPoint(close).Properties.IsLeftButtonPressed) return;
        closePressed = true;
        e.Pointer.Capture(close);
    }

    void OnCloseReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Handled = true;
        if (!closePressed || close is null) return;
        closePressed = false;
        e.Pointer.Capture(null);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(close.Bounds.Size).Contains(e.GetPosition(close))) RequestClose();
    }

    /// <summary>请求关闭（不可用时不关）。</summary>
    internal void RequestClose()
    {
        if (IsClosable && !IsDisabled) RaiseCloseRequested();
    }

    /// <summary>Delete 关闭（TS 的 aria-keyshortcuts="Delete"）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None && IsClosable && !IsDisabled)
        {
            RequestClose();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>鼠标中键：按下、松开都在这个标签上时关闭（同浏览器的标签页）。</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        middlePressed = e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed;
        base.OnPointerPressed(e);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!middlePressed || e.InitialPressMouseButton != MouseButton.Middle) return;
        middlePressed = false;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) RequestClose();
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

/// <summary>标签的部件名（关闭按钮由代码建，叠在模板里 PART_CloseHost 的位置上）。</summary>
public static class TabParts
{
    /// <summary>关闭按钮（TS 的 propsClose）：xs、ghost、圆形的 X 图标按钮，叠在标签的结束端。</summary>
    public const string Close = "PART_Close";
}
