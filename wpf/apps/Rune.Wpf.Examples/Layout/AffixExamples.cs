using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>Affix 的示例（vue-tsx-pro 文档站 layout/Affix）。</summary>
static class AffixExamples
{
    public static Example[] All =>
    [
        new("Affix", "affix", "滚动面板里的操作条", "Affix 放在跟着滚动的那一层里：滚过时钉在滚动区域的顶部（OffsetTop），贴住时加阴影、StuckChanged 报告；滚动区域默认是外层最近的 ScrollViewer，也可以用 Target 指定。面板滚下去之后右下角出现“回到顶部”。", Toolbar),
        new("Affix", "bottom", "贴在底部的提交栏", "OffsetBottom 贴在滚动区域的底边，滚到表单末尾时回到原位；还压着下面的内容时（IsStuck）有阴影，到底了阴影消失——看得出下面还有没有内容。", SubmitBar),
    ];

    static readonly string[] Titles = ["来料检验报告补录", "模具保养计划确认", "供应商对账", "工艺变更评审", "月度销售复盘"];
    static readonly string[] Owners = ["林小满", "周一航", "许悠", "韩笑"];

    static FrameworkElement Toolbar()
    {
        var hint = new Small { Text = "已贴住", Tone = TextTone.Muted, Visibility = Visibility.Collapsed };
        var bar = new Card
        {
            Padding = Space.Sm,
            Content = new Flex
            {
                Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Wrap = true,
                Children =
                {
                    new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Text { Text = "待办任务", Weight = TextWeight.Semibold }, new Badge { Size = TextSize.Xs, Variant = StatusVariant.Soft, Content = "30" }, hint } },
                    new Flex
                    {
                        Gap = Space.Xs,
                        Children =
                        {
                            new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Icon = Lucide.Download, Content = "导出" },
                            new Button { Size = TextSize.Sm, Icon = Lucide.Plus, Content = "新建任务" },
                        },
                    },
                },
            },
        };
        var affix = new Affix { Content = bar };
        affix.StuckChanged += (_, _) => hint.Visibility = affix.IsStuck ? Visibility.Visible : Visibility.Collapsed;
        var list = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Margin = new Thickness(12, 0, 12, 12) };
        list.Children.Add(new Small { Text = "往下滚：操作条会贴在面板顶部", Tone = TextTone.Muted, Margin = new Thickness(0, 12, 0, 12) });
        list.Children.Add(affix);
        for (var index = 0; index < 30; index++)
        {
            var title = new Text { Text = Titles[index % 5], Size = TextSize.Sm };
            Flex.SetGrow(title, true);
            list.Children.Add(new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Md, Margin = new Thickness(12, 0, 12, 0),
                Children = { new Text { Text = $"T-{101 + index:000}", Mono = true, Size = TextSize.Sm, Tone = TextTone.Muted }, title, new Small { Text = Owners[index % 4], Tone = TextTone.Muted } },
            });
        }
        var scroller = new ScrollArea { Height = 320, Content = list };
        return new Card { Padding = Space.None, ClipToBounds = true, Content = new System.Windows.Controls.Grid { Children = { scroller, new BackTop { Target = scroller, Threshold = 200 } } } };
    }

    static FrameworkElement SubmitBar()
    {
        var status = new Small { Text = "下面还有内容，往下滚", Tone = TextTone.Muted };
        var saved = false;
        var form = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Margin = new Thickness(12) };
        foreach (var label in new[] { "供应商名称", "统一社会信用代码", "开户银行", "银行账号", "联系人", "联系电话", "收货地址", "发票抬头" })
            form.Children.Add(new Field { Label = label, Content = new TextField() });
        form.Children.Add(new Field { Label = "备注", Content = new Textarea { Rows = 3 } });
        var cancel = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "取消" };
        var save = new Button { Size = TextSize.Sm, Content = "保存" };
        var affix = new Affix
        {
            OffsetBottom = 0,
            Content = new Card
            {
                Padding = Space.Sm,
                Content = new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Wrap = true, Children = { status, new Flex { Gap = Space.Xs, Children = { cancel, save } } } },
            },
        };
        void Sync() => status.Text = saved ? "已保存" : affix.IsStuck ? "下面还有内容，往下滚" : "到底了";
        affix.StuckChanged += (_, _) => Sync();
        cancel.Click += (_, _) => { saved = false; Sync(); };
        save.Click += (_, _) => { saved = true; Sync(); };
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        form.Children.Add(affix);
        return new Card { Padding = Space.None, ClipToBounds = true, Content = new ScrollArea { Height = 320, Content = form } };
    }
}
