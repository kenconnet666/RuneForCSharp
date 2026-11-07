namespace Rune.WinForms.Examples;

/// <summary>Code 页里 CodeBlock 的示例（vue-tsx-pro 文档站 base/Code）。basic 是行内代码（Code、CodeText）；group、preview 要等 Tabs 与示例卡片。</summary>
static class CodeBlockExamples
{
    public static Example[] All =>
    [
        new("Code", "basic", "行内代码", "句子里的命令、文件名、标识符用 Code；文案写在数据里（配置、接口返回、表格的说明列）时用 CodeText，反引号里的是行内代码，不用拆成多个节点。WinForms 的文字是整段画的：句子里的代码同样用 CodeTextControl。", Inline),
        new("Code", "block", "代码块", "CodeBlockControl 的高亮器经 Highlighter 注入，默认内置一个够用的（TS、C#、XAML、JSON、CSS、SQL、bash）。Title 是标题栏，LineNumbers 显示行号，HighlightLines 强调几行。", Block),
        new("Code", "options", "换行、复制与延后高亮", "Wrap 长行换行而不是横向滚动；Copyable 关掉复制按钮；Highlight 为 false 时按纯文本显示，改回 true 才交给高亮器。", Options),
        new("Code", "flush", "嵌在外框里", "Flush 去掉代码块自己的边框、圆角和底色，几段代码共用一个外框。", Flush),
        new("Code", "diff", "增删与折叠", "AddedLines、RemovedLines 标出增删的行；超过 MaxLines 行时折叠，底部的按钮展开或收起。", Diff),
    ];


    static readonly string[] Tips =
    [
        "`where` 里写数组就是 AND，要 OR 用 `or(a, b)`。",
        "开发时运行 `pnpm dev`，提交前运行 `pnpm check`。",
        "环境变量写在 `.env.local` 里，这个文件不提交。",
    ];

    static Control Inline()
    {
        var intro = new CodeTextControl { Source = "运行 `pnpm add @zerodep-dev/vue-tsx-pro-ui` 安装，然后在 `main.ts` 里调用 `app.use(createUi())`。" };
        var list = new UnorderedListControl();
        foreach (var tip in Tips)
        {
            var item = new ListItemControl();
            item.Controls.Add(new CodeTextControl { Source = tip });
            list.Controls.Add(item);
        }
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm }, intro, list);
    }

    const string Main = """
        import { createApp } from 'vue';
        import { createUi, forestTheme } from '@zerodep-dev/vue-tsx-pro-ui';
        import App from './App';

        createApp(App)
          .use(createUi({ theme: forestTheme }))
          .mount('#app');
        """;

    static Control Block() => new CodeBlockControl { Code = Main, Language = "ts", Title = "main.ts", LineNumbers = true, HighlightLines = "2,6" };

    const string Curl = """curl -X POST https://api.example.com/v1/projects/42/deployments -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"ref":"main","environment":"production","notify":true}'""";

    static Control Options()
    {
        var block = new CodeBlockControl { Code = Curl, Language = "bash", Wrap = true };
        SwitchControl Toggle(string label, Action<bool> apply)
        {
            var toggle = new SwitchControl { Text = label, IsChecked = true };
            toggle.IsCheckedChanged += (_, _) => apply(toggle.IsChecked);
            return toggle;
        }
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
            Build.Flex(new FlexControl { Gap = Space.Lg, Wrap = true },
                Toggle("自动换行", on => block.Wrap = on), Toggle("复制按钮", on => block.Copyable = on), Toggle("语法高亮", on => block.Highlight = on)),
            block);
    }

    const string Query = """
        db.queryable(User)
          .where((u) => u.age.ge(18))
          .toList();
        """;

    static Control Flush() => Build.Card(new CardControl { Variant = CardVariant.Outline, CardPadding = Space.None },
        new CodeBlockControl { Flush = true, Title = "TypeScript", Code = Query, Language = "ts" },
        new SeparatorControl(),
        new CodeBlockControl { Flush = true, Title = "SQL", Code = "SELECT * FROM t_user WHERE age >= $1;", Language = "sql" });

    const string Change = """
        export const Counter = _component(() => {
          const count = ref(0);
          let count = _state(0);
          return () => (
            <button onClick={() => count.value++}>{count.value}</button>
            <button onClick={() => count++}>{count}</button>
          );
        });
        """;

    static Control Diff()
    {
        var items = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"  {{ id: {i}, name: '项目 {i}', done: {((i - 1) % 3 == 0 ? "true" : "false")} }},"));
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
            new CodeBlockControl { Code = Change, Language = "tsx", Title = "Counter.tsx：从 ref 换成符文", LineNumbers = true, RemovedLines = "2,5", AddedLines = "3,6" },
            new CodeBlockControl { Code = $"const items = [\n{items}\n];", Language = "ts", Title = "data.ts", MaxLines = 8 });
    }
}
