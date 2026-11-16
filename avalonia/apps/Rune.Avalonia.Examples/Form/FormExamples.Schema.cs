using System.ComponentModel.DataAnnotations;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Rune.Avalonia.Examples;

/// <summary>Form 页里按模型生成的示例：SchemaForm、页签与只读视图（SchemaView）。</summary>
static partial class FormExamples
{
    /// <summary>供应商准入申请：标签、占位、分组写在 Display 里，栏宽与控件写在 SchemaField 里。</summary>
    public sealed class AdmissionApply : IValidatableObject
    {
        [Display(Name = "公司全称", GroupName = "企业"), Required(ErrorMessage = "请填写公司全称"), MinLength(2, ErrorMessage = "请填写公司全称")]
        public string Company { get; set; } = "";

        [Display(Name = "信用代码", Prompt = "18 位", GroupName = "企业"), Required(ErrorMessage = "统一社会信用代码是 18 位数字或大写字母"),
         RegularExpression("^[0-9A-Z]{18}$", ErrorMessage = "统一社会信用代码是 18 位数字或大写字母")]
        public string CreditCode { get; set; } = "";

        [Display(Name = "供货品类", Prompt = "请选择", GroupName = "企业"), Required(ErrorMessage = "请选择供货品类"),
         SchemaOption("机械件"), SchemaOption("塑料件"), SchemaOption("钣金件"), SchemaOption("标准件"), SchemaOption("线缆")]
        public string? Category { get; set; }

        [Display(Name = "联系人", GroupName = "联系与有效期"), Required(ErrorMessage = "请填写联系人")]
        public string Contact { get; set; } = "";

        [Display(Name = "手机号", GroupName = "联系与有效期"), Required(ErrorMessage = "手机号是 11 位数字"), RegularExpression("^1[0-9]{10}$", ErrorMessage = "手机号是 11 位数字")]
        public string Phone { get; set; } = "";

        [Display(Name = "有效期起", GroupName = "联系与有效期"), Required(ErrorMessage = "请选择开始日期")]
        public DateOnly? ValidFrom { get; set; }

        [Display(Name = "有效期止", GroupName = "联系与有效期"), Required(ErrorMessage = "请选择结束日期")]
        public DateOnly? ValidTo { get; set; }

        [Display(Name = "补充说明", GroupName = "确认"), StringLength(200), SchemaField(Control = SchemaControl.Textarea, Span = SchemaFieldAttribute.Full)]
        public string Note { get; set; } = "";

        [Display(Name = "已阅读并同意准入须知", GroupName = "确认"), Range(typeof(bool), "true", "true", ErrorMessage = "请确认已阅读准入须知"), SchemaField(Control = SchemaControl.Checkbox)]
        public bool Agreed { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (ValidFrom is { } from && ValidTo is { } to && to <= from) yield return new ValidationResult("结束日期要晚于开始日期", [nameof(ValidTo)]);
        }
    }

    static Control Generated()
    {
        FormController<AdmissionApply>? controller = null;
        var form = new SchemaForm { Columns = 2, SubmitText = "提交申请" };
        controller = new FormController<AdmissionApply>(new AdmissionApply())
        {
            Dependencies = new Dictionary<string, IReadOnlyList<string>> { ["ValidTo"] = ["ValidFrom"] },
            OnSubmit = async values =>
            {
                await Task.Delay(600);
                form.Content = $"{values.Company} 的准入申请已提交";
                controller!.Reset();
            },
        };
        form.Controller = controller;
        return form;
    }

    public enum Transport
    {
        [Display(Name = "火车")] Train,
        [Display(Name = "飞机")] Plane,
        [Display(Name = "自驾")] Car,
    }

    public sealed class TripLine
    {
        [Display(Name = "项目"), Required(ErrorMessage = "请填写项目")] public string Item { get; set; } = "";
        [Display(Name = "金额"), Required(ErrorMessage = "请填写金额"), SchemaField(Unit = "元", Sum = true)] public decimal? Cost { get; set; }
    }

    /// <summary>出差申请：字段按 SchemaField 的 Tab 分进标签页（没写的排在页签前面）。</summary>
    public sealed class TripRequest
    {
        [Display(Name = "出差事由"), Required(ErrorMessage = "请填写事由"), SchemaField(Span = SchemaFieldAttribute.Full)] public string Title { get; set; } = "";
        [Display(Name = "目的地"), Required(ErrorMessage = "请填写城市"), SchemaField(Tab = "行程")] public string City { get; set; } = "";
        [Display(Name = "天数"), Required(ErrorMessage = "请填写天数"), SchemaField(Unit = "天", Tab = "行程")] public int? Days { get; set; }
        [Display(Name = "交通方式"), SchemaField(Control = SchemaControl.Radio, Tab = "行程")] public Transport Transport { get; set; }
        [Display(Name = "预支借款"), SchemaField(Tab = "费用")] public bool Advance { get; set; }
        [Display(Name = "预算明细"), SchemaField(Control = SchemaControl.Table, Tab = "费用")] public List<TripLine> Lines { get; set; } = [];
        // 只给财务看：查看时按权限不列
        [Display(Name = "借款账户"), SchemaField(Tab = "费用")] public string? Account { get; set; }
    }

    static Control ReadView()
    {
        var result = "";
        SchemaForm? editor = null;
        var controller = new FormController<TripRequest>(new TripRequest
        {
            Title = "华东区客户回访", Transport = Transport.Train, Advance = true,
            Lines = [new() { Item = "高铁", Cost = 553m }, new() { Item = "酒店", Cost = 1260.5m }],
        })
        {
            OnSubmit = _ =>
            {
                result = "已提交";
                if (editor is not null) editor.Content = result;
                return Task.CompletedTask;
            },
        };
        editor = new SchemaForm { Controller = controller, Columns = 2 };
        var view = new SchemaView { Access = new Dictionary<string, SchemaFieldAccess> { ["Account"] = SchemaFieldAccess.Hidden } };
        view.Styles.Add(new global::Avalonia.Styling.Style(x => x.OfType<Descriptions>().Name(SchemaViewParts.Section)) { Setters = { new global::Avalonia.Styling.Setter(Descriptions.BorderedProperty, true) } });
        var host = new ContentControl { Content = editor };
        var mode = new RadioGroup { Orientation = Orientation.Horizontal, Value = "edit", Items = { new Radio { Value = "edit", Content = "填写" }, new Radio { Value = "view", Content = "查看" } } };
        global::Avalonia.Automation.AutomationProperties.SetName(mode, "视图");
        mode.PropertyChanged += (_, e) =>
        {
            if (e.Property != RadioGroup.ValueProperty) return;
            if (mode.Value == "view")
            {
                view.Values = controller.Values;
                view.Refresh();
                host.Content = view;
            }
            else host.Content = editor;
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Children = { mode, host } };
    }
}
