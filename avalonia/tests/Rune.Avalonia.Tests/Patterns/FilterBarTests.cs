using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>FilterBar：回车与"查询"发 SearchRequested；More 收起时不显示，展开按钮带展开状态与已填几项；高级筛选展开条件构造器、收起时写摘要与"清空"、重置时条件清成 null。</summary>
public class FilterBarTests
{
    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static string TextOf(Button button) => button.Content switch
    {
        string text => text,
        Flex flex => string.Concat(flex.GetLogicalDescendants().OfType<TextBlock>().Select(part => part.Text)),
        _ => "",
    };

    [Fact]
    [Upstream("FilterBar", "提交（回车、点查询）调用 onSearch；重置调用 onReset；条件的标签默认在起始边")]
    public Task Search_reset_and_label_placement() => HeadlessUi.Run(() =>
    {
        var input = new TextField();
        var field = new Field { Label = "项目名称", Content = input };
        var bar = new FilterBar { Fields = { field } };
        int searched = 0, resetted = 0;
        bar.SearchRequested += (_, _) => searched++;
        bar.ResetRequested += (_, _) => resetted++;
        HeadlessUi.Show(bar);
        Assert.Equal(AutomationLandmarkType.Search, AutomationProperties.GetLandmarkType(bar));
        input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = input });
        Click(bar.Part<Button>(FilterBarParts.Search));
        Assert.Equal(2, searched);
        Click(bar.Part<Button>(FilterBarParts.Reset));
        Assert.Equal(1, resetted);
        Assert.Equal(FieldLabelPlacement.Start, field.LabelPlacement);
    });

    [Fact]
    [Upstream("FilterBar", "more 默认收起、不渲染；展开按钮显示收起的条件里填了几项，展开后关联到条件")]
    public Task More_is_collapsed_until_expanded() => HeadlessUi.Run(() =>
    {
        var extra = new Text { Text = "完成年度" };
        // 第一排放得下两个条件：最后的完成年度收进"展开"（宽的时候全部放得下，不出"展开"）
        var bar = new FilterBar { Width = 700, [TextElement.FontSizeProperty] = 14d, MoreActive = 2, Fields = { new Text { Text = "名称" } }, More = { new Text { Text = "责任团队" }, new Text { Text = "状态" }, extra } };
        var window = HeadlessUi.Show(bar);
        var toggle = bar.Part<Button>(FilterBarParts.Toggle);
        Assert.True(toggle.IsVisible);
        Assert.False(extra.IsEffectivelyVisible);
        Assert.Equal(false, toggle.GetValue(Popover.TriggerExpandedProperty));
        Assert.Contains("已设 2 项", TextOf(toggle));
        Click(toggle);
        HeadlessUi.Flush(window);
        Assert.True(extra.IsEffectivelyVisible);
        Assert.True(bar.IsExpanded);
        Assert.Equal(true, toggle.GetValue(Popover.TriggerExpandedProperty));
        Assert.Equal("收起", TextOf(toggle));
    });

    [Fact]
    [Upstream("FilterBar", "高级筛选：按钮旁是比较的条数，展开后是条件构造器；收起时写成一句话、可清空；重置时清成 null")]
    public Task Advanced_filter_summary_clear_and_reset() => HeadlessUi.Run(() =>
    {
        ConditionField[] fields = [new("amount", "金额", ConditionFieldType.Number)];
        var bar = new FilterBar
        {
            ConditionFields = fields,
            Condition = Condition.Any(new ConditionRule("amount", ConditionOp.Gt, 5), new ConditionRule("amount", ConditionOp.Lt, 1)),
        };
        var resetted = 0;
        bar.ResetRequested += (_, _) => resetted++;
        var window = HeadlessUi.Show(bar);
        var toggle = bar.Part<Button>(FilterBarParts.AdvancedToggle);
        Assert.Equal("高级筛选已设 2 项", TextOf(toggle));
        var summary = bar.Part<Flex>(FilterBarParts.Summary);
        Assert.True(summary.IsVisible);
        Assert.Equal("高级筛选：金额 大于 5，或 金额 小于 1", summary.GetLogicalDescendants().OfType<Small>().Single().Text);
        Click(toggle);
        HeadlessUi.Flush(window);
        Assert.Equal(true, toggle.GetValue(Popover.TriggerExpandedProperty));
        Assert.True(bar.Builder.IsEffectivelyVisible);
        Assert.Equal("高级筛选", AutomationProperties.GetName(bar.Builder));
        Assert.Equal(2, bar.Builder.GetLogicalDescendants().OfType<Control>().Count(part => part.Name == ConditionBuilderParts.Rule));
        Click(toggle);
        HeadlessUi.Flush(window);
        Click(bar.Part<Button>(FilterBarParts.ClearAdvanced));
        Assert.Null(bar.Condition);
        bar.Condition = Condition.All(new ConditionRule("amount", ConditionOp.Exists));
        Click(bar.Part<Button>(FilterBarParts.Reset));
        Assert.Null(bar.Condition);
        Assert.Equal(1, resetted);
    });

    [Fact]
    public Task Everything_fits_without_a_toggle() => HeadlessUi.Run(() =>
    {
        var extra = new Text { Text = "完成年度" };
        var bar = new FilterBar { Width = 1200, Fields = { new Text { Text = "名称" } }, More = { extra } };
        HeadlessUi.Show(bar);
        Assert.False(bar.Part<Button>(FilterBarParts.Toggle).IsVisible);
        Assert.True(extra.IsEffectivelyVisible);
    });

    [Fact]
    public void Layout_counts_fields_beside_the_actions()
    {
        // 整排 1000，间距 24，每格至少 200，按钮区带"展开"300（"展开"占 100）：不带"展开"时放得下 3 格，4 个条件放不下；带上"展开"仍放 3 格
        var layout = FilterBar.Layout(1000, 24, 200, 300, 100, 4);
        Assert.True(layout.Toggle);
        Assert.Equal(3, layout.Shown);
        Assert.Equal((1000 - 300 - 24 - 2 * 24) / 3.0, layout.Cell);
        // 按钮区更宽（带"展开"500）：只放 2 格
        Assert.Equal(2, FilterBar.Layout(1000, 24, 200, 500, 100, 4).Shown);
        // 3 个条件全部放得下：不出"展开"
        Assert.False(FilterBar.Layout(1000, 24, 200, 300, 100, 3).Toggle);
        // 窄到一个也放不下：条件独占一排
        Assert.Null(FilterBar.Layout(300, 24, 200, 250, 100, 2).First);
    }

    // 以下同上游的 FilterBar.browser：标题、条件、按钮的位置（headless 里排版是真的）
    static (FilterBar Bar, Window Window) Layout(double width, string[]? more = null, string[]? common = null)
    {
        // 同文档站的正文字号 14px（headless 默认 12px）
        var bar = new FilterBar { Width = width, [TextElement.FontSizeProperty] = 14d, Start = new Text { Text = "采购单", Weight = TextWeight.Semibold }, End = new Button { Variant = ButtonVariant.Outline, Content = "新建" } };
        foreach (var name in common ?? ["关键字", "品类"]) bar.Fields.Add(new Field { Label = name, Content = new TextField() });
        foreach (var name in more ?? ["采购员", "下单日期", "金额"]) bar.More.Add(new Field { Label = name, Content = new TextField() });
        var window = HeadlessUi.Show(bar);
        return (bar, window);
    }

    static Rect BoundsIn(Control control, Control root) => new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);

    static double Top(Control control, Control root) => Math.Round(BoundsIn(control, root).Top);

    static Field FieldNamed(FilterBar bar, string label) => bar.GetLogicalDescendants().OfType<Field>().Single(field => field.Label as string == label);

    static Button ButtonNamed(FilterBar bar, string text) => bar.GetLogicalDescendants().OfType<Button>().First(button => button.IsEffectivelyVisible && TextOf(button).StartsWith(text, StringComparison.Ordinal));

    static Control InputOf(Field field) => (Control)field.Content!;

    [Fact]
    [Upstream("FilterBar.browser", "第一排：标题、常用条件、展开、重置、查询、end 都在同一排，按钮靠结束边")]
    public Task First_row_holds_title_fields_and_buttons() => HeadlessUi.Run(() =>
    {
        var (bar, _) = Layout(80 * 16);
        var row = Top(InputOf(FieldNamed(bar, "关键字")), bar);
        foreach (var name in new[] { "展开", "重置", "查询", "新建" }) Assert.True(Math.Abs(Top(ButtonNamed(bar, name), bar) - row) < 2, name);
        Assert.Equal(Top(FieldNamed(bar, "关键字"), bar), Top(FieldNamed(bar, "品类"), bar));
        Assert.True(Math.Abs(bar.Bounds.Width - BoundsIn(ButtonNamed(bar, "新建"), bar).Right) < 1);
    });

    [Fact]
    [Upstream("FilterBar.browser", "展开后放不下的条件从第二排起，左边、宽度和第一排的条件对齐（不在标题下面）")]
    public Task Expanded_fields_align_with_the_first_row() => HeadlessUi.Run(() =>
    {
        var (bar, window) = Layout(80 * 16);
        Assert.False(FieldNamed(bar, "金额").IsEffectivelyVisible);
        Click(ButtonNamed(bar, "展开"));
        HeadlessUi.Flush(window);
        Assert.Equal("收起", TextOf(bar.Part<Button>(FilterBarParts.Toggle)));
        var amount = FieldNamed(bar, "金额");
        var keyword = FieldNamed(bar, "关键字");
        Assert.True(Top(amount, bar) > Top(keyword, bar));
        Assert.True(Top(ButtonNamed(bar, "查询"), bar) < Top(amount, bar));
        Assert.True(Math.Abs(amount.Bounds.Width - keyword.Bounds.Width) < 2);
        var names = new[] { "关键字", "品类", "采购员", "下单日期", "金额" };
        var last = names.Max(name => Top(FieldNamed(bar, name), bar));
        var firstOfLast = names.Where(name => Top(FieldNamed(bar, name), bar) == last).Min(name => BoundsIn(FieldNamed(bar, name), bar).Left);
        Assert.True(Math.Abs(firstOfLast - BoundsIn(keyword, bar).Left) < 2);
    });

    [Fact]
    [Upstream("FilterBar.browser", "常用条件第一排放不下的也收进“展开”：默认只有一排")]
    public Task Common_fields_that_do_not_fit_are_collapsed() => HeadlessUi.Run(() =>
    {
        string[] names = ["关键字", "品类", "状态", "采购员", "部门", "项目"];
        var (bar, window) = Layout(60 * 16, more: [], common: names);
        var shown = names.Select(name => FieldNamed(bar, name)).Where(field => field.IsEffectivelyVisible).ToList();
        Assert.True(shown.Count < names.Length);
        Assert.All(shown, field => Assert.Equal(Top(shown[0], bar), Top(field, bar)));
        Click(ButtonNamed(bar, "展开"));
        HeadlessUi.Flush(window);
        Assert.All(names, name => Assert.True(FieldNamed(bar, name).IsEffectivelyVisible, name));
    });

    [Fact]
    [Upstream("FilterBar.browser", "窄到条件与按钮放不下一排时，按钮区整体换到下一行，按钮仍在一行")]
    public Task Narrow_bar_moves_buttons_below() => HeadlessUi.Run(() =>
    {
        var (bar, _) = Layout(30 * 16);
        Assert.True(Top(ButtonNamed(bar, "查询"), bar) > Top(InputOf(FieldNamed(bar, "关键字")), bar));
        Assert.Equal(Top(ButtonNamed(bar, "查询"), bar), Top(ButtonNamed(bar, "重置"), bar));
        Assert.Equal(Top(ButtonNamed(bar, "查询"), bar), Top(ButtonNamed(bar, "新建"), bar));
    });
}
