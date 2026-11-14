namespace Rune.WinForms.Examples;

/// <summary>Prose 页的示例（vue-tsx-pro 文档站 layout/Prose）。列表项里的行内代码用 CodeTextControl 的反引号（它不换行，长段落用 ParagraphControl）；WinForms 没有行内排版，段落末尾的链接与文字排成基线对齐的一行。</summary>
static class ProseExamples
{
    public static Example[] All =>
    [
        new("Prose", "article", "一篇文章", "块间距用 FlexControl 的 Gap 统一给（默认 lg），本库的组件都不带外边距。LimitLineLength 默认把行长限制在约 70 个字宽，放在窄栏里时关掉。", Article),
    ];

    const string Install = """
        pnpm add @zerodep-dev/vue-tsx-pro-ui @zerodep-dev/vue-tsx-pro-tsx
        pnpm add -D vite @vitejs/plugin-vue
        """;

    static ListItemControl Item(string source)
    {
        var item = new ListItemControl();
        item.Controls.Add(new CodeTextControl { Source = source });
        return item;
    }

    static T List<T>(T list, params string[] items) where T : Control
    {
        list.Controls.AddRange([.. items.Select(Item)]);
        return list;
    }

    static BlockquoteControl Quote(string text)
    {
        var quote = new BlockquoteControl();
        quote.Controls.Add(new ParagraphControl { Text = text, Dock = DockStyle.Top });
        return quote;
    }

    static Control Article()
    {
        static TableRowControl Row(params string[] cells)
        {
            var row = new TableRowControl();
            row.Controls.AddRange([.. cells.Select(cell => new TableCellControl { Text = cell })]);
            return row;
        }
        var head = new TableRowControl { Controls = { new TableHeaderCellControl { Text = "页面" }, new TableHeaderCellControl { Text = "主要组件" }, new TableHeaderCellControl { Text = "预计工时" } } };
        var body = new TableBodyControl { Controls = { Row("概览", "Stat、Card、Grid", "0.5 天"), Row("订单", "DataTable、Pagination", "1 天"), Row("设置", "Form、Field、Select", "1 天") } };
        var table = new TableControl { AccessibleName = "各页用到的组件", Controls = { new TableHeadControl { Controls = { head } }, body } };

        return Build.Flex(new ProseControl(),
            new H2Control { Text = "从零搭一个后台", TextSize = TextSize.Xxl },
            new SmallControl { Text = "10 月 18 日 · 阅读约 6 分钟", Tone = TextTone.Muted },
            new ParagraphControl { Text = "这篇带你用 AppShell、NavList 和 DataTable 搭出一个能用的订单后台。全文的段落、标题、列表、代码块都放在 ProseControl 里：块之间的间距统一给，行长限制在约 70 个字宽，长文读起来不累。" },
            new H3Control { Text = "准备工作", TextSize = TextSize.Xl },
            new ParagraphControl { Text = "先装好依赖，再在入口里用 createUi 装上主题与语言包：" },
            new CodeBlockControl { Code = Install, Language = "bash" },
            List(new OrderedListControl(), "新建 Vite 项目，选 Vue + TypeScript 模板；", "在 `vite.config.ts` 里加上 vue-tsx-pro 的插件；", "运行 `pnpm dev`，按 O 在浏览器里打开。"),
            Quote("先把骨架搭起来再填内容：布局定下来以后，每个页面只是往正文里放组件。"),
            new H3Control { Text = "页面清单", TextSize = TextSize.Xl },
            List(new UnorderedListControl(), "概览：今日销售额、订单数、新客户（Stat）", "订单：可排序、可勾选的表格（DataTable）", "设置：团队成员与账单（Form）"),
            table,
            Build.Flex(new FlexControl { Align = FlexAlign.Baseline, Gap = Space.Xxs },
                new ParagraphControl { Text = "页面里的分栏与间距见" }, new LinkControl { Text = "Flex 弹性与网格布局", Href = new Uri("https://example.com/components/flex") }));
    }
}
