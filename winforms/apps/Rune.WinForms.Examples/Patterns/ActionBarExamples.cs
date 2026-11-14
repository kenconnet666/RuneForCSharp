namespace Rune.WinForms.Examples;

/// <summary>
/// ActionBar 页的示例（vue-tsx-pro 文档站 patterns/ActionBar）。上游的长表单用 Form 与 FormSection，这里还没有，用 FieldControl 排、自己数改了几处；
/// WinForms 没有吸底，长表单的操作栏放在滚动区域下面，一直看得见。
/// </summary>
static class ActionBarExamples
{
    public static Example[] All =>
    [
        new("ActionBar", "long-form", "长表单的提交栏", "WinForms 没有网页的吸底：操作栏放在滚动区域下面（滚动区域外），一直看得见。Start 写着改了几处，按钮靠结束边。", LongForm),
        new("ActionBar", "batch", "批量操作", "列表下面一行：起始边是已选几项，选了才能操作。", Batch),
    ];

    static readonly (string Label, string Value)[] Fields =
    [
        ("供应商名称", "供应商甲精密制造有限公司"), ("统一社会信用代码", "91310115MA1K4XXX0Q"), ("联系人", "周岚"), ("手机", "13800001234"),
        ("地址", "上海市浦东新区某路 88 号"), ("开户行", "某银行上海浦东支行"), ("账号", "6222 0210 0100 1234 567"),
    ];

    static Control LongForm()
    {
        // 保存过的值：撤销回到它，保存时更新它
        var saved = Fields.Select(field => field.Value).ToArray();
        var inputs = saved.Select(value => new TextFieldControl { Text = value }).ToArray();
        var note = new TextareaControl { Rows = 3 };
        var form = Build.Grid(new GridControl { MinItemWidth = "15em", Gap = Space.Lg }, [.. Fields.Select((field, i) => (Control)new FieldControl { Label = field.Label, Content = inputs[i] })]);
        var reset = new ButtonControl { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Text = "撤销改动" };
        var save = new ButtonControl { Text = "保存" };
        var bar = new ActionBarControl { Dock = DockStyle.Bottom };
        bar.Actions.AddRange([reset, save]);
        int Changed() => inputs.Where((input, i) => input.Text != saved[i]).Count() + (note.Text.Length > 0 ? 1 : 0);
        void Update()
        {
            var changed = Changed();
            bar.Start = changed > 0 ? $"有 {changed} 处改动还没保存" : "没有改动";
            reset.Enabled = save.Enabled = changed > 0;
        }
        foreach (var input in inputs) input.ValueChanged += (_, _) => Update();
        note.TextChanged += (_, _) => Update();
        reset.Click += (_, _) =>
        {
            for (var i = 0; i < inputs.Length; i++) inputs[i].Text = saved[i];
            note.Text = "";
        };
        save.Click += (_, _) =>
        {
            for (var i = 0; i < inputs.Length; i++) saved[i] = inputs[i].Text;
            note.Text = "";
            Update();
        };
        Update();
        var body = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xl, Align = FlexAlign.Stretch, Padding = new Padding(24) },
            new H5Control { Text = "基本信息与结算" }, form, new H5Control { Text = "变更说明" },
            new FieldControl { Label = "为什么改", Description = "审批人会看到，写清楚改了什么、依据是什么", Content = note });
        var area = new ScrollAreaControl { Content = body, AccessibleName = "供应商资料", Dock = DockStyle.Fill };
        var frame = new Panel { Dock = DockStyle.Top };
        frame.Height = frame.LogicalToDeviceUnits(24 * 16);
        frame.Controls.AddRange([area, bar]);
        return Build.Card(new CardControl { CardPadding = Space.None }, frame);
    }

    static readonly (string Id, string Title, string Due)[] Items =
    [
        ("PO-2026-0187", "显示器支架改为一体成型", "剩 2 个工作日"),
        ("PO-2026-0192", "包装胶带换国产料号", "今天到期"),
        ("PO-2026-0203", "线束卡箍合并规格", "剩 5 个工作日"),
        ("PO-2026-0211", "货架紧固件减量", "剩 9 个工作日"),
    ];

    static Control Batch()
    {
        var group = new CheckboxGroupControl { AccessibleName = "待认领的建议", Padding = new Padding(24) };
        group.Controls.AddRange([.. Items.Select(item => new CheckboxControl { Value = item.Id, Text = $"{item.Title}　{item.Id} · {item.Due}" })]);
        var back = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Danger, Icon = Lucide.Undo2, Text = "退回" };
        var claim = new ButtonControl { Icon = Lucide.CheckCheck, Text = "认领" };
        var bar = new ActionBarControl();
        bar.Actions.AddRange([back, claim]);
        var done = "";
        void Update()
        {
            var count = group.Value.Count;
            bar.Start = count > 0 ? $"已选 {count} 项" : done is "" ? "勾选要处理的建议" : done;
            back.Enabled = claim.Enabled = count > 0;
        }
        void Act(string verb)
        {
            done = $"已{verb} {group.Value.Count} 项：{string.Join("、", group.Value)}";
            group.Value = [];
        }
        back.Click += (_, _) => Act("退回");
        claim.Click += (_, _) => Act("认领");
        group.ValueChanged += (_, _) => Update();
        Update();
        return Build.Card(new CardControl { CardPadding = Space.None }, group, bar);
    }
}
