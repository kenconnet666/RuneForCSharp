using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 提示条（vue-tsx-pro 的 Alert）：页面或区块里的说明、成功、警告、错误信息。图标按色调自动选（Lucide），可以换或去掉；可以关闭。
/// <code>
/// &lt;rune:Alert Tone="warning" Title="即将过期"&gt;你的订阅将在 3 天后到期。&lt;/rune:Alert&gt;
/// &lt;rune:Alert Tone="danger" Live="assertive" CloseRequested="OnDismiss"&gt;保存失败，请重试&lt;/rune:Alert&gt;
/// </code>
/// 外观：一行排开图标、文字区（标题中等字重、说明是正文色、额外内容）、操作、关闭按钮，间距 space.sm，内边距 space.md，行高 normal；
/// 颜色 = 样式（<see cref="Variant"/>：soft 默认、surface、outline）× 色调（<see cref="Tone"/>，默认 info），是生成的 AlertRecipe.g.xaml（同 Badge 的色表）；圆角默认 md。
/// 说明写在 <see cref="Description"/> 或内容上；两个都给时内容是说明下面的额外内容。<see cref="Icon"/> 不写时按色调选，写 null 不显示；
/// <see cref="IsLoading"/> 时图标换成加载指示。操作后才出现的提示用 <see cref="Live"/> 让读屏播报（内容变了时发 LiveRegionChanged）。
/// 订阅了 <see cref="CloseRequested"/> 才有关闭按钮：点了先淡出，离场结束才发；使用方移除了，焦点交给相邻的可聚焦元素；没移除就重新显示。
/// 部件样式键见 <see cref="AlertParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class Alert : Control
{
    /// <summary>样式，默认 soft。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(AlertVariant), typeof(Alert),
        new FrameworkPropertyMetadata(AlertVariant.Soft));

    /// <summary>色调，默认 info。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(Alert),
        new FrameworkPropertyMetadata(StatusTone.Info, OnPartChanged));

    /// <summary>圆角，默认 md；贴着页面顶部的通栏写 none。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(Radius), typeof(Alert),
        new FrameworkPropertyMetadata(Radius.Md, OnMetricsChanged));

    /// <summary>字号档位：图标、内边距随之缩放；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Alert),
        new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    /// <summary>标题（中等字重的第一行）。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Alert),
        new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>说明（标题下面的正文）；不写时内容就是说明。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(object), typeof(Alert),
        new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>内容：没写说明时是说明，写了时是说明下面的额外内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(Alert),
        new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>图标；不写时按色调选，写 null 不显示。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(Alert),
        new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>进行中（如"正在上传"）：图标换成加载指示。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(Alert),
        new FrameworkPropertyMetadata(false, OnPartChanged));

    /// <summary>读屏播报方式，默认 off。</summary>
    public static readonly DependencyProperty LiveProperty = DependencyProperty.Register(nameof(Live), typeof(AlertLive), typeof(Alert),
        new FrameworkPropertyMetadata(AlertLive.Off, OnPartChanged));

    /// <summary>右侧的操作（如"重试"按钮）。</summary>
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action), typeof(object), typeof(Alert),
        new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>出现的程度（离场时从 1 到 0）：整块的透明度。</summary>
    static readonly DependencyProperty PresenceProperty = DependencyProperty.Register("Presence", typeof(double), typeof(Alert),
        new FrameworkPropertyMetadata(1d, (d, e) => ((Alert)d).root.Opacity = (double)e.NewValue));

    readonly Border root = new();
    readonly Flex row = new() { Align = FlexAlign.Start, Gap = Space.Sm };
    readonly Icon icon = new() { Name = "PART_Icon" };
    readonly Spinner spinner = new() { Name = "PART_Spinner", Visibility = Visibility.Collapsed };
    readonly Flex body = new() { Name = "PART_Body", Direction = FlexDirection.Column, Gap = Space.Xs };
    readonly Text title = new() { Name = "PART_Title" };
    readonly ContentPresenter description = new() { Name = "PART_Description" };
    readonly ContentPresenter content = new() { Name = "PART_Content" };
    readonly ContentPresenter action = new() { Name = "PART_Action", VerticalAlignment = VerticalAlignment.Center };
    readonly Dismissal dismissal;
    EventHandler? closeRequested;
    CloseButton? close;

    static Alert()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Alert), new FrameworkPropertyMetadata(typeof(Alert)));
        FocusableProperty.OverrideMetadata(typeof(Alert), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Alert), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：图标、文字区、操作排成一行。</summary>
    public Alert()
    {
        icon.SetResourceReference(StyleProperty, AlertParts.Icon);
        spinner.SetResourceReference(StyleProperty, AlertParts.Spinner);
        body.SetResourceReference(StyleProperty, AlertParts.Body);
        title.SetResourceReference(StyleProperty, AlertParts.Title);
        description.SetResourceReference(StyleProperty, AlertParts.Description);
        content.SetResourceReference(StyleProperty, AlertParts.Content);
        action.SetResourceReference(StyleProperty, AlertParts.Action);
        foreach (var part in new UIElement[] { title, description, content }) body.Children.Add(part);
        foreach (var part in new UIElement[] { icon, spinner, body, action }) row.Children.Add(part);
        Flex.SetGrow(body, true);
        root.Child = row;
        root.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { Source = this });
        root.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { Source = this });
        root.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(BorderThickness)) { Source = this });
        root.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding(nameof(Padding)) { Source = this });
        // 字号显式绑过去：在 Gallery、inspect 的宿主里组件自己的子元素没有继承到它（测试的宿主里能继承，原因没查清）
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
    public AlertVariant Variant { get => (AlertVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>进行中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>读屏播报方式。</summary>
    public AlertLive Live { get => (AlertLive)GetValue(LiveProperty); set => SetValue(LiveProperty, value); }

    /// <summary>操作。</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

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
    internal IconData? ShownIcon => icon.Visibility == Visibility.Visible ? icon.Data : null;

    /// <summary>文字区的各部分（测试用）。</summary>
    internal (Text Title, ContentPresenter Description, ContentPresenter Content, ContentPresenter Action) Parts => (title, description, content, action);

    /// <summary>每种色调默认的图标（vue-tsx-pro 的 statusIcons）：提示条、横幅按色调自动选。</summary>
    internal static IconData IconFor(StatusTone tone) => tone.Value switch
    {
        "success" => Lucide.CircleCheck,
        "warning" => Lucide.TriangleExclamationPoint,
        "danger" => Lucide.CircleExclamationPoint,
        _ => Lucide.Info,
    };

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) UpdateMetrics();
    }

    static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Alert)d).Update();

    static void OnMetricsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Alert)d).UpdateMetrics();

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    /// <summary>没给的部分不占位置；说明取 Description，没有时取内容，两个都有时内容放在说明下面。</summary>
    void Update()
    {
        var shown = ReadLocalValue(IconProperty) == DependencyProperty.UnsetValue ? IconFor(Tone) : Icon;
        spinner.Visibility = IsLoading ? Visibility.Visible : Visibility.Collapsed;
        icon.Data = shown;
        icon.Visibility = !IsLoading && shown is not null ? Visibility.Visible : Visibility.Collapsed;
        title.Text = Title;
        title.Visibility = string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
        var both = Description is not null && Content is not null;
        description.Content = Description ?? Content;
        description.Visibility = description.Content is null ? Visibility.Collapsed : Visibility.Visible;
        content.Content = both ? Content : null;
        content.Visibility = both ? Visibility.Visible : Visibility.Collapsed;
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

    /// <summary>em 尺寸：内边距 space.md、行高 normal；图标 1em，与第一行文字对齐（上下各 0.25em）；关闭按钮小一档、上下各收 space.xs；圆角按档位。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        double Resource(string key, double fallback) => TryFindResource(key) as double? ?? fallback;
        root.CornerRadius = new CornerRadius((Radius.ResourceKey is { } key ? Resource(key, 0.375) : 0.375) * em);
        // 内边距随字号，写当前值（使用方的本地值、样式盖得过）；EmSize 的登记会换掉字号的继承，不用它
        SetCurrentValue(PaddingProperty, new Thickness(Resource("Rune.Space.Md", 0.75) * em));
        TextBlock.SetLineHeight(root, Resource("Rune.LineHeight.Normal", 1.5) * em);
        icon.Size = em;
        icon.Margin = spinner.Margin = new Thickness(0, 0.25 * em, 0, 0.25 * em);
        if (close is null) return;
        // 小一档（TS 的 relativeFontSizeStyles.sm：sm / md）
        close.FontSize = Resource("Rune.FontSize.Sm", 13) / Resource("Rune.FontSize.Md", 14) * em;
        var xs = Resource("Rune.Space.Xs", 0.25) * em;
        close.Margin = new Thickness(0, -xs, 0, -xs);
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
        close = new CloseButton { Name = "PART_Close", VerticalAlignment = VerticalAlignment.Top };
        close.SetResourceReference(StyleProperty, AlertParts.Close);
        close.SetBinding(ForegroundProperty, new System.Windows.Data.Binding(nameof(Foreground)) { Source = this });
        // 悬停的底色取提示条的文字色（状态层，14%）：彩色提示条上不是一块灰；用按钮自己的事件，不留静态的属性监听
        close.MouseEnter += (_, _) => UpdateCloseBackground();
        close.MouseLeave += (_, _) => UpdateCloseBackground();
        close.Click += (_, _) => dismissal.Dismiss();
        row.Children.Add(close);
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
    protected override AutomationPeer OnCreateAutomationPeer() => new AlertPeer(this);

    /// <summary>读屏：一组（名称是标题），进行中时带"忙"的状态文字。</summary>
    sealed class AlertPeer(Alert owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Alert);

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Title ?? "";

        protected override string GetItemStatusCore() => owner.IsLoading ? Provider.GetLang(owner).Loading : base.GetItemStatusCore();
    }
}

