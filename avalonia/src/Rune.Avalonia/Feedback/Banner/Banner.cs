using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 横幅（vue-tsx-pro 的 Banner）：贴着页面（或区域）顶部的一行通栏通知——系统维护、试用到期、新功能上线、只读模式这类整页都相关的消息。
/// <code>
/// &lt;rune:Banner Tone="warning" Title="系统维护" CloseRequested="OnHide"&gt;今晚 22:00–23:00 暂停服务&lt;/rune:Banner&gt;
/// </code>
/// 和 <see cref="Alert"/> 的分工：Alert 放在内容里、可以有多行正文；Banner 占满宽度、一行说完，内容居中（窄时换行），没有圆角，放在页面最上方。
/// 排列：[图标][标题（中等字重，与正文同一行）正文][操作（中等字重）]，间距 space.sm（换行时 space.xs），上下 space.sm、左右 space.lg，行高 normal；
/// 关闭按钮贴在结束边（space.sm），不参与居中，有它时结束端多留 2em。颜色 = 样式（<see cref="Variant"/>：soft 默认、solid 实底最醒目）× 色调（默认 info）。
/// 图标不写时按色调选，写 null 不显示；<see cref="Live"/> 同 Alert。订阅了 <see cref="CloseRequested"/> 才有关闭按钮：先淡出再请使用方移除，没移除就重新出现。
/// 部件名见 <see cref="BannerParts"/>。
/// </summary>
public class Banner : Control
{
    /// <summary>样式，默认 soft。</summary>
    public static readonly StyledProperty<BannerVariant> VariantProperty = AvaloniaProperty.Register<Banner, BannerVariant>(nameof(Variant), BannerVariant.Soft);

    /// <summary>色调，默认 info。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Banner, StatusTone>(nameof(Tone), StatusTone.Info);

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Banner, TextSize>(nameof(Size));

    /// <summary>中等字重的开头（"系统维护"），与正文在同一行。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Banner, string?>(nameof(Title));

    /// <summary>正文：文字，或放在文字行里的控件。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Banner, object?>(nameof(Content));

    /// <summary>图标；不写时按色调选，写 null 不显示。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Banner, IconData?>(nameof(Icon));

    /// <summary>正文后的操作（链接或小按钮）。</summary>
    public static readonly StyledProperty<object?> ActionProperty = AvaloniaProperty.Register<Banner, object?>(nameof(Action));

    /// <summary>读屏播报方式，默认 off（同 Alert）。</summary>
    public static readonly StyledProperty<AlertLive> LiveProperty = AvaloniaProperty.Register<Banner, AlertLive>(nameof(Live), AlertLive.Off);

    /// <summary>字号。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Banner>();

    /// <summary>文字与图标的颜色（样式档位从调色板取）。</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Banner>();

    /// <summary>底色（样式档位从调色板取）。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<Banner>();

    /// <summary>出现的程度（离场时从 1 到 0）：整块的透明度。</summary>
    static readonly StyledProperty<double> PresenceProperty = AvaloniaProperty.Register<Banner, double>("Presence", 1);

