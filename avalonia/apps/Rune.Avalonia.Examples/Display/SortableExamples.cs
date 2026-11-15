using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Sortable 的示例（vue-tsx-pro 文档站 display/Sortable）。</summary>
static class SortableExamples
{
    public static Example[] All =>
    [
        new("Sortable", "tasks", "待办排序", "ItemsSource 是可改的列表（ObservableCollection），拖完就地改好顺序并发 Reordered。键盘：Tab 到把手，空格拿起，↑ ↓ 移动，空格放下，Esc 取消；读屏用 GetLabel 的文字播报“移到第几位”。IsEnabled = false 时把手不可用，列表照常显示。", Tasks),
        new("Sortable", "columns", "横排：调整列顺序", "Orientation = Horizontal 横排，键盘用 ← →。项多时在外面包 ScrollArea（横向），拖到边缘会自动滚动。", Columns),
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
        var locked = new Switch { Content = "锁定顺序" };
        var list = new Sortable
        {
            ItemsSource = tasks,
            GetKey = task => ((Todo)task).Id,
            GetLabel = task => ((Todo)task).Title,
            ItemTemplate = new FuncDataTemplate<Todo>((task, _) => new Flex
            {
                Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm,
                Children = { new Text { Text = task.Title }, new Badge { Tone = task.Tone, Variant = StatusVariant.Soft, Content = task.Priority } },
            }),
        };
        AutomationProperties.SetName(list, "今日待办");
        var order = new Small { Tone = TextTone.Muted };
        void Sync() => order.Text = $"顺序：{string.Join(" → ", tasks.Select(task => task.Id))}";
        tasks.CollectionChanged += (_, _) => Sync();
        locked.PropertyChanged += (_, e) => { if (e.Property == Switch.IsCheckedProperty) list.IsEnabled = locked.IsChecked != true; };
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Children = { locked, list, order } };
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
        var list = new Sortable
        {
            ItemsSource = columns,
            Orientation = Orientation.Horizontal,
            GetKey = column => ((Column)column).Key,
            GetLabel = column => ((Column)column).Label,
            ItemTemplate = new FuncDataTemplate<Column>((column, _) => new Flex
            {
                Align = FlexAlign.Center, Gap = Space.Xs,
                Children = { new Icon { Data = column.Icon }, new Text { Text = column.Label } },
            }),
        };
        AutomationProperties.SetName(list, "表格列的顺序");
        var header = new Small { Tone = TextTone.Muted };
        void Sync() => header.Text = $"表头：{string.Join(" | ", columns.Select(column => column.Label))}";
        columns.CollectionChanged += (_, _) => Sync();
        Sync();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
            Children = { new ScrollArea { Axis = ScrollAreaAxis.X, Content = list }, header },
        };
    }
}
