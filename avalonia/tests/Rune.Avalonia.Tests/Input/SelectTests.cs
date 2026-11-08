using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Select：只读不打开、没有清空；空字符串算没有选中；清空写回 null / 空列表；可搜索时才有搜索框那一行；前置图标；
/// 多选的标签可移除、Backspace 移除最后一项、读屏念全部已选；MaxTagCount 收进"+N"；creatable 的"新建"项；OpenChanged 的原因；搜索过滤。
/// </summary>
public class SelectTests
{
    static readonly string[] Cities = ["北京", "上海", "广州"];

    static T Part<T>(Select select, string name) where T : Control =>
        select.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    static void Press(Control control, Key key) =>
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = control });

    static void Click(Select select) =>
        ((IExpandCollapseProvider)select.Peer()).Expand();

    [Fact]
    [Upstream("Select", "只读：点击、按键都不打开")]
    public Task Read_only_does_not_open_or_clear() => HeadlessUi.Run(() =>
    {
        var own = new Select { ItemsSource = Cities, Value = "北京", IsReadOnly = true, Clearable = true };
        var inField = new Select { ItemsSource = Cities, Value = "上海", Clearable = true };
        var window = HeadlessUi.Show(new StackPanel { Children = { own, new Field { Label = "城市", IsReadOnly = true, Content = inField } } });

        foreach (var select in new[] { own, inField })
        {
            Press(select, Key.Down);
            Click(select);
            HeadlessUi.Flush(window);
            Assert.False(select.IsOpen);
            Assert.False(Part<ClearButton>(select, SelectParts.Clear).IsVisible);
            Assert.Contains(":readonly", select.Classes);
        }
    });

    [Fact]
    [Upstream("Select", "单选的值是空字符串时算没有选中")]
    public Task An_empty_string_is_no_selection() => HeadlessUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, Value = "", Placeholder = "选择城市", Clearable = true };
        HeadlessUi.Show(select);

        Assert.Equal("选择城市", Part<TextBlock>(select, SelectParts.Value).Text);
        Assert.Contains(":placeholder", select.Classes);
        Assert.False(Part<ClearButton>(select, SelectParts.Clear).IsVisible);
    });

    [Fact]
    [Upstream("Select", "清空：单选写回 null")]
    public Task Clearing_writes_null_or_an_empty_list() => HeadlessUi.Run(() =>
    {
        var single = new Select { ItemsSource = Cities, Value = "北京", Clearable = true };
        var multiple = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海"], Clearable = true };
        var window = HeadlessUi.Show(new StackPanel { Children = { single, multiple } });

        ((IInvokeProvider)Part<ClearButton>(single, SelectParts.Clear).Peer()).Invoke();
        ((IInvokeProvider)Part<ClearButton>(multiple, SelectParts.Clear).Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.Null(single.Value);
        Assert.Empty(multiple.Values!);
    });

    [Fact]
    [Upstream("Select", "不可搜索时弹层没有头部")]
    public Task Only_a_searchable_select_has_a_search_box() => HeadlessUi.Run(() =>
    {
        var plain = new Select { ItemsSource = Cities };
        var searchable = new Select { ItemsSource = Cities, Searchable = true };
        var window = HeadlessUi.Show(new StackPanel { Children = { plain, searchable } });

        Click(plain);
        HeadlessUi.Flush(window);
        Assert.False(plain.SearchBox.Parent is Control { IsVisible: true });
        plain.IsOpen = false;
        Click(searchable);
        HeadlessUi.Flush(window);
        Assert.True(searchable.SearchBox.IsEffectivelyVisible);
        HeadlessUi.Until(() => searchable.SearchBox.IsKeyboardFocusWithin);
    });

    [Fact]
    [Upstream("Select", "前置图标在触发按钮之前")]
    public Task The_icon_comes_first() => HeadlessUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, Icon = Lucide.MapPin };
        HeadlessUi.Show(select);

        var icon = Part<Icon>(select, SelectParts.Icon);
        Assert.True(icon.IsVisible);
        Assert.True(icon.Bounds.Right <= Part<TextBlock>(select, SelectParts.Value).Bounds.Left);
    });

    [Fact]
    [Upstream("Select", "多选：标签在触发按钮外")]
    [Upstream("Select", "Select 的搜索框：组合中的退格不删已选的项")]
    public Task Multiple_shows_removable_tags_and_backspace_removes_the_last() => HeadlessUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海", "广州"] };
        var window = HeadlessUi.Show(select);

        Assert.Equal(["北京", "上海", "广州"], select.TagRow!.ShownTags.Select(tag => (string)tag.Content!));
        Assert.Equal("北京、上海、广州", ((IValueProvider)select.Peer()).Value);
        Assert.False(Part<TextBlock>(select, SelectParts.Value).IsVisible);

        var first = select.TagRow.ShownTags.First();
        var remove = first.GetVisualChildren().OfType<CloseButton>().Single();
        Assert.False(remove.Focusable);
        ((IInvokeProvider)remove.Peer()).Invoke();
        HeadlessUi.Until(() => select.Values!.Count == 2);
        Assert.Equal(["上海", "广州"], select.Values);

        Press(select, Key.ImeProcessed); // 输入法组合中的退格：平台报 ImeProcessed，交给输入法
        Assert.Equal(["上海", "广州"], select.Values);
        Press(select, Key.Back);
        HeadlessUi.Flush(window);
        Assert.Equal(["上海"], select.Values);
    });

    [Fact]
    [Upstream("Select", "maxTagCount：只显示前几个标签")]
    public Task Max_tag_count_folds_the_rest() => HeadlessUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海", "广州"], MaxTagCount = 1 };
        var readOnly = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海"], MaxTagCount = 1, IsReadOnly = true };
        HeadlessUi.Show(new StackPanel { Children = { select, readOnly } });

        Assert.Equal(["北京"], select.TagRow!.ShownTags.Select(tag => (string)tag.Content!));
        Assert.Equal(2, select.TagRow.HiddenCount);
        Assert.Contains(select.TagRow.Children.OfType<Tag>(), tag => tag.IsVisible && (string?)tag.Content == "+2");
        Assert.Empty(readOnly.TagRow!.ShownTags.SelectMany(tag => tag.GetVisualChildren().OfType<CloseButton>()));
    });

    [Fact]
    [Upstream("SelectedTags", "“+N”标签的部件属性（propsMore）合并到它上面")]
    public Task The_more_tag_is_a_part_styles_can_reach() => HeadlessUi.Run(() =>
    {
        SelectedTag[] tags = [new("a", "甲"), new("b", "乙"), new("c", "丙")];
        var plain = new SelectedTags { Tags = tags, MaxTagCount = 1 };
        var styled = new SelectedTags { Tags = tags, MaxTagCount = 1 };
        styled.Styles.Add(new Style(x => x.OfType<Tag>().Name(SelectedTagsParts.More)) { Setters = { new Setter(Tag.ToneProperty, StatusTone.Danger) } });
        HeadlessUi.Show(new StackPanel { Children = { plain, styled } });
        static Tag More(SelectedTags row) => row.Children.OfType<Tag>().Single(tag => tag.Name == SelectedTagsParts.More);

        Assert.Equal("+2", More(styled).Content);
        Assert.Equal((TextSize.Sm, StatusVariant.Soft), (More(plain).Size, More(plain).Variant)); // 主题给的默认
        Assert.Equal(StatusTone.Danger, More(styled).Tone);
        Assert.Equal((TextSize.Sm, StatusVariant.Soft), (More(styled).Size, More(styled).Variant)); // 没改的仍是默认
    });

    [Fact]
    [Upstream("Select", "creatable：输入的文字没有对应选项时")]
    public Task Creatable_offers_a_new_option() => HeadlessUi.Run(() =>
    {
        string? created = null;
        var select = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = [], Creatable = true };
        select.OptionCreated += (_, e) => created = e.Text;
        var window = HeadlessUi.Show(select);

        Click(select);
        HeadlessUi.Flush(window);
        select.SearchBox.Text = "上";
        HeadlessUi.Flush(window);
        Assert.Equal(["上海", "上"], select.List.State.Options.Select(entry => entry.Option.Value));
        Assert.Equal("新建“上”", select.List.State.Options[^1].Option.Label);
        select.SearchBox.Text = "上海";
        HeadlessUi.Flush(window);
        Assert.Equal(["上海"], select.List.State.Options.Select(entry => entry.Option.Value)); // 已有同名的不再新建

        select.SearchBox.Text = "深圳";
        HeadlessUi.Flush(window);
        select.List.State.SetActive("深圳");
        Press(select.SearchBox, Key.Enter);
        HeadlessUi.Flush(window);
        Assert.Equal("深圳", created);
        Assert.Equal(["深圳"], select.Values);
        Assert.Equal("", select.SearchBox.Text);
    });

    [Fact]
    [Upstream("Select", "onOpenChange：点触发按钮打开是 trigger")]
    public Task Open_changes_report_why() => HeadlessUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities };
        var reasons = new List<(bool, OpenChangeReason)>();
        select.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        var window = HeadlessUi.Show(select);

        Click(select);
        HeadlessUi.Flush(window);
        Press(select, Key.Down);
        Press(select, Key.Enter);
        HeadlessUi.Flush(window);
        Assert.Equal("上海", select.Value);
        Assert.False(select.IsOpen);
        select.IsOpen = true;
        select.IsOpen = false;
        Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Select)], reasons);
    });

    [Fact]
    public Task Typing_in_the_search_box_filters_ignoring_case_and_accents() => HeadlessUi.Run(() =>
    {
        var select = new Select { ItemsSource = new[] { "Café", "Cola", "Tea" }, Searchable = true };
        var window = HeadlessUi.Show(select);

        Click(select);
        HeadlessUi.Flush(window);
        select.SearchBox.Text = "CAFE";
        HeadlessUi.Flush(window);
        Assert.Equal(["Café"], select.List.State.Options.Select(entry => entry.Option.Value));
        Assert.Equal("Café", select.List.State.Active);
        select.SearchBox.Text = "x";
        HeadlessUi.Flush(window);
        Assert.Empty(select.List.State.Options);
    });

    [Fact]
    public Task Keyboard_opens_moves_and_picks_and_typeahead_selects_while_closed() => HeadlessUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, Value = "北京" };
        var window = HeadlessUi.Show(select);

        select.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "广", Source = select });
        Assert.Equal("广州", select.Value);
        Assert.False(select.IsOpen);

        Press(select, Key.Up);
        HeadlessUi.Flush(window);
        Assert.True(select.IsOpen);
        Assert.Equal("广州", select.List.State.Active); // 高亮已选项
        Press(select, Key.Up);
        Press(select, Key.Space);
        Assert.Equal("上海", select.Value);
        Assert.False(select.IsOpen);
        Assert.Equal(AutomationControlType.ComboBox, select.Peer().GetAutomationControlType());
    });

    [Fact]
    [Upstream("Select", "性能：三万个选项全部选中时挂载在 1 秒内")]
    public Task Thirty_thousand_selected_options_mount_within_a_second() => HeadlessUi.Run(() =>
    {
        var items = Enumerable.Range(0, 30_000).Select(index => new ListboxOption(index.ToString(System.Globalization.CultureInfo.InvariantCulture), $"选项 {index}")).ToList();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var select = new Select { ItemsSource = items, SelectionMode = SelectionMode.Multiple, Values = [.. items.Select(item => item.Value)], MaxTagCount = 1 };
        HeadlessUi.Show(select);
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Equal(29_999, select.TagRow!.HiddenCount);
    });
}
