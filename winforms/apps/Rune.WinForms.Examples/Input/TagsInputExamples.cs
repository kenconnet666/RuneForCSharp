namespace Rune.WinForms.Examples;

/// <summary>TagsInput 页的示例（vue-tsx-pro 文档站 input/TagsInput）。</summary>
static class TagsInputExamples
{
    public static Example[] All =>
    [
        new("TagsInput", "basic", "输入与建议", "回车、逗号或分号加一个标签（Delimiters 可以改），粘贴时自动拆开，重复的不加；输入框空着时退格删掉最后一个。给了 ItemsSource 就有建议列表；AllowCustomValue=false 只能从建议里选。", Basic),
        new("TagsInput", "states", "上限、图标与状态", "Max 限制个数，满了以后不再接受新的标签。Icon、Variant、TextSize、Radius 与其他输入框同一套；只读时标签不能移除。失焦时输入框里没确认的文字默认加成标签，AcceptOnBlur=false 时留在框里。", States),
        new("TagsInput", "remote", "异步建议", "Text 是输入框的文字（上游的 inputValue），按 TextChanged 请求建议；LocalFilter=false 不再本地过滤，IsLoading 时建议列表显示加载中。", Remote),
    ];

    static readonly ListboxOption[] Team =
    [
        new("yihang@example.com", "周一航") { Description = "yihang@example.com" },
        new("xiaoman@example.com", "林小满") { Description = "xiaoman@example.com" },
        new("chenmo@example.com", "陈默") { Description = "chenmo@example.com" },
        new("xuyou@example.com", "许悠") { Description = "xuyou@example.com" },
    ];

    static Control Basic() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch },
        new FieldControl { Label = "关键词", Description = "回车或输入逗号加一个；粘贴时按逗号与换行拆开；最多 6 个", Content = new TagsInputControl { Values = ["组件库", "Vue", "TSX"], Max = 6, Clearable = true } },
        new FieldControl { Label = "抄送", Description = "只能从团队成员里选", Content = new TagsInputControl { Values = ["xiaoman@example.com"], ItemsSource = Team, AllowCustomValue = false } });

    static Control States()
    {
        var topics = new TagsInputControl { Values = ["vue", "tsx"], Max = 3, Icon = Lucide.Hash, Clearable = true, PlaceholderText = "输入后回车" };
        var topicsField = new FieldControl { Label = "话题", Content = topics };
        void Count() => topicsField.Description = $"最多 3 个，已有 {topics.Values.Count} 个";
        topics.ValuesChanged += (_, _) => Count();
        Count();
        var keywords = new TagsInputControl { Variant = ControlVariant.Soft, PlaceholderText = "逗号或分号分隔" };
        var keywordsField = new FieldControl { Label = "关键词", IsRequired = true, Error = "至少填一个关键词", Content = keywords };
        keywords.ValuesChanged += (_, _) => keywordsField.Error = keywords.Values.Count > 0 ? null : "至少填一个关键词";
        return Build.Grid(new GridControl { MinItemWidth = "16rem", Gap = Space.Lg },
            topicsField,
            keywordsField,
            new FieldControl { Label = "收件人", Description = "回车才算一个；失焦时没确认的文字留在框里", Content = new TagsInputControl { Values = ["lin@company.cn"], AcceptOnBlur = false, Radius = Radius.Full, PlaceholderText = "邮箱，回车确认" } },
            new FieldControl { Label = "只读", IsReadOnly = true, Content = new TagsInputControl { Values = ["设计", "前端"] } },
            new FieldControl { Label = "不可用", Enabled = false, Content = new TagsInputControl { Values = ["设计", "前端"], TextSize = TextSize.Sm } });
    }

    static readonly string[] Topics = ["前端工程化", "前端性能", "组件设计", "设计系统", "无障碍", "国际化", "可视化", "测试", "构建工具", "状态管理"];

    static Control Remote()
    {
        var input = new TagsInputControl { Values = ["组件设计"], LocalFilter = false, Max = 5 };
        // 模拟接口：输入停下 500 毫秒后按关键词返回；只采用最后一次
        var version = 0;
        input.TextChanged += async (_, _) =>
        {
            var text = input.Text.Trim();
            var mine = ++version;
            if (text.Length == 0)
            {
                input.IsLoading = false;
                input.ItemsSource = Array.Empty<string>();
                return;
            }
            input.IsLoading = true;
            await Task.Delay(500);
            if (mine != version || input.IsDisposed) return;
            input.ItemsSource = Topics.Where(topic => topic.Contains(text)).ToArray();
            input.IsLoading = false;
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch },
            new FieldControl { Label = "话题", Description = "输入“前端”“设计”试试", Content = input });
    }
}
