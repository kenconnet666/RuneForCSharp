using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Checkbox、Radio 与两个组：点击、空格、读屏切换；组的值双向写回；部分勾选；方向键在单选组里移动并选中、Tab 停在选中项；
/// 只读、出错随组；数据生成的项；拦下切换；方框、圆点的颜色。
/// </summary>
public class ChoiceTests
{
    static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(4, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
    }

    [Fact]
    [Upstream("Checkbox", "原生 checkbox：点击写回")]
    public Task Checkbox_toggles_and_shows_an_indeterminate_state() => HeadlessUi.Run(() =>
    {
        var agree = new Checkbox { Content = "同意" };
        var all = new Checkbox { Content = "全选", IsIndeterminate = true };
        var window = HeadlessUi.Show(new StackPanel { Children = { agree, all } });

        Assert.Equal(AutomationControlType.CheckBox, agree.Peer().GetAutomationControlType());
        Assert.Equal("同意", agree.Peer().GetName());
        Click(window, agree);
        Assert.True(agree.IsChecked);
        Assert.Equal(ToggleState.On, ((IToggleProvider)agree.Peer()).ToggleState);

        agree.Focus();
        Press(window, PhysicalKey.Space);
        Assert.False(agree.IsChecked);

        Assert.Equal(ToggleState.Indeterminate, ((IToggleProvider)all.Peer()).ToggleState);
        Assert.Same(Lucide.Minus, all.Part<Icon>(CheckboxParts.Indicator).Data); // 部分勾选：横线
        Assert.Equal(1, all.Part<Icon>(CheckboxParts.Indicator).Opacity);
        Assert.Equal(0, agree.Part<Icon>(CheckboxParts.Indicator).Opacity); // 没勾：标记不显示（仍占位）
    });

    [Fact]
    public Task Checkbox_box_uses_the_tone_fill_when_checked_and_danger_when_invalid() => HeadlessUi.Run(() =>
    {
        var plain = new Checkbox();
        var on = new Checkbox { IsChecked = true, Tone = StatusTone.Success };
        var bad = new Checkbox { IsChecked = true, IsInvalid = true };
        HeadlessUi.Show(new StackPanel { Children = { plain, on, bad } });

        Assert.Equal(plain.ThemeColor("Rune.Brush.BorderStrong"), Lookup.ColorOf(plain.Part<Border>(CheckboxParts.Box).BorderBrush));
        Assert.Equal(plain.ThemeColor("Rune.Brush.Surface"), Lookup.ColorOf(plain.Part<Border>(CheckboxParts.Box).Background));
        Assert.Equal(on.ThemeColor("Rune.Brush.SuccessText"), Lookup.ColorOf(on.Part<Border>(CheckboxParts.Box).Background)); // 绿色的实底用文字色
        Assert.Same(on.ToneOnFill, on.Part<Icon>(CheckboxParts.Indicator).Foreground);
        Assert.Equal(bad.ThemeColor("Rune.Brush.Danger"), Lookup.ColorOf(bad.Part<Border>(CheckboxParts.Box).BorderBrush)); // 出错盖过勾选
        Assert.Equal(1.125 * plain.FontSize, plain.Part<Border>(CheckboxParts.Box).Bounds.Width, tolerance: 0.5); // 布局按像素取整
    });

    [Fact]
    [Upstream("Checkbox", "复选组：value 是勾选项的数组")]
    public Task Checkbox_group_writes_the_checked_values_back() => HeadlessUi.Run(() =>
    {
        var email = new Checkbox { Value = "email", Content = "邮件" };
        var sms = new Checkbox { Value = "sms", Content = "短信" };
        var group = new CheckboxGroup { Value = ["email"], Items = { email, sms } };
        var window = HeadlessUi.Show(group);

        Assert.Equal(AutomationControlType.Group, group.Peer().GetAutomationControlType());
        Assert.Equal((true, false), (email.IsChecked, sms.IsChecked));
        Click(window, sms);
        Assert.Equal(["email", "sms"], group.Value);
        Click(window, email);
        Assert.Equal(["sms"], group.Value);
        Assert.Equal((false, true), (email.IsChecked, sms.IsChecked));

        group.IsReadOnly = true; // 只读随组
        Click(window, sms);
        Assert.Equal(["sms"], group.Value);
        Assert.Contains(":readonly", sms.Classes);
    });

    [Fact]
    [Upstream("RadioGroup", "同组的 radio 共用自动生成的 name")]
    public Task Radio_group_selects_one_value_and_disables_as_a_whole() => HeadlessUi.Run(() =>
    {
        var express = new Radio { Value = "express", Content = "快递" };
        var pickup = new Radio { Value = "pickup", Content = "自提" };
        var group = new RadioGroup { Value = "express", Items = { express, pickup } };
        var window = HeadlessUi.Show(group);

        Assert.Equal(AutomationControlType.RadioButton, express.Peer().GetAutomationControlType());
        Assert.Equal((true, false), (express.IsChecked, pickup.IsChecked));
        Click(window, pickup);
        Assert.Equal("pickup", group.Value);
        Assert.Equal((false, true), (express.IsChecked, pickup.IsChecked));
        Assert.True(((ISelectionItemProvider)pickup.Peer()).IsSelected);

        group.IsEnabled = false;
        Assert.False(express.IsEffectivelyEnabled || pickup.IsEffectivelyEnabled);
    });

