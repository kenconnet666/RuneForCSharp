using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Rune.Wpf;

/// <summary>
/// 空状态（vue-tsx-pro 的 Empty）：列表、表格、搜索结果没有内容时的说明。图标 + 标题 + 说明 + 操作，居中排列。
/// <code>
/// &lt;rune:Empty Title="还没有订单" Description="下单后会显示在这里。"&gt;&lt;rune:Empty.Action&gt;&lt;rune:Button&gt;去逛逛&lt;/rune:Button&gt;&lt;/rune:Empty.Action&gt;&lt;/rune:Empty&gt;
/// &lt;rune:Empty Icon="{x:Static rune:Lucide.SearchX}" Title="没有找到" Size="sm" /&gt;
/// </code>
/// 图标默认 Inbox（2.5em、次要文字色、strokeWidths.sm），写 {x:Null} 不显示；标题中等字重；说明最宽 32em、次要文字色；项之间 space.sm，
/// 操作上面再空 space.sm；上下内边距 space.xl、左右 space.lg，都按 Empty 的字号算（Size 改它，主题样式的触发器）。只是展示，不带读屏角色。
/// 部件样式键：<see cref="EmptyParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class Empty : CompositeControl
{
    /// <summary>图标，默认 Inbox；null 不显示。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(IconData), typeof(Empty), new FrameworkPropertyMetadata(Lucide.Inbox, OnPartChanged));

    /// <summary>标题。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Empty), new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>说明。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(Empty), new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>操作（如"新建"按钮）。</summary>
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(
        nameof(Action), typeof(object), typeof(Empty), new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>标题与说明之外的内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(Empty), new FrameworkPropertyMetadata(null, OnPartChanged));

    /// <summary>字号档位，内部尺寸随之缩放；放在下拉框、小卡片里用 sm。不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Empty), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    static readonly DependencyProperty BlockFactorProperty = DependencyProperty.Register("BlockFactor", typeof(double), typeof(Empty), new FrameworkPropertyMetadata(1.5, OnMetricsChanged));
    static readonly DependencyProperty InlineFactorProperty = DependencyProperty.Register("InlineFactor", typeof(double), typeof(Empty), new FrameworkPropertyMetadata(1d, OnMetricsChanged));
    static readonly DependencyProperty GapFactorProperty = DependencyProperty.Register("GapFactor", typeof(double), typeof(Empty), new FrameworkPropertyMetadata(0.5, OnMetricsChanged));

    readonly Border root = new();
    readonly Flex column = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Sm };
    readonly Icon icon = new();
    readonly Text title = new();
    readonly Paragraph description = new();
    readonly ContentPresenter content = new();
    readonly ContentPresenter action = new();

    static Empty()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Empty), new FrameworkPropertyMetadata(typeof(Empty)));
        FocusableProperty.OverrideMetadata(typeof(Empty), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Empty), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(Empty), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, OnMetricsChanged));
    }

    /// <summary>新建：图标、标题、说明、内容、操作排成一列。</summary>
    public Empty()
    {
        icon.SetResourceReference(StyleProperty, EmptyParts.Icon);
        title.SetResourceReference(StyleProperty, EmptyParts.Title);
        description.SetResourceReference(StyleProperty, EmptyParts.Description);
        action.SetResourceReference(StyleProperty, EmptyParts.Action);
        SetResourceReference(BlockFactorProperty, ThemeKeys.Space.Xl);
        SetResourceReference(InlineFactorProperty, ThemeKeys.Space.Lg);
        SetResourceReference(GapFactorProperty, ThemeKeys.Space.Sm);
        foreach (var part in new UIElement[] { icon, title, description, content, action }) column.Children.Add(part);
        root.Child = column;
        Root = root;
        Update();
    }

    /// <summary>图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>操作。</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <summary>标题与说明之外的内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Empty)d).Update();

    static void OnMetricsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Empty)d).UpdateMetrics();

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    /// <summary>没给的部分不占位置（不留间距）。</summary>
    void Update()
    {
        icon.Data = Icon;
        icon.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible;
        title.Text = Title;
        title.Visibility = string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
        description.Text = Description;
        description.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        content.Content = Content;
        content.Visibility = Content is null ? Visibility.Collapsed : Visibility.Visible;
        action.Content = Action;
        action.Visibility = Action is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateMetrics();
    }

    /// <summary>em 尺寸：图标 2.5em，说明最宽 32em，操作上面再空 space.sm，上下 space.xl、左右 space.lg。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        // 图标按自己的 Size 画（不看 Width、Height）：设成 2.5em 的像素数
        icon.Size = 2.5 * em;
        description.MaxWidth = 32 * em;
        action.Margin = new Thickness(0, (double)GetValue(GapFactorProperty) * em, 0, 0);
        root.Padding = new Thickness((double)GetValue(InlineFactorProperty) * em, (double)GetValue(BlockFactorProperty) * em, (double)GetValue(InlineFactorProperty) * em, (double)GetValue(BlockFactorProperty) * em);
    }

}

/// <summary>Empty 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:EmptyParts.Icon}" TargetType="rune:Icon"&gt;</c>。</summary>
public static class EmptyParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Empty), nameof(Icon), typeof(Icon));

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public static readonly PartKey Title = new(typeof(Empty), nameof(Title), typeof(Text));

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public static readonly PartKey Description = new(typeof(Empty), nameof(Description), typeof(Paragraph));

    /// <summary>操作区（TS 的 propsAction）。</summary>
    public static readonly PartKey Action = new(typeof(Empty), nameof(Action), typeof(ContentPresenter));
}
