using System.ComponentModel.DataAnnotations;

namespace Rune.WinForms.Examples;

/// <summary>Form 的示例（vue-tsx-pro 文档站 form/Form）。WinForms 里是 FormControl、FieldControl。数组字段、按模型生成的表单、分节目录还没做；错误汇总的 admission 要用日期字段。</summary>
static class FormExamples
{
    public static Example[] All =>
    [
        new("Form", "signup", "注册：校验与异步校验", "FieldControl 写 Path、里面只放控件：值自动双向绑定，错误取自 FormController，失焦或提交过之后才显示；必填星号、最大长度、年龄的范围来自模型上的特性。用户名另有异步校验（防抖、只采用最后一次结果，提交时等它完成）。单行输入框里按回车提交，没通过时焦点移到第一个出错的字段。", Signup),
        new("Form", "layout", "标签在左侧、整组只读", "标签放在起始边在 FormControl 上统一设置：标签一列右对齐、控件一列，按第一行文字的基线对齐。IsReadOnly、Enabled 写在 FormControl 上，所有字段与控件（含勾选、开关、分段）一起只读或不可用。", Layout),
        new("Form", "section-review", "审批时整组只读", "申请内容整组只读（IsReadOnly 写在 FormSectionControl 上），只有最后一组“审批意见”能填；标签放在起始边，两组用同一个宽度。分组有标题栏与细线，读屏是以标题为名称的一组。", SectionReview),
        new("Form", "section-collapsible", "可收起的分组、标题栏的操作", "不常填的“高级选项”默认收起（IsCollapsible，标题是按钮，变了发 OpenChanged），两列、每列至少 14em；标题栏结束边放这一组的操作（添加一行）。重复的行只在第一行显示列名，其余行 IsLabelHidden（读屏照样念）。", SectionCollapsible),
    ];

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
        var result = new SmallControl { Tone = TextTone.Muted };
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
        var username = new FieldControl { Path = "Username", Label = "用户名", Description = "admin、root、xiaoman 已被占用", Content = new TextFieldControl() };
        var reset = new ButtonControl { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Text = "重置" };
        var submit = new ButtonControl { Text = "注册" };
        var actions = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md },
            result, Build.Flex(new FlexControl { Gap = Space.Sm }, reset, submit));
        var form = new FormControl { Controller = controller, Dock = DockStyle.Top };
        form.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true, Dock = DockStyle.Top },
            username,
            new FieldControl { Path = "Email", Label = "邮箱", Content = new TextFieldControl() },
            new FieldControl { Path = "Password", Label = "密码", Content = new TextFieldControl { Type = TextFieldType.Password } },
            new FieldControl { Path = "Age", Label = "年龄", Content = new NumberFieldControl() },
            new FieldControl { Path = "Bio", Label = "一句话介绍", Content = new TextFieldControl { ShowCount = true } },
            actions));
        reset.Click += (_, _) => form.Reset();
        submit.Click += (_, _) => _ = form.SubmitAsync();
        controller.Changed += (_, _) =>
        {
            submit.IsLoading = controller.IsSubmitting;
            username.Description = controller.IsValidating("Username") ? "校验中…" : "admin、root、xiaoman 已被占用";
            if (controller.IsDirty) result.Text = "有未保存的改动";
        };
        return Build.Card(new CardControl { Width = 480 }, form);
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
        var form = new FormControl { Controller = new FormController<Project>(new Project()), LabelPlacement = FieldLabelPlacement.Start, LabelWidth = (int)(6.5 * 14), IsReadOnly = true, Dock = DockStyle.Top };
        var category = new RadioGroupControl { Orientation = Orientation.Horizontal };
        category.Controls.AddRange([
            new RadioControl { Value = "display", Text = "陈列调整" },
            new RadioControl { Value = "device", Text = "设备更新" },
            new RadioControl { Value = "process", Text = "流程优化" },
        ]);
        var scope = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm };
        scope.Controls.AddRange([new ToggleItemControl { Value = "all", Text = "全部门店" }, new ToggleItemControl { Value = "pilot", Text = "试点门店" }]);
        var funding = new CheckboxGroupControl { Orientation = Orientation.Horizontal };
        foreach (var (value, label) in new[] { ("budget", "年度预算"), ("special", "专项资金"), ("brand", "品牌合作"), ("self", "其他自筹") })
            funding.Controls.Add(new CheckboxControl { Value = value, Text = label });
        form.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, AutoSize = true, Dock = DockStyle.Top },
            new FieldControl { Path = "Name", Label = "项目名称", IsRequired = true, Content = new TextFieldControl() },
            new FieldControl { Path = "Category", Label = "改造类型", IsRequired = true, Content = category },
            new FieldControl { Path = "Scope", Label = "执行范围", IsRequired = true, Content = scope },
            new FieldControl { Path = "Funding", Label = "经费来源", Description = "可多选", Content = funding },
            new FieldControl { Path = "Stores", Label = "涉及门店", Content = new NumberFieldControl { Min = 0, Suffix = "家" } },
            new FieldControl { Path = "Notify", Label = "通知门店", Content = new SwitchControl { Text = "开工前三天通知店长" } },
            new FieldControl { Path = "Description", Label = "简要描述", IsRequired = true, Description = "说明改造内容与节省金额的计算依据", Content = new TextareaControl { Rows = 3 } }));

        var mode = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = "表单状态" };
        foreach (var (value, label) in new[] { ("edit", "编辑"), ("readonly", "只读"), ("disabled", "不可用") })
            mode.Controls.Add(new ToggleItemControl { Value = value, Text = label });
        mode.Value = "readonly";
        mode.ValueChanged += (_, _) =>
        {
            form.IsReadOnly = mode.Value == "readonly";
            form.Enabled = mode.Value != "disabled";
        };
        return Build.Card(new CardControl { Width = 640, Header = "项目基本信息", Extra = mode }, form);
    }

    static Control SectionReview()
    {
        const int labelWidth = 91;
        var decision = new RadioGroupControl { Orientation = Orientation.Horizontal };
        decision.Controls.AddRange([
            new RadioControl { Value = "approve", Text = "同意" },
            new RadioControl { Value = "return", Text = "退回修改" },
            new RadioControl { Value = "reject", Text = "不同意" },
        ]);
        var noteField = new FieldControl { Label = "意见", IsRequired = true, Content = new TextareaControl { Rows = 3, PlaceholderText = "退回、不同意时写明原因" } };
        var submit = new ButtonControl { Text = "提交", Enabled = false };
        decision.ValueChanged += (_, _) =>
        {
            noteField.IsRequired = decision.Value != "approve";
            submit.Enabled = decision.Value is not null;
        };
        var request = new FormSectionControl { Title = "申请内容", Description = "由申请人填写，审批时只读", IsReadOnly = true, LabelPlacement = FieldLabelPlacement.Start, LabelWidth = labelWidth };
        request.Fields.AddRange([
            new FieldControl { Label = "项目", Content = new TextFieldControl { Value = "显示器支架合并为一体成型" } },
            new FieldControl { Label = "编号", Content = new TextFieldControl { Value = "PO-2026-0187" } },
            new FieldControl { Label = "预计年节省", Content = new NumberFieldControl { Value = 174752, Suffix = "元" } },
            new FieldControl { Label = "说明", Content = new TextareaControl { Text = "两件合并为一件，减少紧固件 36 个，单件减重 0.4 kg。", Rows = 2 } },
        ]);
        var review = new FormSectionControl { Title = "审批意见", LabelPlacement = FieldLabelPlacement.Start, LabelWidth = labelWidth };
        review.Fields.AddRange([new FieldControl { Label = "结论", IsRequired = true, Content = decision }, noteField]);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch },
            request, review, Build.Flex(new FlexControl { Justify = FlexJustify.End }, submit));
    }

    sealed record Contact(string Name, string Role, string Phone);

    static Control SectionCollapsible()
    {
        var contacts = new List<Contact> { new("周岚", "销售经理", "13800001234"), new("陈默", "质量工程师", "13900005678") };
        var add = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.Plus, Text = "添加联系人" };
        var people = new FormSectionControl { Title = "联系人", Extra = add };
        void Rebuild()
        {
            people.Description = $"{contacts.Count} 位，至少保留一位";
            foreach (var row in people.Fields.Cast<Control>().ToList()) row.Dispose();
            for (var index = 0; index < contacts.Count; index++)
            {
                var at = index;
                var contact = contacts[index];
                var remove = new IconButtonControl { Icon = Lucide.Trash, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Enabled = contacts.Count > 1, AccessibleName = $"删除第 {index + 1} 位联系人" };
                remove.Click += (_, _) =>
                {
                    contacts.RemoveAt(at);
                    Rebuild();
                };
                var row = new FlexControl { Align = FlexAlign.End, Gap = Space.Sm, AutoSize = true };
                FieldControl Cell(string label, string value)
                {
                    var field = new FieldControl { Label = label, IsLabelHidden = at > 0, Content = new TextFieldControl { Value = value } };
                    row.SetGrow(field, true);
                    return field;
                }
                row.Controls.AddRange([Cell("姓名", contact.Name), Cell("职务", contact.Role), Cell("手机", contact.Phone), remove]);
                people.Fields.Add(row);
            }
        }
        add.Click += (_, _) =>
        {
            contacts.Add(new Contact("", "", ""));
            Rebuild();
        };
        Rebuild();

        var notify = new SwitchControl { Text = "通知联系人", IsChecked = true };
        var channel = new SelectControl
        {
            ItemsSource = new[] { new ListboxOption("email", "邮件"), new ListboxOption("sms", "短信"), new ListboxOption("portal", "供应商门户站内信") },
            Value = "email",
        };
        notify.IsCheckedChanged += (_, _) => channel.Enabled = notify.IsChecked;
        var advanced = new FormSectionControl
        {
            Title = "高级选项",
            Description = "通知方式、对接系统；不填时按默认",
            IsCollapsible = true,
            IsOpen = false,
            Columns = 2,
            MinColumnWidth = new Length("14em"),
        };
        advanced.Fields.AddRange([new FieldControl { Label = "状态变更时通知", Content = notify }, new FieldControl { Label = "通知方式", Content = channel }]);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch }, people, advanced);
    }
}
