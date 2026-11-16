using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>ConditionBuilder：读入条件（取反包着任一是"都不满足"，嵌套组）、加一条并填完才写回、换字段换比较方式时值跟着变形、换满足方式、删除（删光写回 null）、父级换值时整棵重读、MaxDepth 限制加组；焦点落到新加的条件上。</summary>
public class ConditionBuilderTests
{
    static readonly ConditionField[] Fields =
    [
        new("supplier", "供应商", ConditionFieldType.Text),
        new("category", "品类", ConditionFieldType.Select) { Options = [new("steel", "钢材"), new("aluminum", "铝材")] },
        new("amount", "金额", ConditionFieldType.Number),
    ];

    static (ConditionBuilder Builder, Window Window) Render(Condition? initial, int maxDepth = 2)
    {
        var builder = new ConditionBuilder { Fields = Fields, Value = initial, MaxDepth = maxDepth };
        AutomationProperties.SetName(builder, "审批条件");
        var window = HeadlessUi.Show(builder);
        return (builder, window);
    }

    static List<T> Named<T>(Control owner, string name) where T : Control => [.. owner.GetLogicalDescendants().OfType<T>().Where(part => part.Name == name)];

    static List<Control> Rules(ConditionBuilder builder) => Named<Control>(builder, ConditionBuilderParts.Rule);

    static Select FieldOf(Control rule) => Named<Select>(rule, ConditionBuilderParts.Field).Single();

    static Select OperatorOf(Control rule) => Named<Select>(rule, ConditionBuilderParts.Operator).Single();

    static T ValueOf<T>(Control rule) where T : Control => Named<Border>(rule, ConditionBuilderParts.Value).Single().GetLogicalDescendants().OfType<T>().First();

    static List<Button> Buttons(ConditionBuilder builder, string name) => Named<Button>(builder, name);

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static string? Json(Condition? condition) => condition?.ToJson();

    [Fact]
    [Upstream("ConditionBuilder", "读入条件：not 包着 any 是“都不满足”，嵌套的组是一组，每条显示字段与比较方式")]
    public Task Reads_condition_into_groups() => HeadlessUi.Run(() =>
    {
        var (builder, _) = Render(Condition.Not(Condition.Any(new ConditionRule("supplier", ConditionOp.Contains, "钢"), Condition.All(new ConditionRule("amount", ConditionOp.Gt, 5)))));
        Assert.Equal(["none", "all"], Named<Select>(builder, ConditionBuilderParts.Mode).Select(mode => mode.Value));
        Assert.All(Named<Select>(builder, ConditionBuilderParts.Mode), mode => Assert.Equal("满足方式", AutomationProperties.GetName(mode)));
        Assert.Contains(builder.GetLogicalDescendants().OfType<Border>(), border => AutomationProperties.GetName(border) == "条件组");
        Assert.Equal([("supplier", "contains"), ("amount", "gt")], Rules(builder).Select(rule => (FieldOf(rule).Value, OperatorOf(rule).Value)));
        Assert.Equal("条件 1", AutomationProperties.GetName(Rules(builder)[0]));
    });

    [Fact]
    [Upstream("ConditionBuilder", "加一条：焦点落到它的字段上；选了字段、填了值才写回；换比较方式时值跟着变形")]
    public Task Adding_a_rule_writes_back_when_complete() => HeadlessUi.Run(() =>
    {
        var (builder, window) = Render(Condition.All(new ConditionRule("amount", ConditionOp.Gt, 5)));
        var before = Json(builder.Value);
        Click(Buttons(builder, ConditionBuilderParts.AddRule)[0]);
        HeadlessUi.Flush(window);
        var added = Rules(builder)[1];
        Assert.True(FieldOf(added).IsFocused || FieldOf(added).IsKeyboardFocusWithin);
        Assert.Equal(before, Json(builder.Value));

        FieldOf(added).Value = "category";
        Assert.Equal("eq", OperatorOf(added).Value);
        Assert.Equal(before, Json(builder.Value));

        ValueOf<Select>(added).Value = "steel";
        Assert.Equal(Json(Condition.All(new ConditionRule("amount", ConditionOp.Gt, 5), new ConditionRule("category", ConditionOp.Eq, "steel"))), Json(builder.Value));
        // 是 → 是其中之一：一个值变成一组
        OperatorOf(added).Value = "in";
        Assert.Equal(Json(new ConditionRule("category", ConditionOp.In, new[] { "steel" })), Json(((ConditionAll)builder.Value!).Items[1]));
        Assert.Equal(["steel"], ValueOf<Select>(added).Values);
    });

