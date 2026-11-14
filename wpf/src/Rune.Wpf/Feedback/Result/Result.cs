using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Rune.Wpf;

/// <summary>
/// 结果页（vue-tsx-pro 的 Result）：操作完成后的整块反馈（提交成功、支付失败、权限不足）。大图标（加载时一笔画出）+ 标题 + 说明 + 操作，居中排列。
/// <code>
/// &lt;rune:Result Tone="success" Title="提交成功" Description="审核结果会通过邮件通知你。"&gt;&lt;rune:Result.Action&gt;&lt;rune:Button&gt;返回列表&lt;/rune:Button&gt;&lt;/rune:Result.Action&gt;&lt;/rune:Result&gt;
/// &lt;rune:Result Status="404" /&gt;   常见状态：图标、色调、状态码、标题与说明都有默认值，显式写的优先
/// </code>
/// 图标 4em、细线（strokeWidths.sm）、色调的颜色，下面多空 space.sm；状态码 2.5em 粗体、次要色、等宽数字，读屏跳过（标题已经说明了状态）；
/// 标题半粗、大一档（xl / md）、行高 tight；说明最宽 36em、次要色；内容（如错误详情）在说明下面；操作上面多空 space.md。
/// 项之间 space.sm，上下内边距 space.xl、左右 space.lg，都按 Result 的字号算（Size 改它，主题样式的触发器）。
/// 只是展示，不带读屏角色；操作后才出现、需要播报时，由所在区域设实时区域或把焦点移到标题上。WPF 的 TextBlock 没有字距，状态码不收紧字距。
/// 部件样式键：<see cref="ResultParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class Result : Control
{
    /// <summary>常见状态（403、404、500）：给出图标、色调、状态码与默认的标题、说明。</summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(ResultStatus), typeof(Result), new FrameworkPropertyMetadata(default(ResultStatus), OnPartChanged));

    /// <summary>色调，默认 success（写了 Status 时按状态）。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(Result), new FrameworkPropertyMetadata(default(StatusTone), OnPartChanged));

    /// <summary>图标；不写时按状态或色调选，写 {x:Null} 不显示。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(IconData), typeof(Result), new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>加载时描线画出图标，默认 true；系统关闭动画时直接画好。</summary>
    public static readonly DependencyProperty DrawProperty = DependencyProperty.Register(
        nameof(Draw), typeof(bool), typeof(Result), new FrameworkPropertyMetadata(true, OnPartChanged));

    /// <summary>标题；不写时取状态的默认标题。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Result), new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>说明；不写时取状态的默认说明。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(Result), new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>操作（如"返回""重试"按钮）。</summary>
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(
        nameof(Action), typeof(object), typeof(Result), new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>说明之后的内容（如错误详情）。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(Result), new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>字号档位，内部尺寸随之缩放；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Result), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    readonly Border root = new();
    readonly Flex column = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Icon icon = new() { Name = "PART_Icon" };
    readonly Text code = new() { Name = "PART_Code" };
    readonly Text title = new() { Name = "PART_Title" };
    readonly Paragraph description = new() { Name = "PART_Description" };
    readonly ContentPresenter content = new();
    readonly ContentPresenter action = new() { Name = "PART_Action" };

    static Result()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Result), new FrameworkPropertyMetadata(typeof(Result)));
        FocusableProperty.OverrideMetadata(typeof(Result), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Result), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(Result), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((Result)d).UpdateMetrics()));
    }

    /// <summary>新建：图标、状态码、标题、说明、内容、操作排成一列。</summary>
    public Result()
    {
        icon.SetResourceReference(StyleProperty, ResultParts.Icon);
        code.SetResourceReference(StyleProperty, ResultParts.Code);
        title.SetResourceReference(StyleProperty, ResultParts.Title);
        description.SetResourceReference(StyleProperty, ResultParts.Description);
        action.SetResourceReference(StyleProperty, ResultParts.Action);
        foreach (var part in new UIElement[] { icon, code, title, description, content, action }) column.Children.Add(part);
        root.Child = column;
        root.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding(nameof(Padding)) { Source = this });
        AddVisualChild(root);
        AddLogicalChild(root);
        Update();
    }

    /// <summary>常见状态。</summary>
    public ResultStatus Status { get => (ResultStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>加载时描线画出图标。</summary>
    public bool Draw { get => (bool)GetValue(DrawProperty); set => SetValue(DrawProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>操作。</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <summary>说明之后的内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>显示的色调：写了的 &gt; 状态的 &gt; success。</summary>
    internal StatusTone ShownTone => Tone.Value is not null ? Tone : Status.Look.Tone ?? StatusTone.Success;

    /// <summary>显示的图标（测试用）；不显示时为 null。</summary>
    internal IconData? ShownIcon => icon.Visibility == Visibility.Visible ? icon.Data : null;

    /// <summary>图标部件（测试用）。</summary>
    internal Icon IconPart => icon;

    /// <summary>状态码部件（测试用）。</summary>
    internal Text CodePart => code;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override System.Windows.Media.Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { root }.GetEnumerator();

    static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Result)d).Update();

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
        if (e.Property == Provider.LangProperty) Update();
    }

    /// <summary>显式写的优先，其次是状态的默认值（语言包）；没给的部分不占位置（不留间距）。"写没写图标"按本地值判断。</summary>
    void Update()
    {
        var text = Status.Value is { } key && Provider.GetLang(this).ResultStatus.TryGetValue(key, out var found) ? found : default;
        var tone = ShownTone;
        var shownIcon = ReadLocalValue(IconProperty) != DependencyProperty.UnsetValue ? Icon : Status.Look.Icon ?? Alert.IconFor(tone);
        icon.Data = shownIcon;
        icon.Visibility = Shown(shownIcon is not null);
        icon.Tone = new TextTone(tone.Value);
        icon.Draw = Draw;
        code.Text = Status.Value;
        code.Visibility = Shown(Status.Value is not null);
        var shownTitle = Title ?? text.Title;
        title.Text = shownTitle;
        title.Visibility = Shown(!string.IsNullOrEmpty(shownTitle));
        var shownDescription = Description ?? text.Description;
        description.Text = shownDescription;
        description.Visibility = Shown(!string.IsNullOrEmpty(shownDescription));
        content.Content = Content;
        content.Visibility = Shown(Content is not null);
        action.Content = Action;
        action.Visibility = Shown(Action is not null);
        UpdateMetrics();

        static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>em 尺寸：图标 4em、下面多空 space.sm；状态码 2.5em；标题 xl / md；说明最宽 36em；操作上面多空 space.md；上下 space.xl、左右 space.lg。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        // 图标按自己的 Size 画（不看 Width、Height）：设成 4em 的像素数
        icon.Size = 4 * em;
        icon.Margin = new Thickness(0, 0, 0, this.Resource<double>(ThemeKeys.Space.Sm) * em);
        code.FontSize = 2.5 * em;
        title.FontSize = this.Resource<double>(ThemeKeys.FontSize.Xl) / this.Resource<double>(ThemeKeys.FontSize.Md) * em;
        description.MaxWidth = 36 * em;
        action.Margin = new Thickness(0, this.Resource<double>(ThemeKeys.Space.Md) * em, 0, 0);
        // 内边距随字号，写当前值（使用方的本地值、样式盖得过）；inspect 也按 Result 自己的 Padding 报
        var (block, inline) = (this.Resource<double>(ThemeKeys.Space.Xl) * em, this.Resource<double>(ThemeKeys.Space.Lg) * em);
        SetCurrentValue(PaddingProperty, new Thickness(inline, block, inline, block));
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
    protected override AutomationPeer OnCreateAutomationPeer() => new ResultAutomationPeer(this);

    /// <summary>状态码对读屏是重复的（标题已经说明了状态）：从子元素里去掉。</summary>
    sealed class ResultAutomationPeer(Result owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Result);

        protected override List<AutomationPeer>? GetChildrenCore() =>
            base.GetChildrenCore()?.Where(peer => peer is not FrameworkElementAutomationPeer { Owner: var element } || element != owner.code).ToList();
    }
}

/// <summary>Result 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:ResultParts.Icon}" TargetType="rune:Icon"&gt;</c>。</summary>
public static class ResultParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Result), nameof(Icon), typeof(Icon));

    /// <summary>状态码（TS 的 propsCode）。</summary>
    public static readonly PartKey Code = new(typeof(Result), nameof(Code), typeof(Text));

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public static readonly PartKey Title = new(typeof(Result), nameof(Title), typeof(Text));

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public static readonly PartKey Description = new(typeof(Result), nameof(Description), typeof(Paragraph));

    /// <summary>操作区（TS 的 propsAction）。</summary>
    public static readonly PartKey Action = new(typeof(Result), nameof(Action), typeof(ContentPresenter));
}
