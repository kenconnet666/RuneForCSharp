using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Rune.Avalonia.Examples;

/// <summary>Switch 的示例（vue-tsx-pro 文档站 input/Checkbox 页的 switch）。checkbox、radio 等 Checkbox、Radio。</summary>
static class SwitchExamples
{
    public static Example[] All =>
    [
        new("Checkbox", "switch", "开关", "Switch 是立即生效的设置（需要提交才生效的用 Checkbox）。切换要等接口时写 IsLoading：滑块里转圈、点击无效；在 Toggling 里拦下切换，接口回来后再改 IsChecked。轨道里可以放图标或文字，对读屏隐藏。", Settings),
    ];

    static Control Settings()
    {
        var notify = Named(new Switch { IsChecked = true }, "接收通知");
        var digest = Named(new Switch(), "每周摘要");
        digest.IsEnabled = notify.IsChecked;
        notify.PropertyChanged += (_, e) =>
        {
            if (e.Property == Switch.IsCheckedProperty) digest.IsEnabled = notify.IsChecked;
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
