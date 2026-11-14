using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 结果页（vue-tsx-pro 的 Result）：操作完成后的整块反馈（提交成功、支付失败、权限不足）。大图标（挂上时一笔画出）+ 标题 + 说明 + 操作，居中排列。
/// <code>
/// &lt;rune:Result Tone="success" Title="提交成功" Description="审核结果会通过邮件通知你。"&gt;&lt;rune:Result.Action&gt;&lt;rune:Button&gt;返回列表&lt;/rune:Button&gt;&lt;/rune:Result.Action&gt;&lt;/rune:Result&gt;
/// &lt;rune:Result Status="404" /&gt;   常见状态：图标、色调、状态码、标题与说明都有默认值，显式写的优先
/// </code>
/// 图标 4em、细线（strokeWidths.sm）、色调的颜色，下面多空 space.sm；状态码 2.5em 粗体、次要色、等宽数字，读屏跳过（标题已经说明了状态）；
/// 标题半粗、大一档（xl / md）、行高 tight；说明最宽 36em、次要色；内容（如错误详情）在说明下面；操作上面多空 space.md。
/// 项之间 space.sm，上下内边距 space.xl、左右 space.lg，都按 Result 的字号算（Size 改它）。
/// 只是展示，不带读屏角色；操作后才出现、需要播报时，由所在区域设实时区域或把焦点移到标题上。
/// 部件：<c>rune|Result rune|Icon#PART_Icon</c>、<c>rune|Text#PART_Code</c>、<c>rune|Text#PART_Title</c>、<c>rune|Paragraph#PART_Description</c>、<c>ContentPresenter#PART_Action</c>。
/// </summary>
public class Result : Control
{
    /// <summary>常见状态（403、404、500）：给出图标、色调、状态码与默认的标题、说明。</summary>
    public static readonly StyledProperty<ResultStatus> StatusProperty = AvaloniaProperty.Register<Result, ResultStatus>(nameof(Status));

    /// <summary>色调，默认 success（写了 Status 时按状态）。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Result, StatusTone>(nameof(Tone));

    /// <summary>图标；不写时按状态或色调选，写 null 不显示。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Result, IconData?>(nameof(Icon));

    /// <summary>挂上时描线画出图标，默认 true；系统关闭动画时直接画好。</summary>
    public static readonly StyledProperty<bool> DrawProperty = AvaloniaProperty.Register<Result, bool>(nameof(Draw), true);

    /// <summary>标题；不写时取状态的默认标题。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Result, string?>(nameof(Title));

    /// <summary>说明；不写时取状态的默认说明。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<Result, string?>(nameof(Description));

    /// <summary>操作（如"返回""重试"按钮）。</summary>
    public static readonly StyledProperty<object?> ActionProperty = AvaloniaProperty.Register<Result, object?>(nameof(Action));

    /// <summary>说明之后的内容（如错误详情）。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Result, object?>(nameof(Content));

    /// <summary>字号档位，内部尺寸随之缩放；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Result, TextSize>(nameof(Size));

    /// <summary>字号（继承），em 尺寸按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Result>();

    readonly Border root = new();
    readonly Flex column = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Icon icon = new() { Name = ResultParts.Icon };
    readonly Text code = new() { Name = ResultParts.Code };
    readonly Text title = new() { Name = ResultParts.Title };
    readonly Paragraph description = new() { Name = ResultParts.Description };
    readonly ContentPresenter content = new();
    readonly ContentPresenter action = new() { Name = ResultParts.Action };
    readonly TemplateResources resources;

    static Result() => AffectsMeasure<Result>(FontSizeProperty);

    /// <summary>新建：图标、状态码、标题、说明、内容、操作排成一列。</summary>
    public Result()
    {
        resources = new TemplateResources(this);
        AutomationProperties.SetAccessibilityView(code, AccessibilityView.Raw);
        column.Children.AddRange([icon, code, title, description, content, action]);
        root.Child = column;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        resources.Bind(TextElement.ForegroundProperty, ThemeKeys.Brush.Text);
        Update();
    }

