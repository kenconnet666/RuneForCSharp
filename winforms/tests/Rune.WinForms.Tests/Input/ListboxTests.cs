namespace Rune.WinForms.Tests;

/// <summary>
/// ListState 与 ListboxControl：按分组排序、移动跳过不可用项、到头停住、类型搜索；Enter / 空格选中；
/// 列表获得焦点高亮已选项，方向键移动、Enter 选中；多选切换；没有选项时空状态；
/// ToOption 与字符串列表；Picked 在点击与 Enter 时发，再选已选的那项也发；一千个选项不建控件，加载中骨架。
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

    static void Press(Control control, Keys key)
    {
        control.Focus();
        typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [new KeyEventArgs(key)]);
        WinFormsUi.Pump();
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
    [Upstream("Listbox", "不可用：点选项不改值，也不触发 onPick")]
    public Task Disabled_listboxes_ignore_picks() => WinFormsUi.Run(() =>
    {
        var picks = 0;
        var listbox = new ListboxControl { ItemsSource = Fruits, Value = "cherry", Width = 300, Enabled = false };
        listbox.Picked += (_, _) => picks++;
        using var form = WinFormsUi.Show(listbox);

        listbox.Pick("apple");
        Assert.Equal(("cherry", 0), (listbox.Value, picks));
    });

    [Fact]
    [Upstream("Listbox", "role=")]
    public Task Focus_highlights_the_selection_and_keys_move_and_select() => WinFormsUi.Run(() =>
    {
        var listbox = new ListboxControl { ItemsSource = Fruits, Value = "cherry", Width = 300 };
        using var form = WinFormsUi.Show(listbox);

        Assert.Equal(AccessibleRole.List, listbox.AccessibilityObject.Role);
        Assert.Equal("常见", listbox.AccessibilityObject.GetChild(0)!.Description);
        listbox.Focus();
        WinFormsUi.Pump();
        Assert.Equal("cherry", listbox.State.Active);
        Press(listbox, Keys.Down);
        Assert.Equal("durian", listbox.State.Active);
        Press(listbox, Keys.Enter);
        Assert.Equal("durian", listbox.Value);
        Assert.True(listbox.AccessibilityObject.GetChild(3)!.State.HasFlag(AccessibleStates.Selected));
    });

    [Fact]
    [Upstream("Listbox", "多选：点击切换，aria-multiselectable；没有选项时显示空状态")]
    public Task Multiple_selection_toggles_and_an_empty_list_shows_the_empty_state() => WinFormsUi.Run(() =>
    {
        var listbox = new ListboxControl { ItemsSource = Fruits, SelectionMode = SelectionMode.Multiple, Values = ["apple"], Width = 300 };
        var empty = new ListboxControl { ItemsSource = Array.Empty<string>(), Width = 300, Top = 200 };
        using var form = WinFormsUi.Show(listbox, empty);

        listbox.AccessibilityObject.GetChild(2)!.DoDefaultAction();
        Assert.Equal(["apple", "cherry"], listbox.Values);
        Assert.True(empty.EmptyPart.Visible);
        Assert.Equal(ProviderControl.LangOf(empty).SelectEmpty, empty.EmptyPart.Description);
    });

    [Fact]
    [Upstream("Listbox", "toOption：任意形状的数据直接传给 items，渲染函数拿到原始数据")]
    [Upstream("Listbox", "字符串列表：值与文字都是它，不用 toOption")]
    public Task Any_data_maps_through_to_option_and_strings_stand_for_themselves() => WinFormsUi.Run(() =>
    {
        var users = new[] { (Id: "u1", Name: "周一航"), (Id: "u2", Name: "许悠") };
        var listbox = new ListboxControl { ItemsSource = users, ToOption = item => item is ValueTuple<string, string> user ? new ListboxOption(user.Item1, user.Item2) : throw new InvalidOperationException() };
        var strings = new ListboxControl { ItemsSource = new[] { "设计", "研发" } };
        Assert.Equal(["周一航", "许悠"], listbox.State.Options.Select(entry => entry.Option.Label));
        Assert.Equal(users[0], listbox.State.Options[0].Item);
        Assert.Equal(["设计", "研发"], strings.State.Options.Select(entry => entry.Option.Value));
    });

    [Fact]
    [Upstream("Listbox", "onPick：点击与 Enter 都会调用，再选已选的那项也会")]
    public Task Picked_fires_for_clicks_and_enter_even_on_the_selected_option() => WinFormsUi.Run(() =>
    {
        var listbox = new ListboxControl { ItemsSource = Fruits, Value = "apple", Width = 300 };
        using var form = WinFormsUi.Show(listbox);
        var picked = new List<string>();
        listbox.Picked += (_, e) => picked.Add(e.Value);

        listbox.AccessibilityObject.GetChild(0)!.DoDefaultAction();
        listbox.State.SetActive("apple");
        Press(listbox, Keys.Enter);
        Assert.Equal(["apple", "apple"], picked);
        Assert.Equal("apple", listbox.Value);
    });

    [Fact]
    [Upstream("Listbox", "虚拟滚动：一千个选项只渲染看得见的一部分；loading 且没有选项时显示骨架行")]
    public Task A_thousand_options_build_no_controls_and_loading_shows_skeleton_rows() => WinFormsUi.Run(() =>
    {
        var listbox = new ListboxControl { ItemsSource = Enumerable.Range(0, 1000).Select(index => $"选项 {index}").ToList(), Width = 300, Height = 300 };
        var loading = new ListboxControl { IsLoading = true, Width = 300, Top = 320 };
        using var form = WinFormsUi.Show(listbox, loading);
        Assert.Empty(listbox.View.Controls);

        Press(listbox, Keys.End);
        Assert.Equal("选项 999", listbox.State.Active);
        Assert.True(listbox.View.RectangleToScreen(listbox.View.ClientRectangle).Bottom >= listbox.RectangleToScreen(listbox.ClientRectangle).Bottom);

        WinFormsUi.Wait(TimeSpan.FromMilliseconds(400));
        Assert.True(loading.SkeletonPart.Visible);
    });
}
