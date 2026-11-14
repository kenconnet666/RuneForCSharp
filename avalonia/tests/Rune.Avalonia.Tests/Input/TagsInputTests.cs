using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>TagsInput：回车、分隔符、粘贴加入；重复与上限；退格删最后一个；失焦加入；建议列表；不可用、只读。</summary>
public class TagsInputTests
{
    static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    static string?[] Labels(TagsInput input) => [.. input.Tags.Select(tag => tag.Content as string)];

    static CloseButton? RemoveButton(Tag tag) => tag.GetVisualChildren().OfType<CloseButton>().SingleOrDefault();

    static TagsInput Named(TagsInput input, string name)
    {
        AutomationProperties.SetName(input, name);
        return input;
    }

    [Fact]
    [Upstream("TagsInput", "回车加入并清空输入；重复的不加；输入框为空时退格删掉最后一个")]
    public Task Enter_adds_trimmed_text_once_and_backspace_removes_the_last() => HeadlessUi.Run(() =>
    {
        var input = Named(new TagsInput(), "关键词");
        HeadlessUi.Show(input);

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
    public Task Blur_accepts_pending_text_unless_turned_off() => HeadlessUi.Run(() =>
    {
        var accepting = Named(new TagsInput(), "关键词");
        var keeping = Named(new TagsInput { AcceptOnBlur = false }, "备注");
        var other = new TextField();
        HeadlessUi.Show(new StackPanel { Children = { accepting, keeping, other } });

        accepting.Focus();
        accepting.Text = "销售";
        other.Focus();
        Assert.Equal(["销售"], accepting.Values);
        Assert.Equal("", accepting.Text);
        keeping.Focus();
        keeping.Text = "草稿";
        other.Focus();
        Assert.Null(keeping.Values);
        Assert.Equal("草稿", keeping.Text);
    });

    [Fact]
    [Upstream("TagsInput", "输入分隔符时加入前面的部分；粘贴按分隔符与换行拆开；max 之外不加")]
    public Task Delimiters_and_paste_split_and_max_caps() => HeadlessUi.Run(() =>
    {
        var input = Named(new TagsInput { Max = 3 }, "x");
        HeadlessUi.Show(input);

        input.Text = "a，b";
        Assert.Equal(["a"], input.Values);
        Assert.Equal("b", input.Text);
        input.PasteText("c\nd;e");
        Assert.Equal(["a", "c", "d"], input.Values);
        Assert.Equal(["a", "c", "d"], Labels(input));
        // 只有一段时照常粘贴到光标处
        input.CaretIndex = 1;
        input.PasteText("x");
        Assert.Equal("bx", input.Text);
    });

    [Fact]
    [Upstream("TagsInput", "有建议时回车：允许自己输入的加打的字（不变成第一条建议），只能从建议里选的选第一条")]
    public Task Enter_with_suggestions_adds_typed_text_or_the_first_match() => HeadlessUi.Run(() =>
    {
        var free = Named(new TagsInput { ItemsSource = new[] { "javascript", "java" } }, "技能");
        var strict = Named(new TagsInput { ItemsSource = new[] { "javascript", "java" }, AllowCustomValue = false }, "语言");
        HeadlessUi.Show(new StackPanel { Children = { free, strict } });

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
        // 不在建议里的文字加不进去
        strict.Text = "go";
        Press(strict, Key.Enter);
        Assert.Equal(["javascript"], strict.Values);
        Press(strict, Key.Escape);
        Assert.False(strict.IsOpen);
    });

    [Fact]
    [Upstream("TagsInput", "只读时粘贴不加标签；从建议里选的也受 max 限制")]
    public Task Read_only_paste_and_max_from_suggestions() => HeadlessUi.Run(() =>
    {
        var locked = Named(new TagsInput { Values = ["a"], IsReadOnly = true }, "只读");
        var cities = Named(new TagsInput { Values = ["北京"], Max = 1, ItemsSource = new[] { "北京", "上海" } }, "城市");
        HeadlessUi.Show(new StackPanel { Children = { locked, cities } });

        locked.PasteText("b,c");
        Assert.Equal(["a"], locked.Values);
        Assert.Null(RemoveButton(locked.Tags.Single()));

        cities.Text = "上";
        cities.List.State.Select("上海");
        Assert.Equal(["北京"], cities.Values);
        // 取消选中照常（清空输入，建议不再过滤）
        cities.Text = "";
        cities.List.State.Select("北京");
        Assert.Empty(cities.Values!);
    });

    [Fact]
    [Upstream("TagsInput", "异步搜索（filter={false}）：还没有建议时也是 combobox；没有 items、也不是异步时不是")]
    public Task Remote_suggestions_are_a_combobox_even_when_empty() => HeadlessUi.Run(() =>
    {
        var remote = Named(new TagsInput { LocalFilter = false, IsLoading = true }, "话题");
        var free = Named(new TagsInput(), "关键词");
        HeadlessUi.Show(new StackPanel { Children = { remote, free } });

        Assert.Equal(AutomationControlType.ComboBox, remote.Peer().GetAutomationControlType());
        Assert.Equal(ExpandCollapseState.Collapsed, ((global::Avalonia.Automation.Provider.IExpandCollapseProvider)remote.Peer()).ExpandCollapseState);
        Assert.Equal(AutomationControlType.Edit, free.Peer().GetAutomationControlType());
        Assert.Equal("话题", remote.Peer().GetName());
        // 异步时不在本地过滤：建议原样显示
        remote.ItemsSource = new[] { "前端工程化", "设计系统" };
        remote.Text = "设计";
        Assert.Equal(2, remote.List.State.Options.Count);
    });

    [Fact]
    [Upstream("TagsInput", "不可用：输入框 disabled，外壳 aria-disabled（标签在外壳里），标签没有移除按钮")]
    public Task Disabled_has_no_remove_buttons() => HeadlessUi.Run(() =>
    {
        var input = Named(new TagsInput { Values = ["a", "b"], IsEnabled = false }, "x");
        var enabled = Named(new TagsInput { Values = ["a"] }, "y");
        HeadlessUi.Show(new StackPanel { Children = { input, enabled } });

        Assert.False(input.IsEffectivelyEnabled);
        Assert.All(input.Tags, tag => Assert.Null(RemoveButton(tag)));
        Assert.NotNull(RemoveButton(enabled.Tags.Single()));
    });

    [Fact]
    [Upstream("TagsInput", "TagsInput：组合中按 Enter 不加标签（两种浏览器的写法），组合结束后的 Enter 才加")]
    public Task Enter_while_composing_does_not_add() => HeadlessUi.Run(() =>
    {
        var input = Named(new TagsInput(), "关键词");
        HeadlessUi.Show(input);

        input.Text = "zhong";
        // 输入法组合中的 Enter（确认候选）：平台报 ImeProcessed，交给输入法
        Press(input, Key.ImeProcessed);
        Assert.Null(input.Values);
        input.Text = "中";
        Press(input, Key.Enter);
        Assert.Equal(["中"], input.Values);
    });

    [Fact]
    public Task Clear_removes_every_tag_and_labels_come_from_suggestions() => HeadlessUi.Run(() =>
    {
        var team = new[] { new ListboxOption("xiaoman@example.com", "林小满"), new ListboxOption("chenmo@example.com", "陈默") };
        var input = Named(new TagsInput { ItemsSource = team, Values = ["xiaoman@example.com"], Clearable = true }, "抄送");
        HeadlessUi.Show(input);

        Assert.Equal(["林小满"], Labels(input));
        // 输入建议的文字也换成它的值
        input.Text = "陈默";
        Press(input, Key.Enter);
        Assert.Equal(["xiaoman@example.com", "chenmo@example.com"], input.Values);
        input.Focus();
        var clear = input.GetVisualDescendants().OfType<ClearButton>().Single();
        Assert.True(clear.IsVisible);
        clear.Peer().GetProvider<global::Avalonia.Automation.Provider.IInvokeProvider>()!.Invoke();
        Assert.Empty(input.Values!);
        Assert.False(clear.IsVisible);
    });
}
