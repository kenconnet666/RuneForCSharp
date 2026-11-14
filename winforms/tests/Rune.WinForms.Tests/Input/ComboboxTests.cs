using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>ComboboxControl：输入过滤并高亮第一项、Enter 选中、失焦恢复或清空、自由输入、Esc 关上再清空、↓ 与展开按钮显示全部；AutocompleteControl：值就是文字、没有建议不弹出。</summary>
public class ComboboxTests
{
    static readonly string[] Cities = ["北京", "北海", "上海", "广州"];

    /// <summary>按键：输入框的按键转给外壳的 OnKeyDown。</summary>
    static void Press(Control control, Keys key) =>
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [new KeyEventArgs(key)]);

    static ProviderControl Host(params Control[] children)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        var stack = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Top };
        foreach (var child in children) child.Width = 300;
        stack.Controls.AddRange(children);
        host.Controls.Add(stack);
        return host;
    }

    static string[] Shown(ComboboxControl box) => [.. box.List.State.Options.Select(entry => entry.Option.Label)];

    [Fact]
    public Task Typing_filters_highlights_the_first_and_enter_picks() => WinFormsUi.Run(() =>
    {
        var box = new ComboboxControl { ItemsSource = Cities, AccessibleName = "城市" };
        var other = new TextFieldControl();
        using var form = WinFormsUi.Show(Host(box, other));

        Assert.Equal(AccessibleRole.ComboBox, box.AccessibilityObject.Role);
        box.Input.Focus();
        box.Text = "北";
        Assert.True(box.IsOpen);
        Assert.Equal(["北京", "北海"], Shown(box));
        Assert.Equal("北京", box.List.State.Active);
        Press(box, Keys.Down);
        Press(box, Keys.Enter);
        Assert.Equal(("北海", "北海"), (box.Value, box.Text));
        Assert.False(box.IsOpen);

        box.Text = "北海道";
        other.Input.Focus();
        Assert.Equal(("北海", "北海"), (box.Value, box.Text));
        box.Input.Focus();
        box.Text = "";
        other.Input.Focus();
        Assert.Null(box.Value);
    });

    [Fact]
    public Task Value_from_the_parent_shows_its_label() => WinFormsUi.Run(() =>
    {
        var box = new ComboboxControl { ToOption = item => new ListboxOption(((string)item!)[..1], (string)item!), Value = "上", AccessibleName = "城市" };
        using var form = WinFormsUi.Show(Host(box));
        box.ItemsSource = Cities;
        Assert.Equal("上海", box.Text);
        box.Value = "广";
        Assert.Equal("广州", box.Text);
    });

    [Fact]
    public Task Custom_value_keeps_the_text_and_clears_the_value() => WinFormsUi.Run(() =>
    {
        var box = new ComboboxControl { ItemsSource = Cities, AllowCustomValue = true, Value = "北京", AccessibleName = "城市" };
        var other = new TextFieldControl();
        using var form = WinFormsUi.Show(Host(box, other));

        box.Input.Focus();
        box.Text = "雄安";
        other.Input.Focus();
        Assert.Equal("雄安", box.Text);
        Assert.Null(box.Value);
    });

    [Fact]
    public Task Escape_closes_then_clears_and_arrows_or_toggle_show_all() => WinFormsUi.Run(() =>
    {
        var box = new ComboboxControl { ItemsSource = Cities, Value = "上海", AccessibleName = "城市" };
        var reasons = new List<OpenChangeReason>();
        box.OpenChanged += (_, e) => reasons.Add(e.Reason);
        using var form = WinFormsUi.Show(Host(box));
        box.Input.Focus();

        Press(box, Keys.Down);
        Assert.True(box.IsOpen);
        Assert.Equal(Cities, Shown(box));
        Assert.Equal("上海", box.List.State.Active);
        Press(box, Keys.Escape);
        Assert.False(box.IsOpen);
        Assert.Equal("上海", box.Value);
        Press(box, Keys.Escape);
        Assert.Equal(("", null), (box.Text, box.Value));

        Assert.Equal("显示选项", box.Toggle.AccessibleName);
        Assert.False(box.Toggle.TabStop);
        box.Toggle.PerformClick();
        Assert.True(box.IsOpen);
        Assert.Equal([OpenChangeReason.Trigger, OpenChangeReason.Escape, OpenChangeReason.Trigger], reasons);
    });

    [Fact]
    [Upstream("Combobox", "Combobox：组合中的 ↓ 与 Enter 不移动高亮、不选中")]
    public Task Keys_while_composing_do_nothing() => WinFormsUi.Run(() =>
    {
        var box = new ComboboxControl { ItemsSource = Cities, AccessibleName = "城市" };
        using var form = WinFormsUi.Show(Host(box));
        box.Input.Focus();
        box.Text = "北";

        // 输入法组合中的 ↓、Enter：平台报 ProcessKey，交给输入法
        Press(box, Keys.ProcessKey);
        Press(box, Keys.ProcessKey);
        Assert.Null(box.Value);
        Press(box, Keys.Down);
        Press(box, Keys.Enter);
        Assert.NotNull(box.Value);
    });

    [Fact]
    [Upstream("Combobox", "输入停下 300 ms 后按输入请求")]
    public Task Remote_load_replaces_the_items() => WinFormsUi.Run(() =>
    {
        var queries = new List<string>();
        var box = new ComboboxControl
        {
            AccessibleName = "成员",
            Load = (query, _, _) =>
            {
                queries.Add(query);
                return Task.FromResult(OptionsPage.All(new[] { "zhouyihang", "linxiaoman" }.Where(user => user.Contains(query))));
            },
        };
        using var form = WinFormsUi.Show(Host(box));
        box.Input.Focus();

        box.Text = "lin";
        WinFormsUi.Until(() => Shown(box) is ["linxiaoman"]);
        Assert.Equal("lin", queries[^1]);
        Press(box, Keys.Enter);
        Assert.Equal("linxiaoman", box.Value);
    });

    [Fact]
    [Upstream("Autocomplete", "输入就是值；有建议时展开，选一项填入它的文字；没有展开按钮")]
    public Task Autocomplete_text_is_the_value_and_picking_fills_it() => WinFormsUi.Run(() =>
    {
        var box = new AutocompleteControl { LocalFilter = false, AccessibleName = "邮箱" };
        using var form = WinFormsUi.Show(Host(box));
        box.Input.Focus();

        Assert.False(box.Toggle.Visible);
        box.Text = "lin";
        box.ItemsSource = new[] { "lin@qq.com", "lin@163.com", "lin@gmail.com" };
        Assert.True(box.Shown);
        Assert.Equal(["lin@qq.com", "lin@163.com", "lin@gmail.com"], Shown(box));
        box.List.State.SetActive("lin@163.com");
        Press(box, Keys.Enter);
        Assert.Equal("lin@163.com", box.Text);
    });

    [Fact]
    [Upstream("Autocomplete", "没有可选的建议时不弹出（不显示“没有匹配”）；表单提交的是文字")]
    public Task Autocomplete_without_suggestions_stays_closed_and_keeps_the_text() => WinFormsUi.Run(() =>
    {
        var box = new AutocompleteControl { ItemsSource = new[] { "lin@qq.com" }, AccessibleName = "邮箱" };
        var other = new TextFieldControl();
        using var form = WinFormsUi.Show(Host(box, other));
        box.Input.Focus();

        box.Text = "lin@x";
        Assert.True(box.IsOpen);
        Assert.False(box.Shown);
        Assert.True(box.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed));
        box.Text = "自定义的文字";
        other.Input.Focus();
        Assert.Equal("自定义的文字", box.Text);
    });

    [Fact]
    public Task A_disabled_field_dims_the_shell_and_the_chevron() => WinFormsUi.Run(() =>
    {
        var box = new ComboboxControl { ItemsSource = Cities, Value = "上海" };
        var enabled = new ComboboxControl { ItemsSource = Cities, Value = "上海" };
        // 先放进已经不可用的字段：平台不为它发 OnEnabledChanged，元素的可用状态要靠 OnEffectiveEnabledChanged 同步
        var field = new FieldControl { Label = "不可用", Enabled = false, Content = box };
        using var form = WinFormsUi.Show(Host(field, enabled));

        Assert.False(box.Toggle.Element.IsEnabled);
        Assert.True(enabled.Toggle.Element.IsEnabled);
        // 里面的 TextBox 底色跟着外壳变淡，和外壳接得上
        Assert.NotEqual(enabled.Input.BackColor, box.Input.BackColor);
    });
}
