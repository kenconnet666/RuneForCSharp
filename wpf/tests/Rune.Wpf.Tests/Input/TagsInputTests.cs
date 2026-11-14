using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>TagsInput：回车、分隔符、粘贴加入；重复与上限；退格删最后一个；失焦加入；建议列表；不可用、只读。</summary>
public class TagsInputTests
{
    /// <summary>按键先到 PreviewKeyDown（TagsInput 在 TextBox 自己处理之前接住）。</summary>
    static void Press(UIElement target, Key key) =>
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static string?[] Labels(TagsInput input) => [.. input.Tags.Select(tag => tag.Content as string)];

    static CloseButton? RemoveButton(Tag tag) => WpfUi.Descendants(tag).OfType<CloseButton>().SingleOrDefault();

    static TagsInput Named(TagsInput input, string name)
    {
        AutomationProperties.SetName(input, name);
        return input;
    }

    [Fact]
    [Upstream("TagsInput", "回车加入并清空输入；重复的不加；输入框为空时退格删掉最后一个")]
    public Task Enter_adds_trimmed_text_once_and_backspace_removes_the_last() => WpfUi.Run(() =>
    {
        var input = Named(new TagsInput(), "关键词");
        WpfUi.Show(Host(input));

        input.Text = " vue ";
        Press(input, Key.Enter);
        Assert.Equal(["vue"], input.Values);
        Assert.Equal("", input.Text);
        Assert.Equal(["vue"], Labels(input));
        input.Text = "vue";
        Press(input, Key.Enter);
        Assert.Equal(["vue"], input.Values);
        input.Text = "";
        Press(input, Key.Back);
        Assert.Empty(input.Values!);
        Assert.Empty(input.Tags);
    });

    [Fact]
    [Upstream("TagsInput", "失焦时把没确认的文字加成标签；acceptOnBlur={false} 时文字留在框里")]
    public Task Blur_accepts_pending_text_unless_turned_off() => WpfUi.Run(() =>
    {
        var accepting = Named(new TagsInput(), "关键词");
        var keeping = Named(new TagsInput { AcceptOnBlur = false }, "备注");
        var other = new TextField();
        WpfUi.Show(Host(accepting, keeping, other));

        Keyboard.Focus(accepting);
        accepting.Text = "销售";
        Keyboard.Focus(other);
        Assert.Equal(["销售"], accepting.Values);
        Assert.Equal("", accepting.Text);
        Keyboard.Focus(keeping);
        keeping.Text = "草稿";
        Keyboard.Focus(other);
        Assert.Null(keeping.Values);
        Assert.Equal("草稿", keeping.Text);
    });

    [Fact]
    [Upstream("TagsInput", "输入分隔符时加入前面的部分；粘贴按分隔符与换行拆开；max 之外不加")]
    public Task Delimiters_and_paste_split_and_max_caps() => WpfUi.Run(() =>
    {
        var input = Named(new TagsInput { Max = 3 }, "x");
        WpfUi.Show(Host(input));

        input.Text = "a，b";
        Assert.Equal(["a"], input.Values);
        Assert.Equal("b", input.Text);
        Assert.True(input.PasteText("c\nd;e"));
        Assert.Equal(["a", "c", "d"], input.Values);
        Assert.Equal(["a", "c", "d"], Labels(input));
        // 只有一段时交给平台照常粘贴
        Assert.False(input.PasteText("x"));
    });

    [Fact]
    [Upstream("TagsInput", "有建议时回车：允许自己输入的加打的字（不变成第一条建议），只能从建议里选的选第一条")]
    public Task Enter_with_suggestions_adds_typed_text_or_the_first_match() => WpfUi.Run(() =>
    {
        var free = Named(new TagsInput { ItemsSource = new[] { "javascript", "java" } }, "技能");
        var strict = Named(new TagsInput { ItemsSource = new[] { "javascript", "java" }, AllowCustomValue = false }, "语言");
        WpfUi.Show(Host(free, strict));

        free.Text = "jav";
        Assert.True(free.IsOpen);
        Press(free, Key.Enter);
        Assert.Equal(["jav"], free.Values);
        // 要选建议：按 ↓ 再回车
        free.Text = "javas";
        Press(free, Key.Down);
        Press(free, Key.Enter);
        Assert.Equal(["jav", "javascript"], free.Values);
        Assert.Equal("", free.Text);

        strict.Text = "jav";
        Press(strict, Key.Enter);
        Assert.Equal(["javascript"], strict.Values);
        strict.Text = "go";
        Press(strict, Key.Enter);
        Assert.Equal(["javascript"], strict.Values);
        Press(strict, Key.Escape);
        Assert.False(strict.IsOpen);
    });

