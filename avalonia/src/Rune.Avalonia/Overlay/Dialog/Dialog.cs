using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 模态对话框（vue-tsx-pro 的 Dialog 与内核 ModalCore）：居中显示，下面一层遮罩挡住窗口里的其他内容。
/// <code>
/// &lt;rune:Dialog IsOpen="{Binding Editing}" Title="编辑资料" Description="修改后立即对团队成员可见。" Confirm="{Binding Save}"&gt;…表单…&lt;/rune:Dialog&gt;
/// </code>
/// 对话框写在哪里都行（自己不可见、不参与排版）：打开时盖住所在窗口的客户区。结构是标题行（H2 lg 与关闭按钮）、说明（muted）、正文（内容长时只滚正文）、页脚
/// （<see cref="Footer"/>，靠结束边；是布局面板时占满一行；写了 <see cref="Confirm"/> 时后面是"取消 / 确认"两个按钮）。面板 surfaceRaised、xs 描边、xl 圆角、xl 阴影，宽度档位 <see cref="Size"/>
/// （默认 xs，不超出窗口，四周至少留 1em）。
/// 打开时焦点移到第一个能聚焦的元素（没有时在面板上），Tab 在对话框里循环，关上后还给打开前的元素。关闭按钮、取消、Esc、点遮罩（<see cref="Dismissible"/>）
/// 都先问 <see cref="BeforeClose"/>；确认返回的任务进行中按钮加载、关不掉，结果为 false 或失败时保持打开。<see cref="OpenChanged"/> 报告原因
/// （close、confirm、escape、outside；代码里改 IsOpen 不报告）。读屏：对话框，以标题为名称、说明为帮助文字。部件名见 <see cref="DialogParts"/>。
/// <see cref="Draggable"/> 时按住标题行能把对话框挪开（核对数据时看后面被挡住的内容）：从标题行上的按钮、输入框按下不拖，拖到哪都不超出窗口，每次打开回到正中。
/// 命令式（TS 的 useDialog）：不放进界面，<c>await new Dialog { … }.ShowAsync(owner)</c> 在 owner 所在的窗口打开，沿用 owner 处的主题、语言；
/// 得到关上的原因（代码调用 <see cref="Close"/> 是 imperative）。开着时改属性就是上游句柄的 update；owner 离开界面时跟着关上。
/// </summary>
public class Dialog : Control
{
    /// <summary>开着（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Dialog, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>标题，同时是对话框的读屏名称。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Dialog, string?>(nameof(Title));

    /// <summary>标题下的说明，读屏打开时一并念出。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<Dialog, string?>(nameof(Description));

    /// <summary>正文。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Dialog, object?>(nameof(Content));

    /// <summary>页脚的操作（靠结束边排列，确认按钮之前）。</summary>
    public static readonly StyledProperty<object?> FooterProperty = AvaloniaProperty.Register<Dialog, object?>(nameof(Footer));

    /// <summary>标题行右侧的关闭按钮，默认有。</summary>
    public static readonly StyledProperty<bool> ClosableProperty = AvaloniaProperty.Register<Dialog, bool>(nameof(Closable), true);

    /// <summary>点遮罩关闭，默认是；要用户明确选择时关掉（Esc 仍可关闭）。</summary>
    public static readonly StyledProperty<bool> DismissibleProperty = AvaloniaProperty.Register<Dialog, bool>(nameof(Dismissible), true);

    /// <summary>用户要关闭时（关闭按钮、取消、Esc、点遮罩）先问它：参数是原因，结果为 false 时不关（如表单有未保存的改动时先确认）。</summary>
    public static readonly StyledProperty<Func<OpenChangeReason, Task<bool>>?> BeforeCloseProperty =
        AvaloniaProperty.Register<Dialog, Func<OpenChangeReason, Task<bool>>?>(nameof(BeforeClose));

