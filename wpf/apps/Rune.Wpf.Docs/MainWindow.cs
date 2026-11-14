using System.Windows;
using System.Windows.Automation;
using Rune.Wpf.Examples;

namespace Rune.Wpf.Docs;

/// <summary>
/// 文档应用的主窗口（vue-tsx-pro 文档站的桌面版）：顶栏（品牌、主题切换）、侧栏（分类与页面，同上游的侧栏顺序）、正文（一页）、右侧栏（本页目录）。
/// 页面与示例的清单来自 <see cref="DocSite"/>（上游文档站抽出来的），示例的内容与源码来自 <see cref="ExampleCatalog"/>、<see cref="ExampleSources"/>；
/// 还没移植的示例显示一个空状态，平台特有的示例（上游没有的）排在上游的示例后面。
/// </summary>
public sealed class MainWindow : Window
{
    readonly NavList navigation = new();
    readonly TableOfContents contents = new();
    readonly Container page = new() { Size = ContainerSize.Xl };
    readonly AppShell shell;

    /// <summary>列出页面，打开第一页有示例的页面。</summary>
    public MainWindow()
    {
        Title = "Rune.Wpf 文档";
        Width = 1360;
        Height = 880;
        AutomationProperties.SetName(navigation, "组件");
        foreach (var category in DocSite.Current.Categories)
        {
            var section = new NavSection { Label = category.Label };
            foreach (var docPage in category.Pages)
            {
                var ported = ExampleCatalog.All.Count(example => example.Component == docPage.Name);
                section.Items.Add(new NavItem { Href = docPage.Name, Content = docPage.Title, Badge = ported, BadgeTone = StatusTone.Neutral });
            }
            navigation.Children.Add(section);
        }
        navigation.Navigate += (_, e) => Show(e.Href);
        var header = new Flex { Justify = FlexJustify.End };
        header.Children.Add(Themes());
        shell = new AppShell
        {
            Brand = new AppShellBrand { Icon = Lucide.Box, Content = "Rune.Wpf" },
            Header = header,
            Sidebar = navigation,
            Content = page,
            Aside = contents,
        };
        contents.ScrollRoot = shell.MainScroll;
        Content = shell;
        var first = DocSite.Current.Pages.First(docPage => ExampleCatalog.All.Any(example => example.Component == docPage.Name));
        navigation.CurrentHref = first.Name;
        Show(first.Name);
    }

    /// <summary>主题切换：内置的 16 套主题，换的是应用资源里 RuneTheme 的主题字典。</summary>
    static Select Themes()
    {
        var resources = Application.Current.Resources;
        var theme = resources as RuneTheme ?? resources.MergedDictionaries.OfType<RuneTheme>().Single();
        var select = new Select
        {
            ItemsSource = RuneTheme.BuiltinThemes.Select(entry => new ListboxOption(entry.Name, entry.Label)).ToList(),
            Value = theme.Theme,
            Size = TextSize.Sm,
            Width = 10 * 16,
        };
        AutomationProperties.SetName(select, "主题");
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Select.ValueProperty, typeof(Select)).AddValueChanged(select, (_, _) =>
        {
            if (select.Value is { } name) theme.Theme = name;
        });
        return select;
    }

    /// <summary>换到某一页：文章（标题、简介、示例按列、键盘表），本页目录列出各节，滚回顶上。</summary>
    void Show(string? name)
    {
        if (name is null || DocSite.Current.Find(name) is not { } docPage) return;
        var article = new DocArticle { Title = docPage.Title, Description = docPage.Description };
        var headings = new List<HeadingEntry>();
        var grid = new Grid { Columns = docPage.Wide ? 1 : Math.Max(1, docPage.Columns.Count), Gap = Space.Xl };
        foreach (var column in docPage.Columns)
        {
            var stack = new Flex { Direction = FlexDirection.Column, Gap = Space.Xl };
            foreach (var meta in column)
            {
                stack.Children.Add(Section(docPage, meta.Id, meta.Title, DocRun.ToCodeText(meta.Description), ExampleCatalog.Find($"{docPage.Name}/{meta.Id}")));
                headings.Add(new HeadingEntry(Anchor(meta.Id), meta.Title, 2));
            }
            grid.Children.Add(stack);
        }
        article.Content.Add(grid);
        // 平台特有的示例（上游没有这一节，如 AppShell 的"顶栏兼作标题栏"）
        var known = docPage.Examples.Select(meta => meta.Id).ToHashSet();
        foreach (var extra in ExampleCatalog.All.Where(example => example.Component == docPage.Name && !known.Contains(example.Id)))
        {
            article.Content.Add(Section(docPage, extra.Id, extra.Title, extra.Description, extra));
            headings.Add(new HeadingEntry(Anchor(extra.Id), extra.Title, 2));
        }
        if (docPage.Keyboard.Count > 0)
        {
            var keyboard = new DocSection { SectionId = "keyboard", Title = "键盘" };
            keyboard.TitlePart.Name = "keyboard";
            keyboard.Content.Add(new TextTable
            {
                Head = ["按键", "作用"],
                TextRows = [.. docPage.Keyboard.Select(row => (IReadOnlyList<string>)[row.Keys, DocRun.ToCodeText(row.Action)])],
            });
            article.Content.Add(keyboard);
            headings.Add(new HeadingEntry("keyboard", "键盘", 2));
        }
        page.Child = article;
        contents.Items = headings;
        shell.MainScroll.ScrollToHome();
    }

    /// <summary>本页目录按名字找标题：示例 id 里的连字符不能做 WPF 的 Name，换成下划线。</summary>
    static string Anchor(string id) => id.Replace('-', '_');

    /// <summary>
    /// 一个示例：带锚点的标题、说明、示例卡片（效果 + 源码）。说明优先用平台自己写的（属性名、平台写法与上游不同），没移植的用上游的；
    /// 还没移植的示例是一个空状态。
    /// </summary>
    static DocSection Section(DocPage docPage, string id, string title, string description, Example? example)
    {
        var section = new DocSection { SectionId = id, Title = title };
        section.TitlePart.Name = Anchor(id);
        section.Content.Add(new CodeText { Source = example?.Description ?? description, Tone = TextTone.Muted, TextWrapping = TextWrapping.Wrap });
        if (example is null)
        {
            section.Content.Add(new Card { Variant = CardVariant.Outline, Content = new Empty { Icon = Lucide.Construction, Title = "这个示例还没移植", Description = $"{docPage.Name}/{id}" } });
            return section;
        }
        var content = example.Create();
        var source = ExampleSources.Of(example.Create, content);
        section.Content.Add(new CodePreview
        {
            Content = content,
            Code = source,
            CodeLanguage = source?.TrimStart().StartsWith('<') == true ? "xml" : "csharp",
        });
        return section;
    }
}