    [Fact]
    public Task Radio_group_has_one_tab_stop_and_arrows_move_and_select() => HeadlessUi.Run(() =>
    {
        var (a, b, c) = (new Radio { Value = "a", Content = "甲" }, new Radio { Value = "b", Content = "乙", IsEnabled = false }, new Radio { Value = "c", Content = "丙" });
        var group = new RadioGroup { Items = { a, b, c } };
        var window = HeadlessUi.Show(group);

        Assert.Equal((true, false, false), (KeyboardNavigation.GetIsTabStop(a), KeyboardNavigation.GetIsTabStop(b), KeyboardNavigation.GetIsTabStop(c))); // 没选中时停在第一项
        group.Value = "c";
        Assert.Equal((false, true), (KeyboardNavigation.GetIsTabStop(a), KeyboardNavigation.GetIsTabStop(c)));

        c.Focus();
        Press(window, PhysicalKey.ArrowDown); // 到头从另一端继续
        Assert.Equal("a", group.Value);
        Press(window, PhysicalKey.ArrowRight); // 跳过不可用的
        Assert.Equal("c", group.Value);
        Assert.True(c.IsFocused);

        group.IsReadOnly = true;
        Press(window, PhysicalKey.ArrowUp);
        Assert.Equal("c", group.Value);
    });

    [Fact]
    public Task Radio_dot_and_circle_follow_the_group_tone() => HeadlessUi.Run(() =>
    {
        var on = new Radio { Value = "x" };
        var off = new Radio { Value = "y" };
        var group = new RadioGroup { Value = "x", Tone = StatusTone.Warning, Items = { on, off } };
        HeadlessUi.Show(group);

        Assert.Equal(on.ThemeColor("Rune.Brush.WarningText"), Lookup.ColorOf(on.Part<Border>(RadioParts.Circle).Background));
        Assert.Same(on.ToneOnFill, on.Part<Border>(RadioParts.Indicator).Background);
        Assert.Equal(TransformOperations.Identity, on.Part<Border>(RadioParts.Indicator).RenderTransform);
        Assert.NotEqual(TransformOperations.Identity, off.Part<Border>(RadioParts.Indicator).RenderTransform); // 没选中：圆点缩成 0
        Assert.Equal(off.ThemeColor("Rune.Brush.BorderStrong"), Lookup.ColorOf(off.Part<Border>(RadioParts.Circle).BorderBrush));
    });

    [Fact]
    [Upstream("Switch", "Switch 与 Checkbox")]
    [Upstream("Radio", "Radio：整组对回父级的值")]
    public Task Cancelled_toggles_keep_the_old_value() => HeadlessUi.Run(() =>
    {
        var checkbox = new Checkbox { IsChecked = true };
        checkbox.Toggling += (_, e) => e.Cancel = true;
        var (a, b) = (new Radio { Value = "a" }, new Radio { Value = "b" });
        b.Toggling += (_, e) => e.Cancel = true;
        var group = new RadioGroup { Value = "a", Items = { a, b } };
        var window = HeadlessUi.Show(new StackPanel { Children = { checkbox, group } });

        Click(window, checkbox);
        Click(window, b);
        Assert.True(checkbox.IsChecked);
        Assert.Equal(("a", true, false), (group.Value, a.IsChecked, b.IsChecked));
    });

    [Fact]
    [Upstream("CheckboxGroup", "CheckboxGroup 的 items")]
    public Task Checkbox_group_items_come_from_options() => HeadlessUi.Run(() =>
    {
        var group = new CheckboxGroup
        {
            Value = ["email"],
            ItemsSource = new[]
            {
                new ListboxOption("email", "邮件") { Description = "每天汇总一封" },
                new ListboxOption("sms", "短信") { IsDisabled = true },
                new ListboxOption("app", "站内信"),
            },
        };
        var window = HeadlessUi.Show(group);
        var items = group.GetLogicalDescendants().OfType<Checkbox>().ToList();

        Assert.Equal(["email", "sms", "app"], items.Select(item => item.Value));
        Assert.True(items[0].IsChecked);
        Assert.False(items[1].IsEnabled);
        Assert.Equal("每天汇总一封", items[0].Peer().GetHelpText());
        Assert.True(items[0].Part<global::Avalonia.Controls.Presenters.ContentPresenter>(ChoiceBaseParts.Description).IsVisible);
        Click(window, items[2]);
        Assert.Equal(["email", "app"], group.Value);
    });

    [Fact]
    [Upstream("RadioGroup", "RadioGroup 的 items")]
    public Task Radio_group_items_come_from_strings_or_mapped_data() => HeadlessUi.Run(() =>
    {
        var plain = new RadioGroup { ItemsSource = new[] { "日", "周", "月" } };
        var plans = new RadioGroup
        {
            ItemsSource = new[] { ("basic", "基础版", "免费"), ("pro", "专业版", "¥99 / 月") },
            ToOption = item => item is (string id, string name, string price) ? new ListboxOption(id, name) { Description = price } : throw new ArgumentException(null, nameof(item)),
        };
        HeadlessUi.Show(new StackPanel { Children = { plain, plans } });

        Assert.Equal(["日", "周", "月"], plain.GetLogicalDescendants().OfType<Radio>().Select(radio => radio.Content as string));
        var pro = plans.GetLogicalDescendants().OfType<Radio>().ElementAt(1);
        Assert.Equal(("pro", "专业版", "¥99 / 月"), (pro.Value, pro.Content as string, pro.Description as string));
    });
}
