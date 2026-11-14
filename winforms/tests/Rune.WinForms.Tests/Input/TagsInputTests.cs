using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>TagsInputControl：回车、分隔符、粘贴加入；重复与上限；退格删最后一个；失焦加入；建议列表；不可用、只读。</summary>
public class TagsInputTests
{
    /// <summary>按键：输入框的按键转给外壳的 OnKeyDown（同使用方订阅外壳的 KeyDown）。</summary>
    static KeyEventArgs Press(Control control, Keys key)
    {
        var args = new KeyEventArgs(key);
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [args]);
        return args;
    }

    static ProviderControl Host(params Control[] children)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        var stack = new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch, Dock = DockStyle.Top };
        foreach (var child in children) child.Width = 300;
        stack.Controls.AddRange(children);
        host.Controls.Add(stack);
        return host;
    }

    static string[] Labels(TagsInputControl input) => [.. input.Tags.Select(tag => tag.Text)];

    [Fact]
    [Upstream("TagsInput", "回车加入并清空输入；重复的不加；输入框为空时退格删掉最后一个")]
    public Task Enter_adds_trimmed_text_once_and_backspace_removes_the_last() => WinFormsUi.Run(() =>
    {
        var input = new TagsInputControl { AccessibleName = "关键词" };
        using var form = WinFormsUi.Show(Host(input));

        input.Text = " vue ";
        Assert.True(Press(input, Keys.Enter).SuppressKeyPress);
        Assert.Equal(["vue"], input.Values);
        Assert.Equal("", input.Text);
        Assert.Equal(["vue"], Labels(input));
        input.Text = "vue";
        Press(input, Keys.Enter);
        Assert.Equal(["vue"], input.Values);
        input.Text = "";
        Press(input, Keys.Back);
        Assert.Empty(input.Values);
        Assert.Empty(input.Tags);
    });

    [Fact]
    [Upstream("TagsInput", "失焦时把没确认的文字加成标签；acceptOnBlur={false} 时文字留在框里")]
    public Task Blur_accepts_pending_text_unless_turned_off() => WinFormsUi.Run(() =>
    {
        var accepting = new TagsInputControl { AccessibleName = "关键词" };
        var keeping = new TagsInputControl { AccessibleName = "备注", AcceptOnBlur = false };
        var other = new TextFieldControl();
        using var form = WinFormsUi.Show(Host(accepting, keeping, other));

        accepting.Input.Focus();
        accepting.Text = "销售";
        other.Input.Focus();
        Assert.Equal(["销售"], accepting.Values);
        Assert.Equal("", accepting.Text);
        keeping.Input.Focus();
        keeping.Text = "草稿";
        other.Input.Focus();
        Assert.Empty(keeping.Values);
        Assert.Equal("草稿", keeping.Text);
    });

    [Fact]
    [Upstream("TagsInput", "输入分隔符时加入前面的部分；粘贴按分隔符与换行拆开；max 之外不加")]
    public Task Delimiters_and_paste_split_and_max_caps() => WinFormsUi.Run(() =>
    {
        var input = new TagsInputControl { Max = 3, AccessibleName = "x" };
        var changes = 0;
        input.ValuesChanged += (_, _) => changes++;
        using var form = WinFormsUi.Show(Host(input));

        input.Text = "a，b";
        Assert.Equal(["a"], input.Values);
        Assert.Equal("b", input.Text);
        Assert.True(input.PasteText("c\nd;e"));
        Assert.Equal(["a", "c", "d"], input.Values);
        Assert.Equal(["a", "c", "d"], Labels(input));
        Assert.Equal(2, changes);
        // 只有一段时交给平台照常粘贴
        Assert.False(input.PasteText("x"));
    });

    [Fact]
    [Upstream("TagsInput", "有建议时回车：允许自己输入的加打的字（不变成第一条建议），只能从建议里选的选第一条")]
    public Task Enter_with_suggestions_adds_typed_text_or_the_first_match() => WinFormsUi.Run(() =>
    {
        var free = new TagsInputControl { ItemsSource = new[] { "javascript", "java" }, AccessibleName = "技能" };
        var strict = new TagsInputControl { ItemsSource = new[] { "javascript", "java" }, AllowCustomValue = false, AccessibleName = "语言" };
        using var form = WinFormsUi.Show(Host(free, strict));

        var log = new List<string>();
        free.OpenChanged += (_, e) => log.Add($"{e.IsOpen}:{e.Reason}");
        free.Input.Focus();
        free.Text = "jav";
        Assert.Equal("True:Trigger", string.Join(",", log));
        Assert.True(free.IsOpen);
        Assert.True(free.Input.Focused);
        Press(free, Keys.Enter);
        Assert.Equal(["jav"], free.Values);
        // 要选建议：按 ↓ 再回车
        free.Text = "javas";
        Press(free, Keys.Down);
        Press(free, Keys.Enter);
        Assert.Equal(["jav", "javascript"], free.Values);
        Assert.Equal("", free.Text);

        strict.Text = "jav";
        Press(strict, Keys.Enter);
        Assert.Equal(["javascript"], strict.Values);
        strict.Text = "go";
        Press(strict, Keys.Enter);
        Assert.Equal(["javascript"], strict.Values);
        Press(strict, Keys.Escape);
        Assert.False(strict.IsOpen);
    });

    [Fact]
    [Upstream("TagsInput", "只读时粘贴不加标签；从建议里选的也受 max 限制")]
    public Task Read_only_paste_and_max_from_suggestions() => WinFormsUi.Run(() =>
    {
        var locked = new TagsInputControl { Values = ["a"], IsReadOnly = true, AccessibleName = "只读" };
        var cities = new TagsInputControl { Values = ["北京"], Max = 1, ItemsSource = new[] { "北京", "上海" }, AccessibleName = "城市" };
        using var form = WinFormsUi.Show(Host(locked, cities));

        locked.PasteText("b,c");
        Assert.Equal(["a"], locked.Values);
        Assert.Null(locked.Tags.Single().RemoveButton);

        cities.Text = "上";
        cities.List.State.Select("上海");
        Assert.Equal(["北京"], cities.Values);
        cities.Text = "";
        cities.List.State.Select("北京");
        Assert.Empty(cities.Values);
    });

    [Fact]
    [Upstream("TagsInput", "异步搜索（filter={false}）：还没有建议时也是 combobox；没有 items、也不是异步时不是")]
    public Task Remote_suggestions_are_a_combobox_even_when_empty() => WinFormsUi.Run(() =>
    {
        var remote = new TagsInputControl { LocalFilter = false, IsLoading = true, AccessibleName = "话题" };
        var free = new TagsInputControl { AccessibleName = "关键词" };
        using var form = WinFormsUi.Show(Host(remote, free));

        Assert.Equal(AccessibleRole.ComboBox, remote.AccessibilityObject.Role);
        Assert.True(remote.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed));
        Assert.Equal(AccessibleRole.Text, free.AccessibilityObject.Role);
        Assert.Equal("话题", remote.AccessibilityObject.Name);
        remote.ItemsSource = new[] { "前端工程化", "设计系统" };
        remote.Text = "设计";
        Assert.Equal(2, remote.List.State.Options.Count);
    });

    [Fact]
    [Upstream("TagsInput", "不可用：输入框 disabled，外壳 aria-disabled（标签在外壳里），标签没有移除按钮")]
    public Task Disabled_has_no_remove_buttons() => WinFormsUi.Run(() =>
    {
        var input = new TagsInputControl { Values = ["a", "b"], Enabled = false, AccessibleName = "x" };
        var enabled = new TagsInputControl { Values = ["a"], AccessibleName = "y" };
        using var form = WinFormsUi.Show(Host(input, enabled));

        Assert.All(input.Tags, tag => Assert.Null(tag.RemoveButton));
        Assert.NotNull(enabled.Tags.Single().RemoveButton);
        Assert.True(enabled.Tags.Single().RemoveButton!.TabStop);
    });

    [Fact]
    [Upstream("TagsInput", "TagsInput：组合中按 Enter 不加标签（两种浏览器的写法），组合结束后的 Enter 才加")]
    public Task Enter_while_composing_does_not_add() => WinFormsUi.Run(() =>
    {
        var input = new TagsInputControl { AccessibleName = "关键词" };
        using var form = WinFormsUi.Show(Host(input));

        input.Text = "zhong";
        // 输入法组合中的 Enter（确认候选）：平台报 ProcessKey，交给输入法
        Press(input, Keys.ProcessKey);
        Assert.Empty(input.Values);
        input.Text = "中";
        Press(input, Keys.Enter);
        Assert.Equal(["中"], input.Values);
    });

    [Fact]
    public Task Clear_removes_every_tag_and_tags_come_before_the_input() => WinFormsUi.Run(() =>
    {
        var team = new[] { new ListboxOption("xiaoman@example.com", "林小满"), new ListboxOption("chenmo@example.com", "陈默") };
        var input = new TagsInputControl { ItemsSource = team, Values = ["xiaoman@example.com"], Clearable = true, AccessibleName = "抄送" };
        using var form = WinFormsUi.Show(Host(input));

        Assert.Equal(["林小满"], Labels(input));
        input.Text = "陈默";
        Press(input, Keys.Enter);
        Assert.Equal(["xiaoman@example.com", "chenmo@example.com"], input.Values);
        var order = input.Controls.Cast<Control>().ToList();
        Assert.True(order.IndexOf(input.Tags.Last()) < order.IndexOf(input.Input));
        Assert.True(input.ClearButton.Visible);
        input.ClearButton.PerformClick();
        Assert.Empty(input.Values);
        Assert.False(input.ClearButton.Visible);
    });
}
