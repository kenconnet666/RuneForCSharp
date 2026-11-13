using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf.Examples;

/// <summary>Alert 的示例（vue-tsx-pro 文档站 feedback/Alert）。banner 在 Banner 做了以后加。</summary>
static class AlertExamples
{
    public static Example[] All =>
    [
        new("Alert", "tones", "色调", "Tone 决定颜色与默认图标：info、success、warning、danger、neutral、accent。说明写在内容上，标题可选。", Tones),
        new("Alert", "status", "进行中、播报与尺寸", "IsLoading 时图标换成加载指示；操作后才出现的提示写 Live=\"polite\"（错误用 assertive），读屏会念出来。Size 跟字号档位缩放。", Status),
        new("Alert", "variants", "样式、操作与关闭", "Variant：soft（默认）、surface（加描边）、outline（只有描边）。Action 放右侧的操作；订阅了 CloseRequested 才有关闭按钮，淡出后再请使用方移除。", Variants),
    ];

    static FrameworkElement Tones()
    {
        // 说明里的链接：WPF 的 Link 就是行内元素
        var renew = new TextBlock { TextWrapping = TextWrapping.Wrap };
        renew.Inlines.Add(new Run("你的专业版将在 3 天后到期，"));
        renew.Inlines.Add(new Link { Href = new Uri("https://example.com/renew"), Inlines = { new Run("现在续费") } });
        renew.Inlines.Add(new Run("可享 8 折。"));
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm,
            Children =
            {
                new Alert { Tone = StatusTone.Info, Title = "维护通知", Content = "本周六 02:00–04:00 系统升级，期间无法下单。" },
                new Alert { Tone = StatusTone.Success, Title = "付款成功", Content = "订单 #10234 已付款 ¥1,280.00，预计明天发货。" },
                new Alert { Tone = StatusTone.Warning, Title = "订阅即将到期", Content = renew },
                new Alert { Tone = StatusTone.Danger, Title = "同步失败", Content = "网络连接中断，3 个文件没有上传。" },
                new Alert { Tone = StatusTone.Neutral, Content = "没有标题时只显示正文：适合一行就说清楚的提示。" },
            },
        };
    }

    static FrameworkElement Status()
    {
        var result = new Alert { Live = AlertLive.Polite, Visibility = Visibility.Collapsed };
        var check = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.RefreshCw, Content = "检查更新" };
        check.Click += async (_, _) =>
        {
            check.IsLoading = result.IsLoading = true;
            (result.Tone, result.Title, result.Content, result.Visibility) = (StatusTone.Info, "正在检查更新…", "连接更新服务器", Visibility.Visible);
            await Task.Delay(1500);
            check.IsLoading = result.IsLoading = false;
            (result.Tone, result.Title, result.Content) = (StatusTone.Success, "已是最新版本", "当前版本 2.4.0，发布于 10 月 18 日。");
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { new Flex { Children = { check } }, result } },
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Sm,
                    Children =
                    {
                        new Alert { Size = TextSize.Sm, Tone = StatusTone.Warning, Title = "Size=\"sm\"", Content = "侧栏、表单字段旁的紧凑提示。" },
                        new Alert { Size = TextSize.Lg, Tone = StatusTone.Accent, Title = "Size=\"lg\"", Content = "页面顶部的重要公告。" },
                    },
                },
            },
        };
    }

    static FrameworkElement Variants()
    {
        var list = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm };
        list.Children.Add(new Alert { Variant = AlertVariant.Soft, Tone = StatusTone.Accent, Icon = Lucide.Sparkles, Title = "soft（默认）", Content = "浅色底，没有描边。" });
        list.Children.Add(new Alert { Variant = AlertVariant.Surface, Tone = StatusTone.Accent, Title = "surface", Content = "浅色底加描边：放在白色卡片里也看得清边界。" });
        list.Children.Add(new Alert { Variant = AlertVariant.Outline, Tone = StatusTone.Accent, Title = "outline", Content = "只有描边：信息不那么紧急时用。" });
        var retry = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Danger, Content = "重试" };
        var failed = new Alert { Variant = AlertVariant.Surface, Tone = StatusTone.Danger, Title = "保存失败", Content = "服务器暂时不可用。IsLoading 时图标换成旋转指示。", Action = retry };
        retry.Click += async (_, _) =>
        {
            retry.IsLoading = failed.IsLoading = true;
            await Task.Delay(1500);
            retry.IsLoading = failed.IsLoading = false;
        };
        list.Children.Add(failed);
        var tip = new Alert { Tone = StatusTone.Info, Title = "小提示", Content = "关掉以后，点下面的按钮可以重新显示。" };
        var again = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Content = "重新显示提示", Visibility = Visibility.Collapsed };
        tip.CloseRequested += (_, _) =>
        {
            list.Children.Remove(tip);
            again.Visibility = Visibility.Visible;
        };
        again.Click += (_, _) =>
        {
            list.Children.Insert(list.Children.IndexOf(again), tip);
            again.Visibility = Visibility.Collapsed;
        };
        list.Children.Add(tip);
        list.Children.Add(again);
        return list;
    }
}
