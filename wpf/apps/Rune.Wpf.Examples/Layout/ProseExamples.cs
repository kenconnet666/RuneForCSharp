using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;

namespace Rune.Wpf.Examples;

/// <summary>Prose 页的示例（vue-tsx-pro 文档站 layout/Prose）。段落里的行内代码用 CodeText 的反引号。</summary>
static class ProseExamples
{
    public static Example[] All =>
    [
        new("Prose", "article", "一篇文章", "块间距用 Flex 的 Gap 统一给（默认 lg），本库的组件都不带外边距。LimitLineLength 默认把行长限制在约 70 个字宽，放在窄栏里时关掉。", Article),
    ];

    const string Install = """
        pnpm add @zerodep-dev/vue-tsx-pro-ui @zerodep-dev/vue-tsx-pro-tsx
        pnpm add -D vite @vitejs/plugin-vue
        """;

    static ListItem Item(string source) => new() { Content = new CodeText { Source = source, Wrap = TextWrap.Wrap } };

    static FrameworkElement Article()
    {
        static TableRow Row(params string[] cells)
        {
            var row = new TableRow();
            foreach (var cell in cells) row.Cells.Add(new TableCell { Content = cell });
            return row;
        }
        var body = new TableBody { Rows = { Row("概览", "Stat、Card、Grid", "0.5 天"), Row("订单", "DataTable、Pagination", "1 天"), Row("设置", "Form、Field、Select", "1 天") } };
        var table = new Table { Sections = { new TableHead { Rows = { new TableRow { Cells = { new TableHeaderCell { Content = "页面" }, new TableHeaderCell { Content = "主要组件" }, new TableHeaderCell { Content = "预计工时" } } } } }, body } };
        AutomationProperties.SetName(table, "各页用到的组件");

        var more = new Paragraph();
        more.Inlines.Add(new Run("页面里的分栏与间距见 "));
        more.Inlines.Add(new Link { Href = new Uri("https://example.com/components/flex"), Inlines = { new Run("Flex 弹性与网格布局") } });
        more.Inlines.Add(new Run("。"));

        return new Prose
        {
            Children =
            {
                new H2 { Text = "从零搭一个后台", Size = TextSize.Xxl },
                new Small { Text = "10 月 18 日 · 阅读约 6 分钟", Tone = TextTone.Muted },
                new CodeText { Wrap = TextWrap.Wrap, Source = "这篇带你用 AppShell、NavList 和 DataTable 搭出一个能用的订单后台。全文的段落、标题、列表、代码块都放在 `Prose` 里：块之间的间距统一给，行长限制在约 70 个字宽，长文读起来不累。" },
                new H3 { Text = "准备工作", Size = TextSize.Xl },
                new Paragraph { Text = "先装好依赖，再在入口里用 createUi 装上主题与语言包：" },
                new CodeBlock { Code = Install, CodeLanguage = "bash" },
                new OrderedList
                {
                    Children =
                    {
                        Item("新建 Vite 项目，选 Vue + TypeScript 模板；"),
                        Item("在 `vite.config.ts` 里加上 vue-tsx-pro 的插件；"),
                        Item("运行 `pnpm dev`，按 O 在浏览器里打开。"),
                    },
                },
                new Blockquote { Content = "先把骨架搭起来再填内容：布局定下来以后，每个页面只是往正文里放组件。" },
                new H3 { Text = "页面清单", Size = TextSize.Xl },
                new List
                {
                    Children =
                    {
                        Item("概览：今日销售额、订单数、新客户（Stat）"),
                        Item("订单：可排序、可勾选的表格（DataTable）"),
                        Item("设置：团队成员与账单（Form）"),
                    },
                },
                table,
                more,
            },
        };
    }
}
