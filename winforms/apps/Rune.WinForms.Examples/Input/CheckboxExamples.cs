namespace Rune.WinForms.Examples;

/// <summary>
/// vue-tsx-pro 文档站 input/Checkbox 页的示例：复选框、选项来自数据、卡片外观、开关、单选、必选与出错。
/// WinForms 的文字、说明、结束边都是字符串，字重、字号写在部件属性里。
/// </summary>
static class CheckboxExamples
{
    public static Example[] All =>
    [
        new("Checkbox", "checkbox", "复选框", "单个复选框绑 IsChecked；一组用 CheckboxGroupControl，Value 是勾选项的值。全选框部分勾选时写 IsIndeterminate，读屏念“混合”。", Permissions),
        new("Checkbox", "items", "选项来自数据", "CheckboxGroupControl、RadioGroupControl 的 ItemsSource 可以是 ListboxOption（值、文字、说明、不可用）、字符串，或任意数据加 ToOption。Description 显示在文字下方，读屏作为帮助文字；单独的 Checkbox、Radio 也能写。", Items),
        new("Checkbox", "cards", "卡片外观", "组上写 Card：每个选项是一张卡片，整块可点，选中时边框与底色换成色调色（Tone），组里的每一项都跟着（单项也能写 Card）。横排时按宽度分列，每张不窄于 MinItemWidth（默认 14em）。End 放在结束边（价格、标记）。键盘与读屏同普通的单选、复选。", Cards),
        new("Checkbox", "switch", "开关", "Switch 是立即生效的设置（需要提交才生效的用 Checkbox）。切换要等接口时写 IsLoading：滑块里转圈、点击无效；在 Toggling 里拦下切换，接口回来后再改 IsChecked。轨道里可以放图标或文字，对读屏隐藏。", Settings),
        new("Checkbox", "radio", "单选", "整组一个 Tab 位置，停在选中项；方向键在组内移动并选中。说明写在 Description，价格这类放在结束边的写 End；Orientation = Horizontal 横排。", Shipping),
        new("Checkbox", "validation", "必选与出错", "提交时才检查：FieldControl 的 IsRequired 标出必填，Error 让整组标红并把错误作为帮助文字念出；单个复选框（同意条款）同样包在字段里显示错误。", Validation),
    ];

    static readonly (string Value, string Label)[] PermissionOptions =
    [
        ("orders:read", "查看订单"),
        ("orders:write", "修改订单"),
        ("refunds", "处理退款"),
        ("reports", "导出报表"),
    ];

