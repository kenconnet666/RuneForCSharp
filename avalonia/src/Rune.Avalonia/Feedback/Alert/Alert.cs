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
/// 提示条（vue-tsx-pro 的 Alert）：页面或区块里的说明、成功、警告、错误信息。图标按色调自动选（Lucide），可以换或去掉；可以关闭。
/// <code>
/// &lt;rune:Alert Tone="warning" Title="即将过期"&gt;你的订阅将在 3 天后到期。&lt;/rune:Alert&gt;
/// &lt;rune:Alert Tone="danger" Live="assertive" CloseRequested="OnDismiss"&gt;保存失败，请重试&lt;/rune:Alert&gt;
/// </code>
/// 外观：一行排开图标、文字区（标题中等字重、说明是正文色、额外内容）、操作、关闭按钮，间距 space.sm，内边距 space.md，行高 normal；
/// 颜色 = 样式（<see cref="Variant"/>：soft 默认、surface、outline）× 色调（<see cref="Tone"/>，默认 info），同 Badge 的色表；圆角默认 md。
/// 说明写在 <see cref="Description"/> 或内容上；两个都给时内容是说明下面的额外内容。<see cref="Icon"/> 不写时按色调选，写 null 不显示；
/// <see cref="IsLoading"/> 时图标换成加载指示。页面加载时就在的提示不需要读屏打断；操作后才出现的用 <see cref="Live"/> 让读屏播报。
/// 订阅了 <see cref="CloseRequested"/> 才有关闭按钮：点了先淡出，离场结束才发；使用方移除了，焦点交给相邻的可聚焦元素；没移除就重新显示。
/// 部件名见 <see cref="AlertParts"/>。
/// </summary>
public class Alert : Control
{
    /// <summary>样式，默认 soft。</summary>
    public static readonly StyledProperty<AlertVariant> VariantProperty = AvaloniaProperty.Register<Alert, AlertVariant>(nameof(Variant), AlertVariant.Soft);

    /// <summary>色调，默认 info。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Alert, StatusTone>(nameof(Tone), StatusTone.Info);

    /// <summary>圆角，默认 md；贴着页面顶部的通栏写 none。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<Alert, Radius>(nameof(Radius), Radius.Md);

    /// <summary>字号档位：图标、内边距随之缩放；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Alert, TextSize>(nameof(Size));

    /// <summary>标题（中等字重的第一行）。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Alert, string?>(nameof(Title));

    /// <summary>说明（标题下面的正文）；不写时内容就是说明。</summary>
    public static readonly StyledProperty<object?> DescriptionProperty = AvaloniaProperty.Register<Alert, object?>(nameof(Description));

    /// <summary>内容：没写说明时是说明，写了时是说明下面的额外内容。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Alert, object?>(nameof(Content));

    /// <summary>图标；不写时按色调选，写 null 不显示。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Alert, IconData?>(nameof(Icon));

    /// <summary>进行中（如"正在上传"）：图标换成加载指示。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Alert, bool>(nameof(IsLoading));

    /// <summary>读屏播报方式，默认 off。</summary>
    public static readonly StyledProperty<AlertLive> LiveProperty = AvaloniaProperty.Register<Alert, AlertLive>(nameof(Live), AlertLive.Off);

    /// <summary>右侧的操作（如"重试"按钮）。</summary>
    public static readonly StyledProperty<object?> ActionProperty = AvaloniaProperty.Register<Alert, object?>(nameof(Action));

    /// <summary>字号。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Alert>();

    /// <summary>文字与图标的颜色（继承给子元素；样式档位从调色板取）。</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Alert>();

    /// <summary>底色（样式档位从调色板取，outline 时为 null）。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<Alert>();

    /// <summary>描边色（样式档位从调色板取，soft 时为 null，边框仍占位）。</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty = Border.BorderBrushProperty.AddOwner<Alert>();

    /// <summary>出现的程度（离场时从 1 到 0）：整块的透明度。</summary>
    static readonly StyledProperty<double> PresenceProperty = AvaloniaProperty.Register<Alert, double>("Presence", 1);

