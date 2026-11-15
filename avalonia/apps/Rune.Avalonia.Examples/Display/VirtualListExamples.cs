using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>VirtualList、InfiniteListStatus 的示例（vue-tsx-pro 文档站 display/VirtualList）。图纸同 Image 的示例（docs/site/drawings）。</summary>
static class VirtualListExamples
{
    public static Example[] All =>
    [
        new("VirtualList", "contacts", "一万条联系人", "组件自己是滚动区域，高度由使用方给；平台的虚拟化面板按每项的实际高度排，只建看得见的几项。没有数据时显示 Empty；读屏念“第几项，共几项”，没建的也算在总数里。", Contacts),
        new("VirtualList", "feed", "滚动加载", "RangeChanged 报告可见范围，接近末尾时取下一页。IsLoading 时只用骨架：没有数据时铺几行，已有数据（取下一页）时接在末尾。页码、去重、是否还有更多与失败重试交给 InfiniteList，列表下方的 InfiniteListStatus 显示“重试”与“没有更多了”（这里第 3 页第一次会失败）。", Feed),
        new("VirtualList", "horizontal", "横向与跳到指定项", "Orientation = Horizontal 横向排列与滚动（给宽度）。ScrollToIndex、ScrollToOffset 滚动，ScrollAlign 决定停在视口的开头、中间、末尾或就近。", Horizontal),
    ];

    const string Surnames = "赵钱孙李周吴郑王冯陈褚卫蒋沈韩杨朱秦尤许何吕施张孔曹严华金魏陶姜";
    static readonly string[] GivenNames = ["小满", "一航", "默", "悠", "晴", "浩然", "思远", "雨桐", "子墨", "若溪", "安"];
    static readonly string[] Departments = ["设计中心", "研发部", "市场部", "财务部", "客服部"];

    sealed record Contact(int Id, string Name, string Department, string Phone);

    static readonly Contact[] Contacts_ = [.. Enumerable.Range(0, 10_000).Select(index => new Contact(
        index, $"{Surnames[index % Surnames.Length]}{GivenNames[index % GivenNames.Length]}", Departments[index % Departments.Length], $"138{index:D8}"))];

    static Thickness Padded => new(16, 8);

