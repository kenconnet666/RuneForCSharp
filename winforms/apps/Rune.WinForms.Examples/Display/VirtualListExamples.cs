namespace Rune.WinForms.Examples;

/// <summary>VirtualListControl、InfiniteListStatusControl 的示例（vue-tsx-pro 文档站 display/VirtualList）。图纸同 Image 的示例（docs/site/drawings）。</summary>
static class VirtualListExamples
{
    public static Example[] All =>
    [
        new("VirtualList", "contacts", "一万条联系人", "组件自己是滚动区域，高度由使用方给；ItemFactory 只为看得见的几项建控件，先按 EstimateSize 排，建出来后按实际高度量。没有数据时显示 Empty；读屏念“第几项，共几项”，没建的也算在总数里。", Contacts),
        new("VirtualList", "feed", "滚动加载", "RangeChanged 报告可见范围，接近末尾时取下一页。IsLoading 时只用骨架：没有数据时铺几行，已有数据（取下一页）时接在末尾。页码、去重、是否还有更多与失败重试交给 InfiniteList，列表下方的 InfiniteListStatusControl 显示“重试”与“没有更多了”（这里第 3 页第一次会失败）。", Feed),
        new("VirtualList", "horizontal", "横向与跳到指定项", "Orientation = Horizontal 横向排列与滚动（给宽度）。ScrollToIndex、ScrollToOffset 滚动，ScrollAlign 决定停在视口的开头、中间、末尾或就近；Overscan 是可见范围前后多建几项。", Horizontal),
    ];

    const string Surnames = "赵钱孙李周吴郑王冯陈褚卫蒋沈韩杨朱秦尤许何吕施张孔曹严华金魏陶姜";
    static readonly string[] GivenNames = ["小满", "一航", "默", "悠", "晴", "浩然", "思远", "雨桐", "子墨", "若溪", "安"];
    static readonly string[] Departments = ["设计中心", "研发部", "市场部", "财务部", "客服部"];

    sealed record Contact(int Id, string Name, string Department, string Phone);

    static readonly Contact[] Contacts_ = [.. Enumerable.Range(0, 10_000).Select(index => new Contact(
        index, $"{Surnames[index % Surnames.Length]}{GivenNames[index % GivenNames.Length]}", Departments[index % Departments.Length], $"138{index:D8}"))];

    static Padding Padded(Control control) => new(control.LogicalToDeviceUnits(16), control.LogicalToDeviceUnits(8), control.LogicalToDeviceUnits(16), control.LogicalToDeviceUnits(8));

