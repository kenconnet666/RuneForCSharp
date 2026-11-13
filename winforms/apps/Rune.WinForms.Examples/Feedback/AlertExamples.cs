namespace Rune.WinForms.Examples;

/// <summary>Alert 的示例（vue-tsx-pro 文档站 feedback/Alert，Banner 的示例也在这一页）。说明是画出来的文字，里面不能放链接：链接作为下面的额外内容。</summary>
static class AlertExamples
{
    public static Example[] All =>
    [
        new("Alert", "tones", "色调", "Tone 决定颜色与默认图标：info、success、warning、danger、neutral、accent。说明写在 Description 上，标题可选。", Tones),
        new("Alert", "status", "进行中、播报与尺寸", "IsLoading 时图标换成加载指示；操作后才出现的提示写 Live = polite（错误用 assertive），读屏会念出来。TextSize 跟字号档位缩放。", Status),
        new("Alert", "variants", "样式、操作与关闭", "Variant：soft（默认）、surface（加描边）、outline（只有描边）。Action 放右侧的操作；订阅了 CloseRequested 才有关闭按钮，请使用方移除。", Variants),
        new("Alert", "banner", "页面顶部的横幅", "BannerControl 占满宽度、一行说完、内容居中，没有圆角，放在页面最上方；Variant = solid 最醒目，用于必须先看到的消息。订阅了 CloseRequested 才有关闭按钮，请使用方移除（关掉的可以记下来，下次不再出现）。", Banner),
    ];

    static FlexControl Column(Space gap, params Control[] children) => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = gap, Align = FlexAlign.Stretch }, children);

    static Control Tones()
    {
        var renew = new AlertControl { Tone = StatusTone.Warning, Title = "订阅即将到期", Description = "你的专业版将在 3 天后到期，续费可享 8 折。" };
        renew.Controls.Add(new LinkControl { Text = "现在续费", Href = new Uri("https://example.com/renew") });
        return Column(Space.Sm,
            new AlertControl { Tone = StatusTone.Info, Title = "维护通知", Description = "本周六 02:00–04:00 系统升级，期间无法下单。" },
            new AlertControl { Tone = StatusTone.Success, Title = "付款成功", Description = "订单 #10234 已付款 ¥1,280.00，预计明天发货。" },
            renew,
            new AlertControl { Tone = StatusTone.Danger, Title = "同步失败", Description = "网络连接中断，3 个文件没有上传。" },
            new AlertControl { Tone = StatusTone.Neutral, Description = "没有标题时只显示正文：适合一行就说清楚的提示。" });
    }

    static Control Status()
    {
        var result = new AlertControl { Live = AlertLive.Polite, Visible = false };
        var check = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.RefreshCw, Text = "检查更新" };
        check.Click += async (_, _) =>
        {
            check.IsLoading = result.IsLoading = true;
            (result.Tone, result.Title, result.Description, result.Visible) = (StatusTone.Info, "正在检查更新…", "连接更新服务器", true);
            await Task.Delay(1500);
            check.IsLoading = result.IsLoading = false;
            (result.Tone, result.Title, result.Description) = (StatusTone.Success, "已是最新版本", "当前版本 2.4.0，发布于 10 月 18 日。");
        };
        return Column(Space.Lg,
            Column(Space.Sm, Build.Flex(new FlexControl(), check), result),
            Column(Space.Sm,
                new AlertControl { TextSize = TextSize.Sm, Tone = StatusTone.Warning, Title = "TextSize = sm", Description = "侧栏、表单字段旁的紧凑提示。" },
                new AlertControl { TextSize = TextSize.Lg, Tone = StatusTone.Accent, Title = "TextSize = lg", Description = "页面顶部的重要公告。" }));
    }

    static Control Variants()
    {
        var retry = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Text = "重试" };
        var failed = new AlertControl { Variant = AlertVariant.Surface, Tone = StatusTone.Danger, Title = "保存失败", Description = "服务器暂时不可用。IsLoading 时图标换成旋转指示。", Action = retry };
        retry.Click += async (_, _) =>
        {
            retry.IsLoading = failed.IsLoading = true;
            await Task.Delay(1500);
            retry.IsLoading = failed.IsLoading = false;
        };
        var tip = new AlertControl { Tone = StatusTone.Info, Title = "小提示", Description = "关掉以后，点下面的按钮可以重新显示。" };
        var again = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Text = "重新显示提示", Visible = false };
        var list = Column(Space.Sm,
            new AlertControl { Variant = AlertVariant.Soft, Tone = StatusTone.Accent, Icon = Lucide.Sparkles, Title = "soft（默认）", Description = "浅色底，没有描边。" },
            new AlertControl { Variant = AlertVariant.Surface, Tone = StatusTone.Accent, Title = "surface", Description = "浅色底加描边：放在白色卡片里也看得清边界。" },
            new AlertControl { Variant = AlertVariant.Outline, Tone = StatusTone.Accent, Title = "outline", Description = "只有描边：信息不那么紧急时用。" },
            failed, tip, again);
        tip.CloseRequested += (_, _) =>
        {
            tip.Visible = false;
            again.Visible = true;
        };
        again.Click += (_, _) =>
        {
            tip.Visible = true;
            again.Visible = false;
        };
        return list;
    }

    static Control Banner()
    {
        // 实底上的链接跟随横幅的文字颜色，对比度才够
        var maintenance = new BannerControl
        {
            Variant = BannerVariant.Solid, Tone = StatusTone.Warning, Icon = Lucide.Wrench, Title = "系统维护", Description = "今晚 22:00–23:00 暂停服务，请提前保存草稿",
            Action = new LinkControl { Href = new Uri("https://example.com/maintenance"), Tone = TextTone.Inherit, Underline = LinkUnderline.Always, Text = "查看公告" },
        };
        var trial = new BannerControl { Tone = StatusTone.Accent, Description = "试用还剩 3 天，到期后项目将变为只读", Action = new ButtonControl { TextSize = TextSize.Xs, Variant = ButtonVariant.Solid, Text = "升级到正式版" } };
        var archived = new BannerControl { Tone = StatusTone.Neutral, Icon = Lucide.Lock, TextSize = TextSize.Sm, Description = "当前项目已归档，只能查看" };
        var restore = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, Text = "重新显示关掉的横幅", Visible = false };
        var body = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Start, Padding = new Padding(18, 14, 18, 14) },
            new TextControl { Text = "项目台账", Weight = TextWeight.Semibold },
            new TextControl { TextSize = TextSize.Sm, Tone = TextTone.Muted, Text = "横幅占满宽度、一行说完，放在页面最上方；关掉的写进本地存储，下次不再出现。" },
            restore);
        var page = Column(Space.None, maintenance, trial, archived, body);
        maintenance.CloseRequested += (_, _) => maintenance.Visible = false;
        trial.CloseRequested += (_, _) => trial.Visible = false;
        maintenance.VisibleChanged += (_, _) => restore.Visible = !maintenance.Visible || !trial.Visible;
        trial.VisibleChanged += (_, _) => restore.Visible = !maintenance.Visible || !trial.Visible;
        restore.Click += (_, _) => maintenance.Visible = trial.Visible = true;
        return Build.Card(new CardControl { CardPadding = Space.None }, page);
    }
}
