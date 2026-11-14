using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>ActionBar 页的示例（vue-tsx-pro 文档站 patterns/ActionBar）。上游的长表单用 Form 与 FormSection，这里还没有，用 Field 排、自己数改了几处。</summary>
static class ActionBarExamples
{
    public static Example[] All =>
    [
        new("ActionBar", "long-form", "长表单的提交栏", "放在滚动区域里内容的最后：还没滚到底时贴着滚动区域的底边，带阴影提示下面还有内容；滚到底时与内容平齐。Start 写着改了几处，按钮靠结束边。", LongForm),
        new("ActionBar", "batch", "批量操作", "Sticky=False 是普通的一行（列表下面、对话框里已经固定的底栏）。起始边是已选几项，选了才能操作。", Batch),
    ];

    static readonly (string Label, string Value)[] Fields =
    [
        ("供应商名称", "供应商甲精密制造有限公司"), ("统一社会信用代码", "91310115MA1K4XXX0Q"), ("联系人", "周岚"), ("手机", "13800001234"),
        ("地址", "上海市浦东新区某路 88 号"), ("开户行", "某银行上海浦东支行"), ("账号", "6222 0210 0100 1234 567"),
    ];

    static FrameworkElement LongForm()
    {
        // 保存过的值：撤销回到它，保存时更新它
        var saved = Fields.Select(field => field.Value).ToArray();
        var inputs = saved.Select(value => new TextField { Value = value }).ToArray();
        var note = new Textarea { Rows = 3 };
        var form = new Grid { MinItemWidth = "15em", Gap = Space.Lg };
        for (var i = 0; i < Fields.Length; i++) form.Children.Add(new Field { Label = Fields[i].Label, Content = inputs[i] });
        var reset = new Button { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "撤销改动" };
        var save = new Button { Content = "保存" };
        var bar = new ActionBar { Children = { reset, save } };
        int Changed() => inputs.Where((input, i) => input.Value != saved[i]).Count() + (string.IsNullOrEmpty(note.Text) ? 0 : 1);
        void Update()
        {
            var changed = Changed();
            bar.Start = changed > 0 ? $"有 {changed} 处改动还没保存" : "没有改动";
            reset.IsEnabled = save.IsEnabled = changed > 0;
        }
        foreach (var input in inputs) System.ComponentModel.DependencyPropertyDescriptor.FromProperty(TextField.ValueProperty, typeof(TextField)).AddValueChanged(input, (_, _) => Update());
        note.TextChanged += (_, _) => Update();
        reset.Click += (_, _) =>
        {
            for (var i = 0; i < inputs.Length; i++) inputs[i].Value = saved[i];
            note.Text = "";
        };
        save.Click += (_, _) =>
        {
            for (var i = 0; i < inputs.Length; i++) saved[i] = inputs[i].Value;
            note.Text = "";
            Update();
        };
        Update();
        var body = new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Xl, Margin = new Thickness(24),
            Children =
            {
                new H5 { Text = "基本信息与结算" }, form,
                new H5 { Text = "变更说明" },
                new Field { Label = "为什么改", Description = "审批人会看到，写清楚改了什么、依据是什么", Content = note },
            },
        };
        return new Card { Padding = Space.None, Content = new ScrollArea { Height = 24 * 16, Content = new StackPanel { Children = { body, bar } } } };
    }

    static readonly (string Id, string Title, string Due)[] Items =
    [
        ("PO-2026-0187", "显示器支架改为一体成型", "剩 2 个工作日"),
        ("PO-2026-0192", "包装胶带换国产料号", "今天到期"),
        ("PO-2026-0203", "线束卡箍合并规格", "剩 5 个工作日"),
        ("PO-2026-0211", "货架紧固件减量", "剩 9 个工作日"),
    ];

    static FrameworkElement Batch()
    {
        var group = new CheckboxGroup { Margin = new Thickness(24) };
        AutomationProperties.SetName(group, "待认领的建议");
        foreach (var (id, title, due) in Items)
            group.Items.Add(new Checkbox
            {
                Value = id,
                Content = new Flex { Gap = Space.Sm, Align = FlexAlign.Baseline, Wrap = FlexWrap.Wrap, Children = { new Text { Text = title }, new Small { Tone = TextTone.Muted, Text = $"{id} · {due}" } } },
            });
        var back = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Danger, Icon = Lucide.Undo2, Content = "退回" };
        var claim = new Button { Icon = Lucide.CheckCheck, Content = "认领" };
        var bar = new ActionBar { Sticky = false, Children = { back, claim } };
        var done = "";
        void Update()
        {
            var count = group.Value.Count;
            bar.Start = count > 0 ? $"已选 {count} 项" : done is "" ? "勾选要处理的建议" : done;
            back.IsEnabled = claim.IsEnabled = count > 0;
        }
        void Act(string verb)
        {
            done = $"已{verb} {group.Value.Count} 项：{string.Join("、", group.Value)}";
            group.Value = [];
        }
        back.Click += (_, _) => Act("退回");
        claim.Click += (_, _) => Act("认领");
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(CheckboxGroup.ValueProperty, typeof(CheckboxGroup)).AddValueChanged(group, (_, _) => Update());
        Update();
        return new Card { Padding = Space.None, Content = new StackPanel { Children = { group, bar } } };
    }
}
