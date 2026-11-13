using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Menu 的示例（vue-tsx-pro 文档站 overlay/Menu）。</summary>
static class MenuExamples
{
    public static Example[] All =>
    [
        new("Menu", "actions", "操作菜单", "Click、Command 在点击、Enter、空格时执行，随后关闭菜单。Shortcut 显示成 Ctrl+D 这样的写法，窗口里按下就执行这一项；Tone=\"danger\" 标出破坏性操作；MenuSub 是子菜单。", Actions),
        new("Menu", "description", "带说明的菜单项", "Description 是名称下方的一行说明，弱化显示；读屏先念名称、再把说明作为帮助文字念出，不混进名称里。MenuGroup 的 Label 是组标题，也是这一组的名称。", Create),
        new("Menu", "checkable", "复选与单选", "MenuCheckboxItem 勾选后菜单不关，方便连续勾几项；MenuRadioGroup 里选一项后关闭。Label 是这一组的名称，读屏先念组名。", View),
        new("Menu", "account", "账户菜单：链接项", "触发器可以是任何控件（这里是 UserButton）。MenuLinkItem 的 Href 是绝对地址，执行时用系统打开；不可用时方向键跳过它。", Account),
        new("Menu", "zoom", "尺寸与连续操作", "Size 是菜单的字号档位，菜单项、图标、内边距随之缩放；不写时跟随上下文。CloseOnSelect=\"False\" 让执行后不关菜单，适合放大、缩小这类要连按几次的操作。", Zoom),
        new("Menu", "context-menu", "右键菜单", "菜单项与 Menu 完全相同。区域本身不可聚焦：键盘用户聚焦里面的元素后按 Shift+F10 或菜单键，从元素左下角弹出；关闭后焦点回到原处。这里的文件是 RadioGroup 的卡片：方向键换选中的文件，右键先选中再弹菜单。", Files),
    ];

    static readonly string[] Folders = ["设计稿", "会议纪要", "归档 / 2025"];

    static MenuItem Item(string text, IconData icon, Action<string> run, string? shortcut = null)
    {
        var item = new MenuItem { Content = text, Icon = icon, Shortcut = shortcut };
        item.Click += (_, _) => run(text);
        return item;
    }

