using System.ComponentModel.DataAnnotations;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Form 的示例（vue-tsx-pro 文档站 form/Form）。数组字段、按模型生成的表单、分节目录还没做。</summary>
static partial class FormExamples
{
    public static Example[] All =>
    [
        new("Form", "admission", "供应商准入：自动绑定与错误汇总", "Field 写 Path、里面只放控件：值自动绑定模型的这个字段（开关写回 IsChecked）。FormErrorSummary 放在表单顶上：提交没通过时列出显示着的错误（按字段在表单里的顺序、带字段名），焦点先到它上面，点哪一项跳到那个字段，改对一项就少一项。跨字段的规则写在模型的 IValidatableObject 里，成员名指到要标错的字段；Dependencies 让开始日期改了之后结束日期跟着重新校验。", Admission),
        new("Form", "generated", "按 schema 生成：SchemaForm", "同一份准入申请：SchemaForm 按表单控制器的模型生成字段，控件按属性的类型选（文字、枚举或写了 SchemaOption 的下拉、DateOnly 的日期、数字、布尔的开关、枚举集合的多选、嵌套对象的一组、对象列表的可增删的行）。标签、说明、占位、分组用 Display（Name、Description、Prompt、GroupName），栏宽（Span）、换控件（Control：Radio、Checkbox、Checkboxes、Textarea……）写在 SchemaField 特性里。模型自己的默认值就是空白的初始值。某个字段要别的控件时写 Fields（字段路径 → 控件）；只想生成一部分时，在手写的 Form 里放 SchemaFields（Pick / Omit），其余照旧手写。", Generated),
        new("Form", "conditional", "字段的条件与权限", "显示、必填、只读的条件写在 SchemaField 特性里（VisibleWhen / RequiredWhen / ReadonlyWhen，条件同 ConditionBuilder 的 JSON）；FormController 的 Access 按字段路径给权限（Hidden、Readonly、Editable、Required，拿到当前的值，可以随环节变；明细表的列没写时跟着明细表），与流程的环节权限一一对应。不显示的字段不渲染、不校验，条件必填的空着报“必填”。FieldState(path) 给出字段此刻的样子。对象的列表写 Control = Table 排成明细表（一行一条，能增删行），列写 Sum = true 在表底精确合计（decimal 相加，不经过浮点数）。", Conditional),
        new("Form", "sources", "数据来源：字典、远程搜索、人员、附件", "选项、人员、上传的数据由应用登记一次：Provider.FormSources（Options、People、Upload；单个表单用 SchemaFields 的 Sources 覆盖）。字段的 SchemaField 特性只写名字：Source = \"CostCenters\" 取登记的选项——Items 是字典，Search 是按搜索词远程取（下拉带搜索框）；Control = People 用 PeoplePicker 从组织架构里选，Control = File 用 Upload 交给登记的上传函数。", Sources),
        new("Form", "read-view", "页签与只读视图", "SchemaField 特性写 Tab = \"费用\" 分进标签页（页签里照旧分组，没写的排在页签前面）。SchemaView 按同一个模型只读地显示值（审批时看单据、打印）：选项显示文字（含登记的字典）、开关显示是否、人员与附件显示名字、对象展开成“对象 · 字段”、明细表排成表格带合计；Access 与 VisibleWhen 不显示的不列，分组与页签各成一块。", ReadView),
        new("Form", "signup", "注册：校验与异步校验", "Field 写 Path、里面只放控件：值自动双向绑定，错误取自 FormController，失焦或提交过之后才显示；必填星号、最大长度、年龄的范围来自模型上的特性。用户名另有异步校验（防抖、只采用最后一次结果，提交时等它完成）。单行输入框里按回车提交，没通过时焦点移到第一个出错的字段。", Signup),
        new("Form", "layout", "标签在左侧、整组只读", "标签放在起始边在 Form 上统一设置：标签一列右对齐、控件一列，按第一行文字的基线对齐。IsReadOnly、IsEnabled 写在 Form 上，所有字段与控件（含勾选、开关、分段）一起只读或不可用。", Layout),
        new("Form", "section-review", "审批时整组只读", "申请内容整组只读（IsReadOnly 写在 FormSection 上），只有最后一组“审批意见”能填；标签放在起始边，两组用同一个宽度。分组有标题栏与细线，读屏是以标题为名称的一组。", SectionReview),
        new("Form", "section-collapsible", "可收起的分组、标题栏的操作", "不常填的“高级选项”默认收起（IsCollapsible，标题是按钮，IsOpen 双向绑定），两列、每列至少 14em；标题栏结束边放这一组的操作（添加一行）。重复的行只在第一行显示列名，其余行 IsLabelHidden（读屏照样念）。", SectionCollapsible),
    ];