    readonly Border root = new();
    readonly Flex row = new() { Align = FlexAlign.Start, Gap = Space.Sm };
    readonly Icon icon = new() { Name = AlertParts.Icon };
    readonly Spinner spinner = new() { Name = AlertParts.Spinner, IsVisible = false };
    readonly Flex body = new() { Name = AlertParts.Body, Direction = FlexDirection.Column, Gap = Space.Xs };
    readonly Text title = new() { Name = AlertParts.Title };
    readonly ContentPresenter description = new() { Name = AlertParts.Description };
    readonly ContentPresenter content = new() { Name = AlertParts.Content };
    readonly ContentPresenter action = new() { Name = AlertParts.Action, VerticalAlignment = VerticalAlignment.Center };
    readonly TemplateResources resources;
    readonly Dismissal dismissal;
    EventHandler? closeRequested;
    CloseButton? close;

    static Alert() => AffectsMeasure<Alert>(FontSizeProperty);

    /// <summary>新建：图标、文字区、操作排成一行；色调、圆角按档位解析成主题资源。</summary>
    public Alert()
    {
        resources = new TemplateResources(this);
        Flex.SetGrow(body, true);
        body.Children.AddRange([title, description, content]);
        row.Children.AddRange([icon, spinner, body, action]);
        root.Child = row;
        // 每种样式都留出边框的位置（浅色底时透明），切换样式时尺寸不变
        root.BorderThickness = new Thickness(1);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        root.Bind(Border.BackgroundProperty, this.GetObservable(BackgroundProperty));
        root.Bind(Border.BorderBrushProperty, this.GetObservable(BorderBrushProperty));
        title.Bind(TextBlock.FontWeightProperty, title.GetResourceObservable(ThemeKeys.FontWeight.Medium));
        // 说明是正文色（标题、图标随色调）
        description.Bind(TextElement.ForegroundProperty, description.GetResourceObservable(ThemeKeys.Brush.Text));
        dismissal = new Dismissal(this, PresenceProperty, () => closeRequested?.Invoke(this, EventArgs.Empty));
        Resolved.Bind(this, Resolved.PaletteProperty, Tone.PaletteKey);
        Resolved.Bind(this, Resolved.RadiusProperty, Radius.ResourceKey);
        Update();
    }

    /// <summary>样式。</summary>
    public AlertVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>进行中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>读屏播报方式。</summary>
    public AlertLive Live { get => GetValue(LiveProperty); set => SetValue(LiveProperty, value); }

    /// <summary>操作。</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>文字颜色。</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>底色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>描边色。</summary>
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>请求关闭（TS 的 onClose）：离场结束才发，使用方移除提示条。订阅了才有关闭按钮。</summary>
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

    /// <summary>显示的图标（测试用）：写了 null 或加载中时为 null。</summary>
    internal IconData? ShownIcon => icon.IsVisible ? icon.Data : null;

