using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Field：标签、说明、错误；字段的出错、必填、只读交给里面的控件（控件自己写的优先）；第一个控件认领标签（读屏名称、点标签聚焦）；
/// 标签在起始边时按基线对齐；标签只给读屏。
/// </summary>
public class FieldTests
{
    static T Part<T>(Control owner, string name) where T : Control => owner.GetLogicalDescendants().OfType<T>().First(part => part.Name == name);

    static AutomationPeer Peer(Control control) => ControlAutomationPeer.CreatePeerForElement(control);

    [Fact]
    [Upstream("Field", "标签的 for 指向控件")]
    public Task Label_description_and_error_are_tied_to_the_control() => HeadlessUi.Run(() =>
    {
        var (email, sms) = (new Checkbox { Value = "email", Content = "邮件" }, new Checkbox { Value = "sms", Content = "短信" });
        var group = new CheckboxGroup { Items = { email, sms } };
        var field = new Field { Label = "通知方式", Description = "至少选一种", Error = "请至少选一种", IsRequired = true, Content = group };
        var window = HeadlessUi.Show(field);

        Assert.Equal("通知方式", Peer(group).GetName()); // 组以字段的标签为名称
        Assert.Equal("至少选一种 请至少选一种", Peer(group).GetHelpText());
        Assert.True(Part<Icon>(field, FieldLabelParts.Required).IsVisible); // 必填的星号
        Assert.True(Part<Flex>(field, FieldParts.Error).IsVisible);
        Assert.Contains(":invalid", email.Classes); // 有错误：控件出错

        var label = Part<FieldLabel>(field, FieldParts.Label);
        var point = label.TranslatePoint(new Point(4, 4), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Assert.True(email.IsFocused); // 点标签聚焦组里的第一项
    });

    [Fact]
    [Upstream("Field", "控件上显式写的状态优先于 Field")]
    public Task Explicit_control_state_beats_the_field_and_invalid_false_hides_the_error() => HeadlessUi.Run(() =>
    {
        var quiet = new Field { Label = "名字", Error = "必填", IsInvalid = false, Content = new Switch() };
        var own = new Switch { IsInvalid = false, IsReadOnly = false };
        var loud = new Field { Label = "开关", Error = "出错", IsReadOnly = true, Content = own };
        var inherited = new Switch();
        var readOnly = new Field { IsReadOnly = true, Content = inherited };
        HeadlessUi.Show(new StackPanel { Children = { quiet, loud, readOnly } });

        Assert.False(Part<Flex>(quiet, FieldParts.Error).IsVisible);
        Assert.DoesNotContain(":invalid", ((Switch)quiet.Content!).Classes);
        Assert.DoesNotContain(":invalid", own.Classes); // 控件上写了 false
        Assert.DoesNotContain(":readonly", own.Classes);
        Assert.Contains(":readonly", inherited.Classes); // 没写：取字段的
    });

    [Fact]
    [Upstream("Field", "Field 的 error、说明、标签写成 cond")]
    public Task Empty_label_description_and_error_count_as_not_written() => HeadlessUi.Run(() =>
    {
        var checkbox = new Checkbox { Content = "记住我" };
        var field = new Field { Label = "", Description = "", Error = "", Content = checkbox };
        HeadlessUi.Show(field);

        Assert.False(Part<FieldLabel>(field, FieldParts.Label).IsVisible);
        Assert.False(Part<Text>(field, FieldParts.Description).IsVisible);
        Assert.False(Part<Flex>(field, FieldParts.Error).IsVisible);
        Assert.DoesNotContain(":invalid", checkbox.Classes);
        Assert.True(string.IsNullOrEmpty(Peer(checkbox).GetHelpText()));
    });

    [Fact]
    public Task Only_the_first_control_takes_the_field_label() => HeadlessUi.Run(() =>
    {
        var (first, second) = (new Checkbox(), new Checkbox { Content = "委派" });
        var field = new Field { Label = "允许的操作", Content = new StackPanel { Children = { first, second } } };
        HeadlessUi.Show(field);

        Assert.Equal("允许的操作", Peer(first).GetName());
        Assert.Equal("委派", Peer(second).GetName());
        Assert.Same(first, Part<FieldLabel>(field, FieldParts.Label).Target);
    });

    [Fact]
    public Task Start_placement_puts_the_label_in_a_column_on_the_controls_baseline() => HeadlessUi.Run(() =>
    {
        var toggle = new Switch { Content = "开工前三天通知店长" };
        var field = new Field { Label = "通知门店", LabelPlacement = FieldLabelPlacement.Start, Description = "可多选", Content = toggle };
        HeadlessUi.Show(field);
        var label = Part<FieldLabel>(field, FieldParts.Label);
        var em = field.FontSize;

        Assert.Equal(8 * em + field.FindResource<double>("Rune.Space.Md") * em, toggle.Bounds.X, tolerance: 0.5); // 标签列 8em + 列距 md
        Assert.Equal(8 * em, label.Bounds.Right, tolerance: 0.5); // 标签贴着控件（end）
        Assert.Equal(Field.FirstBaseline(toggle)!.Value + toggle.Bounds.Y, label.Baseline!.Value + label.Bounds.Y, tolerance: 1); // 第一行基线对齐
        Assert.True(Part<Text>(field, FieldParts.Description).Bounds.X >= toggle.Bounds.X); // 说明在控件列
    });

    [Fact]
    public Task Hidden_label_names_the_control_without_taking_space() => HeadlessUi.Run(() =>
    {
        var group = new RadioGroup { ItemsSource = new[] { "日", "周" } };
        var field = new Field { Label = "周期", IsLabelHidden = true, LabelPlacement = FieldLabelPlacement.Start, Content = group };
        HeadlessUi.Show(field);

        Assert.False(Part<FieldLabel>(field, FieldParts.Label).IsVisible);
        Assert.Equal(0, group.Bounds.X); // 不占标签列
        Assert.Equal("周期", Peer(group).GetName());
    });
}

static class ResourceExtensions
{
    /// <summary>按控件的主题变体查资源。</summary>
    public static T FindResource<T>(this Control control, string key) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value) && value is T typed ? typed : throw new KeyNotFoundException(key);
}
