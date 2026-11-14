using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>Combobox：输入过滤并高亮第一项、Enter 选中、失焦恢复或清空、自由输入、Esc 关上再清空、↓ 与展开按钮显示全部；Autocomplete：值就是文字、没有建议不弹出。</summary>
public class ComboboxTests
{
    static readonly string[] Cities = ["北京", "北海", "上海", "广州"];

    /// <summary>按键先到 PreviewKeyDown（Combobox 在 TextBox 自己处理之前接住）。</summary>
    static void Press(UIElement target, Key key) =>
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static string[] Shown(Combobox box) => [.. box.List.State.Options.Select(entry => entry.Option.Label)];

    static T Named<T>(T box, string name) where T : DependencyObject
    {
        AutomationProperties.SetName(box, name);
        return box;
    }

    [Fact]
    public Task Typing_filters_highlights_the_first_and_enter_picks() => WpfUi.Run(() =>
    {
        var box = Named(new Combobox { ItemsSource = Cities }, "城市");
        var other = new TextField();
        WpfUi.Show(Host(box, other));

        Assert.Equal(AutomationControlType.ComboBox, box.Peer().GetAutomationControlType());
        Keyboard.Focus(box);
        box.Text = "北";
        Assert.True(box.IsOpen);
        Assert.Equal(["北京", "北海"], Shown(box));
        Assert.Equal("北京", box.List.State.Active);
        Press(box, Key.Down);
        Press(box, Key.Enter);
        Assert.Equal("北海", box.Value);
        Assert.Equal("北海", box.Text);
        Assert.False(box.IsOpen);

        // 失焦时文字不是选项：恢复成已选项的文字
        box.Text = "北海道";
        Keyboard.Focus(other);
        Assert.Equal(("北海", "北海"), (box.Value, box.Text));
        // 清空后失焦：取消选择
        Keyboard.Focus(box);
        box.Text = "";
        Keyboard.Focus(other);
        Assert.Null(box.Value);
    });

    [Fact]
    public Task Value_from_the_parent_shows_its_label() => WpfUi.Run(() =>
    {
        var box = Named(new Combobox { ToOption = item => new ListboxOption(((string)item!)[..1], (string)item!), Value = "上" }, "城市");
        WpfUi.Show(Host(box));
        // 选项后到：输入框跟着显示它的文字
        box.ItemsSource = Cities;
        Assert.Equal("上海", box.Text);
        box.Value = "广";
        Assert.Equal("广州", box.Text);
    });

    [Fact]
    public Task Custom_value_keeps_the_text_and_clears_the_value() => WpfUi.Run(() =>
    {
        var box = Named(new Combobox { ItemsSource = Cities, AllowCustomValue = true, Value = "北京" }, "城市");
        var other = new TextField();
        WpfUi.Show(Host(box, other));

        Keyboard.Focus(box);
        box.Text = "雄安";
        Keyboard.Focus(other);
        Assert.Equal("雄安", box.Text);
        Assert.Null(box.Value);
    });

    [Fact]
    public Task Escape_closes_then_clears_and_arrows_or_toggle_show_all() => WpfUi.Run(() =>
    {
        var box = Named(new Combobox { ItemsSource = Cities, Value = "上海" }, "城市");
        var reasons = new List<OpenChangeReason>();
        box.OpenChanged += (_, e) => reasons.Add(e.Reason);
        WpfUi.Show(Host(box));
        Keyboard.Focus(box);

        Press(box, Key.Down);
        Assert.True(box.IsOpen);
        Assert.Equal(Cities, Shown(box));
        Assert.Equal("上海", box.List.State.Active);
        Press(box, Key.Escape);
        Assert.False(box.IsOpen);
        Assert.Equal("上海", box.Value);
        Press(box, Key.Escape);
        Assert.Equal(("", null), (box.Text, box.Value));

        var toggle = box.ToggleButton!;
        Assert.Equal("显示选项", toggle.Peer().GetName());
        Assert.False(toggle.Focusable);
        ((IInvokeProvider)toggle.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.True(box.IsOpen);
        Assert.Equal([OpenChangeReason.Trigger, OpenChangeReason.Escape, OpenChangeReason.Trigger], reasons);
    });

    [Fact]
    [Upstream("Combobox", "Combobox：组合中的 ↓ 与 Enter 不移动高亮、不选中")]
    public Task Keys_while_composing_do_nothing() => WpfUi.Run(() =>
    {
        var box = Named(new Combobox { ItemsSource = Cities }, "城市");
        WpfUi.Show(Host(box));
        Keyboard.Focus(box);
        box.Text = "北";

        // 输入法组合中的 ↓、Enter：平台报 ImeProcessed，交给输入法
        Press(box, Key.ImeProcessed);
        Press(box, Key.ImeProcessed);
        Assert.Null(box.Value);
        Press(box, Key.Down);
        Press(box, Key.Enter);
        Assert.NotNull(box.Value);
    });

    [Fact]
    [Upstream("Combobox", "输入停下 300 ms 后按输入请求")]
    public Task Remote_load_replaces_the_items() => WpfUi.Run(() =>
    {
        var queries = new List<string>();
        var box = Named(new Combobox
        {
            Load = (query, _, _) =>
            {
                queries.Add(query);
                return Task.FromResult(OptionsPage.All(new[] { "zhouyihang", "linxiaoman" }.Where(user => user.Contains(query))));
            },
        }, "成员");
        WpfUi.Show(Host(box));
        Keyboard.Focus(box);

        box.Text = "lin";
        WpfUi.Until(() => Shown(box) is ["linxiaoman"]);
        Assert.Equal("lin", queries[^1]);
        Press(box, Key.Enter);
        Assert.Equal("linxiaoman", box.Value);
    });

    [Fact]
    [Upstream("Autocomplete", "输入就是值；有建议时展开，选一项填入它的文字；没有展开按钮")]
    public Task Autocomplete_text_is_the_value_and_picking_fills_it() => WpfUi.Run(() =>
    {
        var box = Named(new Autocomplete { LocalFilter = false }, "邮箱");
        WpfUi.Show(Host(box));
        Keyboard.Focus(box);

        Assert.Equal(Visibility.Collapsed, box.ToggleButton!.Visibility);
        box.Text = "lin";
        box.ItemsSource = new[] { "lin@qq.com", "lin@163.com", "lin@gmail.com" };
        Assert.True(box.IsShown);
        Assert.Equal(["lin@qq.com", "lin@163.com", "lin@gmail.com"], Shown(box));
        box.List.State.SetActive("lin@163.com");
        Press(box, Key.Enter);
        Assert.Equal("lin@163.com", box.Text);
    });

    [Fact]
    [Upstream("Autocomplete", "没有可选的建议时不弹出（不显示“没有匹配”）；表单提交的是文字")]
    public Task Autocomplete_without_suggestions_stays_closed_and_keeps_the_text() => WpfUi.Run(() =>
    {
        var box = Named(new Autocomplete { ItemsSource = new[] { "lin@qq.com" } }, "邮箱");
        var other = new TextField();
        WpfUi.Show(Host(box, other));
        Keyboard.Focus(box);

        box.Text = "lin@x";
        Assert.True(box.IsOpen);
        Assert.False(box.IsShown);
        Assert.Equal(ExpandCollapseState.Collapsed, ((IExpandCollapseProvider)box.Peer().GetPattern(PatternInterface.ExpandCollapse)).ExpandCollapseState);
        box.Text = "自定义的文字";
        Keyboard.Focus(other);
        Assert.Equal("自定义的文字", box.Text);
    });
}
