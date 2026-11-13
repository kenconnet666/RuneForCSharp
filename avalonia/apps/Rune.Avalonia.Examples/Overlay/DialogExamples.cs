using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Dialog 的示例（vue-tsx-pro 文档站 overlay/Dialog）。</summary>
static class DialogExamples
{
    public static Example[] All =>
    [
        new("Dialog", "dialog", "编辑表单", "写了 Confirm 时页脚是“取消 / 确认”：确认的任务进行中按钮加载、关不掉，结果为 false（姓名没填）时对话框留着。焦点进对话框里第一个能聚焦的元素，关上后回到打开它的按钮。", Profile),
        new("Dialog", "alert-dialog", "确认操作", "AlertDialog 没有右上角的关闭按钮、点遮罩不关，打开时焦点在“取消”上（误按 Enter 不会执行）；Confirm 的任务进行中确认按钮加载。只想在一个操作前问一句时用 AlertDialog.ConfirmAsync(owner, 选项)，得到是否确认；给了 ConfirmReason 时多一个填理由的输入框，得到填的理由（取消时 null）。", Confirm),
        new("Dialog", "long-content", "宽度与长内容", "Size 是宽度档位（默认 xs），不超出窗口；内容长时只滚正文，标题与页脚固定。Footer 是布局面板时占满一行。", Terms),
        new("Dialog", "imperative", "命令式与关闭的原因", "不必在界面里放对话框、管 IsOpen：new Dialog { … }.ShowAsync(owner) 从列表的一行、回调里直接弹出，沿用 owner 处的主题与语言，关上后自动放开；开着时改属性（这里改标题），Close() 关上。ShowAsync 得到关上的原因（确认是 confirm）。所有浮层的 OpenChanged 都报告原因：关闭按钮 close、Esc escape、点遮罩 outside、确认 confirm、代码调用 imperative；直接改 IsOpen 时不报告。要在关闭前拦下用 BeforeClose，它也拿到原因。", Imperative),
        new("Dialog", "blocking", "进行中的任务", "Closable = false 去掉右上角的关闭按钮，Dismissible = false 让点遮罩不关闭——任务进行中别被误关；Esc 仍然可以关，在 OpenChanged 里收尾。", Blocking),
        new("Dialog", "draggable", "可以拖动", "Draggable 时按住标题行能把对话框挪开（核对数据时看后面被挡住的内容），拖到哪都不超出窗口，下次打开回到正中；标题行上的关闭按钮照常点。（付款信息上游用 Descriptions，还没有：先排成标签、值两列）", Payable),
    ];

    static readonly (string Label, string Value)[] Payment =
    [
        ("收款单位", "华辰精密机械有限公司"),
        ("开户行", "招商银行上海张江支行"),
        ("账号", "6214 8301 2233 4455"),
        ("金额", "¥128,000.00"),
    ];

