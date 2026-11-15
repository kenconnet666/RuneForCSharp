using System.ComponentModel;
using System.IO;
using System.Windows;

namespace Rune.Wpf.Examples;

/// <summary>Toast 的示例（vue-tsx-pro 文档站 feedback/Toast）。每个示例自己的控制器与显示区域，显示区域盖在示例框上、停在一角。</summary>
static class ToastExamples
{
    public static Example[] All =>
    [
        new("Toast", "basic", "基础用法", "应用里建一个 ToasterController（静态字段、服务里都能用），窗口里放一个 Toaster 显示区域，然后在任意位置 Show(…)。每条通知就是 Alert：图标、标题、正文、操作、关闭按钮都一样；Promise 跟踪一个任务，从加载中换成成功或失败。默认 5 秒后消失，指针停在上面、焦点在里面、窗口不在前台时暂停；横向拖动可以关掉。", Basic),
        new("Toast", "persistent", "常驻与更新", "Duration = Timeout.InfiniteTimeSpan 不自动消失，Closable = false 去掉关闭按钮——问题解决前一直提醒。用同一个编号再 Show 把这条通知整个换成新的内容（Update 只换写了的），计时按新的 Duration 重新开始；Icon 换掉按色调选的图标。", Persistent),
        new("Toast", "layout", "位置、叠放与剩余时间", "Placement 是窗口的六个位置；Stack 像 Sonner 一样叠成一摞，悬停或聚焦时展开；Progress 在底部显示剩余时间，暂停时一起停；Max 限制同时显示的条数，更早的先排队。new ToasterController(duration) 改默认的显示时长。", Layout),
        new("Toast", "shortcuts", "快捷方法、同编号只留一条、关闭回调", "Success / Error / Warning / Info / Loading 第一个参数是标题；Error 默认读屏立即播报，Loading 不自动消失。写了 Id 的通知只有一条：同一个编号再显示时换掉原来那条并重新计时；先 Loading(\"正在导出\", new() { Id = … })，完成后 Success(\"已导出\", new() { Id = … }) 原地换成结果。OnClose 在开始关闭时调用，参数是到时（Timeout）还是被关掉（Dismissed）。", Shortcuts),
    ];

    /// <summary>示例框：内容在上，显示区域盖在整个框上、停在一角。</summary>
    static FrameworkElement Frame(UIElement content, Toaster toaster) => new System.Windows.Controls.Grid { MinHeight = 360, Children = { content, toaster } };

    static readonly (StatusTone Tone, string Title, string Description)[] Messages =
    [
        (StatusTone.Success, "已保存", "改动已同步到所有设备。"),
        (StatusTone.Info, "有新版本", "重启后即可使用 2.4.0。"),
        (StatusTone.Warning, "存储空间不足", "还剩 2 GB，请清理回收站。"),
        (StatusTone.Danger, "发送失败", "对方的邮箱地址无效。"),
    ];

