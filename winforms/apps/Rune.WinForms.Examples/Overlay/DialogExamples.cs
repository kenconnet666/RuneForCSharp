namespace Rune.WinForms.Examples;

/// <summary>Dialog 的示例（vue-tsx-pro 文档站 overlay/Dialog）。</summary>
static class DialogExamples
{
    public static Example[] All =>
    [
        new("Dialog", "dialog", "编辑表单", "写了 Confirm 时页脚是“取消 / 确认”：确认的任务进行中按钮加载、关不掉，结果为 false（姓名没填）时对话框留着。焦点进对话框里第一个能聚焦的控件，关上后回到打开它的按钮。", Profile),
        new("Dialog", "alert-dialog", "确认操作", "AlertDialogControl 没有右上角的关闭按钮、点遮罩不关，打开时焦点在“取消”上（误按 Enter 不会执行）；Confirm 的任务进行中确认按钮加载。只想在一个操作前问一句时用 AlertDialogControl.ConfirmAsync(owner, 选项)，得到是否确认；给了 ConfirmReason 时多一个填理由的输入框，得到填的理由（取消时 null）。", Confirm),
        new("Dialog", "long-content", "宽度与长内容", "DialogSize 是宽度档位（默认 xs），不超出窗体；内容长时只滚正文，标题与页脚固定。Footer 是布局面板时占满一行。", Terms),
        new("Dialog", "imperative", "命令式与关闭的原因", "不必在界面里放对话框、管 IsOpen：new DialogControl { … }.ShowAsync(owner) 从列表的一行、回调里直接弹出，沿用 owner 处的作用域与字体；开着时改属性（这里改标题），Close() 关上。ShowAsync 得到关上的原因（确认是 confirm）。所有浮层的 OpenChanged 都报告原因：关闭按钮 close、Esc escape、点遮罩 outside、确认 confirm、代码调用 imperative；直接改 IsOpen 时不报告。要在关闭前拦下用 BeforeClose，它也拿到原因。", Imperative),
        new("Dialog", "blocking", "进行中的任务", "Closable = false 去掉右上角的关闭按钮，Dismissible = false 让点遮罩不关闭——任务进行中别被误关；Esc 仍然可以关，在 OpenChanged 里收尾。", Blocking),
        new("Dialog", "draggable", "可以拖动", "Draggable 时按住标题行能把对话框挪开（核对数据时看后面被挡住的内容），拖到哪都不超出窗体，下次打开回到正中；标题行上的关闭按钮照常点。（付款信息上游用 Descriptions，还没有：先排成标签、值两列）", Payable),
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
        var result = new SmallControl { Tone = TextTone.Muted, Text = "还没有操作" };
        // Confirm 的任务进行中确认按钮显示加载中，完成后才关闭
        var alert = new AlertDialogControl
        {
            Tone = StatusTone.Danger, Title = "删除项目“官网改版”？", Description = "项目里的 48 个文件和全部评论会一起删除，无法恢复。", ConfirmText = "删除",
        };
        alert.Confirm = async () =>
        {
            await Task.Delay(1000);
            result.Text = "项目“官网改版”已删除";
            return true;
        };
        var remove = new ButtonControl { Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Icon = Lucide.Trash, Text = "删除项目" };
        remove.Click += (_, _) => alert.IsOpen = true;
        var signOut = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.LogOut, Text = "退出登录（ConfirmAsync）" };
        signOut.Click += async (_, _) =>
        {
            var ok = await AlertDialogControl.ConfirmAsync(signOut, new ConfirmOptions { Title = "退出登录？", Description = "未保存的草稿会保留在这台设备上。", ConfirmText = "退出" });
            result.Text = ok ? "已退出登录" : "取消了退出";
        };
        // 要填理由：得到填的理由，取消时是 null
        var reject = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Danger, Icon = Lucide.Undo2, Text = "驳回（填理由）" };
        reject.Click += async (_, _) =>
        {
            var reason = await AlertDialogControl.ConfirmAsync(reject,
                new ConfirmOptions { Title = "驳回“10 月差旅报销”？", Tone = StatusTone.Danger, ConfirmText = "驳回" },
                new ConfirmReason { Label = "驳回理由", IsRequired = true, Description = "申请人会在通知里看到这段理由", Placeholder = "如：缺少 10 月 12 日的酒店发票", MaxLength = 200 });
            result.Text = reason is null ? "取消了驳回" : $"已驳回：{reason}";
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start },
            Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = true }, remove, signOut, reject), result, alert);
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
    static FlexControl Pairs(IEnumerable<(string Label, string Value)> pairs)
    {
        var rows = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm };
        foreach (var (label, value) in pairs)
        {
            var name = new SmallControl { Tone = TextTone.Muted, Text = label, AutoSize = false };
            name.Size = new Size(name.LogicalToDeviceUnits(6 * 14), name.GetPreferredSize(Size.Empty).Height);
            rows.Controls.Add(Build.Flex(new FlexControl { Gap = Space.Md }, name, new TextControl { TextSize = TextSize.Sm, Text = value }));
        }
        return rows;
    }

    static Control Imperative()
    {
        var log = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs };
        var lines = new List<string>();
        void Render()
        {
            log.SuspendLayout();
            foreach (var old in log.Controls.Cast<Control>().ToList()) old.Dispose();
            log.Controls.Add(new SmallControl { Tone = TextTone.Muted, Text = "OpenChanged" });
            if (lines.Count == 0) log.Controls.Add(new SmallControl { Tone = TextTone.Muted, Text = "（还没有）" });
            foreach (var text in lines) log.Controls.Add(new SmallControl { Text = text });
            log.ResumeLayout();
        }
        Render();
        var list = new ItemListControl { Divided = true, Inset = Space.Md };
        foreach (var (no, vendor, amount, date) in Invoices)
        {
            var check = new BadgeControl { Tone = StatusTone.Success, Text = "已核对", Visible = false };
            var view = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Text = "查看" };
            view.Click += (_, _) =>
            {
                var dialog = new DialogControl
                {
                    Title = $"发票 {no}", Description = vendor, DialogSize = DialogSize.Sm, ConfirmText = "去付款",
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
                var mark = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Text = "标记已核对并关上" };
                mark.Click += async (_, _) =>
                {
                    check.Visible = true;
                    dialog.Title = $"发票 {no}（已核对）";
                    await Task.Delay(800);
                    dialog.Close();
                };
                dialog.Content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md }, Pairs([("供应商", vendor), ("金额", amount), ("开票日期", date)]), mark);
                // 关上后不再用：放掉两层窗口
                _ = dialog.ShowAsync(view).ContinueWith(_ => dialog.Dispose(), TaskScheduler.FromCurrentSynchronizationContext());
            };
            list.Controls.Add(new ItemListItemControl { Text = vendor, Description = $"{no} · {amount}", End = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm }, check, view) });
        }
        // 列表占满剩下的宽度、至少 18em（上游 flex: 1 1 18rem）
        var card = Build.Card(new CardControl { CardPadding = Space.None }, list);
        card.MinimumSize = new Size(card.LogicalToDeviceUnits(18 * 14), 0);
        var row = Build.Flex(new FlexControl { Gap = Space.Lg, Align = FlexAlign.Start, Wrap = true }, card, log);
        row.SetGrow(card, true);
        return row;
    }

    const int Contacts = 1280;

    static Control Blocking()
    {
        var result = new SmallControl { Tone = TextTone.Muted, Text = "导入期间点遮罩不会关闭，也没有右上角的关闭按钮" };
        var progress = new ProgressControl { Max = Contacts, Label = "导入进度", ValueText = (value, max) => $"已导入 {value} / {max} 位" };
        var cancel = new ButtonControl { Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Text = "取消导入" };
        var dialog = new DialogControl
        {
            Title = "正在导入通讯录", Description = "完成后自动关闭；可以随时取消，已导入的联系人会保留。",
            Closable = false, Dismissible = false, Content = progress, Footer = cancel,
        };
        var timer = new System.Windows.Forms.Timer { Interval = 150 };
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
            if (!e.IsOpen && timer.Enabled) Stop($"取消了，已导入 {done} 位");
        };
        cancel.Click += (_, _) =>
        {
            Stop($"取消了，已导入 {done} 位");
            dialog.IsOpen = false;
        };
        var start = new ButtonControl { Icon = Lucide.Contact, Text = "导入通讯录" };
        start.Click += (_, _) =>
        {
            Show(0);
            dialog.IsOpen = true;
            timer.Start();
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start }, start, result, dialog);
    }

    static Control Payable()
    {
        var rows = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, AutoSize = true };
        foreach (var (label, value) in Payment)
        {
            var name = new SmallControl { Tone = TextTone.Muted, Text = label, AutoSize = false };
            name.Size = new Size(name.LogicalToDeviceUnits(6 * 14), name.GetPreferredSize(Size.Empty).Height);
            rows.Controls.Add(Build.Flex(new FlexControl { Gap = Space.Md }, name, new TextControl { TextSize = TextSize.Sm, Text = value }));
        }
        var done = new ButtonControl { Text = "确认无误" };
        var dialog = new DialogControl { Title = "付款信息", Draggable = true, Content = rows, Footer = done };
        done.Click += (_, _) => dialog.IsOpen = false;
        var open = new ButtonControl { Variant = ButtonVariant.Outline, Text = "核对付款信息" };
        open.Click += (_, _) => dialog.IsOpen = true;
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Start },
            open, new SmallControl { Tone = TextTone.Muted, Text = "打开后按住标题栏拖动" }, dialog);
    }

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