    static Control Permissions()
    {
        var granted = new CheckboxGroupControl { Value = ["orders:read", "reports"], AccessibleName = "权限" };
        granted.Controls.AddRange([.. PermissionOptions.Select(permission => new CheckboxControl { Value = permission.Value, Text = permission.Label })]);
        var all = new CheckboxControl { Text = "订单管理员（全部权限）" };
        var summary = new SmallControl { Tone = TextTone.Muted };
        // 全选：部分勾选时 IsIndeterminate；切换时整组全选或全不选
        all.Toggling += (_, e) =>
        {
            e.Cancel = true;
            granted.Value = granted.Value.Count == PermissionOptions.Length ? [] : [.. PermissionOptions.Select(permission => permission.Value)];
        };
        void Sync()
        {
            var count = granted.Value.Count;
            all.IsChecked = count == PermissionOptions.Length;
            all.IsIndeterminate = count > 0 && count < PermissionOptions.Length;
            summary.Text = $"已授予：{(count > 0 ? string.Join("、", granted.Value) : "无")}";
        }
        granted.ValueChanged += (_, _) => Sync();
        Sync();
        // 子项缩进到全选框的文字下面（1.75em）
        granted.Padding = new Padding((int)(1.75f * granted.Font.Size * granted.DeviceDpi / 72), 0, 0, 0);

        var channels = new CheckboxGroupControl { Value = ["email"], Orientation = Orientation.Horizontal };
        channels.Controls.AddRange([
            new CheckboxControl { Value = "email", Text = "邮件" },
            new CheckboxControl { Value = "sms", Text = "短信" },
            new CheckboxControl { Value = "push", Text = "App 推送" },
            new CheckboxControl { Value = "phone", Text = "电话（企业版）", Enabled = false },
        ]);
        var channelsField = new FieldControl { Label = "通知方式", Description = "至少选一种", Content = channels };
        channels.ValueChanged += (_, _) => channelsField.Error = channels.Value.Count > 0 ? null : "请至少选一种";

        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            Build.Card(new CardControl { CardPadding = Space.Sm }, Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm }, all, granted)),
            channelsField,
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs }, new CheckboxControl { Text = "我已阅读并同意《服务条款》", Tone = StatusTone.Success }, summary));
    }

    /// <summary>套餐（任意数据，经 ToOption 变成选项）。</summary>
    sealed record Plan(string Id, string Name, string Price, int? Seats);

    static Control Items()
    {
        var plan = new RadioGroupControl
        {
            // 任意数据：ToOption 取出值、文字与说明
            ToOption = item => item is Plan { } p
                ? new ListboxOption(p.Id, p.Name) { Description = $"{p.Price}，{(p.Seats is { } count ? $"最多 {count} 人" : "人数不限")}" }
                : throw new ArgumentException("不是套餐", nameof(item)),
            ItemsSource = new Plan[]
            {
                new("team", "团队版", "¥49 / 人 / 月", 50),
                new("business", "企业版", "¥99 / 人 / 月", 500),
                new("enterprise", "私有部署", "按需报价", null),
            },
            Value = "business",
        };
        var notify = new CheckboxGroupControl
        {
            Value = ["email", "app"],
            ItemsSource = new[]
            {
                new ListboxOption("email", "邮件") { Description = "每天 9:00 汇总一封" },
                new ListboxOption("app", "站内信") { Description = "实时推送，登录后可见" },
                new ListboxOption("sms", "短信") { Description = "只发紧急的待办（需要绑定手机号）", IsDisabled = true },
            },
        };
        var notifyField = new FieldControl { Label = "通知方式", Content = notify };
        void Sync() => notifyField.Description = $"已选：{(notify.Value.Count > 0 ? string.Join("、", notify.Value) : "（无）")}";
        notify.ValueChanged += (_, _) => Sync();
        Sync();
        var period = new RadioGroupControl { ItemsSource = new[] { "按日", "按周", "按月" }, Value = "按周", Orientation = Orientation.Horizontal };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            new FieldControl { Label = "套餐", Content = plan },
            notifyField,
            new FieldControl { Label = "横排的字符串选项", Content = period });
    }

    static Control Shipping()
    {
        (string Value, string Title, string Time, string Price)[] options =
        [
            ("standard", "标准快递", "3–5 天送达", "免运费"),
            ("express", "顺丰次日达", "明天 18:00 前", "¥12"),
            ("pickup", "门店自提", "今天 15:00 后可取", "免运费"),
        ];
        var shipping = new RadioGroupControl { Value = "express" };
        foreach (var (value, title, time, price) in options)
            shipping.Controls.Add(new RadioControl { Value = value, Text = title, Description = time, End = price, PropsEnd = new TextProps { Size = TextSize.Xs } });
        var invoice = new RadioGroupControl { Orientation = Orientation.Horizontal };
        invoice.Controls.AddRange([
            new RadioControl { Value = "none", Text = "不开发票" },
            new RadioControl { Value = "personal", Text = "个人" },
            new RadioControl { Value = "company", Text = "企业" },
        ]);
        var invoiceField = new FieldControl { Label = "发票", IsRequired = true, Error = "请选择发票类型", Content = invoice };
        invoice.ValueChanged += (_, _) => invoiceField.Error = invoice.Value is null ? "请选择发票类型" : null;
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg }, new FieldControl { Label = "配送方式", Content = shipping }, invoiceField);
    }

    static readonly (string Id, string Name, string Price, string Detail)[] Plans =
    [
        ("free", "个人版", "免费", "3 个项目 · 1 GB 存储"),
        ("team", "团队版", "¥99 / 月", "不限项目 · 50 GB · 10 人"),
        ("business", "企业版", "¥499 / 月", "SSO 登录 · 审计日志 · 专属支持"),
    ];

    static readonly (string Id, string Name, string Price, string Detail)[] Addons =
    [
        ("backup", "异地备份", "+¥20 / 月", "每天一次，保留 30 天"),
        ("audit", "操作审计", "+¥50 / 月", "谁在什么时候改了什么"),
    ];

    static Control Cards()
    {
        var strong = new TextProps { Weight = TextWeight.Semibold };
        var plan = new RadioGroupControl { Value = "team", Card = true };
        foreach (var (id, name, price, detail) in Plans)
            plan.Controls.Add(new RadioControl { Value = id, Text = name, PropsLabel = strong, Description = detail, End = price, PropsEnd = strong });
        var extras = new CheckboxGroupControl { Value = ["backup"], Card = true, Orientation = Orientation.Horizontal };
        foreach (var (id, name, price, detail) in Addons)
            extras.Controls.Add(new CheckboxControl { Value = id, Text = name, Description = detail, End = price, PropsEnd = new TextProps { Size = TextSize.Xs } });
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            new FieldControl { Label = "套餐", Content = plan },
            new FieldControl { Label = "附加服务", Description = "可以多选，按月计费", Content = extras });
    }

    static Control Validation()
    {
        var channels = new CheckboxGroupControl { Orientation = Orientation.Horizontal };
        channels.Controls.AddRange([
            new CheckboxControl { Value = "email", Text = "邮件" },
            new CheckboxControl { Value = "sms", Text = "短信" },
            new CheckboxControl { Value = "inbox", Text = "站内信" },
        ]);
        var channelsField = new FieldControl { Label = "通知方式", IsRequired = true, Content = channels };
        var agreed = new CheckboxControl { Text = "我已阅读并同意服务条款", IsRequired = true };
        var agreedField = new FieldControl { Content = agreed };
        var done = new SmallControl { Text = "已保存", Tone = TextTone.Success, Visible = false };
        var submitted = false;
        // 提交时才检查；提交过之后随勾选实时更新
        void Check()
        {
            if (!submitted) return;
            channelsField.Error = channels.Value.Count == 0 ? "至少选一种通知方式" : null;
            agreedField.Error = agreed.IsChecked ? null : "请先同意服务条款";
            done.Visible = channelsField.Error is null && agreedField.Error is null;
        }
        channels.ValueChanged += (_, _) => Check();
        agreed.IsCheckedChanged += (_, _) => Check();
        var save = new ButtonControl { Text = "保存设置" };
        save.Click += (_, _) =>
        {
            submitted = true;
            Check();
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Start },
            channelsField, agreedField, Build.Flex(new FlexControl { Gap = Space.Sm, Align = FlexAlign.Center }, save, done));
    }

    static Control Settings()
    {
        var notify = new SwitchControl { IsChecked = true, AccessibleName = "接收通知" };
        var digest = new SwitchControl { Enabled = notify.IsChecked, AccessibleName = "每周摘要" };
        notify.IsCheckedChanged += (_, _) => digest.Enabled = notify.IsChecked;
        var dark = new SwitchControl
        {
            IsChecked = true, CheckedContent = Lucide.Moon, UncheckedContent = Lucide.Sun, TextSize = TextSize.Lg, AccessibleName = "暗色模式",
        };

        // 切换要等接口：拦下切换、滑块里转圈，接口回来再写回
        var syncDescription = new SmallControl { Text = "在所有设备上保持一致", Tone = TextTone.Muted };
        var sync = new SwitchControl { Tone = StatusTone.Success, AccessibleName = "云端同步" };
        sync.Toggling += (_, e) =>
        {
            e.Cancel = true;
            sync.IsLoading = true;
            syncDescription.Text = "正在保存…";
            After(TimeSpan.FromMilliseconds(1200), () =>
            {
                sync.IsChecked = e.IsChecked;
                sync.IsLoading = false;
                syncDescription.Text = "在所有设备上保持一致";
            });
        };

        return Build.Card(new CardControl(), Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md },
            Row("接收通知", new SmallControl { Text = "订单状态变化时推送", Tone = TextTone.Muted }, notify),
            new SeparatorControl(),
            Row("每周摘要", new SmallControl { Text = "每周一早上发送上周数据", Tone = TextTone.Muted }, digest),
            new SeparatorControl(),
            Row("暗色模式", new SmallControl { Text = "轨道里可以放图标或文字", Tone = TextTone.Muted }, dark),
            new SeparatorControl(),
            Row("云端同步", syncDescription, sync)));
    }

    static FlexControl Row(string title, SmallControl description, SwitchControl control)
    {
        var text = Build.Flex(new FlexControl { Direction = FlexDirection.Column },
            new TextControl { Text = title, Weight = TextWeight.Medium }, description);
        var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Lg }, text, control);
        row.SetGrow(text, true);
        return row;
    }

    /// <summary>过一会儿在 UI 线程上执行一次。</summary>
    static void After(TimeSpan delay, Action action)
    {
        var timer = new System.Windows.Forms.Timer { Interval = (int)delay.TotalMilliseconds };
        timer.Tick += (_, _) =>
        {
            timer.Dispose();
            action();
        };
        timer.Start();
    }
}
