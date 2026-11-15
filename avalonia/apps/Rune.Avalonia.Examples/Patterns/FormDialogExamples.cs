using System.ComponentModel.DataAnnotations;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>FormDialog 的示例（vue-tsx-pro 文档站 patterns/FormDialog）。reschedule（日期字段）等日期组件做了再补。</summary>
static class FormDialogExamples
{
    public static Example[] All =>
    [
        new("FormDialog", "prompt", "退回与改派", "命令式的小表单对话框 FormDialog.PromptAsync：退回要写原因（必填、至少 6 个字），改派从名单里选；OnSubmit 进行时确认按钮加载，抛异常时对话框留着。确认后得到填好的值，取消、Esc 时是 null。", Prompt),
        new("FormDialog", "component", "FormDialog 组件：自己排版的表单", "字段多、有联动时用 FormDialog 组件自己排版，确认就是提交：没通过校验时留着、焦点到错误汇总；这里模拟后端，名称重复时 OnSubmit 抛 FormSubmitException，说明显示在表单顶上，对话框留着。", Component),
    ];

    static Control Prompt()
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
            Children = { new Flex { Gap = Space.Sm, Wrap = true, Children = { giveBack, reassign } }, result },
        };
    }

    public sealed class Department
    {
        [Required(ErrorMessage = "请填部门名称"), StringLength(20, ErrorMessage = "最多 20 个字")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "请选类型")]
        public string Kind { get; set; } = "business";
    }

    static readonly string[] Taken = ["采购部", "财务部"];

    static Control Component()
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