    // 四种色调、可撤销的删除、跟踪一个任务的上传
    static FrameworkElement Basic()
    {
        var toaster = new ToasterController();
        var tones = new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap };
        foreach (var (tone, title, description) in Messages)
        {
            var button = new Button { Content = title, Variant = ButtonVariant.Soft, Tone = tone, Size = TextSize.Sm };
            button.Click += (_, _) => toaster.Show(new() { Tone = tone, Title = title, Description = description });
            tones.Children.Add(button);
        }
        var remove = new Button { Content = "删除（可撤销）", Icon = Lucide.Trash, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Size = TextSize.Sm };
        remove.Click += (_, _) =>
        {
            var undo = new Button { Content = "撤销", Variant = ButtonVariant.Soft, Size = TextSize.Sm };
            var id = toaster.Show(new() { Title = "已删除 3 个文件", Action = undo });
            undo.Click += (_, _) =>
            {
                toaster.Dismiss(id);
                toaster.Success("已恢复");
            };
        };
        var upload = new Button { Content = "上传（Promise）", Icon = Lucide.Upload, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Size = TextSize.Sm };
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
        var content = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { tones, new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { remove, upload } } } };
        return Frame(content, new Toaster { Controller = toaster, Placement = ToastPlacement.BottomEnd });
    }

    /// <summary>假的上传：1.8 秒后七成成功、三成失败。</summary>
    static async Task<string> Upload()
    {
        await Task.Delay(1800);
        return Random.Shared.NextDouble() > 0.3 ? "report.pdf" : throw new IOException("网络中断");
    }

    // 断网时一直提醒、不能手动关；恢复后同一条换成成功，3 秒后消失
    static FrameworkElement Persistent()
    {
        var toaster = new ToasterController();
        string? offline = null;
        var restore = new Button { Content = "恢复连接", Icon = Lucide.Wifi, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, IsEnabled = false };
        void Reconnect()
        {
            if (offline is null) return;
            toaster.Show(new()
            {
                Id = offline, Tone = StatusTone.Success, Icon = Lucide.Wifi, Title = "已重新连接", Description = "离线期间的改动已同步。", Duration = TimeSpan.FromSeconds(3),
            });
            offline = null;
            restore.IsEnabled = false;
        }
        var disconnect = new Button { Content = "模拟断网", Icon = Lucide.WifiOff, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral };
        disconnect.Click += (_, _) =>
        {
            if (offline is not null) return;
            var retry = new Button { Content = "重试", Variant = ButtonVariant.Soft, Tone = StatusTone.Warning, Size = TextSize.Sm };
            retry.Click += (_, _) => Reconnect();
            offline = toaster.Show(new()
            {
                Tone = StatusTone.Warning, Icon = Lucide.WifiOff, Title = "网络已断开", Description = "改动先保存在本机，恢复连接后自动同步。",
                // 问题没解决前一直显示，也不能手动关掉
                Duration = Timeout.InfiniteTimeSpan, Closable = false, Action = retry,
            });
            restore.IsEnabled = true;
        };
        restore.Click += (_, _) => Reconnect();
        var content = new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { disconnect, restore } };
        return Frame(content, new Toaster { Controller = toaster, Placement = ToastPlacement.BottomStart });
    }

    static readonly (string Value, string Label)[] Placements =
    [
        ("top-start", "左上"), ("top", "顶部居中"), ("top-end", "右上"), ("bottom-start", "左下"), ("bottom", "底部居中"), ("bottom-end", "右下"),
    ];

    static readonly (StatusTone Tone, string Title)[] Samples =
    [
        (StatusTone.Success, "报表已导出"), (StatusTone.Info, "林小满评论了你的文档"), (StatusTone.Warning, "登录状态将在 5 分钟后过期"), (StatusTone.Neutral, "已复制到剪贴板"),
    ];

    static void Watch(DependencyObject target, DependencyProperty property, Action changed) =>
        DependencyPropertyDescriptor.FromProperty(property, target.GetType()).AddValueChanged(target, (_, _) => changed());

    // 位置、最多几条、叠成一摞、剩余时间条都能改；一打开先放三条，看叠放的样子
    static FrameworkElement Layout()
    {
        var toaster = new ToasterController(TimeSpan.FromSeconds(6));
        var view = new Toaster { Controller = toaster, Placement = ToastPlacement.TopEnd, Stack = true, Progress = true };
        var count = 0;
        void Notify()
        {
            var (tone, title) = Samples[count++ % Samples.Length];
            toaster.Show(new() { Tone = tone, Title = title, Description = $"第 {count} 条通知" });
        }
        var placement = new Select { ItemsSource = Placements.Select(item => new ListboxOption(item.Value, item.Label)).ToArray(), Value = "top-end" };
        Watch(placement, Select.ValueProperty, () =>
        {
            if (placement.Value is { } value) view.Placement = ToastPlacement.Parse(value);
        });
        var max = new NumberField { Value = 3, Min = 1, Max = 6 };
        Watch(max, NumberField.ValueProperty, () => view.Max = (int)(max.Value ?? 1));
        var stack = new Switch { Content = "叠成一摞", IsChecked = true };
        Watch(stack, Switch.IsCheckedProperty, () => view.Stack = stack.IsChecked);
        var progress = new Switch { Content = "剩余时间条", IsChecked = true };
        Watch(progress, Switch.IsCheckedProperty, () => view.Progress = progress.IsChecked);
        var notify = new Button { Content = "发一条通知", Icon = Lucide.BellRing };
        notify.Click += (_, _) => Notify();
        var clear = new Button { Content = "全部关闭", Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral };
        clear.Click += (_, _) => toaster.Dismiss();
        var content = new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg, Width = 360, HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                new Field { Label = "位置", Content = placement },
                new Field { Label = "最多同时显示", Content = max },
                new Flex { Gap = Space.Lg, Wrap = FlexWrap.Wrap, Children = { stack, progress } },
                new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { notify, clear } },
            },
        };
        for (var i = 0; i < 3; i++) Notify();
        return Frame(content, view);
    }

    // 连点只留一条、loading 原地换成 success、关闭的原因
    static FrameworkElement Shortcuts()
    {
        var toaster = new ToasterController();
        var log = new List<string>();
        var record = new Small { Tone = TextTone.Muted, Text = "OnClose 的记录显示在这里" };
        void Record(string line)
        {
            log.Insert(0, line);
            if (log.Count > 4) log.RemoveAt(4);
            record.Text = string.Join("；", log);
        }
        string Reason(ToastCloseReason reason) => reason == ToastCloseReason.Timeout ? "到时" : "手动";
        var draft = new Button { Content = "保存草稿（连点试试）", Icon = Lucide.Save, Variant = ButtonVariant.Soft, Tone = StatusTone.Success, Size = TextSize.Sm };
        // 连点几次也只有一条：同一个编号换掉原来的那条并重新计时
        draft.Click += (_, _) => toaster.Success("草稿已保存", new() { Id = "draft" });
        var export = new Button { Content = "导出（Loading → Success）", Icon = Lucide.Download, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Size = TextSize.Sm };
        export.Click += async (_, _) =>
        {
            toaster.Loading("正在导出 10 月对账单", new() { Id = "export" });
            await Task.Delay(1500);
            toaster.Success("对账单已导出", new() { Id = "export", Description = "共 1,284 行，已保存到下载目录", OnClose = reason => Record($"导出通知关闭：{Reason(reason)}") });
        };
        var sync = new Button { Content = "同步失败", Icon = Lucide.RefreshCw, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Size = TextSize.Sm };
        sync.Click += (_, _) => toaster.Error("同步失败", new() { Id = "sync", Description = "服务器暂时不可用，稍后会自动重试", OnClose = reason => Record($"同步通知关闭：{Reason(reason)}") });
        var content = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap, Children = { draft, export, sync } }, record } };
        return Frame(content, new Toaster { Controller = toaster, Placement = ToastPlacement.BottomEnd });
    }
}
