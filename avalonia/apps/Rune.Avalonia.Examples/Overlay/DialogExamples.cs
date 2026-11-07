using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Dialog 的示例（vue-tsx-pro 文档站 overlay/Dialog）。alert-dialog 要等 AlertDialog，imperative 要等命令式打开，blocking 要等 Progress 的导入流程。</summary>
static class DialogExamples
{
    public static Example[] All =>
    [
        new("Dialog", "dialog", "编辑表单", "写了 Confirm 时页脚是“取消 / 确认”：确认的任务进行中按钮加载、关不掉，结果为 false（姓名没填）时对话框留着。焦点进对话框里第一个能聚焦的元素，关上后回到打开它的按钮。", Profile),
        new("Dialog", "long-content", "宽度与长内容", "Size 是宽度档位（默认 xs），不超出窗口；内容长时只滚正文，标题与页脚固定。Footer 是布局面板时占满一行。", Terms),
    ];

    static readonly string[] Departments = ["设计中心", "研发部", "市场部", "财务部"];

    static Control Profile()
    {
        var (name, department, bio) = ("周一航", "设计中心", "做了八年交互设计，喜欢把复杂的事讲简单。");
        var title = new Strong();
        var summary = new Small { Tone = TextTone.Muted };
        void Show()
        {
            title.Text = name;
            summary.Text = $"{department} · {bio}";
        }
        var nameField = new TextField();
        var nameWrap = new Field { Label = "姓名", IsRequired = true, Content = nameField };
        var departmentField = new Select { ItemsSource = Departments };
        var bioField = new Textarea { Rows = 3 };
        var dialog = new Dialog
        {
            Title = "编辑资料",
            Description = "修改后立即对团队成员可见。",
            ConfirmText = "保存",
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Lg,
                Children =
                {
                    nameWrap,
                    new Field { Label = "部门", Content = departmentField },
                    new Field { Label = "简介", Description = "显示在个人主页，最多 80 字。", Content = bioField },
                },
            },
        };
        dialog.Confirm = async () =>
        {
            var draft = nameField.Text?.Trim() ?? "";
            nameWrap.Error = draft.Length > 0 ? null : "请填写姓名";
            if (draft.Length == 0) return false;
            await Task.Delay(800);
            (name, department, bio) = (draft, departmentField.Value ?? department, bioField.Text ?? "");
            Show();
            return true;
        };
        var edit = new Button { Variant = ButtonVariant.Soft, Icon = Lucide.Pencil, Content = "编辑" };
        edit.Click += (_, _) =>
        {
            nameField.Text = name;
            departmentField.Value = department;
            bioField.Text = bio;
            nameWrap.Error = null;
            dialog.IsOpen = true;
        };
        Show();
        var text = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { title, summary } };
        Flex.SetGrow(text, true);
        return new Card
        {
            Content = new Flex { Align = FlexAlign.Center, Gap = Space.Md, Children = { new Avatar { DisplayName = name }, text, edit, dialog } },
        };
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
        var sizes = new ToggleGroup { IsRequired = true, Size = TextSize.Sm, Value = "md" };
        foreach (var value in new[] { "xs", "sm", "md", "lg", "full" }) sizes.Children.Add(new ToggleItem { Value = value, Content = value });
        AutomationProperties.SetName(sizes, "宽度");
        var agreed = new Checkbox { Content = "我已阅读并同意" };
        var proceed = new Button { Content = "继续", IsEnabled = false };
        agreed.PropertyChanged += (_, e) =>
        {
            if (e.Property == Checkbox.IsCheckedProperty) proceed.IsEnabled = agreed.IsChecked == true;
        };
        var body = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg };
        foreach (var (title, text) in Sections.Concat(Sections).Concat(Sections))
            body.Children.Add(new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new H3 { Size = TextSize.Md, Text = title }, new Paragraph { Size = TextSize.Sm, Text = text } } });
        var dialog = new Dialog
        {
            Title = "用户协议",
            Description = "更新于 2026 年 10 月 1 日",
            Content = body,
            Footer = new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = FlexWrap.Wrap, Children = { agreed, proceed } },
        };
        proceed.Click += (_, _) => dialog.IsOpen = false;
        var open = new Button { Variant = ButtonVariant.Outline };
        void Sync()
        {
            open.Content = $"阅读用户协议（size=\"{sizes.Value}\"）";
            dialog.Size = DialogSize.Parse(sizes.Value ?? "md");
        }
        sizes.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleGroup.ValueProperty) Sync();
        };
        open.Click += (_, _) => dialog.IsOpen = true;
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { sizes, open, dialog } };
    }
}