    static Control Confirm()
    {
        var result = new Small { Tone = TextTone.Muted, Text = "还没有操作" };
        // Confirm 的任务进行中确认按钮显示加载中，完成后才关闭
        var alert = new AlertDialog
        {
            Tone = StatusTone.Danger, Title = "删除项目“官网改版”？", Description = "项目里的 48 个文件和全部评论会一起删除，无法恢复。", ConfirmText = "删除",
        };
        alert.Confirm = async () =>
        {
            await Task.Delay(1000);
            result.Text = "项目“官网改版”已删除";
            return true;
        };
        var remove = new Button { Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Icon = Lucide.Trash, Content = "删除项目" };
        remove.Click += (_, _) => alert.IsOpen = true;
        var signOut = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.LogOut, Content = "退出登录（ConfirmAsync）" };
        signOut.Click += async (_, _) =>
        {
            var ok = await AlertDialog.ConfirmAsync(signOut, new ConfirmOptions { Title = "退出登录？", Description = "未保存的草稿会保留在这台设备上。", ConfirmText = "退出" });
            result.Text = ok ? "已退出登录" : "取消了退出";
        };
        // 要填理由：得到填的理由，取消时是 null
        var reject = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Danger, Icon = Lucide.Undo2, Content = "驳回（填理由）" };
        reject.Click += async (_, _) =>
        {
            var reason = await AlertDialog.ConfirmAsync(reject,
                new ConfirmOptions { Title = "驳回“10 月差旅报销”？", Tone = StatusTone.Danger, ConfirmText = "驳回" },
                new ConfirmReason { Label = "驳回理由", IsRequired = true, Description = "申请人会在通知里看到这段理由", Placeholder = "如：缺少 10 月 12 日的酒店发票", MaxLength = 200 });
            result.Text = reason is null ? "取消了驳回" : $"已驳回：{reason}";
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start,
            Children = { new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { remove, signOut, reject } }, result, alert },
        };
    }

    static readonly (string No, string Vendor, string Amount, string Date)[] Invoices =
    [
        ("FP-20261021-031", "华辰精密", "¥48,210.00", "2026-10-21"),
        ("FP-20261019-118", "启航塑胶", "¥12,860.00", "2026-10-19"),
        ("FP-20261015-007", "远山钣金", "¥3,420.50", "2026-10-15"),
    ];

    static readonly Dictionary<OpenChangeReason, string> ReasonText = new()
    {
        [OpenChangeReason.Trigger] = "触发器", [OpenChangeReason.Outside] = "点遮罩", [OpenChangeReason.Escape] = "Esc", [OpenChangeReason.Close] = "关闭按钮",
        [OpenChangeReason.Confirm] = "确认", [OpenChangeReason.Select] = "选中", [OpenChangeReason.Focus] = "焦点", [OpenChangeReason.Imperative] = "代码调用",
    };

    /// <summary>标签、值两列（上游用 Descriptions，还没有）。</summary>
    static Flex Pairs(IEnumerable<(string Label, string Value)> pairs)
    {
        var rows = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm };
        foreach (var (label, value) in pairs)
            rows.Children.Add(new Flex { Gap = Space.Md, Children = { new Small { Tone = TextTone.Muted, Text = label, Width = 6 * 14 }, new Text { Size = TextSize.Sm, Text = value } } });
        return rows;
    }

    static Control Imperative()
    {
        var log = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, MinWidth = 10 * 14 };
        var lines = new List<string>();
        void Render()
        {
            log.Children.Clear();
            log.Children.Add(new Small { Tone = TextTone.Muted, Text = "OpenChanged" });
            if (lines.Count == 0) log.Children.Add(new Small { Tone = TextTone.Muted, Text = "（还没有）" });
            foreach (var text in lines) log.Children.Add(new Small { Text = text });
        }
        Render();
        var list = new ItemList { Divided = true, Inset = Space.Md };
        foreach (var (no, vendor, amount, date) in Invoices)
        {
            var check = new Badge { Tone = StatusTone.Success, Content = "已核对", IsVisible = false };
            var view = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = "查看" };
            view.Click += (_, _) =>
            {
                var dialog = new Dialog
                {
                    Title = $"发票 {no}", Description = vendor, Size = DialogSize.Sm, ConfirmText = "去付款",
                    Confirm = async () =>
                    {
                        await Task.Delay(600);
                        return true;
                    },
                };
                dialog.OpenChanged += (_, e) =>
                {
                    lines.Insert(0, $"{(e.IsOpen ? "打开" : "关上")} · {ReasonText[e.Reason]}");
                    if (lines.Count > 6) lines.RemoveAt(6);
                    Render();
                };
                var mark = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Content = "标记已核对并关上" };
                mark.Click += async (_, _) =>
                {
                    check.IsVisible = true;
                    dialog.Title = $"发票 {no}（已核对）";
                    await Task.Delay(800);
                    dialog.Close();
                };
                dialog.Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { Pairs([("供应商", vendor), ("金额", amount), ("开票日期", date)]), mark } };
                _ = dialog.ShowAsync(view);
            };
            list.Children.Add(new ItemListItem { Title = vendor, Description = $"{no} · {amount}", End = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { check, view } } });
        }
        return new Flex
        {
            Gap = Space.Lg, Align = FlexAlign.Start, Wrap = FlexWrap.Wrap,
            Children = { new Card { Padding = Space.None, MinWidth = 18 * 14, Content = list }, log },
        };
    }

    const int Contacts = 1280;

    static Control Blocking()
    {
        var result = new Small { Tone = TextTone.Muted, Text = "导入期间点遮罩不会关闭，也没有右上角的关闭按钮" };
        var progress = new Progress { Max = Contacts, Label = "导入进度", ValueText = (value, max) => $"已导入 {value} / {max} 位" };
        var cancel = new Button { Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Content = "取消导入" };
        var dialog = new Dialog
        {
            Title = "正在导入通讯录", Description = "完成后自动关闭；可以随时取消，已导入的联系人会保留。",
            Closable = false, Dismissible = false, Content = progress, Footer = cancel,
        };
        var timer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        var done = 0;
        void Show(int value)
        {
            done = value;
            progress.Value = value;
            progress.ValueLabel = $"{value} / {Contacts}";
        }
        void Stop(string text)
        {
            timer.Stop();
            result.Text = text;
        }
        timer.Tick += async (_, _) =>
        {
            Show(Math.Min(Contacts, done + 96));
            if (done < Contacts) return;
            Stop($"导入了 {Contacts} 位联系人");
            await Task.Delay(400);
            dialog.IsOpen = false;
        };
        // Esc 仍然能关：在 OpenChanged 里收尾
        dialog.OpenChanged += (_, e) =>
        {
            if (!e.IsOpen && timer.IsEnabled) Stop($"取消了，已导入 {done} 位");
        };
        cancel.Click += (_, _) =>
        {
            Stop($"取消了，已导入 {done} 位");
            dialog.IsOpen = false;
        };
        var start = new Button { Icon = Lucide.Contact, Content = "导入通讯录" };
        start.Click += (_, _) =>
        {
            Show(0);
            dialog.IsOpen = true;
            timer.Start();
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { start, result, dialog } };
    }

    static Control Payable()
    {
        var rows = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm };
        foreach (var (label, value) in Payment)
        {
            var name = new Small { Tone = TextTone.Muted, Text = label, Width = 6 * 14 };
            rows.Children.Add(new Flex { Gap = Space.Md, Children = { name, new Text { Size = TextSize.Sm, Text = value } } });
        }
        var done = new Button { Content = "确认无误" };
        var dialog = new Dialog { Title = "付款信息", Draggable = true, Content = rows, Footer = done };
        done.Click += (_, _) => dialog.IsOpen = false;
        var open = new Button { Variant = ButtonVariant.Outline, Content = "核对付款信息" };
        open.Click += (_, _) => dialog.IsOpen = true;
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Start,
            Children = { open, new Small { Tone = TextTone.Muted, Text = "打开后按住标题栏拖动" }, dialog },
        };
    }

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
