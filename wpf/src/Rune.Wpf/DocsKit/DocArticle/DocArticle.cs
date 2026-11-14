using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace Rune.Wpf;

/// <summary>
/// 文档页的骨架（vue-tsx-pro docs-kit 的 DocArticle）：标题、简介与正文。正文里一节一节是 <see cref="DocSection"/>。
/// <code>
/// &lt;rune:DocArticle Title="快速开始" Description="两分钟跑起来"&gt;&lt;rune:DocSection SectionId="install" Title="安装"&gt;…&lt;/rune:DocSection&gt;&lt;/rune:DocArticle&gt;
/// </code>
/// 长文阅读：字号 15（比界面文字大一号），行高 relaxed；头部与正文之间、正文各节之间 3rem，头部里各项之间 space.lg。
/// 简介是 lg 档、次要色的段落，最宽 48rem（宽版面时也保持适合阅读的行宽）。页面的宽度由外面的 Container 决定。部件样式键：<see cref="DocArticleParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class DocArticle : CompositeControl
{
    /// <summary>标题。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(DocArticle),
        new FrameworkPropertyMetadata(null, (d, _) => ((DocArticle)d).Update()));

    /// <summary>简介。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(DocArticle),
        new FrameworkPropertyMetadata(null, (d, _) => ((DocArticle)d).Update()));

    /// <summary>简介下面、正文之前的内容：导入语句之类。</summary>
    public static readonly DependencyProperty LeadProperty = DependencyProperty.Register(nameof(Lead), typeof(object), typeof(DocArticle),
        new FrameworkPropertyMetadata(null, (d, _) => ((DocArticle)d).Update()));

    readonly StackPanel root = new();
    readonly Flex header = new() { Direction = FlexDirection.Column, Gap = Space.Lg };
    readonly H1 title = new();
    // 靠起始边：设了 MaxWidth 时拉伸的元素会被居中
    readonly Paragraph description = new() { Size = TextSize.Lg, Tone = TextTone.Muted, MaxWidth = 48 * 16, HorizontalAlignment = HorizontalAlignment.Left };
    readonly ContentControl lead = new() { Focusable = false };

    /// <summary>新建：没有内容。</summary>
    public DocArticle()
    {
        header.SetResourceReference(StyleProperty, DocArticleParts.Header);
        title.SetResourceReference(StyleProperty, DocArticleParts.Title);
        description.SetResourceReference(StyleProperty, DocArticleParts.Description);
        header.Children.Add(title);
        header.Children.Add(description);
        header.Children.Add(lead);
        root.Children.Add(header);
        Content.CollectionChanged += OnContentChanged;
        Root = root;
        // 长文阅读：比界面文字（14px）大一号、行距宽松；写当前值，使用方的本地值、样式盖得过
        SetCurrentValue(FontSizeProperty, 15d);
        SetCurrentValue(Leading.FactorProperty, this.Resource<double>(ThemeKeys.LineHeight.Relaxed));
        Update();
    }

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>简介。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>简介下面、正文之前的内容。</summary>
    public object? Lead { get => GetValue(LeadProperty); set => SetValue(LeadProperty, value); }

    /// <summary>正文（一般是一节一节的 DocSection）。</summary>
    public ObservableCollection<UIElement> Content { get; } = [];

    void Update()
    {
        title.Text = Title;
        title.Visibility = Title is null ? Visibility.Collapsed : Visibility.Visible;
        description.Text = Description;
        description.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        lead.Content = Lead;
        lead.Visibility = Lead is null ? Visibility.Collapsed : Visibility.Visible;
    }

    void OnContentChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        root.Children.RemoveRange(1, root.Children.Count - 1);
        foreach (var child in Content)
        {
            // 头部与正文、正文各节之间 3rem：按根字号（16）算，不随文章的字号
            if (child is FrameworkElement element) element.Margin = new Thickness(0, 3 * 16, 0, 0);
            root.Children.Add(child);
        }
    }
}

/// <summary>
/// 文档页的一节（vue-tsx-pro docs-kit 的 DocSection）：带锚点的标题（H2，xl 档）与内容，之间 space.lg。
/// <see cref="SectionId"/> 是锚点，也是本页目录（TableOfContents）里这一节的 id。部件样式键：<see cref="DocSectionParts"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class DocSection : CompositeControl
{
    /// <summary>标题的 id（TS 的 id）：锚点，也进本页目录。</summary>
    public static readonly DependencyProperty SectionIdProperty = DependencyProperty.Register(nameof(SectionId), typeof(string), typeof(DocSection));

    /// <summary>标题。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(DocSection),
        new FrameworkPropertyMetadata(null, (d, _) => ((DocSection)d).UpdateTitle()));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Lg };
    readonly H2 title = new() { Size = TextSize.Xl };

    /// <summary>新建：没有内容。</summary>
    public DocSection()
    {
        title.SetResourceReference(StyleProperty, DocSectionParts.Title);
        root.Children.Add(title);
        Content.CollectionChanged += (_, _) =>
        {
            root.Children.RemoveRange(1, root.Children.Count - 1);
            foreach (var child in Content) root.Children.Add(child);
        };
        Root = root;
    }

    /// <summary>标题的 id：锚点，也进本页目录。</summary>
    public string? SectionId { get => (string?)GetValue(SectionIdProperty); set => SetValue(SectionIdProperty, value); }

    /// <summary>标题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>内容。</summary>
    public ObservableCollection<UIElement> Content { get; } = [];

    /// <summary>标题（本页目录按它定位）。</summary>
    public H2 TitlePart => title;

    // 写成方法：表达式 lambda 带返回值时，FrameworkPropertyMetadata(null, lambda) 会匹配成强制值回调，把值强制成旧值
    void UpdateTitle() => title.Text = Title;
}

/// <summary>DocArticle 的部件样式键。</summary>
public static class DocArticleParts
{
    /// <summary>头部：标题、简介、lead（TS 的 propsHeader）。</summary>
    public static readonly PartKey Header = new(typeof(DocArticle), nameof(Header), typeof(Flex));

    /// <summary>标题（TS 的 propsTitle）。</summary>
    public static readonly PartKey Title = new(typeof(DocArticle), nameof(Title), typeof(H1));

    /// <summary>简介（TS 的 propsDescription）。</summary>
    public static readonly PartKey Description = new(typeof(DocArticle), nameof(Description), typeof(Paragraph));
}

/// <summary>DocSection 的部件样式键。</summary>
public static class DocSectionParts
{
    /// <summary>标题（TS 的 propsTitle）。</summary>
    public static readonly PartKey Title = new(typeof(DocSection), nameof(Title), typeof(H2));
}
