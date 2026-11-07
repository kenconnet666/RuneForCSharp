using static Rune.WinForms.Examples.Layout;

namespace Rune.WinForms.Examples;

/// <summary>List 的示例（vue-tsx-pro 文档站 base/List）。</summary>
static class ListExamples
{
    public static Example[] All =>
    [
        new("List", "basic", "无序、有序与嵌套", "正文里的列表：UnorderedListControl 是圆点（WinForms 自己有 ListControl），OrderedListControl 是编号（Start 从几开始），Gap 调项之间的间距。ListItemControl 的 Text 是项的文字，嵌套的列表加成它的子控件。", Basic),
        new("List", "icons", "图标代替圆点", "Icon 让每项前面画一个图标（功能清单的对勾），PropsIcon 改图标颜色；ListItemControl 的 Icon、PropsIcon 逐项覆盖。图标是装饰，读屏照常念\"列表，几项\"。TextSize 是字号档位。", Features),
    ];

    static readonly (string Name, string Price, (string Feature, bool Included)[] Features)[] Plans =
    [
        ("专业版", "¥39 / 月", [("不限项目数", true), ("100 GB 存储", true), ("版本历史 30 天", true), ("单点登录", false)]),
        ("团队版", "¥99 / 人 / 月", [("专业版的全部功能", true), ("共享空间与权限", true), ("版本历史 1 年", true), ("单点登录", true)]),
    ];

    /// <summary>功能清单：包含的打勾（success），不包含的画减号、文字淡一些（muted）。</summary>
    static Control Features() => Build.Grid(new GridControl { MinItemWidth = "14em", Gap = Space.Md },
        [.. Plans.Select(plan =>
        {
            var list = new UnorderedListControl { Icon = Lucide.Check, PropsIcon = new() { Tone = TextTone.Success }, Gap = Space.Sm, TextSize = TextSize.Sm };
            list.Controls.AddRange([.. plan.Features.Select(entry => entry.Included
                ? Item(new TextControl { Text = entry.Feature })
                : Muted(Item(new TextControl { Text = entry.Feature, Tone = TextTone.Muted })))]);
            return Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
                Build.Flex(new FlexControl { Direction = FlexDirection.Column },
                    new StrongControl { Text = plan.Name, TextSize = TextSize.Lg },
                    new TextControl { Text = plan.Price, Tone = TextTone.Muted, TextSize = TextSize.Sm }),
                list));
        })]);

    static ListItemControl Muted(ListItemControl item)
    {
        item.Icon = Lucide.Minus;
        item.PropsIcon = new() { Tone = TextTone.Muted };
        return item;
    }

    static Control Basic()
    {
        var nested = Items(new UnorderedListControl(), "图标单个引入", "图表、3D 在子路径");
        var packages = new ListItemControl { Text = "组件按需引入，打包时只带用到的" };
        packages.Controls.Add(nested);
        var features = new UnorderedListControl();
        features.Controls.AddRange([new ListItemControl { Text = "支持亮色、暗色与纸张主题" }, packages, new ListItemControl { Text = "键盘、读屏都可用" }]);

        // 行内代码、加粗：项里放一个 CodeTextControl 或一行控件（WinForms 的文字是整段画的）
        var setup = new OrderedListControl();
        setup.Controls.AddRange([
            Item(new CodeTextControl { Source = "安装 `@zerodep-dev/vue-tsx-pro-ui`" }),
            Item(new CodeTextControl { Source = "在 Vite 配置里加上 `vueTsxPro()`" }),
            Item(Row(0, new StrongControl { Text = "createUi" }, new TextControl { Text = " 选好主题与语言" })),
        ]);
        var next = Items(new OrderedListControl { Start = 4, Gap = Space.Sm }, "写第一个页面（接着上一列，从 4 开始）", "跑起开发服务器，改代码立即看到效果");

        // TS 的 Grid minItemWidth="16rem" gap="lg"：Grid 做出来前每列定宽 256（高度自动）
        foreach (var list in new ListBaseControl[] { features, setup, next }) list.MinimumSize = list.MaximumSize = new Size(Dip(256), 0);
        return Wrap(16, features, setup, next);
    }

    static T Items<T>(T list, params string[] texts) where T : ListBaseControl
    {
        list.Controls.AddRange([.. texts.Select(text => new ListItemControl { Text = text })]);
        return list;
    }

    static ListItemControl Item(Control content)
    {
        var item = new ListItemControl();
        item.Controls.Add(content);
        return item;
    }
}