    readonly Border root = new() { BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent };
    readonly Panel layers = new();
    readonly Flex row = new() { Align = FlexAlign.Center, Justify = FlexJustify.Center, Wrap = FlexWrap.Wrap, Gap = Space.Sm, RowGap = Space.Xs };
    readonly Icon icon = new() { Name = BannerParts.Icon };
    readonly TextBlock text = new() { Name = BannerParts.Content, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
    readonly Run title = new() { Name = BannerParts.Title };
    readonly ContentPresenter action = new() { Name = BannerParts.Action };
    readonly TemplateResources resources;
    readonly Dismissal dismissal;
    EventHandler? closeRequested;
    CloseButton? close;

    static Banner() => AffectsMeasure<Banner>(FontSizeProperty);

    /// <summary>新建：图标、文字、操作居中排成一行（窄时换行），关闭按钮叠在结束边。</summary>
    public Banner()
    {
        resources = new TemplateResources(this);
        row.Children.AddRange([icon, text, action]);
        layers.Children.Add(row);
        root.Child = layers;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        root.Bind(Border.BackgroundProperty, this.GetObservable(BackgroundProperty));
        title.Bind(TextElement.FontWeightProperty, title.GetResourceObservable("Rune.FontWeight.Medium"));
        action.Bind(TextElement.FontWeightProperty, action.GetResourceObservable("Rune.FontWeight.Medium"));
        dismissal = new Dismissal(this, PresenceProperty, () => closeRequested?.Invoke(this, EventArgs.Empty));
        Resolved.Bind(this, Resolved.PaletteProperty, Tone.PaletteKey);
        Update();
    }

    /// <summary>样式。</summary>
    public BannerVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>正文。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>操作。</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <summary>读屏播报方式。</summary>
    public AlertLive Live { get => GetValue(LiveProperty); set => SetValue(LiveProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>文字颜色。</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>底色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

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
    internal IconData? ShownIcon => icon.IsVisible ? icon.Data : null;

    /// <summary>文字行（测试用）：标题与正文。</summary>
    internal TextBlock TextLine => text;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == ToneProperty)
        {
            Resolved.Bind(this, Resolved.PaletteProperty, Tone.PaletteKey);
            Update();
        }
        else if (change.Property == VariantProperty || change.Property == Resolved.PaletteProperty) UpdateColors();
        else if (change.Property == ForegroundProperty) UpdateClosePalette();
        else if (change.Property == FontSizeProperty)
        {
            UpdateMetrics();
            // 标题与正文之间的空白随字号
            Update();
        }
        else if (change.Property == PresenceProperty) root.Opacity = GetValue(PresenceProperty);
        else if (change.Property == TitleProperty || change.Property == ContentProperty || change.Property == IconProperty
                 || change.Property == ActionProperty || change.Property == LiveProperty) Update();
    }

    /// <summary>文字行：标题与正文同一行（标题后空 space.sm），正文是控件时放在行里；没给的部分不占位置。</summary>
    void Update()
    {
        var shown = IsSet(IconProperty) ? Icon : Alert.IconFor(Tone);
        icon.Data = shown;
        icon.IsVisible = shown is not null;
        var inlines = text.Inlines!;
        inlines.Clear();
        if (!string.IsNullOrEmpty(Title))
        {
            title.Text = Title;
            inlines.Add(title);
            if (Content is not null) inlines.Add(new InlineUIContainer(new Control { Width = 0.5 * FontSize }));
        }
        switch (Content)
        {
            case null:
                break;
            case Control control:
                inlines.Add(new InlineUIContainer(control) { BaselineAlignment = BaselineAlignment.Center });
                break;
            default:
                inlines.Add(new Run(Content.ToString()));
                break;
        }
        text.IsVisible = inlines.Count > 0;
        action.Content = Action;
        action.IsVisible = Action is not null;
        AutomationProperties.SetLiveSetting(this, Live.Value switch
        {
            "polite" => AutomationLiveSetting.Polite,
            "assertive" => AutomationLiveSetting.Assertive,
            _ => AutomationLiveSetting.Off,
        });
    }

    /// <summary>样式档位取调色板的字段：soft 浅色底、solid 实底；按模板优先级设到自己的颜色属性上（使用方的样式盖得过）。</summary>
    void UpdateColors()
    {
        if (Resolved.GetPalette(this) is not { } p) return;
        var (fore, back) = Variant == BannerVariant.Solid ? (p.Contrast, p.Solid) : (p.Text, p.Soft);
        resources.Set(BackgroundProperty, back);
        resources.Set(ForegroundProperty, fore);
        UpdateClosePalette();
    }

    /// <summary>关闭按钮的悬停底色取横幅的文字色（状态层，14%）：彩色横幅上不是一块灰。</summary>
    void UpdateClosePalette()
    {
        if (close is not null && Foreground is ISolidColorBrush fore)
            close.Palette = new TonePalette { Text = fore, SubtleHover = new SolidColorBrush(fore.Color, 0.14 * fore.Opacity) };
    }

    /// <summary>em 尺寸：上下 space.sm、左右 space.lg（有关闭按钮时结束端多 2em），行高 normal；关闭按钮小一档、离结束边 space.sm。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        var (sm, lg) = (this.Resource("Rune.Space.Sm", 0.5) * em, this.Resource("Rune.Space.Lg", 1.0) * em);
        row.Margin = new Thickness(lg, sm, lg + (close is null ? 0 : 2 * em), sm);
        root.SetValue(TextBlock.LineHeightProperty, this.Resource("Rune.LineHeight.Normal", 1.5) * em);
        if (close is null) return;
        close.FontSize = this.Resource("Rune.FontSize.Sm", 13.0) / this.Resource("Rune.FontSize.Md", 14.0) * em;
        close.Margin = new Thickness(0, 0, sm, 0);
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
        close = new CloseButton { Name = BannerParts.Close, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        close.Click += (_, _) => dismissal.Dismiss();
        layers.Children.Add(close);
        UpdateColors();
        UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateColors();
        UpdateMetrics();
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BannerPeer(this);

    /// <summary>读屏：一组（名称是标题）。</summary>
    sealed class BannerPeer(Banner owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Title;
    }
}

/// <summary>Banner 的部件名：样式里用 <c>rune|Banner TextBlock#PART_Content</c> 这样选中。</summary>
public static class BannerParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>文字行：标题与正文（TS 的 propsContent）。</summary>
    public const string Content = "PART_Content";

    /// <summary>标题（TS 的 propsTitle，文字行里的一段 Run）。</summary>
    public const string Title = "PART_Title";

    /// <summary>操作区（TS 的 propsAction）。</summary>
    public const string Action = "PART_Action";

    /// <summary>关闭按钮（TS 的 propsClose）。</summary>
    public const string Close = "PART_Close";
}
