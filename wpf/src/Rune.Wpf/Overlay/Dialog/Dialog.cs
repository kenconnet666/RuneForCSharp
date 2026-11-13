using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 模态对话框（vue-tsx-pro 的 Dialog 与内核 ModalCore）：居中显示，下面一层遮罩挡住窗口里的其他内容。
/// <code>
/// &lt;rune:Dialog IsOpen="{Binding Editing}" Title="编辑资料" Description="修改后立即对团队成员可见。" Confirm="{Binding Save}"&gt;…表单…&lt;/rune:Dialog&gt;
/// </code>
/// 对话框写在哪里都行（自己不可见、不参与排版）：打开时盖住所在窗口的客户区。结构是标题行（H2 lg 与关闭按钮）、说明（muted）、正文（内容长时只滚正文）、页脚
/// （<see cref="Footer"/>，靠结束边；是布局面板时占满一行；写了 <see cref="Confirm"/> 时后面是"取消 / 确认"两个按钮）。面板 surfaceRaised、xs 描边、xl 圆角、
/// xl 阴影，宽度档位 <see cref="Size"/>（默认 xs，不超出窗口，四周至少留 1em）。
/// 打开时焦点移到第一个能聚焦的元素（同原生 dialog：有关闭按钮时就是它），Tab 在对话框里循环，关上后还给打开前的元素。关闭按钮、取消、Esc、点遮罩
/// （<see cref="Dismissible"/>）都先问 <see cref="BeforeClose"/>；确认返回的任务进行中按钮加载、关不掉，结果为 false 或失败时保持打开。<see cref="OpenChanged"/> 报告原因
/// （close、confirm、escape、outside；代码里改 IsOpen 不报告）。<see cref="Draggable"/> 时按住标题行能把对话框挪开：从标题行上的按钮、输入框按下不拖，
/// 拖到哪都不超出窗口，每次打开回到正中。读屏：对话框，以标题为名称、说明为帮助文字。部件样式键见 <see cref="DialogParts"/>。
/// 命令式（TS 的 useDialog）：不放进界面，<c>await new Dialog { … }.ShowAsync(owner)</c> 在 owner 所在的窗口打开，沿用 owner 处的主题、语言；
/// 得到关上的原因（代码调用 <see cref="Close"/> 是 imperative）。开着时改属性就是上游句柄的 update；owner 离开界面时跟着关上。
/// </summary>
[ContentProperty(nameof(Content))]
public class Dialog : FrameworkElement
{
    /// <summary>开着（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(Dialog),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((Dialog)d).Toggle()));

