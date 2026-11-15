using System.ComponentModel.DataAnnotations;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Form 的示例（vue-tsx-pro 文档站 form/Form）。错误汇总、分组、数组字段、按模型生成的表单还没做。</summary>
static class FormExamples
{
    public static Example[] All =>
    [
        new("Form", "signup", "注册：校验与异步校验", "Field 写 Path、里面只放控件：值自动双向绑定，错误取自 FormController，失焦或提交过之后才显示；必填星号、最大长度、年龄的范围来自模型上的特性。用户名另有异步校验（防抖、只采用最后一次结果，提交时等它完成）。单行输入框里按回车提交，没通过时焦点移到第一个出错的字段。", Signup),
        new("Form", "layout", "标签在左侧、整组只读", "标签放在起始边在 Form 上统一设置：标签一列右对齐、控件一列，按第一行文字的基线对齐。IsReadOnly、IsEnabled 写在 Form 上，所有字段与控件（含勾选、开关、分段）一起只读或不可用。", Layout),
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
        return new Card { MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Left, Content = form };
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
}