    /// <summary>确认：写了就在页脚放"取消 / 确认"；任务进行中按钮加载、关不掉，结果为 false 或失败时保持打开，否则关闭。</summary>
    public static readonly StyledProperty<Func<Task<bool>>?> ConfirmProperty = AvaloniaProperty.Register<Dialog, Func<Task<bool>>?>(nameof(Confirm));

    /// <summary>确认按钮的文字；不写时取语言包（确定）。</summary>
    public static readonly StyledProperty<string?> ConfirmTextProperty = AvaloniaProperty.Register<Dialog, string?>(nameof(ConfirmText));

    /// <summary>取消按钮的文字；不写时取语言包（取消）。</summary>
    public static readonly StyledProperty<string?> CancelTextProperty = AvaloniaProperty.Register<Dialog, string?>(nameof(CancelText));

    /// <summary>确认按钮的色调，默认 accent；删除、驳回这类破坏性操作用 danger。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Dialog, StatusTone>(nameof(Tone), StatusTone.Accent);

    /// <summary>宽度档位，默认 xs（480）；full 占满窗口（四周留 1em）。</summary>
    public static readonly StyledProperty<DialogSize> SizeProperty = AvaloniaProperty.Register<Dialog, DialogSize>(nameof(Size), DialogSize.Xs);

    /// <summary>可以拖动：按住标题行把对话框挪开，不超出窗口；每次打开回到正中。</summary>
    public static readonly StyledProperty<bool> DraggableProperty = AvaloniaProperty.Register<Dialog, bool>(nameof(Draggable));

    readonly Popup popup = new() { WindowManagerAddShadowHint = false, Placement = global::Avalonia.Controls.PlacementMode.Center };
    readonly DialogLayer layer;
    readonly Border scrim = new() { Name = DialogParts.Scrim };
    readonly Border panel = new() { Name = DialogParts.Panel, Focusable = true, ClipToBounds = true };
    readonly Flex header = new() { Name = DialogParts.Header, Align = FlexAlign.Start, Gap = Space.Md };
    readonly H2 title = new() { Name = DialogParts.Title, Size = TextSize.Lg };
    readonly CloseButton close = new() { Name = DialogParts.Close };
    readonly Paragraph description = new() { Name = DialogParts.Description, Tone = TextTone.Muted };
    readonly ScrollArea body = new() { Name = DialogParts.Body };
    readonly ContentPresenter content = new();
    readonly Flex footer = new() { Name = DialogParts.Footer, Justify = FlexJustify.End, Gap = Space.Sm, Wrap = FlexWrap.Wrap };
    readonly ContentPresenter footerContent = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    readonly Button cancel = new() { Name = DialogParts.Cancel, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral };
    readonly Button confirm = new() { Name = DialogParts.Confirm };
    OpenChangeReason? pending;
    Control? anchor;
    bool detached;
    TaskCompletionSource<OpenChangeReason>? closed;
    IInputElement? returnFocus;
    TopLevel? host;
    bool confirming;