    [Fact]
    [Upstream("ConditionBuilder", "文字字段：输入就写回；换满足方式写回 any；删掉一条、删光写回 null")]
    public Task Text_mode_and_remove() => HeadlessUi.Run(() =>
    {
        var (builder, window) = Render(null);
        Assert.Equal("还没有条件", Named<Small>(builder, ConditionBuilderParts.Empty).Single().Text);
        Click(Buttons(builder, ConditionBuilderParts.AddRule)[0]);
        HeadlessUi.Flush(window);
        FieldOf(Rules(builder)[0]).Value = "supplier";
        ValueOf<TextField>(Rules(builder)[0]).Text = "华东";
        Assert.Equal(Json(Condition.All(new ConditionRule("supplier", ConditionOp.Contains, "华东"))), Json(builder.Value));

        Named<Select>(builder, ConditionBuilderParts.Mode).Single().Value = "any";
        Assert.Equal(Json(Condition.Any(new ConditionRule("supplier", ConditionOp.Contains, "华东"))), Json(builder.Value));

        Click(Named<IconButton>(Rules(builder)[0], ConditionBuilderParts.RemoveRule).Single());
        HeadlessUi.Flush(window);
        Assert.Null(builder.Value);
        Assert.Empty(Rules(builder));
    });

    [Fact]
    [Upstream("ConditionBuilder", "父级换了值就整棵重读；自己写回的不重读（没填完的条也留着）")]
    public Task Parent_value_rereads_the_tree() => HeadlessUi.Run(() =>
    {
        var (builder, window) = Render(Condition.All(new ConditionRule("amount", ConditionOp.Gt, 5)));
        Click(Buttons(builder, ConditionBuilderParts.AddRule)[0]);
        HeadlessUi.Flush(window);
        Assert.Equal(2, Rules(builder).Count);
        builder.Value = Condition.Any(new ConditionRule("supplier", ConditionOp.Eq, "甲"), new ConditionRule("supplier", ConditionOp.Eq, "乙"), new ConditionRule("amount", ConditionOp.Exists));
        HeadlessUi.Flush(window);
        Assert.Equal(3, Rules(builder).Count);
    });

    [Fact]
    [Upstream("ConditionBuilder", "maxDepth=1 时不能加组；默认根组里能加一层，组里不再有“添加条件组”")]
    public Task Max_depth_limits_groups() => HeadlessUi.Run(() =>
    {
        var (flat, _) = Render(null, maxDepth: 1);
        Assert.Empty(Buttons(flat, ConditionBuilderParts.AddGroup));
        var (nested, window) = Render(null);
        Click(Buttons(nested, ConditionBuilderParts.AddGroup)[0]);
        HeadlessUi.Flush(window);
        Assert.Single(Buttons(nested, ConditionBuilderParts.AddGroup));
        Assert.Equal(2, Buttons(nested, ConditionBuilderParts.AddRule).Count);
    });

    [Fact]
    public Task Read_only_field_locks_adding_and_removing() => HeadlessUi.Run(() =>
    {
        var builder = new ConditionBuilder { Fields = Fields, Value = Condition.All(new ConditionRule("amount", ConditionOp.Gt, 5)) };
        HeadlessUi.Show(new Field { Label = "审批条件", IsReadOnly = true, Content = builder });
        Assert.All(Buttons(builder, ConditionBuilderParts.AddRule), button => Assert.False(button.IsEnabled));
        Assert.False(Named<IconButton>(builder, ConditionBuilderParts.RemoveRule).Single().IsEnabled);
    });
}
