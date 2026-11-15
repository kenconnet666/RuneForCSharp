using System.ComponentModel.DataAnnotations;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>StepsForm 的示例（vue-tsx-pro 文档站 patterns/StepsForm）。</summary>
static class StepsFormExamples
{
    public static Example[] All =>
    [
        new("StepsForm", "onboarding", "供应商开户", "三步：企业信息、联系人与账号、确认提交。“下一步”只校验这一步的字段（单行输入框里按回车也是下一步）；第三步是只读的汇总，提交没通过（第一步的信用代码被改坏）时跳回出错的那一步。Field 里只放控件，写了 Path 就自动绑定值。", Onboarding),
        new("StepsForm", "review", "标签在左、只读回看", "标签布局、只读同 Form：LabelPlacement = Start 把标签排在左边（LabelWidth 定宽、LabelAlign 对齐）；IsReadOnly 用来回看已经提交的单子——各步照常切换，控件都改不了，最后一步没有“提交”可点。", Review),
    ];

    public sealed class Supplier : IValidatableObject
    {
        [Required(ErrorMessage = "请填写公司全称"), MinLength(2, ErrorMessage = "请填写公司全称")]
        public string Company { get; set; } = "";

        [Required(ErrorMessage = "统一社会信用代码是 18 位数字或大写字母"), RegularExpression("^[0-9A-Z]{18}$", ErrorMessage = "统一社会信用代码是 18 位数字或大写字母")]
        public string CreditCode { get; set; } = "";

        [Required(ErrorMessage = "请选择供货品类")]
        public string? Category { get; set; }

        [Required(ErrorMessage = "请填写月产能"), Range(1, int.MaxValue, ErrorMessage = "至少 1 件")]
        public int? Capacity { get; set; }

        [Required(ErrorMessage = "请填写联系人")]
        public string Contact { get; set; } = "";

        [Required(ErrorMessage = "手机号是 11 位数字"), RegularExpression(@"^1\d{10}$", ErrorMessage = "手机号是 11 位数字")]
        public string Phone { get; set; } = "";

