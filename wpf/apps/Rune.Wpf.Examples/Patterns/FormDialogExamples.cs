using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>FormDialog 的示例（vue-tsx-pro 文档站 patterns/FormDialog）。</summary>
static class FormDialogExamples
{
    public static Example[] All =>
    [
        new("FormDialog", "prompt", "退回与改派", "命令式的小表单对话框 FormDialog.PromptAsync：退回要写原因（必填、至少 6 个字），改派从名单里选；OnSubmit 进行时确认按钮加载，抛异常时对话框留着。确认后得到填好的值，取消、Esc 时是 null。", Prompt),
        new("FormDialog", "reschedule", "申请调整计划日期", "日期字段限定范围（Min、Max），原因从带说明的选项里选，“其他”时说明必填（跨字段校验）；推迟超过 30 天时 OnSubmit 抛异常，提交被拒、对话框留着。", Reschedule),
        new("FormDialog", "component", "FormDialog 组件：自己排版的表单", "字段多、有联动时用 FormDialog 组件自己排版，确认就是提交：没通过校验时留着、焦点到错误汇总；这里模拟后端，名称重复时 OnSubmit 抛 FormSubmitException，说明显示在表单顶上，对话框留着。", Component),
    ];

    static FrameworkElement Prompt()
    {
        var result = new Small { Tone = TextTone.Muted, Text = "点一个按钮" };
        var giveBack = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Danger, Icon = Lucide.Undo2, Content = "退回收件箱" };
        var reassign = new Button { Variant = ButtonVariant.Outline, Icon = Lucide.UserRoundCog, Content = "指定承办人" };
        giveBack.Click += async (_, _) =>
        {
            var values = await FormDialog.PromptAsync(giveBack, new("退回收件箱",
            [
                new("Reason", "原因")
                {
                    Type = FormPromptFieldType.Textarea,
                    Required = true,
                    MaxLength = 200,
                    Validate = (value, _) => value.Length < 6 ? "至少写 6 个字，说清为什么" : null,
                },
            ])
            {
                Description = "退回后这一条回到岗位收件箱，你不能再认领它。",
                ConfirmText = "退回",
                Tone = StatusTone.Danger,
                OnSubmit = _ => Task.Delay(600),
            });
            result.Text = values is null ? "取消了" : $"已退回：{values["Reason"]}";
        };
        reassign.Click += async (_, _) =>
        {
            var values = await FormDialog.PromptAsync(reassign, new("指定承办人",
            [
                new("Who", "承办人") { Type = FormPromptFieldType.Select, Required = true, Options = [new("E50913", "王晴"), new("E50377", "唐可")] },
                new("Note", "留言") { Placeholder = "可以不写" },
            ])
            { ConfirmText = "指定" });
            result.Text = values is null ? "取消了" : $"已指定 {values["Who"]}{(values["Note"] is string { Length: > 0 } note ? $"：{note}" : "")}";
        };
        return new Flex
        {
            Direction = FlexDirection.Column,
            Gap = Space.Md,
            Align = FlexAlign.Start,
            Children = { new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { giveBack, reassign } }, result },
        };
    }

    static FrameworkElement Reschedule()
    {
        const string original = "2026-11-20";
        var planned = DateOnly.Parse(original, CultureInfo.InvariantCulture);
        var result = new Small { Tone = TextTone.Muted, Text = $"当前计划完成：{original}" };
        var button = new Button { Variant = ButtonVariant.Outline, Icon = Lucide.CalendarClock, Content = "调整计划日期" };
        static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        button.Click += async (_, _) =>
        {
            var values = await FormDialog.PromptAsync(button, new("申请调整计划完成日期",
            [
                new("Due", "新的完成日期")
                {
                    Type = FormPromptFieldType.Date,
                    Required = true,
                    DefaultValue = Iso(planned.AddDays(7)),
                    Min = Iso(planned.AddDays(1)),
                    Max = Iso(planned.AddDays(90)),
                    Description = "只能推迟，最多 90 天",
                },
                new("Cause", "原因")
                {
                    Type = FormPromptFieldType.Select,
                    Required = true,
                    Options =
                    [
                        new("supplier", "供应商交付延迟") { Description = "附上供应商的书面说明" },
                        new("design", "设计变更") { Description = "写明变更单号" },
                        new("test", "验证不通过") { Description = "需要重新试制" },
                        new("other", "其他") { Description = "在说明里写清楚" },
                    ],
                },
                new("Note", "说明")
                {
                    Type = FormPromptFieldType.Textarea,
                    MaxLength = 300,
                    Placeholder = "选“其他”时必填",
                    Validate = (value, all) => all["Cause"] == "other" && value.Length == 0 ? "原因选了“其他”，写一下具体情况" : null,
                },
            ])
            {
                Description = $"当前计划 {Iso(planned)}。提交后由项目经理审批，审批通过前仍按原计划考核。",
                ConfirmText = "提交申请",
                Size = DialogSize.Md,
                // 后端的规则：推迟超过 30 天要走分级审批，这里直接拒绝；拒绝时对话框留着，可以改日期再提交
                OnSubmit = async submitted =>
                {
                    await Task.Delay(600);
                    if (DateOnly.Parse((string)submitted["Due"], CultureInfo.InvariantCulture).DayNumber - planned.DayNumber <= 30) return;
                    result.Text = "提交失败：推迟超过 30 天要走分级审批，改一下日期再提交";
                    result.Tone = TextTone.Danger;
                    throw new InvalidOperationException("too late");
                },
            });
            if (values is null)
            {
                result.Text = "取消了";
                result.Tone = TextTone.Muted;
                return;
            }
            var due = DateOnly.Parse((string)values["Due"], CultureInfo.InvariantCulture);
            planned = due;
            result.Text = $"已提交：推迟到 {Iso(due)}（{due.DayNumber - DateOnly.Parse(original, CultureInfo.InvariantCulture).DayNumber} 天）";
            result.Tone = TextTone.Muted;
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { button, result } };
    }

    public sealed class Department
    {
        [Required(ErrorMessage = "请填部门名称"), StringLength(20, ErrorMessage = "最多 20 个字")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "请选类型")]
        public string Kind { get; set; } = "business";
    }

    static readonly string[] Taken = ["采购部", "财务部"];

    static FrameworkElement Component()
    {
        var created = new List<string>();
        var summary = new Small { Tone = TextTone.Muted };
        void Show() => summary.Text = $"已建：{(created.Count > 0 ? string.Join("、", created) : "还没有")}（试试“采购部”，会被后端拒绝）";
        Show();
        FormDialog? dialog = null;
        var controller = new FormController<Department>(new Department())
        {
            OnSubmit = async values =>
            {
                await Task.Delay(400);
                // 后端的业务拒绝：说明显示在表单顶上，对话框留着
                if (Taken.Contains(values.Name.Trim())) throw new FormSubmitException(form: $"部门名称已存在: {values.Name.Trim()}");
                created.Add(values.Name.Trim());
                Show();
                dialog!.IsOpen = false;
            },
        };
        dialog = new FormDialog
        {
            Controller = controller,
            Title = "新建部门",
            ConfirmText = "保存",
            Size = DialogSize.Sm,
            Content = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Md,
                Children =
                {
                    new Field { Path = "Name", Label = "部门名称", Content = new TextField { PlaceholderText = "例如：质量部" } },
                    new Field { Path = "Kind", Label = "类型", Content = new Select { ItemsSource = new[] { new ListboxOption("business", "业务部门"), new ListboxOption("support", "职能部门") } } },
                },
            },
        };
        var open = new Button { Content = "新建部门" };
        open.Click += (_, _) =>
        {
            controller.Reset(new Department());
            dialog.IsOpen = true;
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Start, Children = { open, summary, dialog } };
    }
}
