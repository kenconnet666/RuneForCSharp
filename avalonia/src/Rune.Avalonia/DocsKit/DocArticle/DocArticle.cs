using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 文档页的骨架（vue-tsx-pro docs-kit 的 DocArticle）：标题、简介与正文。正文里一节一节是 <see cref="DocSection"/>。
/// <code>
/// &lt;rune:DocArticle Title="快速开始" Description="两分钟跑起来"&gt;&lt;rune:DocSection SectionId="install" Title="安装"&gt;…&lt;/rune:DocSection&gt;&lt;/rune:DocArticle&gt;
/// </code>
/// 长文阅读：字号 15（比界面文字大一号），行高 relaxed；头部与正文之间、正文各节之间 3rem，头部里各项之间 space.lg。
/// 简介是 lg 档、次要色的段落，最宽 48rem（宽版面时也保持适合阅读的行宽）。页面的宽度由外面的 Container 决定。
/// 部件：<c>rune|DocArticle rune|Flex#PART_Header</c>、<c>rune|H1#PART_Title</c>、<c>rune|Paragraph#PART_Description</c>。
/// </summary>
public class DocArticle : Control
{
    /// <summary>标题。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<DocArticle, string?>(nameof(Title));

    /// <summary>简介。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<DocArticle, string?>(nameof(Description));

    /// <summary>简介下面、正文之前的内容：导入语句之类。</summary>
    public static readonly StyledProperty<object?> LeadProperty = AvaloniaProperty.Register<DocArticle, object?>(nameof(Lead));

    readonly Flex root = new() { Direction = FlexDirection.Column };
    readonly Flex header = new() { Name = DocArticleParts.Header, Direction = FlexDirection.Column, Gap = Space.Lg };
    readonly H1 title = new() { Name = DocArticleParts.Title };
    // 靠起始边：设了 MaxWidth 时拉伸的元素会被居中
    readonly Paragraph description = new() { Name = DocArticleParts.Description, Size = TextSize.Lg, Tone = TextTone.Muted, MaxWidth = 48 * 16, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
    readonly ContentControl lead = new() { Focusable = false };

    /// <summary>新建：没有内容。</summary>
    public DocArticle()
    {
        header.Children.AddRange([title, description, lead]);
        root.Children.Add(header);
        Content.CollectionChanged += (_, _) => Rebuild();
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        // 长文阅读：比界面文字（14px）大一号、行距宽松；按模板优先级，使用方的样式盖得过
        SetValue(TextElement.FontSizeProperty, 15d, BindingPriority.Template);
        SetValue(Leading.FactorProperty, this.Resource<double>(ThemeKeys.LineHeight.Relaxed), BindingPriority.Template);
        Update();
    }

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>简介。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>简介下面、正文之前的内容。</summary>
    public object? Lead { get => GetValue(LeadProperty); set => SetValue(LeadProperty, value); }

    /// <summary>正文（一般是一节一节的 DocSection）。</summary>
    [Content]
    public Controls Content { get; } = [];

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == LeadProperty) Update();
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

    void Update()
    {
        title.Text = Title;
        title.IsVisible = Title is not null;
        description.Text = Description;
        description.IsVisible = !string.IsNullOrEmpty(Description);
        lead.Content = Lead;
        lead.IsVisible = Lead is not null;
    }

    void Rebuild()
    {
        root.Children.RemoveRange(1, root.Children.Count - 1);
        // 头部与正文、正文各节之间 3rem：按根字号（16）算，不随文章的字号
        foreach (var child in Content) child.Margin = new Thickness(0, 3 * 16, 0, 0);
        root.Children.AddRange(Content);
    }
}

/// <summary>
/// 文档页的一节（vue-tsx-pro docs-kit 的 DocSection）：带锚点的标题（H2，xl 档）与内容，之间 space.lg。
/// <see cref="SectionId"/> 是锚点，也是本页目录（TableOfContents）里这一节的 id。部件：<c>rune|DocSection rune|H2#PART_Title</c>。
/// </summary>
public class DocSection : Control
{
    /// <summary>标题的 id（TS 的 id）：锚点，也进本页目录。</summary>
    public static readonly StyledProperty<string?> SectionIdProperty = AvaloniaProperty.Register<DocSection, string?>(nameof(SectionId));

    /// <summary>标题。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<DocSection, string?>(nameof(Title));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Lg };
    readonly H2 title = new() { Name = DocSectionParts.Title, Size = TextSize.Xl };

    /// <summary>新建：没有内容。</summary>
    public DocSection()
    {
        root.Children.Add(title);
        Content.CollectionChanged += (_, _) =>
        {
            root.Children.RemoveRange(1, root.Children.Count - 1);
            root.Children.AddRange(Content);
        };
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>标题的 id：锚点，也进本页目录。</summary>
    public string? SectionId { get => GetValue(SectionIdProperty); set => SetValue(SectionIdProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public Controls Content { get; } = [];

    /// <summary>标题（本页目录按它定位）。</summary>
    public H2 TitlePart => title;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty) title.Text = Title;
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

/// <summary>DocArticle 的部件名。</summary>
public static class DocArticleParts
{
    /// <summary>头部：标题、简介、lead（TS 的 propsHeader）。</summary>
    public const string Header = "PART_Header";

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";

    /// <summary>简介（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";
}

/// <summary>DocSection 的部件名。</summary>
public static class DocSectionParts
{
    /// <summary>标题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";
}
