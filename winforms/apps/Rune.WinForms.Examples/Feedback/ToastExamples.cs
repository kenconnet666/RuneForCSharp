namespace Rune.WinForms.Examples;

/// <summary>ToasterControl 的示例（vue-tsx-pro 文档站 feedback/Toast）。每个示例自己的控制器与显示区域，显示区域停在示例框的一角。</summary>
static class ToastExamples
{
    public static Example[] All =>
    [
        new("Toast", "basic", "基础用法", "应用里建一个 ToasterController（静态字段、服务里都能用），窗体上放一个 ToasterControl 显示区域，然后在任意位置 Show(…)。每条通知就是 AlertControl：图标、标题、正文、操作、关闭按钮都一样；Promise 跟踪一个任务，从加载中换成成功或失败。默认 5 秒后消失，指针停在上面、焦点在里面、窗体不在前台时暂停；横向拖动可以关掉。", Basic),
        new("Toast", "persistent", "常驻与更新", "Duration = Timeout.InfiniteTimeSpan 不自动消失，Closable = false 去掉关闭按钮——问题解决前一直提醒。用同一个编号再 Show 把这条通知整个换成新的内容（Update 只换写了的），计时按新的 Duration 重新开始；Icon 换掉按色调选的图标。", Persistent),
        new("Toast", "layout", "位置、叠放与剩余时间", "Placement 是六个位置；Stack 叠成一摞（更早的变窄、错开露出一条边），悬停或聚焦时展开；Progress 在底部显示剩余时间，暂停时一起停；Max 限制同时显示的条数，更早的先排队。new ToasterController(duration) 改默认的显示时长。", Layout),
        new("Toast", "shortcuts", "快捷方法、同编号只留一条、关闭回调", "Success / Error / Warning / Info / Loading 第一个参数是标题；Error 默认读屏立即播报，Loading 不自动消失。写了 Id 的通知只有一条：同一个编号再显示时换掉原来那条并重新计时；先 Loading(\"正在导出\", new() { Id = … })，完成后 Success(\"已导出\", new() { Id = … }) 原地换成结果。OnClose 在开始关闭时调用，参数是到时（Timeout）还是被关掉（Dismissed）。", Shortcuts),
    ];

    /// <summary>示例框：内容停在上面，显示区域停在框的一角（在最上层）。</summary>
    static Control Frame(Control content, ToasterControl toaster)
    {
        var frame = new Panel { Dock = DockStyle.Top, BackColor = Color.Transparent };
        frame.Height = frame.MinimumSize.Height + frame.LogicalToDeviceUnits(360);
        content.Dock = DockStyle.Top;
        frame.Controls.Add(content);
        frame.Controls.Add(toaster);
        return frame;
    }

    static ButtonControl Button(string text, IconData? icon = null, ButtonVariant? variant = null, StatusTone? tone = null, bool small = true)
    {
        var button = new ButtonControl { Text = text, Icon = icon };
        if (variant is { } v) button.Variant = v;
        if (tone is { } t) button.Tone = t;
        if (small) button.TextSize = TextSize.Sm;
        return button;
    }

    static readonly (StatusTone Tone, string Title, string Description)[] Messages =
    [
        (StatusTone.Success, "已保存", "改动已同步到所有设备。"),
        (StatusTone.Info, "有新版本", "重启后即可使用 2.4.0。"),
        (StatusTone.Warning, "存储空间不足", "还剩 2 GB，请清理回收站。"),
        (StatusTone.Danger, "发送失败", "对方的邮箱地址无效。"),
    ];