/// <summary>Alert 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:AlertParts.Title}" TargetType="rune:Text"&gt;</c>。</summary>
public static class AlertParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Alert), nameof(Icon), typeof(Icon));

    /// <summary>加载指示（TS 的 propsSpinner）：进行中时代替图标。</summary>
    public static readonly PartKey Spinner = new(typeof(Alert), nameof(Spinner), typeof(Spinner));

    /// <summary>文字区：标题与正文的容器（TS 的 propsBody）。</summary>
    public static readonly PartKey Body = new(typeof(Alert), nameof(Body), typeof(Flex));

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public static readonly PartKey Title = new(typeof(Alert), nameof(Title), typeof(Text));

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public static readonly PartKey Description = new(typeof(Alert), nameof(Description), typeof(ContentPresenter));

    /// <summary>额外内容（TS 的 propsContent）：同时给了说明与内容时的内容。</summary>
    public static readonly PartKey Content = new(typeof(Alert), nameof(Content), typeof(ContentPresenter));

    /// <summary>操作区（TS 的 propsAction）。</summary>
    public static readonly PartKey Action = new(typeof(Alert), nameof(Action), typeof(ContentPresenter));

    /// <summary>关闭按钮（TS 的 propsClose）。</summary>
    public static readonly PartKey Close = new(typeof(Alert), nameof(Close), typeof(CloseButton));
}
