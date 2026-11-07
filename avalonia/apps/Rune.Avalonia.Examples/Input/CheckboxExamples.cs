using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Rune.Avalonia.Examples;

/// <summary>
/// vue-tsx-pro 文档站 input/Checkbox 页的示例：复选框、选项来自数据、开关、单选。validation 等 Field；
/// 上游包在 Field 里的地方先用 <see cref="Labeled"/>（标签、说明、错误摆在上下），Field 做好后换掉。
/// </summary>
static class CheckboxExamples
{
    public static Example[] All =>
    [
        new("Checkbox", "checkbox", "复选框", "单个复选框绑 IsChecked；一组用 CheckboxGroup 绑值的列表。全选框部分勾选时写 IsIndeterminate，读屏念“部分选中”。", Permissions),
        new("Checkbox", "items", "选项来自数据", "CheckboxGroup、RadioGroup 的 ItemsSource 可以是 ListboxOption（值、文字、说明、不可用）、字符串，或任意数据加 ToOption。Description 显示在文字下方，读屏作为帮助文字；单独的 Checkbox、Radio 也能写。", Items),
        new("Checkbox", "switch", "开关", "Switch 是立即生效的设置（需要提交才生效的用 Checkbox）。切换要等接口时写 IsLoading：滑块里转圈、点击无效；在 Toggling 里拦下切换，接口回来后再改 IsChecked。轨道里可以放图标或文字，对读屏隐藏。", Settings),
        new("Checkbox", "radio", "单选", "整组一个 Tab 位置，停在选中项；方向键在组内移动并选中。选项里可以放任意内容（说明、价格）；Orientation=\"Horizontal\" 横排。", Shipping),
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
        var granted = new CheckboxGroup { Value = ["orders:read", "reports"] };
        foreach (var (value, label) in PermissionOptions) granted.Items.Add(new Checkbox { Value = value, Content = label });
        AutomationProperties.SetName(granted, "权限");
        var all = new Checkbox { Content = "订单管理员（全部权限）" };
        var summary = new Small { Tone = TextTone.Muted };
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
        granted.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckboxGroup.ValueProperty) Sync();
        };
        Sync();
        // 子项缩进到全选框的文字下面（1.75em）
        granted.Bind(TemplatedControl.PaddingProperty, new Binding(nameof(granted.FontSize)) { Source = granted, Converter = new FuncValueConverter<double, Thickness>(size => new(1.75 * size, 0, 0, 0)) });

        var channels = new CheckboxGroup { Value = ["email"], Orientation = Orientation.Horizontal };
        channels.Items.Add(new Checkbox { Value = "email", Content = "邮件" });
        channels.Items.Add(new Checkbox { Value = "sms", Content = "短信" });
        channels.Items.Add(new Checkbox { Value = "push", Content = "App 推送" });
        channels.Items.Add(new Checkbox { Value = "phone", Content = "电话（企业版）", IsEnabled = false });
        var channelsField = Labeled("通知方式", channels, "至少选一种");
        channels.PropertyChanged += (_, e) =>
        {
            if (e.Property != CheckboxGroup.ValueProperty) return;
            channels.IsInvalid = channels.Value.Count == 0;
            channelsField.Children[^1] = channels.IsInvalid ? new Small { Text = "请至少选一种", Tone = TextTone.Danger } : new Small { Text = "至少选一种", Tone = TextTone.Muted };
        };

        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                new Card { Padding = Space.Sm, Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { all, granted } } },
                channelsField,
                new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Checkbox { Content = "我已阅读并同意《服务条款》", Tone = StatusTone.Success }, summary } },
            },
        };
    }

    static Control Items()
    {
        Plan[] plans =
        [
            new("team", "团队版", "¥49 / 人 / 月", 50),
            new("business", "企业版", "¥99 / 人 / 月", 500),
            new("enterprise", "私有部署", "按需报价", null),
        ];
        var plan = new RadioGroup
        {
            Value = "business",
            ItemsSource = plans,
            // 任意数据：ToOption 取出值、文字与说明
            ToOption = item => item is Plan { } p
                ? new ListboxOption(p.Id, p.Name) { Description = $"{p.Price}，{(p.Seats is { } count ? $"最多 {count} 人" : "人数不限")}" }
                : throw new ArgumentException("不是套餐", nameof(item)),
        };
        var notify = new CheckboxGroup
        {
            Value = ["email", "app"],
            ItemsSource = new[]
            {
                new ListboxOption("email", "邮件") { Description = "每天 9:00 汇总一封" },
                new ListboxOption("app", "站内信") { Description = "实时推送，登录后可见" },
                new ListboxOption("sms", "短信") { Description = "只发紧急的待办（需要绑定手机号）", IsDisabled = true },
            },
        };
        var selected = new Small { Tone = TextTone.Muted };
        void Sync() => selected.Text = $"已选：{(notify.Value.Count > 0 ? string.Join("、", notify.Value) : "（无）")}";
        notify.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckboxGroup.ValueProperty) Sync();
        };
        Sync();
        var period = new RadioGroup { ItemsSource = new[] { "按日", "按周", "按月" }, Value = "按周", Orientation = Orientation.Horizontal };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                Labeled("套餐", plan),
                Labeled("通知方式", notify, selected),
                Labeled("横排的字符串选项", period),
            },
        };
    }

    static Control Shipping()
    {
        (string Value, string Title, string Time, string Price)[] options =
        [
            ("standard", "标准快递", "3–5 天送达", "免运费"),
            ("express", "顺丰次日达", "明天 18:00 前", "¥12"),
            ("pickup", "门店自提", "今天 15:00 后可取", "免运费"),
        ];
        var shipping = new RadioGroup { Value = "express" };
        foreach (var (value, title, time, price) in options)
        {
            var text = new Flex { Direction = FlexDirection.Column, Children = { new Strong { Text = title }, new Small { Text = time, Tone = TextTone.Muted } } };
            shipping.Items.Add(new Radio
            {
                Value = value,
                Content = new Flex { Justify = FlexJustify.Between, Gap = Space.Lg, Children = { text, new Small { Text = price } } },
                HorizontalAlignment = HorizontalAlignment.Stretch,
            });
            AutomationProperties.SetName((Radio)shipping.Items[^1]!, title);
        }
        var invoice = new RadioGroup { Orientation = Orientation.Horizontal, IsInvalid = true };
        invoice.Items.Add(new Radio { Value = "none", Content = "不开发票" });
        invoice.Items.Add(new Radio { Value = "personal", Content = "个人" });
        invoice.Items.Add(new Radio { Value = "company", Content = "企业" });
        var invoiceField = Labeled("发票 *", invoice, new Small { Text = "请选择发票类型", Tone = TextTone.Danger });
        invoice.PropertyChanged += (_, e) =>
        {
            if (e.Property != RadioGroup.ValueProperty) return;
            invoice.IsInvalid = invoice.Value is null;
            invoiceField.Children[^1].IsVisible = invoice.IsInvalid;
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Children = { Labeled("配送方式", shipping), invoiceField } };
    }

    /// <summary>套餐（任意数据，经 ToOption 变成选项）。</summary>
    sealed record Plan(string Id, string Name, string Price, int? Seats);

    /// <summary>标签在上、内容、说明或错误在下（Field 的临时替代）；组以标签为读屏名称。</summary>
    static Flex Labeled(string label, Control content, object? note = null)
    {
        AutomationProperties.SetName(content, label.TrimEnd(' ', '*'));
        var field = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Text { Text = label, Weight = TextWeight.Medium }, content } };
        if (note is not null) field.Children.Add(note as Control ?? new Small { Text = note.ToString(), Tone = TextTone.Muted });
        return field;
    }

    static Control Settings()
    {
        var notify = Named(new Switch { IsChecked = true }, "接收通知");
        var digest = Named(new Switch(), "每周摘要");
        digest.IsEnabled = notify.IsChecked;
        notify.PropertyChanged += (_, e) =>
        {
            if (e.Property == ChoiceBase.IsCheckedProperty) digest.IsEnabled = notify.IsChecked;
        };
        var dark = Named(new Switch { IsChecked = true, CheckedContent = new Icon { Data = Lucide.Moon }, UncheckedContent = new Icon { Data = Lucide.Sun }, Size = TextSize.Lg }, "暗色模式");

        // 切换要等接口：拦下切换、滑块里转圈，接口回来再写回
        var syncDescription = new Small { Text = "在所有设备上保持一致", Tone = TextTone.Muted };
        var sync = Named(new Switch { Tone = StatusTone.Success }, "云端同步");
        sync.Toggling += (_, e) =>
        {
            e.Cancel = true;
            sync.IsLoading = true;
            syncDescription.Text = "正在保存…";
            DispatcherTimer.RunOnce(() =>
            {
                sync.IsChecked = e.IsChecked;
                sync.IsLoading = false;
                syncDescription.Text = "在所有设备上保持一致";
            }, TimeSpan.FromMilliseconds(1200));
        };

        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    Row("接收通知", new Small { Text = "订单状态变化时推送", Tone = TextTone.Muted }, notify),
                    new Separator(),
                    Row("每周摘要", new Small { Text = "每周一早上发送上周数据", Tone = TextTone.Muted }, digest),
                    new Separator(),
                    Row("暗色模式", new Small { Text = "轨道里可以放图标或文字", Tone = TextTone.Muted }, dark),
                    new Separator(),
                    Row("云端同步", syncDescription, sync),
                },
            },
        };
    }

    static Flex Row(string title, Small description, Switch control)
    {
        var text = new Flex { Direction = FlexDirection.Column, Children = { new Text { Text = title, Weight = TextWeight.Medium }, description } };
        Flex.SetGrow(text, true);
        return new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Lg, Children = { text, control } };
    }

    static Switch Named(Switch control, string name)
    {
        AutomationProperties.SetName(control, name);
        return control;
    }
}