    // 一万条联系人：搜索过滤，没有匹配时显示 Empty
    static Control Contacts()
    {
        var search = new SearchFieldControl { PlaceholderText = "姓名或电话", AccessibleName = "搜索联系人", Delay = TimeSpan.Zero };
        var total = new SmallControl { Tone = TextTone.Muted };
        var empty = new TextControl { Tone = TextTone.Muted };
        var list = new VirtualListControl { EstimateSize = 56, Empty = empty, AccessibleName = "联系人", Dock = DockStyle.Top };
        list.Height = list.LogicalToDeviceUnits(22 * 16);
        list.ItemFactory = item =>
        {
            var contact = (Contact)item!;
            var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md, AccessibleName = contact.Name },
                new AvatarControl { TextSize = TextSize.Sm, DisplayName = contact.Name },
                Build.Flex(new FlexControl { Direction = FlexDirection.Column },
                    new TextControl { Weight = TextWeight.Medium, Text = contact.Name },
                    new SmallControl { Tone = TextTone.Muted, Text = $"{contact.Department} · {contact.Phone}" }));
            row.Padding = Padded(list);
            return row;
        };
        void Filter()
        {
            var keyword = search.Value;
            var found = keyword.Length == 0 ? Contacts_ : [.. Contacts_.Where(contact => contact.Name.Contains(keyword) || contact.Phone.Contains(keyword))];
            list.ItemsSource = found;
            total.Text = $"共 {found.Length:N0} 人";
            empty.Text = $"没有找到“{keyword}”";
        }
        search.ValueChanged += (_, _) => Filter();
        Filter();
        var header = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Dock = DockStyle.Top }, search, total);
        header.SetGrow(search, true);
        header.Padding = Padded(header);
        return Build.Card(new CardControl { CardPadding = Space.None }, Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Top }, header, list));
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
        var list = new VirtualListControl { EstimateSize = 32, AccessibleName = "运行日志", Dock = DockStyle.Top };
        list.Height = list.LogicalToDeviceUnits(22 * 16);
        list.ItemFactory = item =>
        {
            var entry = (LogEntry)item!;
            // 级别定宽，消息才对得齐
            var level = new SmallControl { Tone = ToneOf(entry.Level), Mono = true, Text = entry.Level, AutoSize = false, Width = list.LogicalToDeviceUnits(46) };
            var row = Build.Flex(new FlexControl { Gap = Space.Sm, AccessibleName = $"{entry.Level} {entry.Message}" }, level, new TextControl { TextSize = TextSize.Sm, Text = entry.Message });
            row.Padding = new Padding(list.LogicalToDeviceUnits(16), list.LogicalToDeviceUnits(4), list.LogicalToDeviceUnits(16), list.LogicalToDeviceUnits(4));
            return row;
        };
        var loaded = new SmallControl { Tone = TextTone.Muted };
        void Sync()
        {
            if (!ReferenceEquals(list.ItemsSource, logs.Items)) list.ItemsSource = logs.Items;
            list.IsLoading = logs.IsLoading;
            loaded.Text = $"已加载 {logs.Count} / {page * pages} 条";
        }
        logs.PropertyChanged += (_, _) => Sync();
        list.RangeChanged += (_, range) => logs.OnRangeChanged(range);
        Sync();
        _ = logs.Refresh();
        var footer = Build.Flex(new FlexControl { Dock = DockStyle.Top }, loaded);
        footer.Padding = new Padding(footer.LogicalToDeviceUnits(16), footer.LogicalToDeviceUnits(4), footer.LogicalToDeviceUnits(16), footer.LogicalToDeviceUnits(4));
        return Build.Card(new CardControl { CardPadding = Space.None },
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Top }, list, new InfiniteListStatusControl { List = logs }, footer));
    }

    static readonly string[] Drawings = ["DWG-1021", "DWG-1034", "DWG-1047", "DWG-1052", "DWG-1066", "DWG-1078"];

    // 横向的一千张缩略图：只建看得见的几张，前后各多 2 张；跳到第几张
    static Control Horizontal()
    {
        var list = new VirtualListControl
        {
            ItemsSource = Enumerable.Range(0, 1000).ToList(),
            Orientation = Orientation.Horizontal,
            EstimateSize = 136,
            Overscan = 2,
            AccessibleName = "相册",
            Dock = DockStyle.Top,
        };
        list.Height = list.LogicalToDeviceUnits(136);
        list.ItemFactory = item =>
        {
            var index = (int)item!;
            var side = list.LogicalToDeviceUnits(128);
            var photo = new ImageControl { Source = new Uri($"drawings/{Drawings[index % Drawings.Length]}.png", UriKind.Relative), Alt = $"第 {index + 1} 张", Fit = ImageFit.Cover, Bounds = new Rectangle(0, 0, side, side) };
            // 序号压在图的左下角
            var caption = new SmallControl { TextSize = TextSize.Xs, Text = $"第 {index + 1} 张", BackColor = ProviderControl.ThemeOf(list).Colors.Surface };
            caption.Location = new Point(list.LogicalToDeviceUnits(4), side - caption.PreferredSize.Height - list.LogicalToDeviceUnits(4));
            var cell = new Panel { Size = new Size(side + list.LogicalToDeviceUnits(8), side), AccessibleName = $"第 {index + 1} 张" };
            cell.Controls.Add(caption);
            cell.Controls.Add(photo);
            return cell;
        };
        var target = new NumberFieldControl { Value = 500, Min = 1, Max = 1000, TextSize = TextSize.Sm, AccessibleName = "跳到第几张", Width = list.LogicalToDeviceUnits(120) };
        var jump = new ButtonControl { TextSize = TextSize.Sm, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Text = "跳过去" };
        jump.Click += (_, _) => list.ScrollToIndex((int)(target.Value ?? 1) - 1, ScrollAlign.Start);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Dock = DockStyle.Top },
            list, Build.Flex(new FlexControl { Gap = Space.Sm, Align = FlexAlign.Center, Wrap = true }, target, jump, new SmallControl { Tone = TextTone.Muted, Text = "只建看得见的几张，前后各多 2 张（Overscan）" }));
    }
}
