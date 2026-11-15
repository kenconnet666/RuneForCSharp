using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Platform.Storage;
using AvaloniaImage = Avalonia.Controls.Image;

namespace Rune.Avalonia;

/// <summary>
/// 图片预览（vue-tsx-pro 的 Lightbox）：铺满窗口的深色模态层上看大图——缩放、拖动、旋转、还原、下载，几张图时左右切换。
/// <see cref="Image"/> 的 Preview 打开它（同一个 <see cref="ImageGroup"/> 里的图共用一个），也可以单独用（附件列表里的“预览”）。
/// <code>
/// &lt;rune:Lightbox IsOpen="{Binding Viewing}" Index="{Binding At}" Items="{Binding Photos}" /&gt;
/// </code>
/// 内核同 <see cref="Dialog"/>（模态、焦点循环、Esc、关上后还回焦点、<see cref="Dialog.BeforeClose"/>、命令式 ShowAsync），面板换成：工具栏（计数、缩小、比例、放大、
/// 向左 / 向右旋转、还原、下载、关闭）、图片区、说明，两侧是上一张、下一张。背景总是 scrimStrong、文字 onScrim（看图片本身的颜色，不跟主题变）。
/// 平移与缩放建在 <see cref="PanZoom"/> 上（0.05–8 倍）：拖动平移，滚轮以指针为中心缩放，双击放大 / 还原；打开、换一张、旋转、还原之后整张图放进视口
/// （四周留 24，不超过原大）。键盘：← / → 上一张、下一张（从右到左排版时反过来），+ / − 缩放，0 原大，↑ / ↓ 平移，R 向右旋转，Esc 关闭。
/// 打开时预先读前后两张。下载按钮弹出保存对话框，存原始文件。读屏：对话框名称是“图片预览”，图片区是一张图片、名称是 alt，计数换一张时念出。
/// 部件名见 <see cref="LightboxParts"/>。
/// </summary>
public class Lightbox : Dialog
{
    /// <summary>图片。</summary>
    public static readonly StyledProperty<IReadOnlyList<LightboxItem>?> ItemsProperty = AvaloniaProperty.Register<Lightbox, IReadOnlyList<LightboxItem>?>(nameof(Items));

