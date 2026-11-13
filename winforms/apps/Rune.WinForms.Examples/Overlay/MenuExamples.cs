namespace Rune.WinForms.Examples;

/// <summary>Menu 的示例（vue-tsx-pro 文档站 overlay/Menu）。</summary>
static class MenuExamples
{
    public static Example[] All =>
    [
        new("Menu", "actions", "操作菜单", "Click 在点击、Enter、空格时执行，随后关闭菜单。Shortcut 显示成 Ctrl+D 这样的写法，窗体里按下就执行这一项；Tone = danger 标出破坏性操作；MenuSubControl 是子菜单。", Actions),
        new("Menu", "description", "带说明的菜单项", "Description 是名称下方的一行说明，弱化显示；读屏先念名称、再把说明作为描述念出，不混进名称里。MenuGroupControl 的 Label 是组标题，也是这一组的名称。", Create),
        new("Menu", "checkable", "复选与单选", "MenuCheckboxItemControl 勾选后菜单不关，方便连续勾几项；MenuRadioGroupControl 里选一项后关闭。Label 是这一组的名称，读屏先念组名。", View),
        new("Menu", "account", "账户菜单：链接项", "触发器可以是任何控件（这里是 UserButtonControl）。MenuLinkItemControl 的 Href 是绝对地址，执行时用系统打开；不可用时方向键跳过它。", Account),
        new("Menu", "zoom", "尺寸与连续操作", "TextSize 是菜单的字号档位，菜单项、图标、内边距随之缩放；不写时跟着菜单所在的窗体。CloseOnSelect = false 让执行后不关菜单，适合放大、缩小这类要连按几次的操作。", Zoom),
        new("Menu", "context-menu", "右键菜单", "菜单项与 MenuControl 完全相同。区域本身不可聚焦：键盘用户聚焦里面的控件后按 Shift+F10 或菜单键，从控件下方弹出；关闭后焦点回到原处。这里的文件是 RadioGroupControl 的卡片：方向键换选中的文件。", Files),
    ];

    static readonly string[] Folders = ["设计稿", "会议纪要", "归档 / 2025"];

    static MenuItemControl Item(string text, IconData icon, Action<string> run, string? shortcut = null)
    {
        var item = new MenuItemControl { Text = text, Icon = icon, Shortcut = shortcut };
        item.Click += (_, _) => run(text);
        return item;
    }

