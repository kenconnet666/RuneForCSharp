namespace Rune.WinForms.Examples;

/// <summary>Switch 的示例（vue-tsx-pro 文档站 input/Checkbox 页的 switch）。checkbox、radio 等 Checkbox、Radio。</summary>
static class SwitchExamples
{
    public static Example[] All =>
    [
        new("Checkbox", "switch", "开关", "Switch 是立即生效的设置（需要提交才生效的用 Checkbox）。切换要等接口时写 IsLoading：滑块里转圈、点击无效；在 Toggling 里拦下切换，接口回来后再改 IsChecked。轨道里可以放图标或文字，对读屏隐藏。", Settings),
    ];

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
