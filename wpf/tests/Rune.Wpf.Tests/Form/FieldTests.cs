using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// Field：标签、说明、错误；字段的出错、必填、只读交给里面的控件（控件自己写的优先）；第一个控件认领标签（读屏名称、点标签聚焦）；
/// 标签在起始边时按基线对齐；标签只给读屏。
/// </summary>
public class FieldTests
{
    static StackPanel Show(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        WpfUi.Show(host);
        WpfUi.Flush();
        return host;
    }

    [Fact]
    [Upstream("Field", "标签的 for 指向控件")]
    public Task Label_description_and_error_are_tied_to_the_control() => WpfUi.Run(() =>
    {
        var (email, sms) = (new Checkbox { Value = "email", Content = "邮件" }, new Checkbox { Value = "sms", Content = "短信" });
        var group = new CheckboxGroup { Items = { email, sms } };
        var field = new Field { Label = "通知方式", Description = "至少选一种", Error = "请至少选一种", IsRequired = true, Content = group };
        Show(field);

        Assert.Equal("通知方式", group.Peer().GetName()); // 组以字段的标签为名称
        Assert.Equal("至少选一种 请至少选一种", group.Peer().GetHelpText());
        Assert.Equal(Visibility.Visible, field.Part<Icon>("PART_Required").Visibility); // 必填的星号
        Assert.Equal(Visibility.Visible, field.Part<Flex>("PART_Error").Visibility);
        Assert.True(email.IsEffectivelyInvalid); // 有错误：控件出错

        var label = field.Part<FieldLabel>("PART_Label");
        label.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });
        Assert.True(email.IsFocused); // 点标签聚焦组里的第一项
    });

    [Fact]
    [Upstream("Field", "控件上显式写的状态优先于 Field")]
    public Task Explicit_control_state_beats_the_field_and_invalid_false_hides_the_error() => WpfUi.Run(() =>
    {
        var quiet = new Field { Label = "名字", Error = "必填", IsInvalid = false, Content = new Switch() };
        var own = new Switch { IsInvalid = false, IsReadOnly = false };
        var loud = new Field { Label = "开关", Error = "出错", IsReadOnly = true, Content = own };
        var inherited = new Switch();
        var readOnly = new Field { IsReadOnly = true, Content = inherited };
        Show(quiet, loud, readOnly);

        Assert.Equal(Visibility.Collapsed, quiet.Part<Flex>("PART_Error").Visibility);
        Assert.False(((Switch)quiet.Content!).IsEffectivelyInvalid);
        Assert.False(own.IsEffectivelyInvalid); // 控件上写了 false
        Assert.False(own.IsEffectivelyReadOnly);
        Assert.True(inherited.IsEffectivelyReadOnly); // 没写：取字段的
    });

    [Fact]
    [Upstream("Field", "Field 的 error、说明、标签写成 cond")]
    public Task Empty_label_description_and_error_count_as_not_written() => WpfUi.Run(() =>
    {
        var checkbox = new Checkbox { Content = "记住我" };
        var field = new Field { Label = "", Description = "", Error = "", Content = checkbox };
        Show(field);

        Assert.Equal(Visibility.Collapsed, field.Part<FieldLabel>("PART_Label").Visibility);
        Assert.Equal(Visibility.Collapsed, field.Part<Text>("PART_Description").Visibility);
        Assert.Equal(Visibility.Collapsed, field.Part<Flex>("PART_Error").Visibility);
        Assert.False(checkbox.IsEffectivelyInvalid);
        Assert.Equal("", checkbox.Peer().GetHelpText());
    });

    [Fact]
    public Task Only_the_first_control_takes_the_field_label() => WpfUi.Run(() =>
    {
        var (first, second) = (new Checkbox(), new Checkbox { Content = "委派" });
        var field = new Field { Label = "允许的操作", Content = new StackPanel { Children = { first, second } } };
        Show(field);

        Assert.Equal("允许的操作", first.Peer().GetName());
        Assert.Equal("委派", second.Peer().GetName());
        Assert.Same(first, field.Part<FieldLabel>("PART_Label").Target);
    });

    [Fact]
    public Task Start_placement_puts_the_label_in_a_column_on_the_controls_baseline() => WpfUi.Run(() =>
    {
        var toggle = new Switch { Content = "开工前三天通知店长" };
        var field = new Field { Label = "通知门店", LabelPlacement = FieldLabelPlacement.Start, Description = "可多选", Content = toggle };
        Show(field);
        var label = field.Part<FieldLabel>("PART_Label");
        var em = field.FontSize;
        double Top(FrameworkElement element) => element.TranslatePoint(default, field).Y;
        double Left(FrameworkElement element) => element.TranslatePoint(default, field).X;

        Assert.Equal(8 * em + (double)field.FindResource("Rune.Space.Md") * em, Left(toggle), 0.5); // 标签列 8em + 列距 md
        Assert.Equal(8 * em, Left(label) + label.ActualWidth, 0.5); // 标签贴着控件（end）
        Assert.Equal(Field.FirstBaseline(toggle)!.Value + Top(toggle), label.Baseline!.Value + Top(label), tolerance: 1); // 第一行基线对齐
        Assert.True(Left(field.Part<Text>("PART_Description")) >= Left(toggle)); // 说明在控件列
    });

    [Fact]
    public Task Hidden_label_names_the_control_without_taking_space() => WpfUi.Run(() =>
    {
        var group = new RadioGroup { ItemsSource = new[] { "日", "周" } };
        var field = new Field { Label = "周期", IsLabelHidden = true, LabelPlacement = FieldLabelPlacement.Start, Content = group };
        Show(field);

        Assert.Equal(Visibility.Collapsed, field.Part<FieldLabel>("PART_Label").Visibility);
        Assert.Equal(0, group.TranslatePoint(default, field).X); // 不占标签列
        Assert.Equal("周期", group.Peer().GetName());
    });
}