    // 一万条联系人：搜索过滤，没有匹配时显示 Empty
    static Control Contacts()
    {
        var search = new TextField { Type = TextFieldType.Search, Clearable = true, PlaceholderText = "姓名或电话", Width = 220 };
        AutomationProperties.SetName(search, "搜索联系人");
        var total = new Small { Tone = TextTone.Muted };
        var empty = new Text { Tone = TextTone.Muted, Margin = Padded };
        var list = new VirtualList
        {
            Height = 22 * 16,
            Empty = empty,
            ItemTemplate = new FuncDataTemplate<Contact>((contact, _) => new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Md, Margin = Padded,
                Children =
                {
                    new Avatar { Size = TextSize.Sm, DisplayName = contact.Name },
                    new Flex { Direction = FlexDirection.Column, Children = { new Text { Weight = TextWeight.Medium, Text = contact.Name }, new Small { Tone = TextTone.Muted, Text = $"{contact.Department} · {contact.Phone}" } } },
                },
            }),
        };
        AutomationProperties.SetName(list, "联系人");
        void Filter()
        {
            var keyword = search.Text ?? "";
            var found = keyword.Length == 0 ? Contacts_ : [.. Contacts_.Where(contact => contact.Name.Contains(keyword) || contact.Phone.Contains(keyword))];
            list.ItemsSource = found;
            total.Text = $"共 {found.Length:N0} 人";
            empty.Text = $"没有找到“{keyword}”";
        }
        search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Filter(); };
        Filter();
        return new Card
        {
            Padding = Space.None,
            Content = new StackPanel
            {
                Children = { new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Margin = Padded, Children = { search, total } }, list },
            },
        };
    }

    static readonly string[] Levels = ["INFO", "WARN", "INFO", "ERROR", "INFO", "DEBUG"];
    static readonly string[] Messages =
    [
        "订单服务健康检查通过", "支付回调重试第 2 次", "缓存命中率 97.4%", "库存同步超时，已放入重试队列", "用户 1024 登录成功",
        "定时任务 report:daily 开始执行，预计耗时 3 分钟，完成后会把结果推送到运营群",
    ];

    sealed record LogEntry(int Id, string Level, string Message);

    static TextTone ToneOf(string level) => level switch
    {
        "DEBUG" => TextTone.Muted,
        "WARN" => TextTone.Warning,
        "ERROR" => TextTone.Danger,
        _ => TextTone.Info,
    };

    // 运行日志：一页页取，滚到最后几行时取下一页；第 3 页第一次会失败，列表下方出现“重试”
    static Control Feed()
    {
        const int page = 30, pages = 5;
        var failed = false;
        var logs = new InfiniteList<LogEntry>(async (cursor, cancellation) =>
        {
            var number = (int?)cursor ?? 1;
            await Task.Delay(900, cancellation);
            if (number == 3 && !failed)
            {
                failed = true;
                throw new TimeoutException("网关超时");
            }
            return new InfinitePage<LogEntry>([.. Enumerable.Range((number - 1) * page, page).Select(index => new LogEntry(index, Levels[index % Levels.Length], Messages[index % Messages.Length]))],
                number < pages ? number + 1 : null);
        }) { KeyOf = entry => entry.Id };
        var list = new VirtualList
        {
            Height = 22 * 16,
            EstimateSize = 32,
            ItemTemplate = new FuncDataTemplate<LogEntry>((entry, _) => new Flex
            {
                Gap = Space.Sm, Margin = new Thickness(16, 4),
                Children =
                {
                    new Small { Tone = ToneOf(entry.Level), Mono = true, Width = 3.5 * 13, Text = entry.Level },
                    new Text { Size = TextSize.Sm, Text = entry.Message },
                },
            }),
        };
        AutomationProperties.SetName(list, "运行日志");
        var loaded = new Small { Tone = TextTone.Muted, Margin = new Thickness(16, 4) };
        void Sync()
        {
            list.ItemsSource = logs.Items;
            list.IsLoading = logs.IsLoading;
            loaded.Text = $"已加载 {logs.Count} / {page * pages} 条";
        }
        logs.PropertyChanged += (_, _) => Sync();
        list.RangeChanged += (_, range) => logs.OnRangeChanged(range);
        Sync();
        _ = logs.Refresh();
        return new Card { Padding = Space.None, Content = new StackPanel { Children = { list, new InfiniteListStatus { List = logs }, loaded } } };
    }

    static readonly string[] Drawings = ["DWG-1021", "DWG-1034", "DWG-1047", "DWG-1052", "DWG-1066", "DWG-1078"];

    sealed record Photo(int Id, Uri Source, string Title);

    // 横向的一千张缩略图：只建看得见的几张；跳到第几张
    static Control Horizontal()
    {
        var photos = Enumerable.Range(0, 1000).Select(index => new Photo(index, new Uri($"drawings/{Drawings[index % Drawings.Length]}.png", UriKind.Relative), $"第 {index + 1} 张")).ToList();
        var list = new VirtualList
        {
            ItemsSource = photos,
            Orientation = Orientation.Horizontal,
            EstimateSize = 136,
            Height = 8.5 * 16,
            ItemTemplate = new FuncDataTemplate<Photo>((photo, _) => new Panel
            {
                Width = 128, Height = 128, Margin = new Thickness(0, 0, 8, 0),
                Children =
                {
                    new Image { Source = photo.Source, Alt = photo.Title, Fit = ImageFit.Cover, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch },
                    // 序号压在图的左下角
                    new Border
                    {
                        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4), Padding = new Thickness(4, 0),
                        CornerRadius = new CornerRadius(4), [!Border.BackgroundProperty] = new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(ThemeKeys.Brush.Surface),
                        Child = new Text { Size = TextSize.Xs, Text = photo.Title },
                    },
                },
            }),
        };
        AutomationProperties.SetName(list, "相册");
        var target = new NumberField { Value = 500, Min = 1, Max = 1000, Size = TextSize.Sm, Width = 120 };
        AutomationProperties.SetName(target, "跳到第几张");
        var jump = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Content = "跳过去" };
        jump.Click += (_, _) => list.ScrollToIndex((int)(target.Value ?? 1) - 1, ScrollAlign.Start);
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children =
            {
                list,
                new Flex { Gap = Space.Sm, Align = FlexAlign.Center, Wrap = true, Children = { target, jump, new Small { Tone = TextTone.Muted, Text = "只建看得见的几张" } } },
            },
        };
    }
}