    public sealed class Supplier : IValidatableObject
    {
        [Required(ErrorMessage = "请填写公司全称"), MinLength(2, ErrorMessage = "请填写公司全称")]
        public string Company { get; set; } = "";

        [Required(ErrorMessage = "统一社会信用代码是 18 位数字或大写字母"), RegularExpression("^[0-9A-Z]{18}$", ErrorMessage = "统一社会信用代码是 18 位数字或大写字母")]
        public string CreditCode { get; set; } = "";

        [Required(ErrorMessage = "请选择供货品类")]
        public string? Category { get; set; }

        [Required(ErrorMessage = "请填写联系人")]
        public string Contact { get; set; } = "";

        [Required(ErrorMessage = "手机号是 11 位数字"), RegularExpression(@"^1\d{10}$", ErrorMessage = "手机号是 11 位数字")]
        public string Phone { get; set; } = "";

        [Required(ErrorMessage = "请选择开始日期")]
        public string? ValidFrom { get; set; }

        [Required(ErrorMessage = "请选择结束日期")]
        public string? ValidTo { get; set; }

        [StringLength(200)]
        public string Note { get; set; } = "";

        [Range(typeof(bool), "true", "true", ErrorMessage = "请确认已阅读准入须知")]
        public bool Agreed { get; set; }

