using Rune.WinForms.Examples;

namespace Rune.WinForms.Docs;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

/// <summary>
/// 文档应用的主窗口（vue-tsx-pro 文档站的桌面版）：顶栏（品牌、主题切换）、侧栏（分类与页面，同上游的侧栏顺序）、正文（一页）、右侧栏（本页目录）。
/// 页面与示例的清单来自 <see cref="DocSite"/>（上游文档站抽出来的），示例的内容与源码来自 <see cref="ExampleCatalog"/>、<see cref="ExampleSources"/>；
/// 还没移植的示例显示一个空状态，平台特有的示例（上游没有的）排在上游的示例后面。换主题时写 <see cref="RuneTheme.Current"/>，组件按绘制 effect 自己重画。
/// 换页时先把文章的骨架（标题、说明、空的示例卡片）排好画出来，示例的效果在消息空闲时从上往下一个个填进卡片：
/// WinForms 的每个子控件都是一个窗口，一页十几个示例上千个窗口，一次建完要卡一两秒。
/// </summary>
sealed class MainForm : Form
{
    readonly NavListControl navigation = new() { AccessibleName = "组件" };
    readonly TableOfContentsControl contents = new();
    readonly AppShellControl shell;
    readonly Queue<(CodePreviewControl Preview, Example Example)> pending = new();
    // 计时器的消息排在绘制之后：每填一个示例先让界面画出来、响应输入，再填下一个
    readonly System.Windows.Forms.Timer filler = new() { Interval = 1 };

    public MainForm()
    {
        Text = "Rune.WinForms 文档";
        ClientSize = LogicalToDeviceUnits(new Size(1360, 880));
        Font = new Font(Fonts.Installed(RuneTheme.Current.Peek().Fonts.Sans), Tokens.FontSize.Md * 72 / 96);
        navigation.Navigate += (_, e) => Show(e.Href);
        filler.Tick += (_, _) => FillNext();
        var header = new FlexControl { Justify = FlexJustify.End, AutoSize = true };
        header.Controls.Add(Themes());
        shell = new AppShellControl
        {
            Dock = DockStyle.Fill,
            Brand = new AppShellBrandControl { Icon = Lucide.Box, Text = "Rune.WinForms" },
            Header = header,
            Sidebar = navigation,
            Aside = contents,
        };
        contents.ScrollRoot = shell.MainScroll;
        Controls.Add(shell);
        // 一百多个导航项：侧栏挂上窗体之后再填（字体已是窗体的，挂上时不用整列重套），填完再排一次版
        navigation.SuspendLayout();
        foreach (var category in DocSite.Current.Categories)
        {
            var section = new NavSectionControl { Label = category.Label };
            foreach (var page in category.Pages)
            {
                var ported = ExampleCatalog.All.Count(example => example.Component == page.Name);
                section.Controls.Add(new NavItemControl { Href = page.Name, Text = page.Title, Badge = ported, BadgeTone = StatusTone.Neutral });
            }
            navigation.Controls.Add(section);
        }
        navigation.ResumeLayout();
        var first = DocSite.Current.Pages.First(page => ExampleCatalog.All.Any(example => example.Component == page.Name));
        navigation.CurrentHref = first.Name;
        Show(first.Name);
    }

    /// <summary>主题切换：内置的 16 套主题。</summary>
    static SelectControl Themes()
    {
        var select = new SelectControl
        {
            ItemsSource = BuiltinThemes.All.Select(theme => new ListboxOption(theme.Name, theme.Label)).ToList(),
            Value = RuneTheme.Current.Peek().Name,
            TextSize = TextSize.Sm,
            Width = 160,
            AccessibleName = "主题",
        };
        select.ValueChanged += (_, _) =>
        {
            if (BuiltinThemes.All.FirstOrDefault(theme => theme.Name == select.Value) is { } theme) RuneTheme.Current.Value = theme;
        };
        return select;
    }