    /// <summary>新建：弹出层（遮罩、面板：标题行、说明、正文、页脚）。</summary>
    public Dialog()
    {
        layer = new DialogLayer(this);
        Flex.SetGrow(title, true);
        header.Children.AddRange([title, close]);
        body.Content = content;
        footer.Children.AddRange([footerContent, cancel, confirm]);
        var stack = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(description, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        stack.Children.AddRange([header, description, footer, body]);
        panel.Child = stack;
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetLabeledBy(panel, title);
        layer.Children.AddRange([scrim, panel]);
        popup.Child = layer;
        LogicalChildren.Add(popup);
        VisualChildren.Add(popup);
        close.Click += (_, _) => _ = RequestClose(OpenChangeReason.Close);
        cancel.Click += (_, _) => _ = RequestClose(OpenChangeReason.Close);
        confirm.Click += (_, _) => _ = RunConfirm();
        scrim.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (Dismissible) _ = RequestClose(OpenChangeReason.Outside);
        };
        header.PointerPressed += OnHeaderPressed;
        header.PointerMoved += OnHeaderMoved;
        header.PointerReleased += (_, _) => dragFrom = null;
        header.PointerCaptureLost += (_, _) => dragFrom = null;
        layer.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Escape || e.Handled) return;
            e.Handled = true;
            _ = RequestClose(OpenChangeReason.Escape);
        }, RoutingStrategies.Bubble);
        // 自己不参与排版（同关着的原生 dialog 的 display: none）：放在 Flex 里不多占一个间距；对话框画在弹出层里
        IsVisible = false;
        Sync();
    }

    /// <summary>开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>正文。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>页脚的操作。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>关闭按钮。</summary>
    public bool Closable { get => GetValue(ClosableProperty); set => SetValue(ClosableProperty, value); }

    /// <summary>点遮罩关闭。</summary>
    public bool Dismissible { get => GetValue(DismissibleProperty); set => SetValue(DismissibleProperty, value); }

    /// <summary>关闭前先问它。</summary>
    public Func<OpenChangeReason, Task<bool>>? BeforeClose { get => GetValue(BeforeCloseProperty); set => SetValue(BeforeCloseProperty, value); }

    /// <summary>确认。</summary>
    public Func<Task<bool>>? Confirm { get => GetValue(ConfirmProperty); set => SetValue(ConfirmProperty, value); }

    /// <summary>确认按钮的文字。</summary>
    public string? ConfirmText { get => GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }

    /// <summary>取消按钮的文字。</summary>
    public string? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <summary>确认按钮的色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>宽度档位。</summary>
    public DialogSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>可以拖动。</summary>
    public bool Draggable { get => GetValue(DraggableProperty); set => SetValue(DraggableProperty, value); }

    /// <summary>拖出来的位移（测试用）：面板离正中的偏移。</summary>
    internal Vector Offset { get; private set; }

    Point? dragFrom;
    Vector dragOrigin;

    /// <summary>用户开、关了（关闭按钮、取消、确认、Esc、点遮罩）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>
    /// 命令式打开（TS 的 useDialog().open）：在 <paramref name="owner"/> 所在的窗口打开这个对话框（它不用放进界面，开着时挂在 owner 的逻辑树下，沿用那里的主题、语言）。
    /// 得到关上的原因；owner 离开界面时跟着关上（imperative）。
    /// </summary>
    /// <param name="owner">在哪个窗口打开、沿用谁的设置（一般是点的那个按钮）。</param>
    public Task<OpenChangeReason> ShowAsync(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (IsOpen) throw new InvalidOperationException("对话框已经开着。");
        anchor = owner;
        detached = Parent is null;
        if (detached) ((ISetLogicalParent)this).SetParent(owner);
        owner.DetachedFromVisualTree += OnAnchorDetached;
        var source = closed = new TaskCompletionSource<OpenChangeReason>();
        LastShown = this;
        SetOpen(true, OpenChangeReason.Imperative);
        return source.Task;
    }

    /// <summary>代码关上（TS 句柄的 close）：不问 <see cref="BeforeClose"/>，原因是 imperative。</summary>
    public void Close()
    {
        if (IsOpen) SetOpen(false, OpenChangeReason.Imperative);
    }

    /// <summary>最近一个命令式打开的对话框（测试用：ConfirmAsync 建的对话框不在界面里，找不到）。</summary>
    internal static Dialog? LastShown { get; private set; }

    void OnAnchorDetached(object? sender, VisualTreeAttachmentEventArgs e) => Close();

    /// <summary>打开时先聚焦哪个元素；null 时是第一个能聚焦的（AlertDialog 是取消按钮）。</summary>
    private protected virtual InputElement? InitialFocus => null;

    /// <summary>标题行只给读屏（命令面板：标题是面板的名称，不显示）。</summary>
    internal bool HeaderHidden { get; set { field = value; Sync(); } }

    /// <summary>面板顶边离窗口顶的比例（命令面板停在上部：结果变多变少时输入框不跳）；null 时居中。</summary>
    internal double? TopFraction { get; set { field = value; layer.InvalidateArrange(); } }

    /// <summary>正文不留内边距（命令面板的搜索行、列表贴边）。</summary>
    internal bool BodyFlush { get; set { field = value; UpdateMetrics(); } }

    /// <summary>面板（测试用）。</summary>
    internal Border Panel => panel;

    /// <summary>遮罩（测试用）。</summary>
    internal Border Scrim => scrim;

    /// <summary>取消、确认按钮（测试用）。</summary>
    internal (Button Cancel, Button Confirm) Actions => (cancel, confirm);

    /// <summary>标题行（测试用）。</summary>
    internal Flex Header => header;

    /// <summary>关闭按钮（测试用）。</summary>
    internal CloseButton CloseButton => close;

    double Em => TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == SizeProperty) layer.InvalidateMeasure();
        else if (change.Property == DraggableProperty) header.Cursor = Draggable ? new Cursor(StandardCursorType.SizeAll) : null;
        else if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
        else Sync();
    }

    /// <summary>标题、说明、正文、页脚、按钮跟着属性。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        title.Text = Title;
        description.Text = Description;
        description.IsVisible = !string.IsNullOrEmpty(Description);
        AutomationProperties.SetHelpText(panel, Description);
        close.IsVisible = Closable;
        header.IsVisible = !HeaderHidden;
        if (HeaderHidden) AutomationProperties.SetName(panel, Title);
        else panel.ClearValue(AutomationProperties.NameProperty);
        content.Content = Content;
        footerContent.Content = Footer;
        footerContent.IsVisible = Footer is not null;
        // 页脚内容是布局面板（一行两端分开的 Flex）时占满剩下的宽度；单个按钮照常靠结束边
        Flex.SetGrow(footerContent, Footer is Panel);
        cancel.Content = CancelText ?? lang.Cancel;
        confirm.Content = ConfirmText ?? lang.Confirm;
        confirm.Tone = Tone;
        cancel.IsVisible = confirm.IsVisible = Confirm is not null;
        footer.IsVisible = Footer is not null || Confirm is not null;
        close.IsEnabled = cancel.IsEnabled = !confirming;
        confirm.IsLoading = confirming;
    }

    /// <summary>按 em 的尺寸：面板 xl 圆角；标题行上、左右 space.xl；说明上 space.xs；正文上下 space.lg、左右 space.xl；页脚下、左右 space.xl。</summary>
    void UpdateMetrics()
    {
        var em = Em;
        double Space(string key) => this.Resource<double>(key) * em;
        var (xs, sm, lg, xl) = (Space(ThemeKeys.Space.Xs), Space(ThemeKeys.Space.Sm), Space(ThemeKeys.Space.Lg), Space(ThemeKeys.Space.Xl));
        panel.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Xl) * em);
        header.Margin = new Thickness(xl, xl, xl, 0);
        close.Margin = new Thickness(0, -xs, -sm, -xs);
        description.Margin = new Thickness(xl, xs, xl, 0);
        content.Margin = BodyFlush ? default : new Thickness(xl, lg);
        footer.Margin = new Thickness(xl, 0, xl, xl);
        // 弹出层不在窗口的可视树里：字号同对话框
        layer.SetValue(TextElement.FontSizeProperty, em);
    }

    void SetOpen(bool open, OpenChangeReason reason)
    {
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    /// <summary>打开：盖住窗口、记下焦点来源、焦点移进对话框；关上：还回焦点。</summary>
    void Toggle()
    {
        var reason = pending;
        PseudoClasses.Set(":open", IsOpen);
        if (IsOpen)
        {
            Offset = default;
            var top = TopLevel.GetTopLevel(anchor ?? this);
            returnFocus = top?.FocusManager?.GetFocusedElement();
            popup.PlacementTarget = top;
            host = top;
            if (host is not null) host.PropertyChanged += OnHostChanged;
            UpdateMetrics();
            popup.IsOpen = true;
            Dispatcher.UIThread.Post(() =>
            {
                var first = InitialFocus ?? panel.GetVisualDescendants().OfType<InputElement>()
                    .FirstOrDefault(element => element.Focusable && element.IsEffectivelyEnabled && element.IsEffectivelyVisible);
                (first ?? panel).Focus(NavigationMethod.Tab);
            }, DispatcherPriority.Loaded);
        }
        else
        {
            if (host is not null) host.PropertyChanged -= OnHostChanged;
            host = null;
            popup.IsOpen = false;
            returnFocus?.Focus(NavigationMethod.Tab);
            returnFocus = null;
        }
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(IsOpen, known));
        if (!IsOpen) Release(reason ?? OpenChangeReason.Imperative);
    }

    /// <summary>命令式打开的关上了：放开 owner，等着的 ShowAsync 得到原因。</summary>
    void Release(OpenChangeReason reason)
    {
        if (anchor is null) return;
        anchor.DetachedFromVisualTree -= OnAnchorDetached;
        if (detached) ((ISetLogicalParent)this).SetParent(null);
        anchor = null;
        detached = false;
        var source = closed;
        closed = null;
        source?.TrySetResult(reason);
    }

    /// <summary>窗口变了大小：遮罩跟着铺满，面板重新居中。</summary>
    void OnHostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty) layer.InvalidateMeasure();
    }

    /// <summary>用户要关闭：确认进行中不关；问过 BeforeClose 同意了才关。</summary>
    async Task RequestClose(OpenChangeReason reason)
    {
        if (confirming || !IsOpen) return;
        if (BeforeClose is { } ask && !await ask(reason)) return;
        SetOpen(false, reason);
    }

    /// <summary>确认：进行中按钮加载、关不掉；结果为 false 或失败时保持打开（调用方负责提示原因），否则关闭。</summary>
    async Task RunConfirm()
    {
        if (confirming || Confirm is not { } action) return;
        confirming = true;
        Sync();
        bool done;
        try
        {
            done = await action();
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceWarning($"对话框的 Confirm 失败，保持打开：{error}");
            done = false;
        }
        finally
        {
            confirming = false;
            Sync();
        }
        if (done) SetOpen(false, OpenChangeReason.Confirm);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        popup.Measure(availableSize);
        return default;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new DialogPeer(this);

    /// <summary>对话框自己不占位置，读屏看不到它（面板才是对话框）。</summary>
    sealed class DialogPeer(Dialog owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }

    /// <summary>按住标题行开始拖：从按钮、输入框上按下不拖（关闭按钮照常点）。</summary>
    void OnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!Draggable || !e.GetCurrentPoint(header).Properties.IsLeftButtonPressed) return;
        for (var node = e.Source as Visual; node is not null && node != header; node = node.GetVisualParent())
            if (node is global::Avalonia.Controls.Button or TextBox) return;
        dragFrom = e.GetPosition(layer);
        dragOrigin = Offset;
        e.Pointer.Capture(header);
        e.Handled = true;
    }

    void OnHeaderMoved(object? sender, PointerEventArgs e)
    {
        if (dragFrom is not { } from) return;
        Offset = dragOrigin + (e.GetPosition(layer) - from);
        layer.InvalidateArrange();
    }

    /// <summary>弹出层的根：和窗口的客户区一样大；遮罩铺满，面板居中，宽度按档位、不超出窗口（四周至少 1em），高度按内容、不超出窗口。</summary>
    sealed class DialogLayer(Dialog owner) : Panel
    {
        Size Area => TopLevel.GetTopLevel(owner) is { } top ? top.ClientSize : default;

        (double Width, double MaxHeight) PanelLimits(Size area)
        {
            var em = owner.Em;
            var room = Math.Max(0, area.Width - 2 * em);
            var width = owner.Size == DialogSize.Full ? room : Math.Min(room, owner.Resource<double>(owner.Size.ResourceKey!));
            return (width, Math.Max(0, area.Height - 2 * em));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var area = Area;
            owner.scrim.Measure(area);
            var (width, maxHeight) = PanelLimits(area);
            owner.panel.Measure(new Size(width, maxHeight));
            return area;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            owner.scrim.Arrange(new Rect(finalSize));
            var (width, maxHeight) = PanelLimits(finalSize);
            var height = owner.Size == DialogSize.Full ? maxHeight : Math.Min(maxHeight, owner.panel.DesiredSize.Height);
            var top = owner.TopFraction is { } fraction ? Math.Min(finalSize.Height * fraction, (finalSize.Height - height) / 2) : (finalSize.Height - height) / 2;
            var box = new Rect((finalSize.Width - width) / 2, top, width, height);
            // 拖出来的位移：整块不超出窗口
            owner.Offset = new Vector(
                Math.Clamp(owner.Offset.X, -box.X, Math.Max(-box.X, finalSize.Width - box.Right)),
                Math.Clamp(owner.Offset.Y, -box.Y, Math.Max(-box.Y, finalSize.Height - box.Bottom)));
            owner.panel.Arrange(box.Translate(owner.Offset));
            return finalSize;
        }
    }
}

