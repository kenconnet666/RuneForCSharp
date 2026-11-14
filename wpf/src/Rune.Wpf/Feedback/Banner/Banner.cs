using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 横幅（vue-tsx-pro 的 Banner）：贴着页面（或区域）顶部的一行通栏通知——系统维护、试用到期、新功能上线、只读模式这类整页都相关的消息。
/// <code>
/// &lt;rune:Banner Tone="warning" Title="系统维护" CloseRequested="OnHide"&gt;今晚 22:00–23:00 暂停服务&lt;/rune:Banner&gt;
/// </code>
/// 和 <see cref="Alert"/> 的分工：Alert 放在内容里、可以有多行正文；Banner 占满宽度、一行说完，内容居中（窄时换行），没有圆角，放在页面最上方。
/// 排列：[图标][标题（中等字重，与正文同一行）正文][操作（中等字重）]，间距 space.sm（换行时 space.xs），上下 space.sm、左右 space.lg，行高 normal；
/// 关闭按钮贴在结束边（space.sm），不参与居中，有它时结束端多留 2em。颜色 = 样式（<see cref="Variant"/>：soft 默认、solid）× 色调（默认 info），
/// 是生成的 BannerRecipe.g.xaml。图标不写时按色调选，写 null 不显示；<see cref="Live"/> 同 Alert。订阅了 <see cref="CloseRequested"/> 才有关闭按钮：
/// 先淡出再请使用方移除，没移除就重新出现。部件样式键见 <see cref="BannerParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class Banner : Control
{
    /// <summary>样式，默认 soft。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(BannerVariant), typeof(Banner),
        new FrameworkPropertyMetadata(BannerVariant.Soft));

    /// <summary>色调，默认 info。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(Banner),
        new FrameworkPropertyMetadata(StatusTone.Info, OnPartChanged));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Banner),
        new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>中等字重的开头（"系统维护"），与正文在同一行。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Banner),
        new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>正文：文字，或放在文字行里的控件。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(Banner),
        new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>图标；不写时按色调选，写 null 不显示。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(Banner),
        new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>正文后的操作（链接或小按钮）。</summary>
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action), typeof(object), typeof(Banner),
        new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>读屏播报方式，默认 off（同 Alert）。</summary>
    public static readonly DependencyProperty LiveProperty = DependencyProperty.Register(nameof(Live), typeof(AlertLive), typeof(Banner),
        new FrameworkPropertyMetadata(AlertLive.Off, OnPartChanged));

    /// <summary>出现的程度（离场时从 1 到 0）：整块的透明度。</summary>
    static readonly DependencyProperty PresenceProperty = DependencyProperty.Register("Presence", typeof(double), typeof(Banner),
        new FrameworkPropertyMetadata(1d, (d, e) => ((Banner)d).root.Opacity = (double)e.NewValue));

    readonly Border root = new();
    readonly System.Windows.Controls.Grid layers = new();
    readonly Flex row = new() { Align = FlexAlign.Center, Justify = FlexJustify.Center, Wrap = FlexWrap.Wrap, Gap = Space.Sm, RowGap = Space.Xs };
    readonly Icon icon = new() { Name = "PART_Icon" };
    readonly TextBlock text = new() { Name = "PART_Content", TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
    readonly Run title = new();
    readonly ContentPresenter action = new() { Name = "PART_Action", VerticalAlignment = VerticalAlignment.Center };
    readonly Dismissal dismissal;
    EventHandler? closeRequested;
    CloseButton? close;

    static Banner()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Banner), new FrameworkPropertyMetadata(typeof(Banner)));
        FocusableProperty.OverrideMetadata(typeof(Banner), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Banner), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：图标、文字、操作居中排成一行（窄时换行），关闭按钮叠在结束边。</summary>
    public Banner()
    {
        icon.SetResourceReference(StyleProperty, BannerParts.Icon);
        text.SetResourceReference(StyleProperty, BannerParts.Content);
        action.SetResourceReference(StyleProperty, BannerParts.Action);
        title.SetResourceReference(TextElement.FontWeightProperty, ThemeKeys.FontWeight.Medium);
        foreach (var part in new UIElement[] { icon, text, action }) row.Children.Add(part);
        layers.Children.Add(row);
        root.Child = layers;
        root.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { Source = this });
        root.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { Source = this });
        root.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(BorderThickness)) { Source = this });
        root.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding(nameof(Padding)) { Source = this });
        // 字号显式绑过去：在 Gallery、inspect 的宿主里组件自己的子元素没有继承到它（同 Alert）
        root.SetBinding(TextElement.FontSizeProperty, new System.Windows.Data.Binding(nameof(FontSize)) { Source = this });
        AddVisualChild(root);
        AddLogicalChild(root);
        dismissal = new Dismissal(this, PresenceProperty, () => closeRequested?.Invoke(this, EventArgs.Empty));
        Loaded += (_, _) =>
        {
            UpdateMetrics();
            Announce();
        };
        Update();
    }

    /// <summary>样式。</summary>
    public BannerVariant Variant { get => (BannerVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>正文。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>操作。</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <summary>读屏播报方式。</summary>
    public AlertLive Live { get => (AlertLive)GetValue(LiveProperty); set => SetValue(LiveProperty, value); }

    /// <summary>请求关闭（TS 的 onClose）：离场结束才发，使用方移除横幅并记住"已关闭"。订阅了才有关闭按钮。</summary>
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

    /// <summary>正在离场。</summary>
    public bool IsDismissing => dismissal.IsDismissing;

    /// <summary>关闭按钮（测试用）；没有订阅时为 null。</summary>
    internal CloseButton? CloseButton => close;

    /// <summary>显示的图标（测试用）。</summary>
    internal IconData? ShownIcon => icon.Visibility == Visibility.Visible ? icon.Data : null;

    /// <summary>文字行（测试用）：标题与正文。</summary>
    internal TextBlock TextLine => text;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Banner)d).Update();

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != FontSizeProperty) return;
        UpdateMetrics();
        // 标题与正文之间的空白随字号
        Update();
    }

    /// <summary>文字行：标题与正文同一行（标题后空 space.sm），正文是控件时放在行里；没给的部分不占位置。</summary>
    void Update()
    {
        var shown = ReadLocalValue(IconProperty) == DependencyProperty.UnsetValue ? Alert.IconFor(Tone) : Icon;
        icon.Data = shown;
        icon.Visibility = shown is null ? Visibility.Collapsed : Visibility.Visible;
        text.Inlines.Clear();
        if (!string.IsNullOrEmpty(Title))
        {
            title.Text = Title;
            text.Inlines.Add(title);
            if (Content is not null) text.Inlines.Add(new InlineUIContainer(new FrameworkElement { Width = 0.5 * FontSize }));
        }
        switch (Content)
        {
            case null:
                break;
            case UIElement element:
                text.Inlines.Add(new InlineUIContainer(element) { BaselineAlignment = BaselineAlignment.Center });
                break;
            default:
                text.Inlines.Add(new Run(Content.ToString()));
                break;
        }
        text.Visibility = text.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        action.Content = Action;
        action.Visibility = Action is null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetLiveSetting(this, Live.Value switch
        {
            "polite" => AutomationLiveSetting.Polite,
            "assertive" => AutomationLiveSetting.Assertive,
            _ => AutomationLiveSetting.Off,
        });
        Announce();
    }

    /// <summary>实时区域：内容变了让读屏念（WPF 不会自己发）。</summary>
    void Announce()
    {
        if (Live == AlertLive.Off || !IsLoaded) return;
        (UIElementAutomationPeer.FromElement(this) ?? UIElementAutomationPeer.CreatePeerForElement(this))?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    /// <summary>em 尺寸：上下 space.sm、左右 space.lg（有关闭按钮时结束端多 2em），行高 normal；关闭按钮小一档、离结束边 space.sm。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        var (sm, lg) = (this.Resource<double>(ThemeKeys.Space.Sm) * em, this.Resource<double>(ThemeKeys.Space.Lg) * em);
        // 内边距随字号与有没有关闭按钮，写当前值（使用方的本地值、样式盖得过）
        SetCurrentValue(PaddingProperty, new Thickness(lg, sm, lg + (close is null ? 0 : 2 * em), sm));
        TextBlock.SetLineHeight(root, this.Resource<double>(ThemeKeys.LineHeight.Normal) * em);
        icon.Size = em;
        if (close is null) return;
        close.FontSize = this.Resource<double>(ThemeKeys.FontSize.Sm) / this.Resource<double>(ThemeKeys.FontSize.Md) * em;
        // 关闭按钮贴着横幅的结束边（不在内边距里）
        close.Margin = new Thickness(0, -sm, sm - lg - 2 * em, -sm);
    }

    /// <summary>订阅了关闭就放上关闭按钮（叠在结束边、竖直居中），退订了就拿掉。</summary>
    void SyncClose()
    {
        if (closeRequested is null)
        {
            if (close is not null) layers.Children.Remove(close);
            close = null;
            UpdateMetrics();
            return;
        }
        if (close is not null) return;
        close = new CloseButton { Name = "PART_Close", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        close.SetResourceReference(StyleProperty, BannerParts.Close);
        close.SetBinding(ForegroundProperty, new System.Windows.Data.Binding(nameof(Foreground)) { Source = this });
        // 悬停的底色取横幅的文字色（状态层，14%）：彩色横幅上不是一块灰
        close.MouseEnter += (_, _) => UpdateCloseBackground();
        close.MouseLeave += (_, _) => UpdateCloseBackground();
        close.Click += (_, _) => dismissal.Dismiss();
        layers.Children.Add(close);
        UpdateMetrics();
    }

    void UpdateCloseBackground()
    {
        if (close is null) return;
        if (close.IsMouseOver && Foreground is SolidColorBrush fore) close.Background = new SolidColorBrush(fore.Color) { Opacity = 0.14 * fore.Opacity };
        else close.ClearValue(BackgroundProperty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        root.Measure(constraint);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        root.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BannerPeer(this);

    /// <summary>读屏：一组（名称是标题）。</summary>
    sealed class BannerPeer(Banner owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Banner);

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Title ?? "";
    }
}

/// <summary>Banner 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:BannerParts.Content}" TargetType="TextBlock"&gt;</c>。</summary>
public static class BannerParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Banner), nameof(Icon), typeof(Icon));

    /// <summary>文字行：标题与正文（TS 的 propsContent）。</summary>
    public static readonly PartKey Content = new(typeof(Banner), nameof(Content), typeof(TextBlock));

    /// <summary>操作区（TS 的 propsAction）。</summary>
    public static readonly PartKey Action = new(typeof(Banner), nameof(Action), typeof(ContentPresenter));

    /// <summary>关闭按钮（TS 的 propsClose）。</summary>
    public static readonly PartKey Close = new(typeof(Banner), nameof(Close), typeof(CloseButton));
}
