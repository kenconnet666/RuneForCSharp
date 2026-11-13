namespace Rune.WinForms.Examples;

/// <summary>ItemList 的示例（vue-tsx-pro 文档站 display/ItemList）。</summary>
static class ItemListExamples
{
    public static Example[] All =>
    [
        new("ItemList", "todos", "待办", "待办、通知这类界面列表：起始边色点（StatusDotControl），说明是编号与流程，结束边是时限（DeadlineBadgeControl）。条目的标题是文字，链接写成整条的 Href。", Todos),
        new("ItemList", "notifications", "通知列表：整条可点", "整条可点，点哪里都打开（桌面上应用里的跳转用 Invoked，这里点开就标为已读）；结束边的\"标为已读\"是独立的按钮，照常可点。", Notifications),
        new("ItemList", "selectable", "可选中的行", "主从视图左边的列表：点一条在右边看详情；正在看的一条是选中色，读屏里是选中的。不可用的一条变淡、点不了。", Selectable),
    ];

    static readonly DateOnly TodosToday = new(2026, 10, 21);

    static readonly (string Node, string Code, DateOnly Due, bool Returned)[] TodoItems =
    [
        ("资料审核", "PO-2026-0187", new(2026, 10, 23), false),
        ("方案设计", "PO-2026-0214", new(2026, 10, 16), true),
        ("财务复核", "PO-2026-0233", new(2026, 11, 4), false),
    ];

    static Control Todos()
    {
        var list = new ItemListControl { Divided = true, Hoverable = true, Dock = DockStyle.Top };
        foreach (var todo in TodoItems)
            list.Controls.Add(new ItemListItemControl
            {
                Align = ItemListAlign.Start,
                Start = new StatusDotControl { Tone = todo.Returned ? StatusTone.Warning : StatusTone.Accent },
                Text = todo.Node, Href = new Uri($"https://example.com/todos/{todo.Code}"),
                Description = todo.Code + (todo.Returned ? " · 被退回" : ""),
                End = new DeadlineBadgeControl { Due = todo.Due, Today = TodosToday },
            });
        return Build.Card(new CardControl
        {
            Variant = CardVariant.Outline, CardPadding = Space.Sm, Header = "我的待办", Dock = DockStyle.Top,
            Extra = new LinkControl { Text = "打开收件箱", Href = new Uri("https://example.com/inbox") },
        }, list);
    }

    sealed class Notice(string title, string from, string time, bool read)
    {
        public string Title { get; } = title;
        public string From { get; } = from;
        public string Time { get; } = time;
        public bool Read { get; set; } = read;
    }

    static Control Notifications()
    {
        Notice[] notices =
        [
            new("新的待办：资料审核", "PO-2026-0187 · 办公椅供应商切换", "09:42", false),
            new("被退回：方案设计", "验收报告缺少试验报告", "08:10", false),
            new("已通过：财务复核", "PO-2026-0233 · 显示器支架", "昨天", true),
        ];
        var list = new ItemListControl { Divided = true, Inset = Space.Sm, Dock = DockStyle.Top };
        foreach (var notice in notices)
        {
            var dot = new StatusDotControl();
            var markRead = new IconButtonControl { Icon = Lucide.Check, TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost, AccessibleName = $"把\"{notice.Title}\"标为已读" };
            var item = new ItemListItemControl
            {
                Align = ItemListAlign.Start, Start = dot, Text = notice.Title, Description = notice.From,
                End = Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs, AutoSize = true }, new SmallControl { Text = notice.Time, Tone = TextTone.Muted }, markRead),
            };
            void Sync()
            {
                dot.Tone = notice.Read ? StatusTone.Neutral : StatusTone.Accent;
                dot.AccessibleName = notice.Read ? null : "未读";
                markRead.Visible = !notice.Read;
            }
            void Read()
            {
                notice.Read = true;
                Sync();
            }
            markRead.Click += (_, _) => Read();
            item.Invoked += (_, _) => Read();
            Sync();
            list.Controls.Add(item);
        }
        return Build.Card(new CardControl { CardPadding = Space.Xs, Dock = DockStyle.Top }, list);
    }

    static Control Selectable()
    {
        (string Id, string Name, IconData Icon, int Count, string Note, bool Disabled)[] folders =
        [
            ("inbox", "收件箱", Lucide.Inbox, 12, "12 封未读", false),
            ("projects", "项目", Lucide.FolderKanban, 3, "3 个进行中", false),
            ("sent", "已发送", Lucide.Send, 0, "最近一封在 10:42", false),
            ("trash", "回收站", Lucide.Trash, 0, "30 天后自动清空", true),
        ];
        var name = new TextControl { Weight = TextWeight.Semibold };
        var note = new SmallControl { Tone = TextTone.Muted };
        var list = new ItemListControl { Inset = Space.Sm, AccessibleName = "文件夹", Dock = DockStyle.Top };
        var items = new List<ItemListItemControl>();
        void Show(string id)
        {
            var folder = folders.First(folder => folder.Id == id);
            (name.Text, note.Text) = (folder.Name, folder.Note);
            for (var i = 0; i < folders.Length; i++) items[i].IsSelected = folders[i].Id == id;
        }
        foreach (var folder in folders)
        {
            var item = new ItemListItemControl
            {
                Start = new IconControl { Data = folder.Icon, Tone = folder.Disabled ? TextTone.Inherit : TextTone.Muted }, Text = folder.Name, Enabled = !folder.Disabled,
                End = folder.Count > 0 ? new BadgeControl { Radius = Radius.Full, Text = folder.Count.ToString() } : null,
            };
            item.Invoked += (_, _) => Show(folder.Id);
            items.Add(item);
            list.Controls.Add(item);
        }
        Show("inbox");
        var master = Build.Card(new CardControl { CardPadding = Space.Xs, Width = 14 * 16, MinimumSize = new Size(14 * 16, 0) }, list);
        var detail = Build.Card(new CardControl { Width = 16 * 16, MinimumSize = new Size(16 * 16, 0) },
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true, Dock = DockStyle.Top }, name, note));
        var row = Build.Flex(new FlexControl { Gap = Space.Md, Align = FlexAlign.Stretch, Dock = DockStyle.Top, AutoSize = true }, master, detail);
        row.SetGrow(master, true);
        row.SetGrow(detail, true);
        return row;
    }
}