        // 跨字段：结束日期要晚于开始日期，错误标在结束日期上（ISO 日期按字符串比较就是先后）
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ValidFrom is { } from && ValidTo is { } to && string.CompareOrdinal(to, from) <= 0)
                yield return new ValidationResult("结束日期要晚于开始日期", [nameof(ValidTo)]);
        }
    }

    static Control Admission()
    {
        var result = new Small { Tone = TextTone.Muted };
        FormController<Supplier>? controller = null;
        controller = new FormController<Supplier>(new Supplier())
        {
            // 开始日期改了，结束日期（填过的话）跟着重新校验
            Dependencies = new Dictionary<string, IReadOnlyList<string>> { ["ValidTo"] = ["ValidFrom"] },
            OnSubmit = async values =>
            {
                await Task.Delay(600);
                result.Text = $"{values.Company} 的准入申请已提交";
                controller!.Reset(new Supplier());
            },
        };
        var grid = new Grid { MinItemWidth = new Length("20em"), Gap = Space.Md };
        grid.Children.AddRange(
        [
            new Field { Path = "Company", Label = "公司全称", Content = new TextField() },
            new Field { Path = "CreditCode", Label = "信用代码", Content = new TextField { PlaceholderText = "18 位" } },
            new Field { Path = "Category", Label = "供货品类", Content = new Select { ItemsSource = new[] { "机械件", "塑料件", "钣金件", "标准件", "线缆" }, Placeholder = "请选择" } },
            new Field { Path = "Contact", Label = "联系人", Content = new TextField() },
            new Field { Path = "Phone", Label = "手机号", Content = new TextField() },
            new Field { Path = "ValidFrom", Label = "有效期起", Content = new DateField() },
            new Field { Path = "ValidTo", Label = "有效期止", Content = new DateField() },
        ]);
        var reset = new Button { Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Content = "重置" };
        var submit = new Button { Content = "提交申请" };
        var form = new Form
        {
            Controller = controller,
            LabelPlacement = FieldLabelPlacement.Start,
            LabelWidth = 7 * 14,
            Child = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Md,
                Children =
                {
                    new FormErrorSummary(),
                    grid,
                    new Field { Path = "Note", Label = "补充说明", Content = new Textarea { Rows = 2, ShowCount = true, MaxLength = 200 } },
                    new Field { Path = "Agreed", Label = "准入须知", Content = new Switch { Content = "已阅读并同意准入须知" } },
                    new Flex { Gap = Space.Sm, Justify = FlexJustify.End, Align = FlexAlign.Center, Children = { result, reset, submit } },
                },
            },
        };
        reset.Command = form.ResetCommand;
        submit.Command = form.SubmitCommand;
        controller.Changed += (_, _) => submit.IsLoading = controller.IsSubmitting;
        return form;
    }

    public sealed class Account
    {
        [Required, StringLength(16, MinimumLength = 2, ErrorMessage = "至少 2 个字")]
        public string Username { get; set; } = "";

        [Required, EmailAddress(ErrorMessage = "邮箱格式不对")]
        public string Email { get; set; } = "";

        [Required, MinLength(8, ErrorMessage = "至少 8 位")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "请填写年龄"), Range(18, 120, ErrorMessage = "未满 18 岁")]
        public int? Age { get; set; } = 18;

        [StringLength(60)]
        public string Bio { get; set; } = "";
    }

    // 假的“用户名已占用”接口：400ms 后返回；新的输入到来时取消
    static readonly HashSet<string> Taken = ["admin", "root", "xiaoman"];

    static async Task<string?> CheckUsername(object? value, Account _, CancellationToken cancellation)
    {
        if (value is not string { Length: >= 2 } name) return null;
        await Task.Delay(400, cancellation);
        return Taken.Contains(name) ? "这个用户名已被占用" : null;
    }

    static Control Signup()
    {
        var result = new Small { Tone = TextTone.Muted };
        FormController<Account>? controller = null;
        controller = new FormController<Account>(new Account())
        {
            FieldValidators = new Dictionary<string, FieldValidator<Account>> { ["Username"] = new(CheckUsername) },
            OnSubmit = async values =>
            {
                await Task.Delay(800);
                result.Text = $"已注册 {values.Username}（{values.Email}）";
                controller!.Reset(controller.Values);
            },
        };
        var username = new Field { Path = "Username", Label = "用户名", Description = "admin、root、xiaoman 已被占用", Content = new TextField() };
        var form = new Form
        {
            Controller = controller,
            Child = new Flex
            {
                Direction = FlexDirection.Column,
                Gap = Space.Md,
                Children =
                {
                    username,
                    new Field { Path = "Email", Label = "邮箱", Content = new TextField() },
                    new Field { Path = "Password", Label = "密码", Content = new TextField { Type = TextFieldType.Password } },
                    new Field { Path = "Age", Label = "年龄", Content = new NumberField() },
                    new Field { Path = "Bio", Label = "一句话介绍", Content = new TextField { ShowCount = true } },
                },
            },
        };
        var reset = new Button { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "重置", Command = form.ResetCommand };
        var submit = new Button { Content = "注册", Command = form.SubmitCommand };
        controller.Changed += (_, _) =>
        {
            submit.IsLoading = controller.IsSubmitting;
            username.Description = controller.IsValidating("Username") ? "校验中…" : "admin、root、xiaoman 已被占用";
            if (controller.IsDirty) result.Text = "有未保存的改动";
        };
        ((Flex)form.Child).Children.Add(new Flex
        {
            Align = FlexAlign.Center,
            Justify = FlexJustify.Between,
            Gap = Space.Md,
            Children = { result, new Flex { Gap = Space.Sm, Children = { reset, submit } } },
        });
        return new Card { Content = form };
    }

    public sealed class Project
    {
        public string Name { get; set; } = "华东门店陈列改造";

        public string Category { get; set; } = "display";

        public string Scope { get; set; } = "all";

        public List<string> Funding { get; set; } = ["budget", "special"];

        public bool Notify { get; set; }

        public decimal? Stores { get; set; } = 40;

        public string Description { get; set; } = "收银台移到入口一侧，高频商品移到视线高度；货架由五层减为四层，单店节省陈列物料约 3000 元。";
    }

    static Control Layout()
    {
        var form = new Form { Controller = new FormController<Project>(new Project()), LabelPlacement = FieldLabelPlacement.Start, LabelWidth = 6.5 * 14, IsReadOnly = true };
        var category = new RadioGroup { Orientation = Orientation.Horizontal };
        category.Items.Add(new Radio { Value = "display", Content = "陈列调整" });
        category.Items.Add(new Radio { Value = "device", Content = "设备更新" });
        category.Items.Add(new Radio { Value = "process", Content = "流程优化" });
        var scope = new ToggleGroup { IsRequired = true, Size = TextSize.Sm };
        scope.Children.AddRange([new ToggleItem { Value = "all", Content = "全部门店" }, new ToggleItem { Value = "pilot", Content = "试点门店" }]);
        var funding = new CheckboxGroup { Orientation = Orientation.Horizontal };
        foreach (var (value, label) in new[] { ("budget", "年度预算"), ("special", "专项资金"), ("brand", "品牌合作"), ("self", "其他自筹") })
            funding.Items.Add(new Checkbox { Value = value, Content = label });
        form.Child = new Flex
        {
            Direction = FlexDirection.Column,
            Gap = Space.Md,
            Children =
            {
                new Field { Path = "Name", Label = "项目名称", IsRequired = true, Content = new TextField() },
                new Field { Path = "Category", Label = "改造类型", IsRequired = true, Content = category },
                new Field { Path = "Scope", Label = "执行范围", IsRequired = true, Content = scope },
                new Field { Path = "Funding", Label = "经费来源", Description = "可多选", Content = funding },
                new Field { Path = "Stores", Label = "涉及门店", Content = new NumberField { Min = 0, Suffix = "家" } },
                new Field { Path = "Notify", Label = "通知门店", Content = new Switch { Content = "开工前三天通知店长" } },
                new Field { Path = "Description", Label = "简要描述", IsRequired = true, Description = "说明改造内容与节省金额的计算依据", Content = new Textarea { Rows = 3 } },
            },
        };

        var mode = new ToggleGroup { IsRequired = true, Size = TextSize.Sm, Value = "readonly" };
        mode.Children.AddRange([new ToggleItem { Value = "edit", Content = "编辑" }, new ToggleItem { Value = "readonly", Content = "只读" }, new ToggleItem { Value = "disabled", Content = "不可用" }]);
        global::Avalonia.Automation.AutomationProperties.SetName(mode, "表单状态");
        mode.PropertyChanged += (_, e) =>
        {
            if (e.Property != ToggleGroup.ValueProperty) return;
            form.IsReadOnly = mode.Value == "readonly";
            form.IsEnabled = mode.Value != "disabled";
        };
        var header = new Flex
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Align = FlexAlign.Center,
            Justify = FlexJustify.Between,
            Gap = Space.Md,
            Wrap = true,
            Children = { new Strong { Text = "项目基本信息" }, mode },
        };
        return new Card { MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left, Header = header, Content = form };
    }

    static Control SectionReview()
    {
        const double labelWidth = 6.5 * 14;
        var decision = new RadioGroup { Orientation = Orientation.Horizontal };
        decision.Items.Add(new Radio { Value = "approve", Content = "同意" });
        decision.Items.Add(new Radio { Value = "return", Content = "退回修改" });
        decision.Items.Add(new Radio { Value = "reject", Content = "不同意" });
        var noteField = new Field { Label = "意见", IsRequired = true, Content = new Textarea { Rows = 3, PlaceholderText = "退回、不同意时写明原因" } };
        var submit = new Button { Content = "提交", IsEnabled = false };
        decision.PropertyChanged += (_, e) =>
        {
            if (e.Property != RadioGroup.ValueProperty) return;
            noteField.IsRequired = decision.Value != "approve";
            submit.IsEnabled = decision.Value is not null;
        };
        return new Flex
        {
            Direction = FlexDirection.Column,
            Gap = Space.Lg,
            Children =
            {
                new FormSection
                {
                    Title = "申请内容",
                    Description = "由申请人填写，审批时只读",
                    IsReadOnly = true,
                    LabelPlacement = FieldLabelPlacement.Start,
                    LabelWidth = labelWidth,
                    Children =
                    {
                        new Field { Label = "项目", Content = new TextField { Value = "显示器支架合并为一体成型" } },
                        new Field { Label = "编号", Content = new TextField { Value = "PO-2026-0187" } },
                        new Field { Label = "预计年节省", Content = new NumberField { Value = 174752, Suffix = "元" } },
                        new Field { Label = "说明", Content = new Textarea { Text = "两件合并为一件，减少紧固件 36 个，单件减重 0.4 kg。", Rows = 2 } },
                    },
                },
                new FormSection
                {
                    Title = "审批意见",
                    LabelPlacement = FieldLabelPlacement.Start,
                    LabelWidth = labelWidth,
                    Children = { new Field { Label = "结论", IsRequired = true, Content = decision }, noteField },
                },
                new Flex { Justify = FlexJustify.End, Children = { submit } },
            },
        };
    }

    sealed record Contact(string Name, string Role, string Phone);

    static Control SectionCollapsible()
    {
        var contacts = new List<Contact> { new("周岚", "销售经理", "13800001234"), new("陈默", "质量工程师", "13900005678") };
        var add = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.Plus, Content = "添加联系人" };
        var people = new FormSection { Title = "联系人", Extra = add };
        void Rebuild()
        {
            people.Description = $"{contacts.Count} 位，至少保留一位";
            people.Children.Clear();
            for (var index = 0; index < contacts.Count; index++)
            {
                var at = index;
                var contact = contacts[index];
                var remove = new IconButton { Icon = Lucide.Trash, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, IsEnabled = contacts.Count > 1 };
                global::Avalonia.Automation.AutomationProperties.SetName(remove, $"删除第 {index + 1} 位联系人");
                remove.Click += (_, _) =>
                {
                    contacts.RemoveAt(at);
                    Rebuild();
                };
                people.Children.Add(new Flex
                {
                    Align = FlexAlign.End,
                    Gap = Space.Sm,
                    Children =
                    {
                        new Field { Label = "姓名", IsLabelHidden = index > 0, [Flex.GrowProperty] = true, Content = new TextField { Value = contact.Name } },
                        new Field { Label = "职务", IsLabelHidden = index > 0, [Flex.GrowProperty] = true, Content = new TextField { Value = contact.Role } },
                        new Field { Label = "手机", IsLabelHidden = index > 0, [Flex.GrowProperty] = true, Content = new TextField { Value = contact.Phone } },
                        remove,
                    },
                });
            }
        }
        add.Click += (_, _) =>
        {
            contacts.Add(new Contact("", "", ""));
            Rebuild();
        };
        Rebuild();

        var notify = new Switch { Content = "通知联系人", IsChecked = true };
        var channel = new Select
        {
            ItemsSource = new[] { new ListboxOption("email", "邮件"), new ListboxOption("sms", "短信"), new ListboxOption("portal", "供应商门户站内信") },
            Value = "email",
        };
        notify.PropertyChanged += (_, e) =>
        {
            if (e.Property == ChoiceBase.IsCheckedProperty) channel.IsEnabled = notify.IsChecked;
        };
        var advanced = new FormSection
        {
            Title = "高级选项",
            Description = "通知方式、对接系统；不填时按默认",
            IsCollapsible = true,
            IsOpen = false,
            Columns = 2,
            MinColumnWidth = new Length("14em"),
            Children =
            {
                new Field { Label = "状态变更时通知", Content = notify },
                new Field { Label = "通知方式", Content = channel },
            },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Children = { people, advanced } };
    }
}