    /// <summary>当前第几张，从 0 开始（默认双向绑定）。</summary>
    public static readonly StyledProperty<int> IndexProperty = AvaloniaProperty.Register<Lightbox, int>(nameof(Index), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>到头之后从另一头继续，默认 false。</summary>
    public static readonly StyledProperty<bool> LoopProperty = AvaloniaProperty.Register<Lightbox, bool>(nameof(Loop));

    /// <summary>打开、换一张、旋转之后整张放进视口时四周留的空白。</summary>
    const double FitPadding = 24;

    readonly Flex toolbar = new() { Name = LightboxParts.Toolbar, Align = FlexAlign.Center, Justify = FlexJustify.End, Gap = Space.Xs };
    readonly Text counter = new() { Name = LightboxParts.Counter, Size = TextSize.Sm, Numeric = true, Opacity = 0.8 };
    readonly Text zoom = new() { Name = LightboxParts.Zoom, Size = TextSize.Sm, Numeric = true, HorizontalAlignment = HorizontalAlignment.Center };
    // 比例的格子至少 3.5em 宽、文字在里面居中：数字位数变了，旁边的按钮不跳
    readonly Border zoomBox = new();
    readonly IconButton zoomOut = Tool(Lucide.ZoomOut);
    readonly IconButton zoomIn = Tool(Lucide.ZoomIn);
    readonly IconButton rotateLeft = Tool(Lucide.RotateCcw);
    readonly IconButton rotateRight = Tool(Lucide.RotateCw);
    readonly IconButton reset = Tool(Lucide.Undo2);
    readonly IconButton download = Tool(Lucide.Download);
    readonly IconButton close = Tool(Lucide.X);
    readonly Stage viewport;
    readonly Panel content = new() { Name = LightboxParts.Content, RenderTransformOrigin = RelativePoint.TopLeft };
    readonly AvaloniaImage image = new() { Name = LightboxParts.Image, Stretch = Stretch.Fill, IsHitTestVisible = false };
    readonly IconButton previous = Tool(Lucide.ChevronLeft);
    readonly IconButton next = Tool(Lucide.ChevronRight);
    readonly ContentPresenter caption = new() { Name = LightboxParts.Caption, HorizontalContentAlignment = HorizontalAlignment.Center, Opacity = 0.9 };
    readonly PanZoom view;
    Size viewportSize;
    Size? natural;
    int rotation;
    bool fitPending;
    int loading;
    Point? pressedAt;
    Point lastDrag;
    bool interacting;

    /// <summary>新建：面板换成工具栏、图片区、说明与两侧的切换按钮。</summary>
    public Lightbox()
    {
        view = new PanZoom(() => (viewportSize.Width, viewportSize.Height))
        {
            Min = 0.05,
            Max = 8,
            Bounds = () => natural is null ? null : (Box.Width, Box.Height),
        };
        view.Changed += UpdateTransform;
        viewport = new Stage(this) { Name = LightboxParts.Viewport };
        // 排完版、量出图片区的大小之后再放进视口：排版途中改比例文字，工具栏会按旧的测量排
        viewport.LayoutUpdated += (_, _) => FitIfPending();
        content.Children.Add(image);
        viewport.Children.Add(content);

        Flex.SetGrow(counter, true);
        zoomBox.Child = zoom;
        toolbar.Children.AddRange([counter, zoomOut, zoomBox, zoomIn, rotateLeft, rotateRight, reset, download, close]);
        var stack = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(caption, Dock.Bottom);
        stack.Children.AddRange([toolbar, caption, viewport]);
        previous.Name = LightboxParts.Previous;
        next.Name = LightboxParts.Next;
        foreach (var side in new[] { previous, next })
        {
            side.Radius = Radius.Full;
            side.VerticalAlignment = VerticalAlignment.Center;
        }
        previous.HorizontalAlignment = HorizontalAlignment.Left;
        next.HorizontalAlignment = HorizontalAlignment.Right;
        var root = new Panel();
        root.Children.AddRange([stack, previous, next]);
        Panel.Child = root;
        HeaderHidden = true;
        AutomationProperties.SetLiveSetting(counter, AutomationLiveSetting.Polite);

        zoomOut.Click += (_, _) => view.ZoomOut();
        zoomIn.Click += (_, _) => view.ZoomIn();
        rotateLeft.Click += (_, _) => Rotate(-1);
        rotateRight.Click += (_, _) => Rotate(1);
        reset.Click += (_, _) => ResetView();
        download.Click += (_, _) => _ = Download();
        close.Click += (_, _) => _ = RequestClose(OpenChangeReason.Close);
        previous.Click += (_, _) => Go(-1);
        next.Click += (_, _) => Go(1);
        root.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    /// <summary>图片。</summary>
    public IReadOnlyList<LightboxItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <summary>当前第几张。</summary>
    public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    /// <summary>到头之后从另一头继续。</summary>
    public bool Loop { get => GetValue(LoopProperty); set => SetValue(LoopProperty, value); }

    /// <summary>平移与缩放（测试用）。</summary>
    internal PanZoom View => view;

    /// <summary>旋转的角度：0、90、180、270（测试用）。</summary>
    internal int Rotation => rotation;

    /// <summary>工具栏的按钮（测试用）。</summary>
    internal (IconButton ZoomOut, IconButton ZoomIn, IconButton RotateLeft, IconButton RotateRight, IconButton Reset, IconButton Download, IconButton Close) Tools =>
        (zoomOut, zoomIn, rotateLeft, rotateRight, reset, download, close);

    /// <summary>上一张、下一张（测试用）。</summary>
    internal (IconButton Previous, IconButton Next) Sides => (previous, next);

    /// <summary>计数与缩放比例（测试用）。</summary>
    internal (Text Counter, Text Zoom) Labels => (counter, zoom);

    /// <summary>图片区（测试用）。</summary>
    internal Panel Viewport => viewport;

    /// <summary>当前这一张（序号夹在范围里）。</summary>
    LightboxItem? Current => Items is { Count: > 0 } items ? items[Math.Clamp(Index, 0, items.Count - 1)] : null;

    /// <summary>旋转之后图片占的框。</summary>
    Size Box => natural is not { } size ? default : rotation % 180 == 0 ? size : new Size(size.Height, size.Width);

    /// <inheritdoc />
    private protected override InputElement? InitialFocus => viewport;

    /// <inheritdoc />
    private protected override CornerRadius PanelCornerRadius(double em) => default;

    /// <inheritdoc />
    private protected override Size PanelConstraint(Size area) => area;

    /// <inheritdoc />
    private protected override Rect PanelBox(Size area, Size constraint, Size desired) => new(area);

    /// <summary>打开时：按语言包写名称，量尺寸、读当前这一张。</summary>
    private protected override void OnPanelOpened()
    {
        var lang = Provider.GetLang(this);
        if (Title is null) SetCurrentValue(TitleProperty, lang.LightboxLabel);
        AutomationProperties.SetName(zoomOut, lang.ZoomOut);
        AutomationProperties.SetName(zoomIn, lang.ZoomIn);
        AutomationProperties.SetName(rotateLeft, lang.LightboxRotateLeft);
        AutomationProperties.SetName(rotateRight, lang.LightboxRotateRight);
        AutomationProperties.SetName(reset, lang.ResetView);
        AutomationProperties.SetName(download, lang.LightboxDownload);
        AutomationProperties.SetName(close, lang.Close);
        AutomationProperties.SetName(previous, lang.LightboxPrevious);
        AutomationProperties.SetName(next, lang.LightboxNext);
        // 深色背景上的按钮：文字 onScrim，悬停时 12% 的 onScrim 底
        var fore = this.Resource<IBrush>(ThemeKeys.Brush.OnScrim) as ISolidColorBrush;
        var palette = fore is null ? null : new TonePalette { Text = fore, SubtleHover = new SolidColorBrush(fore.Color, 0.12) };
        foreach (var button in new[] { zoomOut, zoomIn, rotateLeft, rotateRight, reset, download, close, previous, next }) button.Palette = palette;
        var em = global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);
        double Space(string key) => this.Resource<double>(key) * em;
        toolbar.Margin = new Thickness(Space(ThemeKeys.Space.Md), Space(ThemeKeys.Space.Sm));
        zoomBox.MinWidth = 3.5 * this.Resource<double>(ThemeKeys.FontSize.Sm);
        caption.Margin = new Thickness(Space(ThemeKeys.Space.Xl), Space(ThemeKeys.Space.Md));
        previous.Margin = new Thickness(Space(ThemeKeys.Space.Md), 0);
        next.Margin = new Thickness(Space(ThemeKeys.Space.Md), 0);
        ShowCurrent();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == ItemsProperty || change.Property == IndexProperty || change.Property == LoopProperty) && IsOpen) ShowCurrent();
    }

    /// <summary>换一张（或刚打开）：方向回正，读原图；计数、说明、两侧按钮、下载按钮跟着；预先读前后两张。</summary>
    void ShowCurrent()
    {
        var item = Current;
        var total = Items?.Count ?? 0;
        var lang = Provider.GetLang(this);
        counter.Text = total > 1 ? lang.SlideCounter(Index + 1, total) : "";
        previous.IsVisible = total > 1 && (Loop || Index > 0);
        next.IsVisible = total > 1 && (Loop || Index < total - 1);
        download.IsVisible = item is { Downloadable: true };
        caption.Content = item?.Caption is string text ? new Text { Text = text, Size = TextSize.Sm, Align = TextAlign.Center } : item?.Caption;
        caption.IsVisible = item?.Caption is not null;
        AutomationProperties.SetName(viewport, item?.Alt);
        if (image.Tag as Uri != item?.Source) _ = Load(item?.Source);
        if (Items is { } items)
            foreach (var step in new[] { -1, 1 })
                if (Index + step >= 0 && Index + step < items.Count) ImageLoader.LoadAsync(items[Index + step].Source).ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>读一张图：读到之前不显示（也不限制平移）；读完量原大，整张放进视口。换得快时只认最后一次。</summary>
    async Task Load(Uri? source)
    {
        var version = ++loading;
        image.Tag = source;
        image.Source = null;
        natural = null;
        rotation = 0;
        image.RenderTransform = null;
        if (source is null) return;
        try
        {
            var bitmap = await ImageLoader.LoadAsync(source);
            if (version != loading) return;
            image.Source = bitmap;
            natural = bitmap.Size;
            RequestFit();
        }
        catch (Exception error)
        {
            if (version == loading) System.Diagnostics.Trace.TraceWarning($"Lightbox 读不了图片 {source}：{error.Message}");
        }
    }

    /// <summary>下一次排版时整张放进视口（要等图片区量出大小）。</summary>
    void RequestFit()
    {
        fitPending = true;
        viewport.InvalidateMeasure();
        viewport.InvalidateArrange();
    }

    void FitIfPending()
    {
        if (!fitPending || natural is null || viewportSize.Width <= 0 || viewportSize.Height <= 0) return;
        fitPending = false;
        view.Fit((Box.Width, Box.Height), FitPadding);
    }

    /// <summary>上一张、下一张：到头时 Loop 才从另一头继续。</summary>
    void Go(int step)
    {
        var total = Items?.Count ?? 0;
        if (total < 2) return;
        var target = Index + step;
        if (target < 0 || target >= total)
        {
            if (!Loop) return;
            target = (target + total) % total;
        }
        SetCurrentValue(IndexProperty, target);
    }

    /// <summary>以 90° 为一档旋转，转完整张放进视口。</summary>
    void Rotate(int step)
    {
        rotation = (rotation + step * 90 + 360) % 360;
        image.RenderTransform = rotation == 0 ? null : new RotateTransform(rotation);
        RequestFit();
    }

    /// <summary>还原：方向回正、整张放进视口。</summary>
    void ResetView()
    {
        rotation = 0;
        image.RenderTransform = null;
        RequestFit();
    }

    /// <summary>下载：弹出保存对话框（建议的文件名取 DownloadName 或地址里的名字），存原始文件。</summary>
    async Task Download()
    {
        if (Current is not { } item || TopLevel.GetTopLevel(Panel)?.StorageProvider is not { CanSave: true } storage) return;
        try
        {
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = item.SuggestedFileName });
            if (file is null) return;
            var bytes = await ImageLoader.ReadAsync(item.Source);
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(bytes);
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceWarning($"Lightbox 下载 {item.Source} 失败：{error.Message}");
        }
    }

    /// <summary>← / → 上一张、下一张（从右到左排版时反过来），R 向右旋转；带 Ctrl、Alt、Win 的不管。</summary>
    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
        if (e.Key is Key.Left or Key.Right)
        {
            var rtl = FlowDirection == FlowDirection.RightToLeft;
            Go((e.Key == Key.Right) != rtl ? 1 : -1);
            e.Handled = true;
        }
        else if (e.Key == Key.R)
        {
            Rotate(1);
            e.Handled = true;
        }
    }

    /// <summary>平移、缩放变了：内容框跟着变换，比例跟着；拖动、滚轮之外（按钮、键盘、整张放进视口）平滑过渡。</summary>
    void UpdateTransform()
    {
        var state = view.State;
        var builder = TransformOperations.CreateBuilder(2);
        // Avalonia 按追加的顺序作用在点上（与 CSS 的 transform 列表相反）：先缩放、再平移
        builder.AppendScale(state.Scale, state.Scale);
        builder.AppendTranslate(state.X, state.Y);
        var duration = interacting ? TimeSpan.Zero : Motion.Duration(this, ThemeKeys.Duration.Md);
        content.Transitions = duration <= TimeSpan.Zero ? null : [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = duration }];
        content.RenderTransform = builder.Build();
        zoom.Text = $"{Math.Round(state.Scale * 100)}%";
    }

    void OnPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(viewport).Properties.IsLeftButtonPressed) return;
        viewport.Focus(NavigationMethod.Pointer);
        pressedAt = lastDrag = e.GetPosition(viewport);
        e.Pointer.Capture(viewport);
        e.Handled = true;
    }

    /// <summary>拖动按增量平移；挪开 3 以内不算拖（点一下、双击不会抖）。</summary>
    void OnMoved(PointerEventArgs e)
    {
        if (pressedAt is not { } from) return;
        var point = e.GetPosition(viewport);
        if (!interacting)
        {
            var moved = point - from;
            if (Math.Sqrt(moved.X * moved.X + moved.Y * moved.Y) < 3) return;
            interacting = true;
            viewport.Cursor = new Cursor(StandardCursorType.SizeAll);
        }
        var step = point - lastDrag;
        lastDrag = point;
        view.PanBy(step.X, step.Y);
    }

    void OnReleased()
    {
        pressedAt = null;
        interacting = false;
        viewport.Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>+ / = 放大、− 缩小、0 原大、↑ / ↓ 平移（← / → 是切换，在外层处理）。</summary>
    void OnViewportKey(KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
        PanZoomKey? key = e.Key switch
        {
            Key.OemPlus or Key.Add => PanZoomKey.ZoomIn,
            Key.OemMinus or Key.Subtract => PanZoomKey.ZoomOut,
            Key.D0 or Key.NumPad0 => PanZoomKey.Reset,
            Key.Up => PanZoomKey.Up,
            Key.Down => PanZoomKey.Down,
            _ => null,
        };
        if (key is not { } pressed) return;
        view.Press(pressed);
        e.Handled = true;
    }

    /// <summary>图片区：铺满剩下的空间、裁掉移出去的部分；内容框是旋转之后图片占的框，图片在框的中心旋转。</summary>
    sealed class Stage : Panel
    {
        readonly Lightbox owner;

        public Stage(Lightbox owner)
        {
            this.owner = owner;
            Focusable = true;
            ClipToBounds = true;
            Background = Brushes.Transparent;
            Cursor = new Cursor(StandardCursorType.Hand);
            PointerPressed += (_, e) => owner.OnPressed(e);
            PointerMoved += (_, e) => owner.OnMoved(e);
            PointerReleased += (_, _) => owner.OnReleased();
            PointerCaptureLost += (_, _) => owner.OnReleased();
            DoubleTapped += (_, e) =>
            {
                var point = e.GetPosition(this);
                owner.view.DoubleTap((point.X, point.Y));
                e.Handled = true;
            };
            PointerWheelChanged += (_, e) =>
            {
                var point = e.GetPosition(this);
                owner.view.Wheel(-e.Delta.Y, (point.X, point.Y));
                e.Handled = true;
            };
            KeyDown += (_, e) => owner.OnViewportKey(e);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var box = owner.Box;
            owner.content.Measure(box);
            owner.image.Measure(owner.natural ?? default);
            return default;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            owner.viewportSize = finalSize;
            var box = owner.Box;
            var natural = owner.natural ?? default;
            owner.content.Arrange(new Rect(box));
            owner.image.Arrange(new Rect((box.Width - natural.Width) / 2, (box.Height - natural.Height) / 2, natural.Width, natural.Height));
            return finalSize;
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new StagePeer(this);
    }

    /// <summary>图片区对读屏是一张图片（名称是 alt），里面的元素不另念。</summary>
    sealed class StagePeer(Control owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }

    static IconButton Tool(IconData icon) => new() { Icon = icon, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
}

/// <summary>Lightbox 的部件名（另见 <see cref="DialogParts"/>：面板是 PART_Panel）。</summary>
public static class LightboxParts
{
    /// <summary>工具栏（TS 的 Toolbar）。</summary>
    public const string Toolbar = "PART_Toolbar";

    /// <summary>计数（第 2 张，共 5 张）。</summary>
    public const string Counter = "PART_Counter";

    /// <summary>缩放比例。</summary>
    public const string Zoom = "PART_Zoom";

    /// <summary>图片区（TS 的 Viewport，propsViewport）。</summary>
    public const string Viewport = "PART_Viewport";

    /// <summary>平移缩放的内容框。</summary>
    public const string Content = "PART_Content";

    /// <summary>图片。</summary>
    public const string Image = "PART_Image";

    /// <summary>上一张。</summary>
    public const string Previous = "PART_Previous";

    /// <summary>下一张。</summary>
    public const string Next = "PART_Next";

    /// <summary>说明（TS 的 propsCaption）。</summary>
    public const string Caption = "PART_Caption";
}
