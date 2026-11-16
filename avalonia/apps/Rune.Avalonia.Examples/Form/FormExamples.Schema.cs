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

    public sealed class ExpenseLine
    {
        [Display(Name = "项目"), Required(ErrorMessage = "请填写项目")] public string Item { get; set; } = "";
        [Display(Name = "金额"), Required(ErrorMessage = "请填写金额"), SchemaField(Unit = "元", Sum = true)] public decimal? Cost { get; set; }
    }

    /// <summary>费用报销单：条件写在 SchemaField 特性里，权限按环节给（Access）。</summary>
    public sealed class Expense
    {
        [Display(Name = "费用类型"), Required(ErrorMessage = "请选择费用类型"), SchemaField(Control = SchemaControl.Radio),
         SchemaOption("travel", "差旅"), SchemaOption("meal", "招待"), SchemaOption("other", "其他")]
        public string? Kind { get; set; }

        [Display(Name = "出差城市"), SchemaField(VisibleWhen = """{"field":"Kind","op":"eq","value":"travel"}""")]
        public string? City { get; set; }

        [Display(Name = "招待人数"), SchemaField(Unit = "人", VisibleWhen = """{"field":"Kind","op":"eq","value":"meal"}""", RequiredWhen = """{"field":"Kind","op":"eq","value":"meal"}""")]
        public int? Guests { get; set; }

        [Display(Name = "金额"), Required(ErrorMessage = "请填写金额"), SchemaField(Unit = "元")]
        public decimal? Amount { get; set; }

        // 超过 5000 要写说明
        [Display(Name = "说明"), SchemaField(Control = SchemaControl.Textarea, Span = SchemaFieldAttribute.Full, RequiredWhen = """{"field":"Amount","op":"gt","value":5000}""")]
        public string? Note { get; set; }

        [Display(Name = "核定金额"), SchemaField(Unit = "元")]
        public decimal? Approved { get; set; }

        [Display(Name = "明细"), SchemaField(Control = SchemaControl.Table)]
        public List<ExpenseLine> Lines { get; set; } = [];
    }

    /// <summary>两个环节的字段权限：申请人填写时看不到核定金额，财务审核时只改核定金额。</summary>
    static readonly Dictionary<string, IReadOnlyDictionary<string, SchemaFieldAccess>> Steps = new()
    {
        ["apply"] = new Dictionary<string, SchemaFieldAccess> { ["Approved"] = SchemaFieldAccess.Hidden },
        ["review"] = new Dictionary<string, SchemaFieldAccess>
        {
            ["Kind"] = SchemaFieldAccess.Readonly, ["City"] = SchemaFieldAccess.Readonly, ["Guests"] = SchemaFieldAccess.Readonly, ["Amount"] = SchemaFieldAccess.Readonly,
            ["Note"] = SchemaFieldAccess.Readonly, ["Lines"] = SchemaFieldAccess.Readonly, ["Approved"] = SchemaFieldAccess.Required,
        },
    };

    static Control Conditional()
    {
        var step = "apply";
        var form = new SchemaForm { Columns = 2 };
        var controller = new FormController<Expense>(new Expense { Kind = "travel", Lines = [new() { Item = "高铁", Cost = 553m }, new() { Item = "酒店", Cost = 1260.5m }] })
        {
            Access = _ => Steps[step],
            OnSubmit = async _ =>
            {
                await Task.Delay(400);
                form.Content = step == "apply" ? "已提交，等财务审核" : "已审核";
            },
        };
        form.Controller = controller;
        var steps = new RadioGroup { Orientation = Orientation.Horizontal, Value = step, Items = { new Radio { Value = "apply", Content = "申请人填写" }, new Radio { Value = "review", Content = "财务审核" } } };
        global::Avalonia.Automation.AutomationProperties.SetName(steps, "环节");
        void Sync()
        {
            form.SubmitText = step == "apply" ? "提交" : "审核通过";
            controller.Refresh();
        }
        steps.PropertyChanged += (_, e) =>
        {
            if (e.Property != RadioGroup.ValueProperty) return;
            step = steps.Value ?? "apply";
            Sync();
        };
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Children = { steps, form } };
    }

    /// <summary>采购申请：选项、人员、上传都来自登记的数据来源，字段只写名字。</summary>
    public sealed class PurchaseApply
    {
        [Display(Name = "标题"), Required(ErrorMessage = "请填写标题"), SchemaField(Span = SchemaFieldAttribute.Full)] public string Title { get; set; } = "";
        [Display(Name = "成本中心"), Required(ErrorMessage = "请选择成本中心"), SchemaField(Source = "CostCenters")] public string? CostCenter { get; set; }
        [Display(Name = "供应商", Prompt = "输入名称搜索"), Required(ErrorMessage = "请选择供应商"), SchemaField(Source = "Suppliers")] public string? Supplier { get; set; }
        [Display(Name = "经办人"), MinLength(1, ErrorMessage = "请选择经办人"), SchemaField(Control = SchemaControl.People)] public List<PeopleEntry> Handler { get; set; } = [];
        [Display(Name = "附件"), SchemaField(Control = SchemaControl.File, Span = SchemaFieldAttribute.Full)] public List<UploadFile> Files { get; set; } = [];
    }

    static readonly string[] SupplierNames = ["华东钢材", "华南电子", "北方塑胶", "西部线缆", "东海标准件", "中原包装"];

    static readonly FormSources PurchaseSources = new()
    {
        Options = new Dictionary<string, OptionSource>
        {
            ["CostCenters"] = new SchemaOption[] { new("rd", "研发中心"), new("mfg", "制造部"), new("sales", "销售部") },
            // 远程搜索：真实应用里是请求（取消令牌用来取消被顶掉的那次）
            ["Suppliers"] = new OptionSource
            {
                Search = async (query, cancellation) =>
                {
                    await Task.Delay(300, cancellation);
                    return [.. SupplierNames.Where(name => name.Contains(query)).Select(name => new SchemaOption(name, name))];
                },
            },
        },
        People = PeoplePickerExamples.Local,
        // 上传：真实应用里传到文件服务，返回的结果记在文件上
        Upload = async (file, progress, cancellation) =>
        {
            for (var at = 1; at <= 4; at++)
            {
                await Task.Delay(150, cancellation);
                progress.Report(at / 4d);
            }
            return new Uri($"https://example.com/files/{Uri.EscapeDataString(file.Name)}");
        },
    };

    static Control Sources()
    {
        var form = new SchemaForm { Columns = 2 };
        form.Controller = new FormController<PurchaseApply>(new PurchaseApply())
        {
            OnSubmit = values =>
            {
                form.Content = $"已提交：{values.Title}";
                return Task.CompletedTask;
            },
        };
        return new Provider { FormSources = PurchaseSources, Child = form };
    }
}