/// <summary>对话框的宽度档位（vue-tsx-pro 的 DialogSizes：容器宽度档位加 full）。</summary>
/// <param name="Value">取值（xs、sm、md、lg、xl、full）。</param>
[TypeConverter(typeof(VariantConverter<DialogSize>))]
public readonly record struct DialogSize(string Value) : IVariant<DialogSize>
{
    /// <summary>480（默认）：确认、简短表单。</summary>
    public static readonly DialogSize Xs = new("xs");

    /// <summary>640。</summary>
    public static readonly DialogSize Sm = new("sm");

    /// <summary>768。</summary>
    public static readonly DialogSize Md = new("md");

    /// <summary>1024。</summary>
    public static readonly DialogSize Lg = new("lg");

    /// <summary>1280。</summary>
    public static readonly DialogSize Xl = new("xl");

    /// <summary>占满窗口（四周留 1em）。</summary>
    public static readonly DialogSize Full = new("full");

    /// <inheritdoc />
    public static IReadOnlyList<DialogSize> Known { get; } = [Xs, Sm, Md, Lg, Xl, Full];

    /// <summary>宽度的资源键（同容器档位，<c>Rune.Size.Xs</c>）；full 与 default 时为 null。</summary>
    public string? ResourceKey => Value is null or "full" ? null : $"Rune.Size.{char.ToUpperInvariant(Value[0])}{Value[1..]}";

    /// <inheritdoc />
    public static DialogSize Parse(string value)
    {
        var size = new DialogSize(value);
        Variants.WarnIfUnknown(size);
        return size;
    }

    /// <inheritdoc />
    public override string ToString() => Value ?? "";
}

/// <summary>Dialog 的部件名（面板在弹出层里，逻辑上仍是 Dialog 的后代）：<c>rune|Dialog Border#PART_Panel</c>。</summary>
public static class DialogParts
{
    /// <summary>遮罩（TS 的 ::backdrop）。</summary>
    public const string Scrim = "PART_Scrim";

    /// <summary>面板（TS 的 root：dialog 元素）。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>标题行（TS 的 propsHeader）。</summary>
    public const string Header = "PART_Header";

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";

    /// <summary>关闭按钮（TS 的 propsClose）。</summary>
    public const string Close = "PART_Close";

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>正文的滚动区（TS 的 propsBody）。</summary>
    public const string Body = "PART_Body";

    /// <summary>页脚（TS 的 propsFooter）。</summary>
    public const string Footer = "PART_Footer";

    /// <summary>取消按钮（TS 的 propsCancel）。</summary>
    public const string Cancel = "PART_Cancel";

    /// <summary>确认按钮（TS 的 propsConfirm）。</summary>
    public const string Confirm = "PART_Confirm";
}