    static Control Actions()
    {
        var status = new Small { Tone = TextTone.Muted, Text = "点右侧的 … 打开菜单" };
        void Run(string action) => status.Text = $"执行了：{action}";
        var more = new IconButton { Icon = Lucide.Ellipsis };
        AutomationProperties.SetName(more, "更多操作");
        var move = new MenuSub { Label = "移动到", Icon = Lucide.FolderInput };
        foreach (var folder in Folders)
        {
            var item = new MenuItem { Content = folder, Icon = Lucide.Folder };
            item.Click += (_, _) => Run($"移动到\"{folder}\"");
            move.Items.Add(item);
        }
        var remove = Item("删除", Lucide.Trash, Run);
        remove.Tone = MenuItemTone.Danger;
        var menu = new Menu
        {
            Placement = FloatingPlacement.BottomEnd, Trigger = more,
            Items =
            {
                Item("重命名", Lucide.Pencil, Run, "F2"), Item("创建副本", Lucide.Copy, Run, "Ctrl+D"), move, Item("分享", Lucide.Share2, Run),
                new MenuItem { Content = "归档（没有权限）", Icon = Lucide.Archive, IsEnabled = false },
                new Separator { Decorative = false }, remove,
            },
        };
        var info = new Flex { Direction = FlexDirection.Column, Children = { new Strong { Text = "第三季度经营报告.docx" }, new Small { Tone = TextTone.Muted, Text = "周一航 · 昨天 18:20 修改 · 2.4 MB" } } };
        Flex.SetGrow(info, true);
        var row = new Flex { Align = FlexAlign.Center, Gap = Space.Md, Children = { new Icon { Data = Lucide.FileText, Tone = TextTone.Muted, Size = TextSize.Xl }, info, menu } };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Card { Padding = Space.Sm, Content = row }, status } };
    }

    static Control Create()
    {
        var status = new Small { Tone = TextTone.Muted, Text = "说明文字在名称下方，读屏念完名称再念说明" };
        MenuItem Kind(string text, IconData icon, string? description = null)
        {
            var item = new MenuItem { Content = text, Icon = icon, Description = description };
            item.Click += (_, _) => status.Text = $"新建了：{text.TrimEnd('…').Replace("上传文件", "上传的文件")}";
            return item;
        }
        var menu = new Menu
        {
            Trigger = new Button { Icon = Lucide.Plus, Content = "新建" },
            Items =
            {
                new MenuGroup
                {
                    Label = "文档",
                    Items = { Kind("文档", Lucide.FileText, "空白的文字文档"), Kind("表格", Lucide.FileSpreadsheet, "行列、公式与图表"), Kind("演示文稿", Lucide.Presentation, "一页页的幻灯片，可以全屏演示") },
                },
                new Separator { Decorative = false }, Kind("文件夹", Lucide.FolderPlus), Kind("上传文件…", Lucide.Upload),
            },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { menu, status } };
    }

    static readonly (string Key, string Label)[] Sorts = [("name", "名称"), ("modified", "修改时间"), ("size", "大小")];

    static Control View()
    {
        var (grid, hidden, extensions) = (new MenuCheckboxItem { Content = "网格视图", IsChecked = true }, new MenuCheckboxItem { Content = "显示隐藏文件" }, new MenuCheckboxItem { Content = "显示扩展名", IsChecked = true });
        var sort = new MenuRadioGroup { Label = "排序依据", Value = "modified" };
        foreach (var (key, label) in Sorts) sort.Items.Add(new MenuRadioItem { Value = key, Content = label });
        var order = new MenuRadioGroup { Label = "顺序", Value = "desc", Items = { new MenuRadioItem { Value = "asc", Content = "升序" }, new MenuRadioItem { Value = "desc", Content = "降序" } } };
        var sortButton = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.ArrowDownUp };
        var status = new Small { Tone = TextTone.Muted };
        void Sync()
        {
            var sortLabel = Sorts.First(item => item.Key == sort.Value).Label;
            sortButton.Content = $"排序：{sortLabel}";
            status.Text = $"{(grid.IsChecked ? "网格" : "列表")} · {(hidden.IsChecked ? "含" : "不含")}隐藏文件 · {(extensions.IsChecked ? "显示" : "隐藏")}扩展名 · 按{sortLabel}{(order.Value == "asc" ? "升序" : "降序")}";
        }
        foreach (var control in new Control[] { grid, hidden, extensions, sort, order })
            control.PropertyChanged += (_, e) =>
            {
                if (e.Property == MenuCheckboxItem.IsCheckedProperty || e.Property == MenuRadioGroup.ValueProperty) Sync();
            };
        Sync();
        var show = new Menu
        {
            Trigger = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Eye, Content = "显示" },
            Items = { new MenuGroup { Label = "视图", Items = { grid, hidden, extensions } } },
        };
        var sorting = new Menu { Trigger = sortButton, Items = { sort, order } };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { new Flex { Gap = Space.Sm, Children = { show, sorting } }, status } };
    }

    static Control Account()
    {
        var status = new Small { Tone = TextTone.Muted, Text = "链接项的 Href 是绝对地址，执行时用系统打开" };
        static MenuLinkItem Link(string text, IconData icon, string href, bool enabled = true) =>
            new() { Content = text, Icon = icon, Href = new Uri(href), IsEnabled = enabled };
        var signOut = new MenuItem { Content = "退出登录", Icon = Lucide.LogOut, Tone = MenuItemTone.Danger };
        signOut.Click += (_, _) => status.Text = "已退出登录（示例）";
        var menu = new Menu
        {
            Trigger = new UserButton { DisplayName = "林小满" },
            Items =
            {
                new MenuGroup
                {
                    Label = "xiaoman@example.com",
                    Items =
                    {
                        Link("个人资料", Lucide.UserRound, "https://example.com/profile"), Link("外观设置", Lucide.Palette, "https://example.com/theming"),
                        Link("账单（团队版可用）", Lucide.CreditCard, "https://example.com/billing", enabled: false),
                    },
                },
                new Separator { Decorative = false }, Link("帮助文档", Lucide.BookOpen, "https://example.com/docs"), signOut,
            },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { menu, status } };
    }

    static Control Zoom()
    {
        var zoom = 100;
        var button = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, IconEnd = Lucide.ChevronDown, Content = "缩放 100%" };
        void Step(int delta)
        {
            zoom = Math.Clamp(zoom + delta, 25, 400);
            button.Content = $"缩放 {zoom}%";
        }
        var zoomIn = new MenuItem { Content = "放大", Icon = Lucide.ZoomIn, CloseOnSelect = false };
        zoomIn.Click += (_, _) => Step(25);
        var zoomOut = new MenuItem { Content = "缩小", Icon = Lucide.ZoomOut, CloseOnSelect = false };
        zoomOut.Click += (_, _) => Step(-25);
        var actual = new MenuItem { Content = "实际大小", Icon = Lucide.Maximize };
        actual.Click += (_, _) => Step(100 - zoom);
        var menu = new Menu { Size = TextSize.Md, Trigger = button, Items = { zoomIn, zoomOut, new Separator { Decorative = false }, actual } };
        var sizes = new ToggleGroup { IsRequired = true, Value = "md" };
        AutomationProperties.SetName(sizes, "菜单尺寸");
        foreach (var size in new[] { "sm", "md", "lg" }) sizes.Children.Add(new ToggleItem { Value = size, Content = size });
        sizes.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleGroup.ValueProperty && sizes.Value is string size) menu.Size = TextSize.Parse(size);
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start,
            Children =
            {
                new Flex { Gap = Space.Md, Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Children = { sizes, menu } },
                new Small { Tone = TextTone.Muted, Text = "放大、缩小执行后菜单不关，可以连着按；\"实际大小\"执行后关闭" },
            },
        };
    }

    static readonly (string Name, IconData Icon)[] FileList =
    [
        ("报价单.xlsx", Lucide.FileSpreadsheet), ("合同.pdf", Lucide.FileText), ("首页.png", Lucide.FileImage),
        ("会议纪要.docx", Lucide.FileText), ("logo.svg", Lucide.FileImage), ("预算.xlsx", Lucide.FileSpreadsheet),
    ];

    static Control Files()
    {
        var status = new Small { Tone = TextTone.Muted, Text = "在文件上点右键；键盘聚焦文件后按 Shift+F10 或菜单键" };
        var group = new RadioGroup { Card = true, Orientation = Orientation.Horizontal, MinItemWidth = Length.Parse("11rem"), Value = FileList[0].Name };
        AutomationProperties.SetName(group, "文件");
        foreach (var (name, icon) in FileList)
        {
            var radio = new Radio { Value = name, Content = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = icon, Tone = TextTone.Muted, Size = TextSize.Xl }, new Text { Text = name } } } };
            // 右键先选中再弹菜单
            radio.ContextRequested += (_, _) => group.Value = name;
            group.Items.Add(radio);
        }
        void Run(string action) => status.Text = $"{action}\"{group.Value}\"";
        var remove = Item("删除", Lucide.Trash, Run);
        remove.Tone = MenuItemTone.Danger;
        var region = new ContextMenu
        {
            Content = group,
            Items = { Item("下载", Lucide.Download, Run), Item("重命名", Lucide.Pencil, Run, "F2"), Item("复制", Lucide.Copy, Run), new Separator { Decorative = false }, remove },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { region, status } };
    }
}
