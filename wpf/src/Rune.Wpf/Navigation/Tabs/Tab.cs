using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>
/// 标签（role="tab"）：中等字重，平时 textMuted、悬停与选中时 text；控件高度、左右 space.md、sm 圆角。点击切换（使用方的 Click 先执行，标记 Handled 可以拦下）；
/// 自动切换时键盘聚焦就切换。读屏里是选项卡项，选中状态是选择项的 IsSelected。
/// 订阅了 <see cref="CloseRequested"/> 才能关闭（上游给了 onClose 才有）：末尾一个圆形的关闭按钮（部件 <see cref="TabParts.Close"/>，只给指针用），
/// 标签上按 Delete、鼠标中键点也关闭。使用方移除这个标签；关掉的是当前标签时 Tabs 选它后面（没有时前面）的一个，焦点也跟过去。
/// </summary>
[TemplatePart(Name = "PART_CloseHost", Type = typeof(ContentPresenter))]
public class Tab : ButtonBase
{
    /// <summary>这个标签的值（同对应面板的 Value）。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Tab),
        new FrameworkPropertyMetadata(null, (d, _) => ((Tab)d).OnLabelChanged()));

    static readonly DependencyPropertyKey IsSelectedKey = DependencyProperty.RegisterReadOnly(nameof(IsSelected), typeof(bool), typeof(Tab), new PropertyMetadata(false));

    /// <summary>选中着。</summary>
    public static readonly DependencyProperty IsSelectedProperty = IsSelectedKey.DependencyProperty;

    static readonly DependencyPropertyKey VariantKey = DependencyProperty.RegisterReadOnly(nameof(Variant), typeof(TabsVariant), typeof(Tab), new PropertyMetadata(TabsVariant.Line));

    /// <summary>所在 Tabs 的样式（主题的触发器按它切换外观）。</summary>
    public static readonly DependencyProperty VariantProperty = VariantKey.DependencyProperty;

    static readonly DependencyPropertyKey OrientationKey = DependencyProperty.RegisterReadOnly(nameof(Orientation), typeof(TabsOrientation), typeof(Tab), new PropertyMetadata(TabsOrientation.Horizontal));

    /// <summary>所在 Tabs 的方向。</summary>
    public static readonly DependencyProperty OrientationProperty = OrientationKey.DependencyProperty;

    static readonly DependencyPropertyKey IsClosableKey = DependencyProperty.RegisterReadOnly(nameof(IsClosable), typeof(bool), typeof(Tab), new PropertyMetadata(false));

    /// <summary>能关闭（订阅了 <see cref="CloseRequested"/>；主题按它给关闭按钮留位置）。</summary>
    public static readonly DependencyProperty IsClosableProperty = IsClosableKey.DependencyProperty;

    static readonly DependencyPropertyKey IsDraggingKey = DependencyProperty.RegisterReadOnly(nameof(IsDragging), typeof(bool), typeof(Tab), new PropertyMetadata(false));

    /// <summary>拖动排序中（主题里半透明）。</summary>
    public static readonly DependencyProperty IsDraggingProperty = IsDraggingKey.DependencyProperty;

    Tabs? owner;
    EventHandler? closeRequested;
    IconButton? close;
    ContentPresenter? closeHost;
    bool middlePressed;

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

    /// <summary>能关闭。</summary>
    public bool IsClosable => (bool)GetValue(IsClosableProperty);

    /// <summary>拖动排序中。</summary>
    public bool IsDragging { get => (bool)GetValue(IsDraggingProperty); internal set => SetValue(IsDraggingKey, value); }

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

    /// <summary>读屏、菜单里的文字：内容的文字，没有时是值。</summary>
    internal string Label => Field.PlainText(Content) is { Length: > 0 } text ? text : Value ?? "";

    /// <summary>关闭按钮（测试用）。</summary>
    internal IconButton? CloseButton => close;

    /// <summary>焦点环画在里面：放不下时标签列表横向滚动，画在外面会被裁掉。</summary>
    private protected override double FocusRingOutset => 0;

    /// <inheritdoc />
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        OnLabelChanged();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) PlaceClose();
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        closeHost = GetTemplateChild("PART_CloseHost") as ContentPresenter;
        if (closeHost is not null) closeHost.Content = close;
        PlaceClose();
    }

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

    /// <summary>文字或值变了：关闭按钮的名称、"全部标签页"菜单跟着变。</summary>
    void OnLabelChanged()
    {
        UpdateCloseLabel();
        TabList.Of(this)?.SyncMenuItems();
    }

    /// <summary>订阅了关闭就建关闭按钮，退订了就拿掉。</summary>
    void SyncClose()
    {
        SetValue(IsClosableKey, closeRequested is not null);
        if (closeRequested is null)
        {
            ClearValue(AutomationProperties.AcceleratorKeyProperty);
            close = null;
            if (closeHost is not null) closeHost.Content = null;
            return;
        }
        if (close is not null) return;
        // 只给指针用：标签列表里只能有标签；键盘、读屏用标签上的 Delete。不能聚焦：按下时焦点不动
        close = new IconButton { Name = TabParts.Close, Icon = Lucide.X, Size = TextSize.Xs, Radius = Radius.Full, Focusable = false };
        close.Click += (_, e) =>
        {
            e.Handled = true;
            RequestClose();
        };
        AutomationProperties.SetAcceleratorKey(this, "Delete");
        UpdateCloseLabel();
        PlaceClose();
        if (closeHost is not null) closeHost.Content = close;
    }

    void UpdateCloseLabel()
    {
        if (close is not null) AutomationProperties.SetName(close, Provider.GetLang(this).TabClose(Label));
    }

    /// <summary>关闭按钮离结束边 space.xs。</summary>
    void PlaceClose()
    {
        if (closeHost is not null) closeHost.Margin = new Thickness(0, 0, 0.25 * FontSize, 0);
    }

    /// <summary>请求关闭（不可用时不关）。</summary>
    internal void RequestClose()
    {
        if (closeRequested is not null && !IsDisabled) closeRequested.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Delete 关闭（TS 的 aria-keyshortcuts="Delete"）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None && closeRequested is not null && !IsDisabled)
        {
            RequestClose();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>鼠标中键：按下、松开都在这个标签上时关闭（同浏览器的标签页）。</summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle) middlePressed = true;
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton != MouseButton.Middle || !middlePressed) return;
        middlePressed = false;
        if (IsMouseOver) RequestClose();
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

        // 关闭按钮对读屏隐藏（TS 的 aria-hidden）：读屏用标签上的 Delete
        protected override List<AutomationPeer>? GetChildrenCore() =>
            base.GetChildrenCore()?.Where(peer => (peer as UIElementAutomationPeer)?.Owner != owner.close).ToList();

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

/// <summary>标签的部件名（关闭按钮由代码建，放在模板里 PART_CloseHost 的位置上）。</summary>
public static class TabParts
{
    /// <summary>关闭按钮（TS 的 propsClose）：xs、ghost、圆形的 X 图标按钮，叠在标签的结束端。</summary>
    public const string Close = "PART_Close";
}
