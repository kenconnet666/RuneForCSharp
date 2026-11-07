using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// Select：只读不打开、没有清空；空字符串算没有选中；清空写回 null / 空列表；可搜索时才有搜索框那一行；前置图标；
/// 多选的标签可移除、Backspace 移除最后一项、读屏念全部已选；MaxTagCount 收进"+N"；creatable 的"新建"项；OpenChanged 的原因；搜索过滤。
/// </summary>
public class SelectTests
{
    static readonly string[] Cities = ["北京", "上海", "广州"];

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static T Part<T>(Select select, string name) where T : FrameworkElement => (T)select.Template.FindName(name, select);

    /// <summary>按键；搜索框上发预览事件（单行文本框自己吃掉 ↑↓，Select 在预览阶段处理）。</summary>
    static void Press(UIElement element, Key key, bool preview = false) =>
        element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(element), 0, key) { RoutedEvent = preview ? Keyboard.PreviewKeyDownEvent : Keyboard.KeyDownEvent });

    static void Type(UIElement element, string text) =>
        element.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, element, text)) { RoutedEvent = TextCompositionManager.TextInputEvent });

    static void Click(Select select) =>
        ((IExpandCollapseProvider)select.Peer().GetPattern(PatternInterface.ExpandCollapse)).Expand();

    static void Invoke(UIElement button) => ((IInvokeProvider)button.Peer().GetPattern(PatternInterface.Invoke)).Invoke();

    static string Value(Select select) => ((IValueProvider)select.Peer().GetPattern(PatternInterface.Value)).Value;

    [Fact]
    [Upstream("Select", "只读：点击、按键都不打开")]
    public Task Read_only_does_not_open_or_clear() => WpfUi.Run(() =>
    {
        var own = new Select { ItemsSource = Cities, Value = "北京", IsReadOnly = true, Clearable = true };
        var inField = new Select { ItemsSource = Cities, Value = "上海", Clearable = true };
        WpfUi.Show(Host(own, new Field { Label = "城市", IsReadOnly = true, Content = inField }));

        foreach (var select in new[] { own, inField })
        {
            Press(select, Key.Down);
            Click(select);
            WpfUi.Flush();
            Assert.False(select.IsOpen);
            Assert.False(Part<ClearButton>(select, SelectParts.Clear).IsVisible);
            Assert.True(select.ShowsReadOnly);
        }
    });

    [Fact]
    [Upstream("Select", "单选的值是空字符串时算没有选中")]
    public Task An_empty_string_is_no_selection() => WpfUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, Value = "", Placeholder = "选择城市", Clearable = true };
        WpfUi.Show(Host(select));

        Assert.Equal("选择城市", Part<TextBlock>(select, SelectParts.Value).Text);
        Assert.True(select.ShowsPlaceholder);
        Assert.False(Part<ClearButton>(select, SelectParts.Clear).IsVisible);
    });

    [Fact]
    [Upstream("Select", "清空：单选写回 null")]
    public Task Clearing_writes_null_or_an_empty_list() => WpfUi.Run(() =>
    {
        var single = new Select { ItemsSource = Cities, Value = "北京", Clearable = true };
        var multiple = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海"], Clearable = true };
        WpfUi.Show(Host(single, multiple));

        Invoke(Part<ClearButton>(single, SelectParts.Clear));
        Invoke(Part<ClearButton>(multiple, SelectParts.Clear));
        WpfUi.Flush();
        Assert.Null(single.Value);
        Assert.Empty(multiple.Values!);
    });

    [Fact]
    [Upstream("Select", "不可搜索时弹层没有头部")]
    public Task Only_a_searchable_select_has_a_search_box() => WpfUi.Run(() =>
    {
        var plain = new Select { ItemsSource = Cities };
        var searchable = new Select { ItemsSource = Cities, Searchable = true };
        var window = WpfUi.Show(Host(plain, searchable));
        window.Activate();

        Click(plain);
        WpfUi.Flush();
        Assert.Equal(Visibility.Collapsed, plain.Header.Visibility);
        plain.IsOpen = false;
        Click(searchable);
        WpfUi.Flush();
        Assert.True(searchable.SearchBox.IsVisible);
        WpfUi.Until(() => searchable.SearchBox.IsKeyboardFocusWithin);
        searchable.IsOpen = false;
    });

    [Fact]
    [Upstream("Select", "前置图标在触发按钮之前")]
    public Task The_icon_comes_first() => WpfUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, Icon = Lucide.MapPin };
        WpfUi.Show(Host(select));

        var icon = Part<Icon>(select, "PART_Icon");
        Assert.True(icon.IsVisible);
        Assert.True(icon.TranslatePoint(new Point(icon.ActualWidth, 0), select).X <= Part<TextBlock>(select, SelectParts.Value).TranslatePoint(new Point(), select).X);
    });

    [Fact]
    [Upstream("Select", "多选：标签在触发按钮外")]
    public Task Multiple_shows_removable_tags_and_backspace_removes_the_last() => WpfUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海", "广州"] };
        WpfUi.Show(Host(select));

        Assert.Equal(["北京", "上海", "广州"], select.TagRow!.ShownTags.Select(tag => (string)tag.Content!));
        Assert.Equal("北京、上海、广州", Value(select));
        Assert.False(Part<TextBlock>(select, SelectParts.Value).IsVisible);

        var first = select.TagRow.ShownTags.First();
        var remove = WpfUi.Descendants(first).OfType<CloseButton>().Single();
        Assert.False(remove.Focusable);
        Invoke(remove);
        WpfUi.Until(() => select.Values!.Count == 2);
        Assert.Equal(["上海", "广州"], select.Values);

        Press(select, Key.Back);
        WpfUi.Flush();
        Assert.Equal(["上海"], select.Values);
    });

    [Fact]
    [Upstream("Select", "maxTagCount：只显示前几个标签")]
    public Task Max_tag_count_folds_the_rest() => WpfUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海", "广州"], MaxTagCount = 1 };
        var readOnly = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海"], MaxTagCount = 1, IsReadOnly = true };
        WpfUi.Show(Host(select, readOnly));

        Assert.Equal(["北京"], select.TagRow!.ShownTags.Select(tag => (string)tag.Content!));
        Assert.Equal(2, select.TagRow.HiddenCount);
        Assert.Contains(select.TagRow.Children.OfType<Tag>(), tag => tag.IsVisible && (string?)tag.Content == "+2");
        Assert.Empty(readOnly.TagRow!.ShownTags.SelectMany(tag => WpfUi.Descendants(tag).OfType<CloseButton>()));
    });

    [Fact]
    [Upstream("Select", "creatable：输入的文字没有对应选项时")]
    public Task Creatable_offers_a_new_option() => WpfUi.Run(() =>
    {
        string? created = null;
        var select = new Select { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = [], Creatable = true };
        select.OptionCreated += (_, e) => created = e.Text;
        WpfUi.Show(Host(select));

        Click(select);
        WpfUi.Flush();
        select.SearchBox.Text = "上";
        WpfUi.Flush();
        Assert.Equal(["上海", "上"], select.List.State.Options.Select(entry => entry.Option.Value));
        Assert.Equal("新建“上”", select.List.State.Options[^1].Option.Label);
        select.SearchBox.Text = "上海";
        WpfUi.Flush();
        Assert.Equal(["上海"], select.List.State.Options.Select(entry => entry.Option.Value)); // 已有同名的不再新建

        select.SearchBox.Text = "深圳";
        WpfUi.Flush();
        select.List.State.SetActive("深圳");
        Press(select.SearchBox, Key.Enter, preview: true);
        WpfUi.Flush();
        Assert.Equal("深圳", created);
        Assert.Equal(["深圳"], select.Values);
        Assert.Equal("", select.SearchBox.Text);
        select.IsOpen = false;
    });

    [Fact]
    [Upstream("Select", "onOpenChange：点触发按钮打开是 trigger")]
    public Task Open_changes_report_why() => WpfUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities };
        var reasons = new List<(bool, OpenChangeReason)>();
        select.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        WpfUi.Show(Host(select));

        Click(select);
        WpfUi.Flush();
        Press(select, Key.Down);
        Press(select, Key.Enter);
        WpfUi.Flush();
        Assert.Equal("上海", select.Value);
        Assert.False(select.IsOpen);
        select.IsOpen = true;
        select.IsOpen = false;
        Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Select)], reasons);
    });

    [Fact]
    public Task Typing_in_the_search_box_filters_ignoring_case_and_accents() => WpfUi.Run(() =>
    {
        var select = new Select { ItemsSource = new[] { "Café", "Cola", "Tea" }, Searchable = true };
        WpfUi.Show(Host(select));

        Click(select);
        WpfUi.Flush();
        select.SearchBox.Text = "CAFE";
        WpfUi.Flush();
        Assert.Equal(["Café"], select.List.State.Options.Select(entry => entry.Option.Value));
        Assert.Equal("Café", select.List.State.Active);
        select.SearchBox.Text = "x";
        WpfUi.Flush();
        Assert.Empty(select.List.State.Options);
        select.IsOpen = false;
    });

    [Fact]
    public Task Keyboard_opens_moves_and_picks_and_typeahead_selects_while_closed() => WpfUi.Run(() =>
    {
        var select = new Select { ItemsSource = Cities, Value = "北京" };
        WpfUi.Show(Host(select));

        Type(select, "广");
        Assert.Equal("广州", select.Value);
        Assert.False(select.IsOpen);

        Press(select, Key.Up);
        WpfUi.Flush();
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
    public Task Thirty_thousand_selected_options_mount_within_a_second() => WpfUi.Run(() =>
    {
        var items = Enumerable.Range(0, 30_000).Select(index => new ListboxOption(index.ToString(System.Globalization.CultureInfo.InvariantCulture), $"选项 {index}")).ToList();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var select = new Select { ItemsSource = items, SelectionMode = SelectionMode.Multiple, Values = [.. items.Select(item => item.Value)], MaxTagCount = 1 };
        WpfUi.Show(Host(select));
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Equal(29_999, select.TagRow!.HiddenCount);
    });
}
