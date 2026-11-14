using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Rune.Avalonia.Examples;

/// <summary>Progress 的示例（vue-tsx-pro 文档站 feedback/Progress）。spinner 在 Spinner 页。</summary>
static class ProgressExamples
{
    public static Example[] All =>
    [
        new("Progress", "progress", "进度条", "Value 与 Max（默认 100）算完成比例，超出时夹住；Tone 表示状态。Label 显示在上方，同时是读屏名称；右侧默认是按语言格式化的百分比，ValueLabel 换成自己的文字（只是显示），读屏念的用 ValueText 改，ShowValueLabel=False 不显示。取值是 decimal。Bands 是量表的用法（磁盘占用、配额、评分这类量出来的数）：值落在哪一段就用哪一段的色调；仪表盘样子的量表用 Gauge。", Bars),
        new("Progress", "rings", "环形与目标", "Shape=circle 是环形：中间显示数值，Label 在下方，直径随 Size 缩放；不写 Value 时一段弧一直转。Target 是目标值：条上画一条竖线、环上画一道刻度，读屏念成\"60%，目标 80%\"。", Rings),
        new("Progress", "loading-bar", "换页时的加载条", "LoadingBar 是窗口顶部的一条细线，控制器加显示（同 Toast）：LoadingBarController 的 Start 开始、Finish 走满后淡出、Error 变成危险色走满。不知道真实进度，所以先快后慢地涨，停在 90% 前等完成；Delay（默认 200 毫秒）之内就完成的不显示。它只是视觉提示：读屏读到的应当是内容区域的忙碌状态。示例把加载条放进了卡片里，平时它放在窗口根部、贴着顶边。", LoadingBarExample),
        new("Progress", "overlay", "加载遮罩", "LoadingOverlay 盖住一块区域，内容保留在下面，读屏的“忙”立即生效；很快完成时不出现，出现后至少显示一会儿，淡入淡出。Blur 模糊下面的内容。遮罩挡住指针，键盘仍能操作下面的内容：需要禁止时由内容自己处理（这里保存时输入框不可用）。", OverlayExample),
        new("Progress", "shapes", "粗细与不确定进度", "Size 跟字号档位缩放粗细，Radius 改圆角；不写 Value 是不确定进度，循环滑动，系统关闭动画时变慢。", Shapes),
    ];

    static readonly (string Name, decimal Used, decimal Total, string Unit)[] Quotas =
    [
        ("存储空间", 38.6m, 50, "GB"),
        ("本月流量", 912, 1000, "GB"),
        ("团队成员", 7, 20, "人"),
        ("API 调用", 120_000, 100_000, "次"),
    ];

    static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    /// <summary>用量是量出来的数（量表）：越接近上限越醒目——八成以内正常，到上限为止警告，超出为危险。</summary>
    static ProgressBand[] BandsOf(decimal total) => [new(total * 0.8m, StatusTone.Accent), new(total, StatusTone.Warning), new(decimal.MaxValue, StatusTone.Danger)];

    static string Number(decimal value) => value.ToString("#,0.##", Chinese);

