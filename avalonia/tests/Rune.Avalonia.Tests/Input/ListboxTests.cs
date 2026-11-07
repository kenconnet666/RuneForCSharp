using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// ListState 与 Listbox：按分组排序、移动跳过不可用项、到头停住、类型搜索；Enter / 空格选中；
/// 列表获得焦点高亮已选项，方向键移动、Enter 选中；多选点击切换、读屏能多选；没有选项时空状态；
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
    public Task Focus_highlights_the_selection_and_keys_move_and_select() => HeadlessUi.Run(() =>
    {
        var listbox = new Listbox { ItemsSource = Fruits, Value = "cherry" };
        var window = HeadlessUi.Show(listbox);

        Assert.Equal(AutomationControlType.List, listbox.Peer().GetAutomationControlType());
        var items = listbox.RealizedItems.ToList();
        Assert.Equal("常见", items.First(item => item.Option!.Value == "apple").Peer().GetHelpText());

        listbox.Focus();
        HeadlessUi.Flush(window);
        Assert.Equal("cherry", listbox.State.Active);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal("durian", listbox.State.Active);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        HeadlessUi.Flush(window);
        Assert.Equal("durian", listbox.Value);
        Assert.True(listbox.RealizedItems.Single(item => item.Option!.Value == "durian").IsSelected);
    });

    [Fact]
    [Upstream("Listbox", "多选：点击切换，aria-multiselectable；没有选项时显示空状态")]
    public Task Multiple_selection_toggles_and_an_empty_list_shows_the_empty_state() => HeadlessUi.Run(() =>
    {
        var listbox = new Listbox { ItemsSource = Fruits, SelectionMode = SelectionMode.Multiple, Values = ["apple"] };
        var window = HeadlessUi.Show(listbox);

        Assert.True(((ISelectionProvider)listbox.Peer()).CanSelectMultiple);
        var cherry = listbox.RealizedItems.Single(item => item.Option!.Value == "cherry");
        ((ISelectionItemProvider)cherry.Peer()).Select();
        HeadlessUi.Flush(window);
        Assert.Equal(["apple", "cherry"], listbox.Values);

        var empty = new Listbox { ItemsSource = Array.Empty<string>() };
        HeadlessUi.Show(empty);
        var state = empty.GetLogicalDescendants().OfType<Empty>().Single();
        Assert.Equal(Provider.GetLang(empty).SelectEmpty, state.Description);
    });

    [Fact]
    [Upstream("Listbox", "toOption：任意形状的数据直接传给 items，渲染函数拿到原始数据")]
    [Upstream("Listbox", "字符串列表：值与文字都是它，不用 toOption")]
    public Task Any_data_maps_through_to_option_and_strings_stand_for_themselves() => HeadlessUi.Run(() =>
    {
        var users = new[] { (Id: "u1", Name: "周一航"), (Id: "u2", Name: "许悠") };
        var listbox = new Listbox { ItemsSource = users, ToOption = item => item is ValueTuple<string, string> user ? new ListboxOption(user.Item1, user.Item2) : throw new InvalidOperationException() };
        HeadlessUi.Show(listbox);
        Assert.Equal(["周一航", "许悠"], listbox.RealizedItems.Select(item => item.Option!.Label));
        Assert.Equal(users[0], listbox.State.Options[0].Item);

        var strings = new Listbox { ItemsSource = new[] { "设计", "研发" } };
        HeadlessUi.Show(strings);
        Assert.Equal(["设计", "研发"], strings.State.Options.Select(entry => entry.Option.Value));
    });

    [Fact]
    [Upstream("Listbox", "onPick：点击与 Enter 都会调用，再选已选的那项也会")]
    public Task Picked_fires_for_clicks_and_enter_even_on_the_selected_option() => HeadlessUi.Run(() =>
    {
        var listbox = new Listbox { ItemsSource = Fruits, Value = "apple" };
        var window = HeadlessUi.Show(listbox);
        var picked = new List<string>();
        listbox.Picked += (_, e) => picked.Add(e.Value);

        var apple = listbox.RealizedItems.Single(item => item.Option!.Value == "apple");
        var at = apple.TranslatePoint(new Point(10, 10), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        listbox.Focus();
        listbox.State.SetActive("apple");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(["apple", "apple"], picked);
        Assert.Equal("apple", listbox.Value);
    });

    [Fact]
    [Upstream("Listbox", "虚拟滚动：一千个选项只渲染看得见的一部分；loading 且没有选项时显示骨架行")]
    public Task Only_visible_rows_are_built_and_loading_shows_skeleton_rows() => HeadlessUi.Run(() =>
    {
        var listbox = new Listbox { ItemsSource = Enumerable.Range(0, 1000).Select(index => $"选项 {index}").ToList(), Height = 300 };
        var window = HeadlessUi.Show(listbox);
        Assert.InRange(listbox.RealizedItems.Count(), 1, 60);

        listbox.Focus();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        HeadlessUi.Flush(window);
        Assert.Contains(listbox.RealizedItems, item => item.Option!.Value == "选项 999");

        var loading = new Listbox { IsLoading = true };
        HeadlessUi.Show(loading);
        HeadlessUi.Until(() => loading.GetLogicalDescendants().OfType<StackPanel>().Any(panel => panel.Name == ListboxParts.Skeleton && panel.IsVisible));
    });

    [Fact]
    public Task Keyboard_focus_shows_the_ring_inside_the_list() => HeadlessUi.Run(() =>
    {
        var list = new Listbox { ItemsSource = Fruits, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top };
        var window = HeadlessUi.Show(list);
        var ring = global::Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(list).OfType<Border>().Single(border => border.Name == "PART_FocusRing");
        Assert.False(ring.IsVisible);
        list.Focus(NavigationMethod.Tab);
        HeadlessUi.Flush(window);
        Assert.True(ring.IsVisible); // 键盘聚焦：里侧 2 像素的焦点环
        Assert.Equal(list.Bounds.Size, ring.Bounds.Size);
    });
}
