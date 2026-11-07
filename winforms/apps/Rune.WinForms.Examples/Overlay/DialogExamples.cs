namespace Rune.WinForms.Examples;

/// <summary>Dialog 的示例（vue-tsx-pro 文档站 overlay/Dialog）。alert-dialog 要等 AlertDialog，imperative 要等命令式打开，blocking 要等导入流程。</summary>
static class DialogExamples
{
    public static Example[] All =>
    [
        new("Dialog", "dialog", "编辑表单", "写了 Confirm 时页脚是“取消 / 确认”：确认的任务进行中按钮加载、关不掉，结果为 false（姓名没填）时对话框留着。焦点进对话框里第一个能聚焦的控件，关上后回到打开它的按钮。", Profile),
        new("Dialog", "long-content", "宽度与长内容", "DialogSize 是宽度档位（默认 xs），不超出窗体；内容长时只滚正文，标题与页脚固定。Footer 是布局面板时占满一行。", Terms),
    ];

    static readonly string[] Departments = ["设计中心", "研发部", "市场部", "财务部"];

    static Control Profile()
    {
        var (name, department, bio) = ("周一航", "设计中心", "做了八年交互设计，喜欢把复杂的事讲简单。");
        var title = new StrongControl();
        var summary = new SmallControl { Tone = TextTone.Muted };
        void Show()
        {
            title.Text = name;
            summary.Text = $"{department} · {bio}";
        }
        var nameField = new TextFieldControl();
        var nameWrap = new FieldControl { Label = "姓名", IsRequired = true, Content = nameField };
        var departmentField = new SelectControl { ItemsSource = Departments };
        var bioField = new TextareaControl { Rows = 3 };
        var dialog = new DialogControl
        {
            Title = "编辑资料",
            Description = "修改后立即对团队成员可见。",
            ConfirmText = "保存",
            Content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
                nameWrap,
                new FieldControl { Label = "部门", Content = departmentField },
                new FieldControl { Label = "简介", Description = "显示在个人主页，最多 80 字。", Content = bioField }),
        };
        dialog.Confirm = async () =>
        {
            var draft = nameField.Text.Trim();
            nameWrap.Error = draft.Length > 0 ? null : "请填写姓名";
            if (draft.Length == 0) return false;
            await Task.Delay(800);
            (name, department, bio) = (draft, departmentField.Value ?? department, bioField.Text);
            Show();
            return true;
        };
        var edit = new ButtonControl { Variant = ButtonVariant.Soft, Icon = Lucide.Pencil, Text = "编辑" };
        edit.Click += (_, _) =>
        {
            nameField.Text = name;
            departmentField.Value = department;
            bioField.Text = bio;
            nameWrap.Error = null;
            dialog.IsOpen = true;
        };
        Show();
        var text = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs }, title, summary);
        var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md }, new AvatarControl { DisplayName = name }, text, edit, dialog);
        row.SetGrow(text, true);
        return Build.Card(new CardControl(), row);
    }

    static readonly (string Title, string Text)[] Sections =
    [
        ("一、服务内容", "本服务为用户提供在线组件库文档、示例与代码演练场。我们会不断改进服务，可能增加或调整部分功能。"),
        ("二、账号", "你需要对账号下的全部活动负责。发现账号被盗用时，请立即联系我们。"),
        ("三、隐私", "我们只收集提供服务所必需的信息，不会向第三方出售你的个人信息。详见隐私政策。"),
        ("四、内容", "你在演练场里写的代码归你所有。分享链接只把代码编码在地址里，不经过我们的服务器。"),
        ("五、责任限制", "示例代码按原样提供。用于生产环境前，请自行评估其适用性与安全性。"),
        ("六、变更", "条款更新时，我们会提前 30 天在站内公告。继续使用即视为同意更新后的条款。"),
    ];

    static Control Terms()
    {
        var sizes = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, Value = "md", AccessibleName = "宽度" };
        foreach (var value in new[] { "xs", "sm", "md", "lg", "full" }) sizes.Controls.Add(new ToggleItemControl { Value = value, Text = value });
        var agreed = new CheckboxControl { Text = "我已阅读并同意" };
        var proceed = new ButtonControl { Text = "继续", Enabled = false };
        agreed.IsCheckedChanged += (_, _) => proceed.Enabled = agreed.IsChecked == true;
        var body = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg };
        foreach (var (title, text) in Sections.Concat(Sections).Concat(Sections))
            body.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs },
                new H3Control { Text = title, TextSize = TextSize.Md },
                new ParagraphControl { Text = text, TextSize = TextSize.Sm }));
        var dialog = new DialogControl
        {
            Title = "用户协议",
            Description = "更新于 2026 年 10 月 1 日",
            Content = body,
            Footer = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = true }, agreed, proceed),
        };
        proceed.Click += (_, _) => dialog.IsOpen = false;
        var open = new ButtonControl { Variant = ButtonVariant.Outline };
        void Sync()
        {
            open.Text = $"阅读用户协议（size=\"{sizes.Value}\"）";
            dialog.DialogSize = DialogSize.Parse(sizes.Value ?? "md");
        }
        sizes.ValueChanged += (_, _) => Sync();
        open.Click += (_, _) => dialog.IsOpen = true;
        Sync();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start }, sizes, open, dialog);
    }
}
