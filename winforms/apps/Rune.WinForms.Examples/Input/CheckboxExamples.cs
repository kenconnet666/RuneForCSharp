namespace Rune.WinForms.Examples;

/// <summary>
/// vue-tsx-pro 文档站 input/Checkbox 页的示例：复选框、选项来自数据、开关、单选。validation 等 Field；
/// 上游包在 Field 里的地方先用 <see cref="Labeled"/>（标签、说明、错误摆在上下），Field 做好后换掉。
/// WinForms 的 Radio 文字是字符串：单选的价格放在同一行的另一个控件里。
/// </summary>
static class CheckboxExamples
{
    public static Example[] All =>
    [
        new("Checkbox", "checkbox", "复选框", "单个复选框绑 IsChecked；一组用 CheckboxGroupControl，Value 是勾选项的值。全选框部分勾选时写 IsIndeterminate，读屏念“混合”。", Permissions),
        new("Checkbox", "items", "选项来自数据", "CheckboxGroupControl、RadioGroupControl 的 ItemsSource 可以是 ListboxOption（值、文字、说明、不可用）、字符串，或任意数据加 ToOption。Description 显示在文字下方，读屏作为帮助文字；单独的 Checkbox、Radio 也能写。", Items),
        new("Checkbox", "switch", "开关", "Switch 是立即生效的设置（需要提交才生效的用 Checkbox）。切换要等接口时写 IsLoading：滑块里转圈、点击无效；在 Toggling 里拦下切换，接口回来后再改 IsChecked。轨道里可以放图标或文字，对读屏隐藏。", Settings),
        new("Checkbox", "radio", "单选", "整组一个 Tab 位置，停在选中项；方向键在组内移动并选中。Orientation = Horizontal 横排。", Shipping),
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
        var note = new SmallControl { Text = "至少选一种", Tone = TextTone.Muted };
        channels.ValueChanged += (_, _) =>
        {
            channels.IsInvalid = channels.Value.Count == 0;
            (note.Text, note.Tone) = channels.IsInvalid ? ("请至少选一种", TextTone.Danger) : ("至少选一种", TextTone.Muted);
        };

        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            Build.Card(new CardControl { CardPadding = Space.Sm }, Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm }, all, granted)),
            Labeled("通知方式", channels, note),
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
        var selected = new SmallControl { Tone = TextTone.Muted };
        void Sync() => selected.Text = $"已选：{(notify.Value.Count > 0 ? string.Join("、", notify.Value) : "（无）")}";
        notify.ValueChanged += (_, _) => Sync();
        Sync();
        var period = new RadioGroupControl { ItemsSource = new[] { "按日", "按周", "按月" }, Value = "按周", Orientation = Orientation.Horizontal };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            Labeled("套餐", plan),
            Labeled("通知方式", notify, selected),
            Labeled("横排的字符串选项", period));
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
        {
            var radio = new RadioControl { Value = value, Text = title, Description = time, PropsLabel = new TextProps { Weight = TextWeight.Semibold } };
            var row = Build.Flex(new FlexControl { Justify = FlexJustify.Between, Gap = Space.Lg }, radio, new SmallControl { Text = price });
            row.SetGrow(radio, true);
            shipping.Controls.Add(row);
        }
        var invoice = new RadioGroupControl { Orientation = Orientation.Horizontal, IsInvalid = true };
        invoice.Controls.AddRange([
            new RadioControl { Value = "none", Text = "不开发票" },
            new RadioControl { Value = "personal", Text = "个人" },
            new RadioControl { Value = "company", Text = "企业" },
        ]);
        var error = new SmallControl { Text = "请选择发票类型", Tone = TextTone.Danger };
        invoice.ValueChanged += (_, _) =>
        {
            invoice.IsInvalid = invoice.Value is null;
            error.Visible = invoice.IsInvalid;
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg }, Labeled("配送方式", shipping), Labeled("发票 *", invoice, error));
    }

    /// <summary>标签在上、内容、说明或错误在下（Field 的临时替代）；组以标签为读屏名称。</summary>
    static FlexControl Labeled(string label, Control content, Control? note = null)
    {
        content.AccessibleName ??= label.TrimEnd(' ', '*');
        var field = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs }, new TextControl { Text = label, Weight = TextWeight.Medium }, content);
        if (note is not null) field.Controls.Add(note);
        return field;
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