    /// <summary>常见状态。</summary>
    public ResultStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>挂上时描线画出图标。</summary>
    public bool Draw { get => GetValue(DrawProperty); set => SetValue(DrawProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>操作。</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <summary>说明之后的内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>显示的色调：写了的 &gt; 状态的 &gt; success。</summary>
    internal StatusTone ShownTone => IsSet(ToneProperty) && Tone.Value is not null ? Tone : Status.Look.Tone ?? StatusTone.Success;

    /// <summary>显示的图标（测试用）；不显示时为 null。</summary>
    internal IconData? ShownIcon => icon.IsVisible ? icon.Data : null;

    /// <summary>图标部件（测试用）。</summary>
    internal Icon IconPart => icon;

    /// <summary>状态码部件（测试用）。</summary>
    internal Text CodePart => code;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == FontSizeProperty) UpdateMetrics();
        else if (change.Property == DrawProperty) icon.Draw = Draw;
        else if (change.Property == StatusProperty || change.Property == ToneProperty || change.Property == IconProperty || change.Property == TitleProperty
                 || change.Property == DescriptionProperty || change.Property == ActionProperty || change.Property == ContentProperty
                 || change.Property == Provider.LangProperty) Update();
    }

    /// <summary>显式写的优先，其次是状态的默认值（语言包）；没给的部分不占位置（不留间距）。</summary>
    void Update()
    {
        var text = Status.Value is { } key && Provider.GetLang(this).ResultStatus.TryGetValue(key, out var found) ? found : default;
        var tone = ShownTone;
        var shownIcon = IsSet(IconProperty) ? Icon : Status.Look.Icon ?? Alert.IconFor(tone);
        icon.Data = shownIcon;
        icon.IsVisible = shownIcon is not null;
        icon.Tone = new TextTone(tone.Value);
        icon.Draw = Draw;
        code.Text = Status.Value;
        code.IsVisible = Status.Value is not null;
        var shownTitle = Title ?? text.Title;
        title.Text = shownTitle;
        title.IsVisible = !string.IsNullOrEmpty(shownTitle);
        var shownDescription = Description ?? text.Description;
        description.Text = shownDescription;
        description.IsVisible = !string.IsNullOrEmpty(shownDescription);
        content.Content = Content;
        content.IsVisible = Content is not null;
        action.Content = Action;
        action.IsVisible = Action is not null;
        UpdateMetrics();
    }

    /// <summary>
    /// em 尺寸：图标 4em、下面多空 space.sm；状态码 2.5em、字距 tight；标题 xl / md；说明最宽 36em；操作上面多空 space.md；
    /// 上下 space.xl、左右 space.lg。
    /// </summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        // 图标按自己的 Size 画（不看 Width、Height）：设成 4em 的像素数
        icon.Size = 4 * em;
        icon.Margin = new Thickness(0, 0, 0, this.Resource<double>(ThemeKeys.Space.Sm) * em);
        code.FontSize = 2.5 * em;
        code.LetterSpacing = this.Resource<double>(ThemeKeys.LetterSpacing.Tight) * code.FontSize;
        title.FontSize = this.Resource<double>(ThemeKeys.FontSize.Xl) / this.Resource<double>(ThemeKeys.FontSize.Md) * em;
        description.MaxWidth = 36 * em;
        action.Margin = new Thickness(0, this.Resource<double>(ThemeKeys.Space.Md) * em, 0, 0);
        root.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Lg) * em, this.Resource<double>(ThemeKeys.Space.Xl) * em);
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
}

/// <summary>Result 的部件名。</summary>
public static class ResultParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>状态码（TS 的 propsCode）。</summary>
    public const string Code = "PART_Code";

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>操作区（TS 的 propsAction）。</summary>
    public const string Action = "PART_Action";
}