    /// <summary>标题，同时是对话框的读屏名称。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Dialog), new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>标题下的说明，读屏打开时一并念出。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(Dialog), new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>正文。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(Dialog), new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>页脚的操作（靠结束边排列，确认按钮之前）。</summary>
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(nameof(Footer), typeof(object), typeof(Dialog), new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>标题行右侧的关闭按钮，默认有。</summary>
    public static readonly DependencyProperty ClosableProperty = DependencyProperty.Register(nameof(Closable), typeof(bool), typeof(Dialog), new FrameworkPropertyMetadata(true, OnSync));

    /// <summary>点遮罩关闭，默认是；要用户明确选择时关掉（Esc 仍可关闭）。</summary>
    public static readonly DependencyProperty DismissibleProperty = DependencyProperty.Register(nameof(Dismissible), typeof(bool), typeof(Dialog), new FrameworkPropertyMetadata(true));

    /// <summary>用户要关闭时（关闭按钮、取消、Esc、点遮罩）先问它：参数是原因，结果为 false 时不关。</summary>
    public static readonly DependencyProperty BeforeCloseProperty = DependencyProperty.Register(nameof(BeforeClose), typeof(Func<OpenChangeReason, Task<bool>>), typeof(Dialog));

    /// <summary>确认：写了就在页脚放"取消 / 确认"；任务进行中按钮加载、关不掉，结果为 false 或失败时保持打开，否则关闭。</summary>
    public static readonly DependencyProperty ConfirmProperty = DependencyProperty.Register(nameof(Confirm), typeof(Func<Task<bool>>), typeof(Dialog), new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>确认按钮的文字；不写时取语言包（确定）。</summary>
    public static readonly DependencyProperty ConfirmTextProperty = DependencyProperty.Register(nameof(ConfirmText), typeof(string), typeof(Dialog), new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>取消按钮的文字；不写时取语言包（取消）。</summary>
    public static readonly DependencyProperty CancelTextProperty = DependencyProperty.Register(nameof(CancelText), typeof(string), typeof(Dialog), new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>确认按钮的色调，默认 accent；删除、驳回这类破坏性操作用 danger。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(Dialog), new FrameworkPropertyMetadata(StatusTone.Accent, OnSync));

    /// <summary>宽度档位，默认 xs（480）；full 占满窗口（四周留 1em）。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(DialogSize), typeof(Dialog),
        new FrameworkPropertyMetadata(DialogSize.Xs, (d, _) => ((Dialog)d).layer.InvalidateMeasure()));

    /// <summary>可以拖动：按住标题行把对话框挪开，不超出窗口；每次打开回到正中。</summary>
    public static readonly DependencyProperty DraggableProperty = DependencyProperty.Register(nameof(Draggable), typeof(bool), typeof(Dialog),
        new FrameworkPropertyMetadata(false, (d, e) => ((Dialog)d).header.Cursor = (bool)e.NewValue ? Cursors.SizeAll : null));

    readonly Popup popup = new() { StaysOpen = true, AllowsTransparency = true, Placement = PlacementMode.Center };
    readonly DialogLayer layer;
    readonly Border scrim = new();
    readonly Border shadow = new();
    readonly DialogPanel panel = new() { Focusable = true, FocusVisualStyle = null };
    readonly Flex header = new() { Align = FlexAlign.Start, Gap = Space.Md };
    readonly H2 title = new() { Size = TextSize.Lg };
    readonly CloseButton close = new();
    readonly Paragraph description = new() { Tone = TextTone.Muted };
    readonly ScrollArea body = new();
    readonly ContentPresenter content = new();
    readonly Flex footer = new() { Justify = FlexJustify.End, Gap = Space.Sm, Wrap = FlexWrap.Wrap };
    readonly ContentPresenter footerContent = new();
    readonly Button cancel = new() { Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral };
    readonly Button confirm = new();
    OpenChangeReason? pending;
    FrameworkElement? anchor;
    Adorner? adorner;
    readonly List<ResourceDictionary> borrowed = [];
    TaskCompletionSource<OpenChangeReason>? closed;
    IInputElement? returnFocus;
    Window? host;
    bool confirming;

    /// <summary>新建：弹出层（遮罩、阴影与面板：标题行、说明、正文、页脚）。</summary>
    public Dialog()
    {
        layer = new DialogLayer(this);
        scrim.SetResourceReference(StyleProperty, DialogParts.Scrim);
        shadow.SetResourceReference(StyleProperty, DialogParts.Shadow);
        panel.SetResourceReference(StyleProperty, DialogParts.Panel);
        Flex.SetGrow(title, true);
        header.Children.Add(title);
        header.Children.Add(close);
        body.Content = content;
        footer.Children.Add(footerContent);
        footer.Children.Add(cancel);
        footer.Children.Add(confirm);
        var stack = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(description, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        foreach (var part in new UIElement[] { header, description, footer, body }) stack.Children.Add(part);
        panel.Child = stack;
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetLabeledBy(panel, title);
        layer.Children.Add(scrim);
        layer.Children.Add(shadow);
        layer.Children.Add(panel);
        popup.Child = layer;
        AddLogicalChild(popup);
        close.Click += (_, _) => _ = RequestClose(OpenChangeReason.Close);
        cancel.Click += (_, _) => _ = RequestClose(OpenChangeReason.Close);
        confirm.Click += (_, _) => _ = RunConfirm();
        header.MouseLeftButtonDown += OnHeaderPressed;
        header.MouseMove += OnHeaderMoved;
        header.MouseLeftButtonUp += (_, _) => header.ReleaseMouseCapture();
        header.LostMouseCapture += (_, _) => dragFrom = null;
        scrim.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (Dismissible) _ = RequestClose(OpenChangeReason.Outside);
        };
        layer.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            _ = RequestClose(OpenChangeReason.Escape);
        };
        // 自己不参与排版（同关着的原生 dialog 的 display: none）：放在 Flex 里不多占一个间距；对话框画在弹出层里
        Visibility = Visibility.Collapsed;
        Sync();
    }

    /// <summary>开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>正文。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>页脚的操作。</summary>
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    /// <summary>关闭按钮。</summary>
    public bool Closable { get => (bool)GetValue(ClosableProperty); set => SetValue(ClosableProperty, value); }

    /// <summary>点遮罩关闭。</summary>
    public bool Dismissible { get => (bool)GetValue(DismissibleProperty); set => SetValue(DismissibleProperty, value); }

    /// <summary>关闭前先问它。</summary>
    public Func<OpenChangeReason, Task<bool>>? BeforeClose { get => (Func<OpenChangeReason, Task<bool>>?)GetValue(BeforeCloseProperty); set => SetValue(BeforeCloseProperty, value); }

    /// <summary>确认。</summary>
    public Func<Task<bool>>? Confirm { get => (Func<Task<bool>>?)GetValue(ConfirmProperty); set => SetValue(ConfirmProperty, value); }

    /// <summary>确认按钮的文字。</summary>
    public string? ConfirmText { get => (string?)GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }

    /// <summary>取消按钮的文字。</summary>
    public string? CancelText { get => (string?)GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <summary>确认按钮的色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>宽度档位。</summary>
    public DialogSize Size { get => (DialogSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>可以拖动。</summary>
    public bool Draggable { get => (bool)GetValue(DraggableProperty); set => SetValue(DraggableProperty, value); }

    /// <summary>拖出来的位移（测试用）：面板离正中的偏移。</summary>
    internal Vector Offset { get; private set; }

    /// <summary>标题行（测试用）。</summary>
    internal Flex Header => header;

    /// <summary>
    /// 命令式打开（TS 的 useDialog().open）：在 <paramref name="owner"/> 所在的窗口打开这个对话框。不在界面里的对话框开着时挂在窗口的装饰层上，
    /// 借用 owner 一路上的资源（主题、Provider 的样式层）与继承的语言包、工作日历。得到关上的原因；owner 离开界面时跟着关上（imperative）。
    /// </summary>
    /// <param name="owner">在哪个窗口打开、沿用谁的设置（一般是点的那个按钮）。</param>
    public Task<OpenChangeReason> ShowAsync(FrameworkElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (IsOpen) throw new InvalidOperationException("对话框已经开着。");
        anchor = owner;
        if (Parent is null && VisualTreeHelper.GetParent(this) is null) Borrow(owner);
        owner.Unloaded += OnAnchorUnloaded;
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

    /// <summary>打开时先聚焦哪个元素；null 时是第一个能聚焦的（AlertDialog 是取消按钮）。</summary>
    private protected virtual UIElement? InitialFocus => null;

    void OnAnchorUnloaded(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 借 owner 的上下文：挂到装饰层上（在窗口的可视树里），owner 一路上的资源按由远到近合并进来（近的盖过远的），继承的语言包、工作日历照抄。
    /// 装饰层在窗口内容的旁边，不是 owner 的祖先，光挂上去拿不到 owner 那一支上的资源。
    /// </summary>
    void Borrow(FrameworkElement owner)
    {
        if (AdornerLayer.GetAdornerLayer(owner) is { } layer)
        {
            adorner = new DialogAdorner(owner, this);
            layer.Add(adorner);
        }
        var chain = new List<ResourceDictionary>();
        for (DependencyObject? node = owner; node is not null; node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node))
            if (node is FrameworkElement { Resources: { } resources } && (resources.Count > 0 || resources.MergedDictionaries.Count > 0)) chain.Add(resources);
        chain.Reverse();
        foreach (var resources in chain)
        {
            Resources.MergedDictionaries.Add(resources);
            borrowed.Add(resources);
        }
        foreach (var property in new[] { Provider.LangProperty, Provider.WorkCalendarProperty })
            if (owner.GetValue(property) is { } value) SetValue(property, value);
    }

    /// <summary>命令式打开的关上了：还回借来的东西，等着的 ShowAsync 得到原因。</summary>
    void Release(OpenChangeReason reason)
    {
        if (anchor is null) return;
        anchor.Unloaded -= OnAnchorUnloaded;
        if (adorner is not null) AdornerLayer.GetAdornerLayer(anchor)?.Remove(adorner);
        adorner = null;
        foreach (var resources in borrowed) Resources.MergedDictionaries.Remove(resources);
        if (borrowed.Count > 0)
        {
            ClearValue(Provider.LangProperty);
            ClearValue(Provider.WorkCalendarProperty);
        }
        borrowed.Clear();
        anchor = null;
        var source = closed;
        closed = null;
        source?.TrySetResult(reason);
    }

    /// <summary>装饰层上的一格：只装着命令式打开的对话框（对话框自己不占位置，画在弹出层里）。</summary>
    sealed class DialogAdorner : Adorner
    {
        readonly Dialog dialog;

        public DialogAdorner(UIElement owner, Dialog dialog) : base(owner)
        {
            this.dialog = dialog;
            IsHitTestVisible = false;
            AddVisualChild(dialog);
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) => dialog;

        protected override Size ArrangeOverride(Size finalSize)
        {
            dialog.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }

    Point? dragFrom;
    Vector dragOrigin;

    /// <summary>用户开、关了（关闭按钮、取消、确认、Esc、点遮罩）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

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

    /// <summary>关闭按钮（测试用）。</summary>
    internal CloseButton CloseButton => close;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { popup }.GetEnumerator();

    double Em => TextElement.GetFontSize(this);

    static void OnSync(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Dialog)d).Sync();

    /// <summary>标题、说明、正文、页脚、按钮跟着属性。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        title.Text = Title ?? "";
        description.Text = Description ?? "";
        description.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetHelpText(panel, Description ?? "");
        close.Visibility = Closable ? Visibility.Visible : Visibility.Collapsed;
        header.Visibility = HeaderHidden ? Visibility.Collapsed : Visibility.Visible;
        // 标题行藏起来时 LabeledBy 指向的标题不在了：名称直接写在面板上
        if (HeaderHidden) AutomationProperties.SetName(panel, Title ?? "");
        else panel.ClearValue(AutomationProperties.NameProperty);
        content.Content = Content;
        footerContent.Content = Footer;
        footerContent.Visibility = Footer is null ? Visibility.Collapsed : Visibility.Visible;
        // 页脚内容是布局面板（一行两端分开的 Flex）时占满剩下的宽度；单个按钮照常靠结束边
        Flex.SetGrow(footerContent, Footer is Panel);
        cancel.Content = CancelText ?? lang.Cancel;
        confirm.Content = ConfirmText ?? lang.Confirm;
        confirm.Tone = Tone;
        cancel.Visibility = confirm.Visibility = Confirm is null ? Visibility.Collapsed : Visibility.Visible;
        footer.Visibility = Footer is null && Confirm is null ? Visibility.Collapsed : Visibility.Visible;
        close.IsEnabled = cancel.IsEnabled = !confirming;
        confirm.IsLoading = confirming;
    }

    /// <summary>按 em 的尺寸：面板 xl 圆角；标题行上、左右 space.xl；说明上 space.xs；正文上下 space.lg、左右 space.xl；页脚下、左右 space.xl。</summary>
    void UpdateMetrics()
    {
        var em = Em;
        double Space(string key, double fallback) => (TryFindResource($"Rune.Space.{key}") as double? ?? fallback) * em;
        var (xs, sm, lg, xl) = (Space("Xs", 0.25), Space("Sm", 0.5), Space("Lg", 1), Space("Xl", 1.5));
        panel.CornerRadius = shadow.CornerRadius = new CornerRadius((TryFindResource("Rune.Radius.Xl") as double? ?? 1) * em);
        header.Margin = new Thickness(xl, xl, xl, 0);
        close.Margin = new Thickness(0, -xs, -sm, -xs);
        description.Margin = new Thickness(xl, xs, xl, 0);
        content.Margin = BodyFlush ? default : new Thickness(xl, lg, xl, lg);
        footer.Margin = new Thickness(xl, 0, xl, xl);
        // 弹出层不在窗口的可视树里：字号同对话框
        TextElement.SetFontSize(layer, em);
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
        if (IsOpen)
        {
            Offset = default;
            host = Window.GetWindow(anchor ?? this);
            returnFocus = Keyboard.FocusedElement;
            popup.PlacementTarget = host?.Content as UIElement;
            if (host is not null) host.SizeChanged += OnHostResized;
            UpdateMetrics();
            layer.InvalidateMeasure();
            popup.IsOpen = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => (InitialFocus ?? FirstFocusable(panel) ?? panel).Focus());
        }
        else
        {
            if (host is not null) host.SizeChanged -= OnHostResized;
            host = null;
            popup.IsOpen = false;
            returnFocus?.Focus();
            returnFocus = null;
        }
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(IsOpen, known));
        if (!IsOpen) Release(reason ?? OpenChangeReason.Imperative);
    }

    /// <summary>窗口变了大小：遮罩跟着铺满，面板重新居中。</summary>
    void OnHostResized(object sender, SizeChangedEventArgs e)
    {
        layer.InvalidateMeasure();
        // 尺寸变了的弹出层要重新定位
        popup.HorizontalOffset += 0.01;
        popup.HorizontalOffset -= 0.01;
    }

    /// <summary>按住标题行开始拖：从按钮、输入框上按下不拖（关闭按钮照常点）。</summary>
    void OnHeaderPressed(object sender, MouseButtonEventArgs e)
    {
        if (!Draggable) return;
        for (var node = e.OriginalSource as DependencyObject; node is not null && node != header; node = VisualTreeHelper.GetParent(node))
            if (node is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.TextBoxBase) return;
        dragFrom = e.GetPosition(layer);
        dragOrigin = Offset;
        header.CaptureMouse();
        e.Handled = true;
    }

    void OnHeaderMoved(object sender, MouseEventArgs e)
    {
        if (dragFrom is { } from) DragTo(dragOrigin + (e.GetPosition(layer) - from));
    }

    /// <summary>挪到离正中这么远（排版时限制在窗口里）。</summary>
    internal void DragTo(Vector offset)
    {
        Offset = offset;
        layer.InvalidateArrange();
    }

    /// <summary>可视树里第一个能聚焦（可用、看得见）的元素（同原生 dialog 的初始焦点）。</summary>
    static UIElement? FirstFocusable(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            if (VisualTreeHelper.GetChild(parent, index) is not UIElement child || !child.IsVisible || !child.IsEnabled) continue;
            if (child.Focusable) return child;
            if (FirstFocusable(child) is { } found) return found;
        }
        return null;
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
    protected override AutomationPeer? OnCreateAutomationPeer() => null;

    /// <summary>面板：读屏是一个对话框（TS 的原生 dialog），名称取自标题。</summary>
    sealed class DialogPanel : Border
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new PanelAutomationPeer(this);

        sealed class PanelAutomationPeer(DialogPanel owner) : FrameworkElementAutomationPeer(owner)
        {
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Window;

            protected override string GetClassNameCore() => nameof(Dialog);

            protected override string GetLocalizedControlTypeCore() => "对话框";
        }
    }

    /// <summary>弹出层的根：和窗口的客户区一样大；遮罩铺满，面板居中，宽度按档位、不超出窗口（四周至少 1em），高度按内容、不超出窗口。</summary>
    sealed class DialogLayer(Dialog owner) : Panel
    {
        Size Area => owner.host?.Content is FrameworkElement root ? new Size(root.ActualWidth, root.ActualHeight) : default;

        (double Width, double MaxHeight) PanelLimits(Size area)
        {
            var em = owner.Em;
            var room = Math.Max(0, area.Width - 2 * em);
            var width = owner.Size == DialogSize.Full ? room : Math.Min(room, owner.TryFindResource(owner.Size.ResourceKey!) as double? ?? 480);
            return (width, Math.Max(0, area.Height - 2 * em));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var area = Area;
            owner.scrim.Measure(area);
            var (width, maxHeight) = PanelLimits(area);
            owner.panel.Measure(new Size(width, maxHeight));
            owner.shadow.Measure(new Size(width, maxHeight));
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
            box.Offset(owner.Offset);
            owner.shadow.Arrange(box);
            owner.panel.Arrange(box);
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

/// <summary>Dialog 的部件样式键（DESIGN §7）。</summary>
public static class DialogParts
{
    /// <summary>遮罩（TS 的 ::backdrop）。</summary>
    public static readonly PartKey Scrim = new(typeof(Dialog), nameof(Scrim), typeof(Border));

    /// <summary>面板（TS 的 root：dialog 元素）。</summary>
    public static readonly PartKey Panel = new(typeof(Dialog), nameof(Panel), typeof(Border));

    /// <summary>面板后面画阴影的一层（DropShadowEffect 不放在面板上：面板里的文字不走软件渲染）。</summary>
    public static readonly PartKey Shadow = new(typeof(Dialog), nameof(Shadow), typeof(Border));
}