    [Fact]
    [Upstream("TagsInput", "只读时粘贴不加标签；从建议里选的也受 max 限制")]
    public Task Read_only_paste_and_max_from_suggestions() => WpfUi.Run(() =>
    {
        var locked = Named(new TagsInput { Values = ["a"], IsReadOnly = true }, "只读");
        var cities = Named(new TagsInput { Values = ["北京"], Max = 1, ItemsSource = new[] { "北京", "上海" } }, "城市");
        WpfUi.Show(Host(locked, cities));

        locked.PasteText("b,c");
        Assert.Equal(["a"], locked.Values);
        Assert.Null(RemoveButton(locked.Tags.Single()));

        cities.Text = "上";
        cities.List.State.Select("上海");
        Assert.Equal(["北京"], cities.Values);
        cities.Text = "";
        cities.List.State.Select("北京");
        Assert.Empty(cities.Values!);
    });

    [Fact]
    [Upstream("TagsInput", "异步搜索（filter={false}）：还没有建议时也是 combobox；没有 items、也不是异步时不是")]
    public Task Remote_suggestions_are_a_combobox_even_when_empty() => WpfUi.Run(() =>
    {
        var remote = Named(new TagsInput { LocalFilter = false, IsLoading = true }, "话题");
        var free = Named(new TagsInput(), "关键词");
        WpfUi.Show(Host(remote, free));

        Assert.Equal(AutomationControlType.ComboBox, remote.Peer().GetAutomationControlType());
        Assert.Equal(ExpandCollapseState.Collapsed, ((IExpandCollapseProvider)remote.Peer().GetPattern(PatternInterface.ExpandCollapse)).ExpandCollapseState);
        Assert.Equal(AutomationControlType.Edit, free.Peer().GetAutomationControlType());
        Assert.Equal("话题", remote.Peer().GetName());
        remote.ItemsSource = new[] { "前端工程化", "设计系统" };
        remote.Text = "设计";
        Assert.Equal(2, remote.List.State.Options.Count);
    });

    [Fact]
    [Upstream("TagsInput", "不可用：输入框 disabled，外壳 aria-disabled（标签在外壳里），标签没有移除按钮")]
    public Task Disabled_has_no_remove_buttons() => WpfUi.Run(() =>
    {
        var input = Named(new TagsInput { Values = ["a", "b"], IsEnabled = false }, "x");
        var enabled = Named(new TagsInput { Values = ["a"] }, "y");
        WpfUi.Show(Host(input, enabled));

        Assert.All(input.Tags, tag => Assert.Null(RemoveButton(tag)));
        Assert.NotNull(RemoveButton(enabled.Tags.Single()));
    });

    [Fact]
    [Upstream("TagsInput", "TagsInput：组合中按 Enter 不加标签（两种浏览器的写法），组合结束后的 Enter 才加")]
    public Task Enter_while_composing_does_not_add() => WpfUi.Run(() =>
    {
        var input = Named(new TagsInput(), "关键词");
        WpfUi.Show(Host(input));

        input.Text = "zhong";
        // 输入法组合中的 Enter（确认候选）：平台报 ImeProcessed，交给输入法
        Press(input, Key.ImeProcessed);
        Assert.Null(input.Values);
        input.Text = "中";
        Press(input, Key.Enter);
        Assert.Equal(["中"], input.Values);
    });

    [Fact]
    public Task Clear_removes_every_tag_and_labels_come_from_suggestions() => WpfUi.Run(() =>
    {
        var team = new[] { new ListboxOption("xiaoman@example.com", "林小满"), new ListboxOption("chenmo@example.com", "陈默") };
        var input = Named(new TagsInput { ItemsSource = team, Values = ["xiaoman@example.com"], Clearable = true }, "抄送");
        WpfUi.Show(Host(input));

        Assert.Equal(["林小满"], Labels(input));
        input.Text = "陈默";
        Press(input, Key.Enter);
        Assert.Equal(["xiaoman@example.com", "chenmo@example.com"], input.Values);
        var clear = WpfUi.Descendants(input).OfType<ClearButton>().Single();
        Assert.Equal(Visibility.Visible, clear.Visibility);
        ((IInvokeProvider)clear.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.Empty(input.Values!);
        Assert.Equal(Visibility.Collapsed, clear.Visibility);
    });
}