    /// <summary>换到某一页：文章（标题、简介、示例按列、键盘表），本页目录列出各节，滚回顶上。</summary>
    void Show(string? name)
    {
        if (name is null || DocSite.Current.Find(name) is not { } page) return;
        var article = new DocArticleControl { Title = page.Title, Description = page.Description };
        var headings = new List<HeadingEntry>();
        pending.Clear();
        var grid = new GridControl { Columns = page.Wide ? 1 : Math.Max(1, page.Columns.Count), Gap = Space.Xl, AutoSize = true };
        var columns = new List<List<(CodePreviewControl, Example)>>();
        foreach (var column in page.Columns)
        {
            var stack = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xl, AutoSize = true };
            var previews = new List<(CodePreviewControl, Example)>();
            foreach (var meta in column)
            {
                stack.Controls.Add(Section(page, meta.Id, meta.Title, DocRun.ToCodeText(meta.Description), ExampleCatalog.Find($"{page.Name}/{meta.Id}"), previews));
                headings.Add(new HeadingEntry(meta.Id, meta.Title, 2));
            }
            grid.Controls.Add(stack);
            columns.Add(previews);
        }
        // 按行填：两列并排时先填最上面一排
        for (var row = 0; columns.Any(previews => row < previews.Count); row++)
            foreach (var previews in columns.Where(previews => row < previews.Count)) pending.Enqueue(previews[row]);
        article.Controls.Add(grid);
        // 平台特有的示例（上游没有这一节，如 AppShell 的"顶栏兼作标题栏"）
        var known = page.Examples.Select(meta => meta.Id).ToHashSet();
        foreach (var extra in ExampleCatalog.All.Where(example => example.Component == page.Name && !known.Contains(example.Id)))
        {
            var previews = new List<(CodePreviewControl, Example)>();
            article.Controls.Add(Section(page, extra.Id, extra.Title, extra.Description, extra, previews));
            foreach (var preview in previews) pending.Enqueue(preview);
            headings.Add(new HeadingEntry(extra.Id, extra.Title, 2));
        }
        if (page.Keyboard.Count > 0)
        {
            var keyboard = new DocSectionControl { SectionId = "keyboard", Title = "键盘" };
            keyboard.TitlePart.Name = "keyboard";
            keyboard.Controls.Add(new TextTableControl
            {
                Head = ["按键", "作用"],
                TextRows = [.. page.Keyboard.Select(row => (IReadOnlyList<string>)[row.Keys, DocRun.ToCodeText(row.Action)])],
            });
            article.Controls.Add(keyboard);
            headings.Add(new HeadingEntry("keyboard", "键盘", 2));
        }
        // 旧页先释放再换（直接换下来的会挂到 WinForms 的停放窗口上，整棵树按停放窗口的字体白排一遍）；换完一次画出来
        using (shell.MainScroll.SuspendRedraw())
        {
            shell.Content?.Dispose();
            shell.Content = article;
            shell.MainScroll.ScrollOffset = Point.Empty;
        }
        contents.Items = headings;
        filler.Enabled = pending.Count > 0;
    }

    /// <summary>示例还没填完（测试、计时用）。</summary>
    internal bool Filling => pending.Count > 0;

    /// <summary>填下一个示例的效果与源码；卡片停画到填好再画，不一块块地出来。</summary>
    void FillNext()
    {
        if (!pending.TryDequeue(out var next))
        {
            filler.Enabled = false;
            return;
        }
        var (preview, example) = next;
        if (preview.IsDisposed) return;
        using var _ = preview.SuspendRedraw();
        var content = example.Create();
        preview.Content = content;
        preview.Code = ExampleSources.Of(example.Create, content);
    }

    /// <summary>
    /// 一个示例：带锚点的标题、说明、示例卡片（效果 + 源码）。说明优先用平台自己写的（属性名、平台写法与上游不同），没移植的用上游的；
    /// 还没移植的示例是一个空状态；移植了的先放一张空的示例卡片，记进 previews 等着填。
    /// </summary>
    static DocSectionControl Section(DocPage page, string id, string title, string description, Example? example, List<(CodePreviewControl, Example)> previews)
    {
        var section = new DocSectionControl { SectionId = id, Title = title };
        section.TitlePart.Name = id;
        section.Controls.Add(new CodeTextControl { Source = example?.Description ?? description });
        if (example is null)
        {
            var card = new CardControl { Variant = CardVariant.Outline };
            card.Controls.Add(new EmptyControl { Icon = Lucide.Construction, Title = "这个示例还没移植", Description = $"{page.Name}/{id}" });
            section.Controls.Add(card);
            return section;
        }
        var preview = new CodePreviewControl { Language = "csharp" };
        section.Controls.Add(preview);
        previews.Add((preview, example));
        return section;
    }
}