    static FlexControl Column(params Control[] children) =>
        Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, AutoSize = true, Dock = DockStyle.Top }, children);

    static Control Actions()
    {
        var status = new SmallControl { Tone = TextTone.Muted, Text = "点右侧的 … 打开菜单" };
        void Run(string action) => status.Text = $"执行了：{action}";
        var move = new MenuSubControl { Text = "移动到", Icon = Lucide.FolderInput };
        foreach (var folder in Folders)
        {
            var item = new MenuItemControl { Text = folder, Icon = Lucide.Folder };
            item.Click += (_, _) => Run($"移动到\"{folder}\"");
            move.Items.Add(item);
        }
        var remove = Item("删除", Lucide.Trash, Run);
        remove.Tone = MenuItemTone.Danger;
        var menu = new MenuControl { Placement = FloatingPlacement.BottomEnd, Trigger = new IconButtonControl { Icon = Lucide.Ellipsis, AccessibleName = "更多操作" } };
        menu.Items.AddRange([
            Item("重命名", Lucide.Pencil, Run, "F2"), Item("创建副本", Lucide.Copy, Run, "Ctrl+D"), move, Item("分享", Lucide.Share2, Run),
            new MenuItemControl { Text = "归档（没有权限）", Icon = Lucide.Archive, Enabled = false },
            new SeparatorControl { Decorative = false }, remove,
        ]);
        var info = Build.Flex(new FlexControl { Direction = FlexDirection.Column, AutoSize = true },
            new StrongControl { Text = "第三季度经营报告.docx" }, new SmallControl { Tone = TextTone.Muted, Text = "周一航 · 昨天 18:20 修改 · 2.4 MB" });
        var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md, AutoSize = true, Dock = DockStyle.Top },
            new IconControl { Data = Lucide.FileText, Tone = TextTone.Muted, IconSize = TextSize.Xl }, info, menu);
        row.SetGrow(info, true);
        var column = Column(Build.Card(new CardControl { CardPadding = Space.Sm, Dock = DockStyle.Top }, row), status);
        column.Align = FlexAlign.Stretch;
        return column;
    }

    static Control Create()
    {
        var status = new SmallControl { Tone = TextTone.Muted, Text = "说明文字在名称下方，读屏念完名称再念说明" };
        MenuItemControl Kind(string text, IconData icon, string? description = null)
        {
            var item = new MenuItemControl { Text = text, Icon = icon, Description = description };
            item.Click += (_, _) => status.Text = $"新建了：{text.TrimEnd('…').Replace("上传文件", "上传的文件")}";
            return item;
        }
        var documents = new MenuGroupControl { Label = "文档" };
        documents.Controls.AddRange([Kind("文档", Lucide.FileText, "空白的文字文档"), Kind("表格", Lucide.FileSpreadsheet, "行列、公式与图表"), Kind("演示文稿", Lucide.Presentation, "一页页的幻灯片，可以全屏演示")]);
        var menu = new MenuControl { Trigger = new ButtonControl { Icon = Lucide.Plus, Text = "新建" } };
        menu.Items.AddRange([documents, new SeparatorControl { Decorative = false }, Kind("文件夹", Lucide.FolderPlus), Kind("上传文件…", Lucide.Upload)]);
        return Column(menu, status);
    }

    static readonly (string Key, string Label)[] Sorts = [("name", "名称"), ("modified", "修改时间"), ("size", "大小")];

    static Control View()
    {
        var (grid, hidden, extensions) = (new MenuCheckboxItemControl { Text = "网格视图", IsChecked = true }, new MenuCheckboxItemControl { Text = "显示隐藏文件" }, new MenuCheckboxItemControl { Text = "显示扩展名", IsChecked = true });
        var view = new MenuGroupControl { Label = "视图" };
        view.Controls.AddRange([grid, hidden, extensions]);
        var sort = new MenuRadioGroupControl { Label = "排序依据", Value = "modified" };
        foreach (var (key, label) in Sorts) sort.Controls.Add(new MenuRadioItemControl { Value = key, Text = label });
        var order = new MenuRadioGroupControl { Label = "顺序", Value = "desc" };
        order.Controls.AddRange([new MenuRadioItemControl { Value = "asc", Text = "升序" }, new MenuRadioItemControl { Value = "desc", Text = "降序" }]);
        var sortButton = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.ArrowDownUp };
        var status = new SmallControl { Tone = TextTone.Muted };
        void Sync()
        {
            var sortLabel = Sorts.First(item => item.Key == sort.Value).Label;
            sortButton.Text = $"排序：{sortLabel}";
            status.Text = $"{(grid.IsChecked ? "网格" : "列表")} · {(hidden.IsChecked ? "含" : "不含")}隐藏文件 · {(extensions.IsChecked ? "显示" : "隐藏")}扩展名 · 按{sortLabel}{(order.Value == "asc" ? "升序" : "降序")}";
        }
        foreach (var item in new[] { grid, hidden, extensions }) item.IsCheckedChanged += (_, _) => Sync();
        sort.ValueChanged += (_, _) => Sync();
        order.ValueChanged += (_, _) => Sync();
        Sync();
        var show = new MenuControl { Trigger = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Eye, Text = "显示" } };
        show.Items.Add(view);
        var sorting = new MenuControl { Trigger = sortButton };
        sorting.Items.AddRange([sort, order]);
        return Column(Build.Flex(new FlexControl { Gap = Space.Sm, AutoSize = true }, show, sorting), status);
    }

    static Control Account()
    {
        var status = new SmallControl { Tone = TextTone.Muted, Text = "链接项的 Href 是绝对地址，执行时用系统打开" };
        static MenuLinkItemControl Link(string text, IconData icon, string href, bool enabled = true) =>
            new() { Text = text, Icon = icon, Href = new Uri(href), Enabled = enabled };
        var signOut = new MenuItemControl { Text = "退出登录", Icon = Lucide.LogOut, Tone = MenuItemTone.Danger };
        signOut.Click += (_, _) => status.Text = "已退出登录（示例）";
        var profile = new MenuGroupControl { Label = "xiaoman@example.com" };
        profile.Controls.AddRange([
            Link("个人资料", Lucide.UserRound, "https://example.com/profile"), Link("外观设置", Lucide.Palette, "https://example.com/theming"),
            Link("账单（团队版可用）", Lucide.CreditCard, "https://example.com/billing", enabled: false),
        ]);
        var menu = new MenuControl { Trigger = new UserButtonControl { DisplayName = "林小满" } };
        menu.Items.AddRange([profile, new SeparatorControl { Decorative = false }, Link("帮助文档", Lucide.BookOpen, "https://example.com/docs"), signOut]);
        return Column(menu, status);
    }

    static Control Zoom()
    {
        var zoom = 100;
        var button = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, IconEnd = Lucide.ChevronDown, Text = "缩放 100%" };
        void Step(int delta)
        {
            zoom = Math.Clamp(zoom + delta, 25, 400);
            button.Text = $"缩放 {zoom}%";
        }
        var zoomIn = new MenuItemControl { Text = "放大", Icon = Lucide.ZoomIn, CloseOnSelect = false };
        zoomIn.Click += (_, _) => Step(25);
        var zoomOut = new MenuItemControl { Text = "缩小", Icon = Lucide.ZoomOut, CloseOnSelect = false };
        zoomOut.Click += (_, _) => Step(-25);
        var actual = new MenuItemControl { Text = "实际大小", Icon = Lucide.Maximize };
        actual.Click += (_, _) => Step(100 - zoom);
        var menu = new MenuControl { TextSize = TextSize.Md, Trigger = button };
        menu.Items.AddRange([zoomIn, zoomOut, new SeparatorControl { Decorative = false }, actual]);
        var sizes = new ToggleGroupControl { IsRequired = true, Value = "md", AccessibleName = "菜单尺寸" };
        foreach (var size in new[] { "sm", "md", "lg" }) sizes.Controls.Add(new ToggleItemControl { Value = size, Text = size });
        sizes.ValueChanged += (_, _) =>
        {
            if (sizes.Value is string size) menu.TextSize = TextSize.Parse(size);
        };
        return Column(Build.Flex(new FlexControl { Gap = Space.Md, Align = FlexAlign.Center, AutoSize = true }, sizes, menu),
            new SmallControl { Tone = TextTone.Muted, Text = "放大、缩小执行后菜单不关，可以连着按；\"实际大小\"执行后关闭" });
    }

    static readonly string[] FileNames = ["报价单.xlsx", "合同.pdf", "首页.png", "会议纪要.docx", "logo.svg", "预算.xlsx"];

    static Control Files()
    {
        var status = new SmallControl { Tone = TextTone.Muted, Text = "在文件上点右键；键盘聚焦文件后按 Shift+F10 或菜单键" };
        var group = new RadioGroupControl { Card = true, Orientation = Orientation.Horizontal, MinItemWidth = "11rem", Value = FileNames[0], AccessibleName = "文件", AutoSize = true };
        foreach (var name in FileNames)
        {
            var radio = new RadioControl { Value = name, Text = name };
            // 右键先选中再弹菜单
            radio.MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Right) group.Value = name;
            };
            group.Controls.Add(radio);
        }
        void Run(string action) => status.Text = $"{action}\"{group.Value}\"";
        var remove = Item("删除", Lucide.Trash, Run);
        remove.Tone = MenuItemTone.Danger;
        var region = new ContextMenuControl { Content = group, Dock = DockStyle.Top, Height = 120 };
        region.Items.AddRange([Item("下载", Lucide.Download, Run), Item("重命名", Lucide.Pencil, Run, "F2"), Item("复制", Lucide.Copy, Run), new SeparatorControl { Decorative = false }, remove]);
        var column = Column(region, status);
        column.Align = FlexAlign.Stretch;
        return column;
    }
}
