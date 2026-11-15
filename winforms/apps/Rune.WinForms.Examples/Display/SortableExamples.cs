using System.Collections.ObjectModel;

namespace Rune.WinForms.Examples;

/// <summary>SortableControl 的示例（vue-tsx-pro 文档站 display/Sortable）。</summary>
static class SortableExamples
{
    public static Example[] All =>
    [
        new("Sortable", "tasks", "待办排序", "ItemsSource 是可改的列表（ObservableCollection），拖完就地改好顺序并发 Reordered。键盘：Tab 到把手，空格拿起，↑ ↓ 移动，空格放下，Esc 取消；读屏用 GetLabel 的文字播报“移到第几位”。Enabled = false 时把手不可用，列表照常显示。", Tasks),
        new("Sortable", "columns", "横排：调整列顺序", "Orientation = Horizontal 横排，键盘用 ← →。项多时放进 ScrollAreaControl（横向），拖到边缘会自动滚动。", Columns),
    ];

    sealed record Todo(string Id, string Title, StatusTone Tone, string Priority);

    // 今日待办：拖动或键盘调整顺序，锁定后不能动
    static Control Tasks()
    {
        var tasks = new ObservableCollection<Todo>
        {
            new("review", "评审首页改版设计稿", StatusTone.Danger, "紧急"),
            new("invoice", "处理 9 月发票报销", StatusTone.Warning, "重要"),
            new("interview", "面试前端候选人", StatusTone.Warning, "重要"),
            new("docs", "补充接口文档", StatusTone.Info, "一般"),
            new("weekly", "写周报", StatusTone.Info, "一般"),
        };
        var locked = new SwitchControl { Text = "锁定顺序" };
        var list = new SortableControl
        {
            ItemsSource = tasks,
            GetKey = task => ((Todo)task).Id,
            GetLabel = task => ((Todo)task).Title,
            ItemFactory = item =>
            {
                var task = (Todo)item;
                var row = Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm },
                    new TextControl { Text = task.Title }, new BadgeControl { Text = task.Priority, Tone = task.Tone, Variant = StatusVariant.Soft });
                return row;
            },
            AccessibleName = "今日待办",
            Dock = DockStyle.Top,
        };
        var order = new SmallControl { Tone = TextTone.Muted };
        void Sync() => order.Text = $"顺序：{string.Join(" → ", tasks.Select(task => task.Id))}";
        tasks.CollectionChanged += (_, _) => Sync();
        locked.IsCheckedChanged += (_, _) => list.Enabled = !locked.IsChecked;
        Sync();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Dock = DockStyle.Top }, locked, list, order);
    }

    sealed record Column(string Key, string Label, IconData Icon);

    // 表格列的顺序：横排，放不下时横向滚动
    static Control Columns()
    {
        var columns = new ObservableCollection<Column>
        {
            new("id", "编号", Lucide.Hash),
            new("name", "姓名", Lucide.User),
            new("phone", "电话", Lucide.Phone),
            new("email", "邮箱", Lucide.Mail),
            new("joined", "入职日期", Lucide.CalendarDays),
        };
        var list = new SortableControl
        {
            ItemsSource = columns,
            Orientation = Orientation.Horizontal,
            GetKey = column => ((Column)column).Key,
            GetLabel = column => ((Column)column).Label,
            ItemFactory = item =>
            {
                var column = (Column)item;
                return Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs }, new IconControl { Data = column.Icon }, new TextControl { Text = column.Label });
            },
            AccessibleName = "表格列的顺序",
        };
        var header = new SmallControl { Tone = TextTone.Muted };
        void Sync() => header.Text = $"表头：{string.Join(" | ", columns.Select(column => column.Label))}";
        columns.CollectionChanged += (_, _) => Sync();
        Sync();
        // AutoSize：高同内容，放不下时横向滚动
        var scroll = new ScrollAreaControl { Axis = ScrollAreaAxis.X, Content = list, AutoSize = true };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Dock = DockStyle.Top }, scroll, header);
    }
}