    // 四种色调、可撤销的删除、跟踪一个任务的上传
    static Control Basic()
    {
        var toaster = new ToasterController();
        var tones = new FlexControl { Gap = Space.Sm, Wrap = true };
        foreach (var (tone, title, description) in Messages)
        {
            var button = Button(title, variant: ButtonVariant.Soft, tone: tone);
            button.Click += (_, _) => toaster.Show(new() { Tone = tone, Title = title, Description = description });
            tones.Controls.Add(button);
        }
        var remove = Button("删除（可撤销）", Lucide.Trash, ButtonVariant.Surface, StatusTone.Neutral);
        remove.Click += (_, _) =>
        {
            var undo = Button("撤销", variant: ButtonVariant.Soft);
            var id = toaster.Show(new() { Title = "已删除 3 个文件", Action = undo });
            undo.Click += (_, _) =>
            {
                toaster.Dismiss(id);
                toaster.Success("已恢复");
            };
        };
        var upload = Button("上传（Promise）", Lucide.Upload, ButtonVariant.Surface, StatusTone.Neutral);
        upload.Click += async (_, _) =>
        {
            try
            {
                await toaster.Promise(Upload(), new()
                {
                    Loading = new() { Title = "正在上传 report.pdf" },
                    Success = name => new() { Title = $"{name} 上传完成" },
                    Error = error => new() { Title = "上传失败", Description = error.Message },
                });
            }
            catch (IOException)
            {
                // 失败已经显示在通知里
            }
        };
        var content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md }, tones, Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = true }, remove, upload));
        return Frame(content, new ToasterControl { Controller = toaster, Placement = ToastPlacement.BottomEnd });
    }

    /// <summary>假的上传：1.8 秒后七成成功、三成失败。</summary>
    static async Task<string> Upload()
    {
        await Task.Delay(1800);
        return Random.Shared.NextDouble() > 0.3 ? "report.pdf" : throw new IOException("网络中断");
    }

    // 断网时一直提醒、不能手动关；恢复后同一条换成成功，3 秒后消失
    static Control Persistent()
    {
        var toaster = new ToasterController();
        string? offline = null;
        var restore = Button("恢复连接", Lucide.Wifi, ButtonVariant.Outline, StatusTone.Neutral, small: false);
        restore.Enabled = false;
        void Reconnect()
        {
            if (offline is null) return;
            toaster.Show(new()
            {
                Id = offline, Tone = StatusTone.Success, Icon = Lucide.Wifi, Title = "已重新连接", Description = "离线期间的改动已同步。", Duration = TimeSpan.FromSeconds(3),
            });
            offline = null;
            restore.Enabled = false;
        }
        var disconnect = Button("模拟断网", Lucide.WifiOff, ButtonVariant.Outline, StatusTone.Neutral, small: false);
        disconnect.Click += (_, _) =>
        {
            if (offline is not null) return;
            var retry = Button("重试", variant: ButtonVariant.Soft, tone: StatusTone.Warning);
            retry.Click += (_, _) => Reconnect();
            offline = toaster.Show(new()
            {
                Tone = StatusTone.Warning, Icon = Lucide.WifiOff, Title = "网络已断开", Description = "改动先保存在本机，恢复连接后自动同步。",
                // 问题没解决前一直显示，也不能手动关掉
                Duration = Timeout.InfiniteTimeSpan, Closable = false, Action = retry,
            });
            restore.Enabled = true;
        };
        restore.Click += (_, _) => Reconnect();
        return Frame(Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = true }, disconnect, restore), new ToasterControl { Controller = toaster, Placement = ToastPlacement.BottomStart });
    }

    static readonly (string Value, string Label)[] Placements =
    [
        ("top-start", "左上"), ("top", "顶部居中"), ("top-end", "右上"), ("bottom-start", "左下"), ("bottom", "底部居中"), ("bottom-end", "右下"),
    ];

    static readonly (StatusTone Tone, string Title)[] Samples =
    [
        (StatusTone.Success, "报表已导出"), (StatusTone.Info, "林小满评论了你的文档"), (StatusTone.Warning, "登录状态将在 5 分钟后过期"), (StatusTone.Neutral, "已复制到剪贴板"),
    ];

    // 位置、最多几条、叠成一摞、剩余时间条都能改；一打开先放三条，看叠放的样子
    static Control Layout()
    {
        var toaster = new ToasterController(TimeSpan.FromSeconds(6));
        var view = new ToasterControl { Controller = toaster, Placement = ToastPlacement.TopEnd, Stack = true, Progress = true };
        var count = 0;
        void Notify()
        {
            var (tone, title) = Samples[count++ % Samples.Length];
            toaster.Show(new() { Tone = tone, Title = title, Description = $"第 {count} 条通知" });
        }
        var placement = new SelectControl { ItemsSource = Placements.Select(item => new ListboxOption(item.Value, item.Label)).ToArray(), Value = "top-end" };
        placement.ValueChanged += (_, _) =>
        {
            if (placement.Value is { } value) view.Placement = ToastPlacement.Parse(value);
        };
        var max = new NumberFieldControl { Value = 3, Min = 1, Max = 6 };
        max.ValueChanged += (_, _) => view.Max = (int)(max.Value ?? 1);
        var stack = new SwitchControl { Text = "叠成一摞", IsChecked = true };
        stack.IsCheckedChanged += (_, _) => view.Stack = stack.IsChecked;
        var progress = new SwitchControl { Text = "剩余时间条", IsChecked = true };
        progress.IsCheckedChanged += (_, _) => view.Progress = progress.IsChecked;
        var notify = Button("发一条通知", Lucide.BellRing, small: false);
        notify.Click += (_, _) => Notify();
        var clear = Button("全部关闭", variant: ButtonVariant.Soft, tone: StatusTone.Neutral, small: false);
        clear.Click += (_, _) => toaster.Dismiss();
        // 两个字段宽 360（逻辑像素）
        FieldControl Narrow(FieldControl field)
        {
            field.MaximumSize = new Size(field.LogicalToDeviceUnits(360), 0);
            return field;
        }
        var content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
            Narrow(new FieldControl { Label = "位置", Content = placement }),
            Narrow(new FieldControl { Label = "最多同时显示", Content = max }),
            Build.Flex(new FlexControl { Gap = Space.Lg, Wrap = true }, stack, progress),
            Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = true }, notify, clear));
        for (var i = 0; i < 3; i++) Notify();
        return Frame(content, view);
    }

    // 连点只留一条、loading 原地换成 success、关闭的原因
    static Control Shortcuts()
    {
        var toaster = new ToasterController();
        var log = new List<string>();
        var record = new SmallControl { Tone = TextTone.Muted, Text = "OnClose 的记录显示在这里" };
        void Record(string line)
        {
            log.Insert(0, line);
            if (log.Count > 4) log.RemoveAt(4);
            record.Text = string.Join("；", log);
        }
        string Reason(ToastCloseReason reason) => reason == ToastCloseReason.Timeout ? "到时" : "手动";
        var draft = Button("保存草稿（连点试试）", Lucide.Save, ButtonVariant.Soft, StatusTone.Success);
        // 连点几次也只有一条：同一个编号换掉原来的那条并重新计时
        draft.Click += (_, _) => toaster.Success("草稿已保存", new() { Id = "draft" });
        var export = Button("导出（Loading → Success）", Lucide.Download, ButtonVariant.Surface, StatusTone.Neutral);
        export.Click += async (_, _) =>
        {
            toaster.Loading("正在导出 10 月对账单", new() { Id = "export" });
            await Task.Delay(1500);
            toaster.Success("对账单已导出", new() { Id = "export", Description = "共 1,284 行，已保存到下载目录", OnClose = reason => Record($"导出通知关闭：{Reason(reason)}") });
        };
        var sync = Button("同步失败", Lucide.RefreshCw, ButtonVariant.Soft, StatusTone.Danger);
        sync.Click += (_, _) => toaster.Error("同步失败", new() { Id = "sync", Description = "服务器暂时不可用，稍后会自动重试", OnClose = reason => Record($"同步通知关闭：{Reason(reason)}") });
        var content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md }, Build.Flex(new FlexControl { Gap = Space.Sm, Wrap = true }, draft, export, sync), record);
        return Frame(content, new ToasterControl { Controller = toaster, Placement = ToastPlacement.BottomEnd });
    }
}
