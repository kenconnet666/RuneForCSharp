using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>
/// ListState 与 Listbox：按分组排序、移动跳过不可用项、到头停住、类型搜索；Enter / 空格选中；
/// 列表获得焦点高亮已选项，方向键移动、Enter 选中；多选切换、读屏能多选；没有选项时空状态；
/// ToOption 与字符串列表；Picked 在点击与 Enter 时发，再选已选的那项也发；一千个选项只建看得见的几十行，加载中骨架。
/// </summary>
public class ListboxTests
{
    static readonly ListboxOption[] Fruits =
    [
        new("apple", "Apple") { Group = "常见" },
        new("durian", "Durian") { Group = "热带" },
        new("banana", "Banana") { Group = "常见", IsDisabled = true },
        new("cherry", "Cherry") { Group = "常见" },
    ];

    static StackPanel Host(params UIElement[] children)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        foreach (var child in children) host.Children.Add(child);
        return host;
    }

    static void Press(UIElement owner, Key key)
    {
        owner.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(owner), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        WpfUi.Flush();
    }

    [Fact]
    [Upstream("Listbox", "按分组排序；移动跳过不可用项，到头停住；类型搜索")]
    public void The_state_groups_skips_disabled_options_and_searches_by_type()
    {
        IReadOnlyList<string> selected = [];
        var list = new ListState(next => selected = next);
        list.SetItems(Fruits, null);

        Assert.Equal(["apple", "banana", "cherry", "durian"], list.Options.Select(entry => entry.Option.Value));
        list.Move(1);
        Assert.Equal("apple", list.Active);
        list.Move(1);
        Assert.Equal("cherry", list.Active);
        list.MoveToEnd(last: true);
        list.Move(1);
        Assert.Equal("durian", list.Active);
        Assert.True(list.Typeahead("c"));
        Assert.Equal("cherry", list.Active);
        Assert.False(list.Select("banana"));
        Assert.True(list.Select("cherry"));
        Assert.Equal(["cherry"], selected);
    }

    [Fact]
    [Upstream("Listbox", "键盘：Enter / 空格选中")]
    public void Enter_and_space_select()
    {
        IReadOnlyList<string> selected = ["apple"];
        var list = new ListState(next => selected = next) { Mode = SelectionMode.Multiple, Selected = selected };
        list.SetItems(Fruits, null);

        list.ActivateDefault();
        Assert.Equal("apple", list.Active);
        Assert.True(list.Handle(ListKey.End));
        Assert.True(list.Handle(ListKey.Space));
        Assert.Equal(["apple", "durian"], selected);
        Assert.False(list.Handle(ListKey.Space, space: false));
    }

    [Fact]
    [Upstream("Listbox", "role=")]
    public Task Focus_highlights_the_selection_and_keys_move_and_select() => WpfUi.Run(() =>
    {
        var listbox = new Listbox { ItemsSource = Fruits, Value = "cherry" };
        var window = WpfUi.Show(Host(listbox));
        window.Activate();

        Assert.Equal(AutomationControlType.List, listbox.Peer().GetAutomationControlType());
        var items = listbox.RealizedItems.ToList();
        Assert.Equal("常见", items.First(item => item.Option!.Value == "apple").Peer().GetHelpText());

        listbox.Focus();
        WpfUi.Flush();
        Assert.Equal("cherry", listbox.State.Active);
        Press(listbox, Key.Down);
        Assert.Equal("durian", listbox.State.Active);
        Press(listbox, Key.Enter);
        Assert.Equal("durian", listbox.Value);
        Assert.True(listbox.RealizedItems.Single(item => item.Option!.Value == "durian").IsSelected);
    });

    [Fact]
    [Upstream("Listbox", "多选：点击切换，aria-multiselectable；没有选项时显示空状态")]
    public Task Multiple_selection_toggles_and_an_empty_list_shows_the_empty_state() => WpfUi.Run(() =>
    {
        var listbox = new Listbox { ItemsSource = Fruits, SelectionMode = SelectionMode.Multiple, Values = ["apple"] };
        var empty = new Listbox { ItemsSource = Array.Empty<string>() };
        WpfUi.Show(Host(listbox, empty));

        Assert.True(((ISelectionProvider)listbox.Peer().GetPattern(PatternInterface.Selection)).CanSelectMultiple);
        var cherry = listbox.RealizedItems.Single(item => item.Option!.Value == "cherry");
        ((ISelectionItemProvider)cherry.Peer().GetPattern(PatternInterface.SelectionItem)).Select();
        WpfUi.Flush();
        Assert.Equal(["apple", "cherry"], listbox.Values);

        var state = WpfUi.Descendants(empty).OfType<Empty>().Single();
        Assert.Equal(Provider.GetLang(empty).SelectEmpty, state.Description);
    });

    [Fact]
    [Upstream("Listbox", "toOption：任意形状的数据直接传给 items，渲染函数拿到原始数据")]
    [Upstream("Listbox", "字符串列表：值与文字都是它，不用 toOption")]
    public Task Any_data_maps_through_to_option_and_strings_stand_for_themselves() => WpfUi.Run(() =>
    {
        var users = new[] { (Id: "u1", Name: "周一航"), (Id: "u2", Name: "许悠") };
        var listbox = new Listbox { ItemsSource = users, ToOption = item => item is ValueTuple<string, string> user ? new ListboxOption(user.Item1, user.Item2) : throw new InvalidOperationException() };
        var strings = new Listbox { ItemsSource = new[] { "设计", "研发" } };
        WpfUi.Show(Host(listbox, strings));
        Assert.Equal(["周一航", "许悠"], listbox.RealizedItems.Select(item => item.Option!.Label));
        Assert.Equal(users[0], listbox.State.Options[0].Item);
        Assert.Equal(["设计", "研发"], strings.State.Options.Select(entry => entry.Option.Value));
    });

    [Fact]
    [Upstream("Listbox", "onPick：点击与 Enter 都会调用，再选已选的那项也会")]
    public Task Picked_fires_for_clicks_and_enter_even_on_the_selected_option() => WpfUi.Run(() =>
    {
        var listbox = new Listbox { ItemsSource = Fruits, Value = "apple" };
        WpfUi.Show(Host(listbox));
        var picked = new List<string>();
        listbox.Picked += (_, e) => picked.Add(e.Value);

        var apple = listbox.RealizedItems.Single(item => item.Option!.Value == "apple");
        apple.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
        apple.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        listbox.State.SetActive("apple");
        Press(listbox, Key.Enter);
        Assert.Equal(["apple", "apple"], picked);
        Assert.Equal("apple", listbox.Value);
    });

    [Fact]
    [Upstream("Listbox", "虚拟滚动：一千个选项只渲染看得见的一部分；loading 且没有选项时显示骨架行")]
    public Task Only_visible_rows_are_built_and_loading_shows_skeleton_rows() => WpfUi.Run(() =>
    {
        var listbox = new Listbox { ItemsSource = Enumerable.Range(0, 1000).Select(index => $"选项 {index}").ToList(), Height = 300 };
        var loading = new Listbox { IsLoading = true };
        WpfUi.Show(Host(listbox, loading));
        Assert.InRange(listbox.RealizedItems.Count(), 1, 60);

        listbox.Focus();
        Press(listbox, Key.End);
        Assert.Contains(listbox.RealizedItems, item => item.Option!.Value == "选项 999");

        WpfUi.Until(() => WpfUi.Descendants(loading).OfType<Skeleton>().Any(bar => bar.IsVisible));
    });
}
