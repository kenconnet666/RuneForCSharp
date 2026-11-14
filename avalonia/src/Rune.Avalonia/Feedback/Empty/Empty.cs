using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 空状态（vue-tsx-pro 的 Empty）：列表、表格、搜索结果没有内容时的说明。图标 + 标题 + 说明 + 操作，居中排列。
/// <code>
/// &lt;rune:Empty Title="还没有订单" Description="下单后会显示在这里。"&gt;&lt;rune:Empty.Action&gt;&lt;rune:Button&gt;去逛逛&lt;/rune:Button&gt;&lt;/rune:Empty.Action&gt;&lt;/rune:Empty&gt;
/// &lt;rune:Empty Icon="{x:Static rune:Lucide.SearchX}" Title="没有找到" Size="sm" /&gt;
/// </code>
/// 图标默认 Inbox（2.5em、次要文字色、strokeWidths.sm），写 null 不显示；标题中等字重；说明最宽 32em、次要文字色；项之间 space.sm，
/// 操作上面再空 space.sm；上下内边距 space.xl、左右 space.lg，都按 Empty 的字号算（Size 改它）。只是展示，不带读屏角色。
/// 部件：<c>rune|Empty rune|Icon#PART_Icon</c>、<c>rune|Text#PART_Title</c>、<c>rune|Paragraph#PART_Description</c>、<c>ContentPresenter#PART_Action</c>。
/// </summary>
public class Empty : Control
{
    /// <summary>图标，默认 Inbox；null 不显示。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Empty, IconData?>(nameof(Icon), Lucide.Inbox);

    /// <summary>标题。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Empty, string?>(nameof(Title));

    /// <summary>说明。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<Empty, string?>(nameof(Description));

    /// <summary>操作（如"新建"按钮）。</summary>
    public static readonly StyledProperty<object?> ActionProperty = AvaloniaProperty.Register<Empty, object?>(nameof(Action));

    /// <summary>标题与说明之外的内容。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Empty, object?>(nameof(Content));

    /// <summary>字号档位，内部尺寸随之缩放；放在下拉框、小卡片里用 sm。不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Empty, TextSize>(nameof(Size));

    /// <summary>字号（继承），em 尺寸按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Empty>();

    static readonly StyledProperty<double> BlockFactorProperty = AvaloniaProperty.Register<Empty, double>("BlockFactor", 1.5);
    static readonly StyledProperty<double> InlineFactorProperty = AvaloniaProperty.Register<Empty, double>("InlineFactor", 1);
    static readonly StyledProperty<double> GapFactorProperty = AvaloniaProperty.Register<Empty, double>("GapFactor", 0.5);

    readonly Border root = new();
    readonly Flex column = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Icon icon = new() { Name = EmptyParts.Icon };
    readonly Text title = new() { Name = EmptyParts.Title };
    readonly Paragraph description = new() { Name = EmptyParts.Description };
    readonly ContentPresenter content = new();
    readonly ContentPresenter action = new() { Name = EmptyParts.Action };
    readonly TemplateResources resources;

    static Empty() => AffectsMeasure<Empty>(FontSizeProperty);

    /// <summary>新建：图标、标题、说明、内容、操作排成一列。</summary>
    public Empty()
    {
        resources = new TemplateResources(this);
        column.Children.AddRange([icon, title, description, content, action]);
        root.Child = column;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        resources.Bind(TextElement.ForegroundProperty, ThemeKeys.Brush.Text);
        resources.Bind(BlockFactorProperty, ThemeKeys.Space.Xl);
        resources.Bind(InlineFactorProperty, ThemeKeys.Space.Lg);
        resources.Bind(GapFactorProperty, ThemeKeys.Space.Sm);
        Update();
    }

    /// <summary>图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>操作。</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <summary>标题与说明之外的内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == FontSizeProperty || change.Property == BlockFactorProperty || change.Property == InlineFactorProperty || change.Property == GapFactorProperty) UpdateMetrics();
        else if (change.Property == IconProperty || change.Property == TitleProperty || change.Property == DescriptionProperty
                 || change.Property == ActionProperty || change.Property == ContentProperty) Update();
    }

    /// <summary>没给的部分不占位置（不留间距）。</summary>
    void Update()
    {
        icon.Data = Icon;
        icon.IsVisible = Icon is not null;
        title.Text = Title;
        title.IsVisible = !string.IsNullOrEmpty(Title);
        description.Text = Description;
        description.IsVisible = !string.IsNullOrEmpty(Description);
        content.Content = Content;
        content.IsVisible = Content is not null;
        action.Content = Action;
        action.IsVisible = Action is not null;
        UpdateMetrics();
    }

    /// <summary>em 尺寸：图标 2.5em，说明最宽 32em，操作上面再空 space.sm（0.5em），上下 space.xl、左右 space.lg。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        // 图标按自己的 Size 画（不看 Width、Height）：设成 2.5em 的像素数
        icon.Size = 2.5 * em;
        description.MaxWidth = 32 * em;
        action.Margin = new Thickness(0, GetValue(GapFactorProperty) * em, 0, 0);
        root.Padding = new Thickness(GetValue(InlineFactorProperty) * em, GetValue(BlockFactorProperty) * em);
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

/// <summary>Empty 的部件名。</summary>
public static class EmptyParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>操作区（TS 的 propsAction）。</summary>
    public const string Action = "PART_Action";
}