        [Required(ErrorMessage = "至少 8 位"), MinLength(8, ErrorMessage = "至少 8 位")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "请再输入一次密码")]
        public string Confirm { get; set; } = "";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Confirm.Length > 0 && Confirm != Password) yield return new ValidationResult("两次输入的密码不一致", [nameof(Confirm)]);
        }
    }

    static Control Onboarding()
    {
        var result = new Small { Tone = TextTone.Success, IsVisible = false };
        var form = new StepsForm { SubmitText = "确认开户" };
        FormController<Supplier>? controller = null;
        controller = new FormController<Supplier>(new Supplier())
        {
            Dependencies = new Dictionary<string, IReadOnlyList<string>> { ["Confirm"] = ["Password"] },
            OnSubmit = async values =>
            {
                await Task.Delay(600);
                result.Text = $"{values.Company} 已开户，账号已发到 {values.Phone}";
                result.IsVisible = true;
                controller!.Reset(new Supplier());
                form.Current = 0;
            },
        };

        static Grid Fields(params Field[] fields)
        {
            var grid = new Grid { MinItemWidth = new Length("16rem"), Gap = Space.Md };
            grid.Children.AddRange(fields);
            return grid;
        }

        Control Summary()
        {
            var values = controller.Values;
            return new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
                Children =
                {
                    new Descriptions
                    {
                        Items =
                        {
                            new DescriptionItem { Label = "公司全称", Value = values.Company },
                            new DescriptionItem { Label = "信用代码", Value = values.CreditCode },
                            new DescriptionItem { Label = "供货品类", Value = values.Category },
                            new DescriptionItem { Label = "月产能", Value = $"{values.Capacity?.ToString() ?? "—"} 件" },
                            new DescriptionItem { Label = "联系人", Value = values.Contact },
                            new DescriptionItem { Label = "手机号", Value = values.Phone },
                        },
                    },
                    new Small { Tone = TextTone.Muted, Text = "提交前可以点上方已完成的步骤回去修改。" },
                },
            };
        }

        form.Controller = controller;
        form.Steps =
        [
            new("企业信息", ["Company", "CreditCode", "Category", "Capacity"], () => Fields(
                new Field { Path = "Company", Label = "公司全称", Content = new TextField() },
                new Field { Path = "CreditCode", Label = "统一社会信用代码", Content = new TextField { PlaceholderText = "18 位" } },
                new Field { Path = "Category", Label = "供货品类", Content = new Select { ItemsSource = new[] { "机械件", "塑料件", "钣金件", "标准件", "线缆" }, Placeholder = "请选择" } },
                new Field { Path = "Capacity", Label = "月产能（件）", Content = new NumberField() }))
            { Description = "名称、资质与产能" },
            new("联系人与账号", ["Contact", "Phone", "Password", "Confirm"], () => Fields(
                new Field { Path = "Contact", Label = "联系人", Content = new TextField() },
                new Field { Path = "Phone", Label = "手机号", Content = new TextField() },
                new Field { Path = "Password", Label = "登录密码", Content = new TextField { Type = TextFieldType.Password } },
                new Field { Path = "Confirm", Label = "确认密码", Content = new TextField { Type = TextFieldType.Password } }))
            { Description = "登录账号发到手机" },
            new("确认提交", [], Summary) { Description = "核对后开户" },
        ];
        return new Card { Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Children = { form, result } } };
    }

    public sealed class Request
    {
        [Required(ErrorMessage = "请填写申请名称"), MinLength(2, ErrorMessage = "请填写申请名称")]
        public string Title { get; set; } = "二号线冲压机更换";

        [Required(ErrorMessage = "请选择事由")]
        public string? Reason { get; set; } = "设备更新";

        [Required(ErrorMessage = "请填写金额"), Range(1, double.MaxValue, ErrorMessage = "金额要大于 0")]
        public decimal? Amount { get; set; } = 386000;

        [Required(ErrorMessage = "请选择日期")]
        public string? NeedBy { get; set; } = "2024-09-30";

        [StringLength(200, ErrorMessage = "最多 200 字")]
        public string Note { get; set; } = "旧设备已超期服役两年，故障停机每月超过 20 小时。";
    }

    static Control Review()
    {
        var form = new StepsForm { LabelPlacement = FieldLabelPlacement.Start, LabelWidth = 6 * 14, LabelAlign = FieldLabelAlign.End, IsReadOnly = true, SubmitText = "重新提交" };
        var readOnly = new Switch { IsChecked = true, Content = "只读回看", Description = "关掉后可以修改并重新提交" };
        form.Controller = new FormController<Request>(new Request())
        {
            OnSubmit = async _ =>
            {
                await Task.Delay(400);
                readOnly.IsChecked = true;
            },
        };
        static Flex Fields(params Field[] fields)
        {
            var flex = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch };
            flex.Children.AddRange(fields);
            return flex;
        }
        form.Steps =
        [
            new("申请内容", ["Title", "Reason"], () => Fields(
                new Field { Path = "Title", Label = "申请名称", Content = new TextField() },
                new Field { Path = "Reason", Label = "事由", Content = new Select { ItemsSource = new[] { "设备更新", "产能扩充", "安全整改", "研发试制" } } })),
            new("金额与时间", ["Amount", "NeedBy", "Note"], () => Fields(
                new Field { Path = "Amount", Label = "金额（元）", Content = new NumberField() },
                new Field { Path = "NeedBy", Label = "最晚到位", Content = new DatePicker() },
                new Field { Path = "Note", Label = "说明", Content = new Textarea { Rows = 2 } })),
        ];
        readOnly.PropertyChanged += (_, e) => { if (e.Property == Switch.IsCheckedProperty) form.IsReadOnly = readOnly.IsChecked == true; };
        return new Card { Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Children = { readOnly, form } } };
    }
}