    static Control Bars()
    {
        var column = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Strong { Text = "套餐用量" } } };
        foreach (var (name, used, total, unit) in Quotas)
        {
            var progress = new Progress
            {
                Value = used, Max = total, Bands = BandsOf(total), Label = name,
                ValueLabel = $"{Number(used)} / {Number(total)} {unit}",
                ValueText = (value, max) => $"{value} / {max} {unit}",
            };
            // TS 的 propsValueLabel：超出时数值用危险色
            if (used > total)
                progress.Styles.Add(new Style(x => x.OfType<Text>().Name(ProgressParts.ValueLabel))
                {
                    Setters = { new Setter(Text.ToneProperty, TextTone.Danger) },
                });
            column.Children.Add(progress);
        }
        column.Children.Add(new Progress { Value = 64, Label = "资料完善度" });
        // 只要标签、不要右侧的数值：ShowValueLabel=False
        column.Children.Add(new Progress { Value = 3, Max = 5, Label = "新手任务", ShowValueLabel = false, Tone = StatusTone.Success });
        return new Card { Content = column };
    }

    static readonly (string Name, decimal Done, decimal Target, StatusTone Tone)[] Plants =
    [
        ("一车间", 86, 80, StatusTone.Success),
        ("二车间", 64, 80, StatusTone.Warning),
        ("三车间", 38, 75, StatusTone.Danger),
    ];

    static Control Rings()
    {
        var grid = new Grid { MinItemWidth = "9rem", Gap = Space.Md };
        foreach (var (name, done, target, tone) in Plants)
            grid.Children.Add(new Card
            {
                Content = new Flex
                {
                    Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs,
                    Children =
                    {
                        new Progress { Shape = ProgressShape.Circle, Size = TextSize.Lg, Value = done, Target = target, Tone = tone, Label = $"{name} 产量完成率" },
                        new Small { Text = $"目标 {target}%", Tone = TextTone.Muted },
                    },
                },
            });
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                grid,
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Md,
                    Children =
                    {
                        new Progress { Value = 6.4m, Max = 10, Target = 8, Label = "年度产量（万件）", ValueLabel = "6.4 / 10" },
                        new Progress { Value = 92, Target = 85, Tone = StatusTone.Success, Label = "交付准时率" },
                    },
                },
                new Flex
                {
                    Align = FlexAlign.Center, Gap = Space.Md,
                    Children =
                    {
                        Named(new Progress { Shape = ProgressShape.Circle, Size = TextSize.Sm }, "正在同步"),
                        new Text { Text = "不确定进度：弧线一直转", Size = TextSize.Sm, Tone = TextTone.Muted },
                        Named(new Progress { Shape = ProgressShape.Circle, Value = 100, Size = TextSize.Sm, Tone = StatusTone.Success }, "已完成"),
                    },
                },
            },
        };
    }

    static readonly (string Id, string Label, int Delay, string[] Rows, bool Fails)[] Pages =
    [
        ("orders", "订单列表", 900, ["SO-24101 华东仓 已发货", "SO-24102 华南仓 待出库"], false),
        ("stock", "库存台账", 1600, ["A-12 轴承 312 件", "B-07 密封圈 1 280 件"], false),
        ("report", "财务报表", 1200, [], true),
        ("quick", "个人设置", 120, ["通知：仅提及我", "语言：简体中文"], false),
    ];

    /// <summary>模拟换页：点标签后加载条走起来，数据回来时走满淡出；"财务报表"那页会加载失败（危险色）。</summary>
    static Control LoadingBarExample()
    {
        var controller = new LoadingBarController();
        var list = new TabList();
        global::Avalonia.Automation.AutomationProperties.SetName(list, "页面");
        foreach (var page in Pages) list.Children.Add(new Tab { Value = page.Id, Content = page.Label });
        var tabs = new Tabs { Value = "orders", Children = { list } };
        var body = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, MinHeight = 63 };
        var timer = new global::Avalonia.Threading.DispatcherTimer();
        void Load(string id)
        {
            var page = Pages.First(item => item.Id == id);
            timer.Stop();
            body.Children.Clear();
            body.Children.Add(new Small { Tone = TextTone.Muted, Text = $"正在加载{page.Label}…" });
            controller.Start();
            timer.Interval = TimeSpan.FromMilliseconds(page.Delay);
            timer.Tick += Done;
            timer.Start();
            void Done(object? sender, EventArgs e)
            {
                timer.Tick -= Done;
                timer.Stop();
                body.Children.Clear();
                if (page.Fails)
                {
                    controller.Error();
                    var retry = new Button { Size = TextSize.Xs, Variant = ButtonVariant.Ghost, Content = "重试" };
                    retry.Click += (_, _) => Load(id);
                    body.Children.Add(new Flex
                    {
                        Align = FlexAlign.Center, Gap = Space.Sm,
                        Children = { new Badge { Tone = StatusTone.Danger, Size = TextSize.Xs, Content = "加载失败" }, new Small { Tone = TextTone.Muted, Text = "服务器没有响应，请稍后重试" }, retry },
                    });
                    return;
                }
                controller.Finish();
                foreach (var row in page.Rows) body.Children.Add(new Text { Size = TextSize.Sm, Text = row });
            }
        }
        tabs.PropertyChanged += (_, e) =>
        {
            if (e.Property == Tabs.ValueProperty && tabs.Value is { } id) Load(id);
        };
        Load("orders");
        var card = new Card
        {
            Padding = Space.None, ClipToBounds = true,
            Content = new Panel
            {
                Children =
                {
                    new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Margin = new global::Avalonia.Thickness(16), Children = { tabs, body } },
                    new LoadingBar { Controller = controller },
                },
            },
        };
        Button Manual(string label, Action action, StatusTone tone = default)
        {
            var button = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = tone, Content = label };
            button.Click += (_, _) => action();
            return button;
        }
        var manual = new Flex
        {
            Align = FlexAlign.Center, Gap = Space.Sm, Wrap = FlexWrap.Wrap,
            Children =
            {
                new Small { Tone = TextTone.Muted, Text = "手动控制：" },
                Manual("Start()", controller.Start), Manual("Finish()", controller.Finish), Manual("Error()", controller.Error, StatusTone.Danger),
                new Small { Tone = TextTone.Muted, Text = "“个人设置”120ms 就好，不显示加载条" },
            },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { card, manual } };
    }

    static Control OverlayExample()
    {
        var name = new TextField { Text = "vue-tsx-pro" };
        var owner = new TextField { Text = "林小满" };
        var save = new Button { Content = "保存" };
        var blur = new Switch { Content = "模糊下面的内容", IsChecked = true };
        var overlay = new LoadingOverlay
        {
            Label = "正在保存", Blur = true,
            Content = new Card
            {
                Content = new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Md,
                    Children =
                    {
                        new Grid { MinItemWidth = "12em", Balanced = true, Gap = Space.Md, Children = { new Field { Label = "项目名称", Content = name }, new Field { Label = "负责人", Content = owner } } },
                        new Flex { Justify = FlexJustify.End, Children = { save } },
                    },
                },
            },
        };
        void Saving(bool value)
        {
            overlay.IsLoading = save.IsLoading = value;
            name.IsEnabled = owner.IsEnabled = !value;
        }
        save.Click += async (_, _) =>
        {
            Saving(true);
            await Task.Delay(1600);
            Saving(false);
        };
        blur.PropertyChanged += (_, e) =>
        {
            if (e.Property == Switch.IsCheckedProperty) overlay.Blur = blur.IsChecked == true;
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { blur, overlay } };
    }

    static Control Shapes()
    {
        var column = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg };
        foreach (var size in new[] { TextSize.Xs, TextSize.Sm, TextSize.Md, TextSize.Lg })
        {
            var bar = Named(new Progress { Value = 62, Size = size, Radius = size == TextSize.Lg ? Radius.Sm : Radius.Full }, $"粗细 {size}");
            Flex.SetGrow(bar, true);
            column.Children.Add(new Flex { Align = FlexAlign.Center, Gap = Space.Md, Children = { new Small { Text = size.Value, Tone = TextTone.Muted }, bar } });
        }
        column.Children.Add(new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Xs,
            Children =
            {
                new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Spinner { Size = TextSize.Sm }, new Text { Text = "正在连接服务器…（不确定进度：不写 Value）", Size = TextSize.Sm } } },
                Named(new Progress(), "正在连接服务器"),
            },
        });
        var uploaded = 3;
        var status = new Text { Size = TextSize.Sm };
        var upload = new Button { Content = "再传一个", Size = TextSize.Sm, Variant = ButtonVariant.Soft };
        var files = Named(new Progress { Max = 8, ValueText = (value, _) => $"已上传 {value} 个文件，共 8 个" }, "上传进度");
        void Refresh()
        {
            status.Text = $"已上传 {uploaded} / 8 个文件";
            upload.IsEnabled = uploaded < 8;
            files.Value = uploaded;
            files.Tone = uploaded >= 8 ? StatusTone.Success : StatusTone.Accent;
        }
        upload.Click += (_, _) =>
        {
            uploaded++;
            Refresh();
        };
        Refresh();
        column.Children.Add(new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Xs,
            Children = { new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Children = { status, upload } }, files },
        });
        return column;
    }

    static Progress Named(Progress progress, string name)
    {
        global::Avalonia.Automation.AutomationProperties.SetName(progress, name);
        return progress;
    }
}