    /// <summary>每种色调默认的图标（vue-tsx-pro 的 statusIcons）：提示条、横幅按色调自动选。</summary>
    internal static IconData IconFor(StatusTone tone) => tone.Value switch
    {
        "success" => Lucide.CircleCheck,
        "warning" => Lucide.TriangleExclamationPoint,
        "danger" => Lucide.CircleExclamationPoint,
        _ => Lucide.Info,
    };

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
        else if (change.Property == RadiusProperty) Resolved.Bind(this, Resolved.RadiusProperty, Radius.ResourceKey);
        else if (change.Property == VariantProperty || change.Property == Resolved.PaletteProperty) UpdateColors();
        else if (change.Property == FontSizeProperty || change.Property == Resolved.RadiusProperty) UpdateMetrics();
        else if (change.Property == PresenceProperty) root.Opacity = GetValue(PresenceProperty);
        else if (change.Property == ForegroundProperty) UpdateClosePalette();
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == ContentProperty
                 || change.Property == IconProperty || change.Property == IsLoadingProperty || change.Property == LiveProperty || change.Property == ActionProperty) Update();
    }

    /// <summary>没给的部分不占位置；说明取 Description，没有时取内容，两个都有时内容放在说明下面。</summary>
    void Update()
    {
        var shown = IsSet(IconProperty) ? Icon : IconFor(Tone);
        spinner.IsVisible = IsLoading;
        icon.Data = shown;
        icon.IsVisible = !IsLoading && shown is not null;
        title.Text = Title;
        title.IsVisible = !string.IsNullOrEmpty(Title);
        var both = Description is not null && Content is not null;
        description.Content = Description ?? Content;
        description.IsVisible = description.Content is not null;
        content.Content = both ? Content : null;
        content.IsVisible = both;
        action.Content = Action;
        action.IsVisible = Action is not null;
        AutomationProperties.SetLiveSetting(this, Live.Value switch
        {
            "polite" => AutomationLiveSetting.Polite,
            "assertive" => AutomationLiveSetting.Assertive,
            _ => AutomationLiveSetting.Off,
        });
        UpdateCloseLabel();
    }

    /// <summary>
    /// 样式档位取调色板的字段（同 Badge）：文字、图标随色调，底色与描边按样式；按模板优先级设到自己的颜色属性上（使用方的样式盖得过）。
    /// </summary>
    void UpdateColors()
    {
        if (Resolved.GetPalette(this) is not { } p) return;
        var (back, border) = Variant.Value switch
        {
            "surface" => (p.Surface, p.Border),
            "outline" => ((IBrush?)null, p.Border),
            _ => (p.Soft, (IBrush?)null),
        };
        resources.Set(BackgroundProperty, back);
        resources.Set(BorderBrushProperty, border);
        resources.Set(ForegroundProperty, p.Text);
        UpdateClosePalette();
    }

    /// <summary>关闭按钮的悬停底色取提示条的文字色（状态层，14%）：彩色提示条上不是一块灰。</summary>
    void UpdateClosePalette()
    {
        if (close is not null && Foreground is ISolidColorBrush fore)
            close.Palette = new TonePalette { Text = fore, SubtleHover = new SolidColorBrush(fore.Color, 0.14 * fore.Opacity) };
    }

    /// <summary>em 尺寸：内边距 space.md、行高 normal；图标 1em，与第一行文字对齐（上下各 0.25em）；关闭按钮 sm、上下各收 space.xs；圆角按档位。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        root.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Md) * em);
        root.CornerRadius = new CornerRadius((Resolved.GetRadius(this) ?? 0.375) * em);
        root.SetValue(TextBlock.LineHeightProperty, this.Resource<double>(ThemeKeys.LineHeight.Normal) * em);
        icon.Margin = spinner.Margin = new Thickness(0, 0.25 * em);
        if (close is not null)
        {
            // 小一档（TS 的 relativeFontSizeStyles.sm：sm / md）
            close.FontSize = this.Resource<double>(ThemeKeys.FontSize.Sm) / this.Resource<double>(ThemeKeys.FontSize.Md) * em;
            var xs = this.Resource<double>(ThemeKeys.Space.Xs) * em;
            close.Margin = new Thickness(0, -xs);
        }
    }

    /// <summary>订阅了关闭就放上关闭按钮，退订了就拿掉。</summary>
    void SyncClose()
    {
        if (closeRequested is null)
        {
            if (close is not null) row.Children.Remove(close);
            close = null;
            return;
        }
        if (close is not null) return;
        close = new CloseButton { Name = AlertParts.Close, VerticalAlignment = VerticalAlignment.Top };
        close.Click += (_, _) => dismissal.Dismiss();
        row.Children.Add(close);
        UpdateCloseLabel();
        UpdateColors();
        UpdateMetrics();
    }

    void UpdateCloseLabel()
    {
        if (close is not null) AutomationProperties.SetName(close, Provider.GetLang(this).Close);
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
    protected override AutomationPeer OnCreateAutomationPeer() => new AlertPeer(this);

    /// <summary>读屏：一组（名称是标题），进行中时带"忙"的状态文字；播报方式是实时区域。</summary>
    sealed class AlertPeer(Alert owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Title;

        protected override string? GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();
    }
}

/// <summary>Alert 的部件名：样式里用 <c>rune|Alert rune|Icon#PART_Icon</c> 这样选中。</summary>
public static class AlertParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>加载指示（TS 的 propsSpinner）：进行中时代替图标。</summary>
    public const string Spinner = "PART_Spinner";

    /// <summary>文字区：标题与正文的容器（TS 的 propsBody）。</summary>
    public const string Body = "PART_Body";

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>额外内容（TS 的 propsContent）：同时给了说明与内容时的内容。</summary>
    public const string Content = "PART_Content";

    /// <summary>操作区（TS 的 propsAction）。</summary>
    public const string Action = "PART_Action";

    /// <summary>关闭按钮（TS 的 propsClose）。</summary>
    public const string Close = "PART_Close";
}
