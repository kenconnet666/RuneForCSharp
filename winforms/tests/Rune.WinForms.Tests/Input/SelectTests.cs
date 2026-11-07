using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// SelectControl：只读不打开、没有清空；空字符串算没有选中；清空写回 null / 空列表；可搜索时才有搜索框那一行；前置图标；
/// 多选的标签可移除、Backspace 移除最后一项、读屏念全部已选；MaxTagCount 收进"+N"；creatable 的"新建"项；OpenChanged 的原因；搜索过滤；键盘。
/// </summary>
public class SelectTests
{
    static readonly string[] Cities = ["北京", "上海", "广州"];

    static void Raise(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [args]);

    static void Press(Control control, Keys key) => Raise(control, "OnKeyDown", new KeyEventArgs(key));

    static void Type(Control control, char key) => Raise(control, "OnKeyPress", new KeyPressEventArgs(key));

    static void Click(SelectControl select) => select.AccessibilityObject.DoDefaultAction();

    /// <summary>屏幕上的窗体（弹出层按屏幕定位）。</summary>
    static void WithForm(Action<Form> body, params Control[] controls)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(300, 300), Size = new Size(500, 400),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        var stack = new FlexControl { Direction = FlexDirection.Column, Dock = DockStyle.Fill };
        stack.Controls.AddRange(controls);
        form.Controls.Add(stack);
        form.Show();
        form.Activate();
        WinFormsUi.Pump();
        try
        {
            body(form);
        }
        finally
        {
            foreach (var select in controls.OfType<SelectControl>()) select.IsOpen = false;
        }
    }

    [Fact]
    [Upstream("Select", "只读：点击、按键都不打开")]
    public Task Read_only_does_not_open_or_clear() => WinFormsUi.Run(() =>
    {
        var own = new SelectControl { ItemsSource = Cities, Value = "北京", IsReadOnly = true, Clearable = true };
        var inField = new SelectControl { ItemsSource = Cities, Value = "上海", Clearable = true };
        var field = new FieldControl { Label = "城市", IsReadOnly = true, Content = inField };
        WithForm(_ =>
        {
            foreach (var select in new[] { own, inField })
            {
                Press(select, Keys.Down);
                Click(select);
                WinFormsUi.Pump();
                Assert.False(select.IsOpen);
                Assert.False(select.ClearPart.Visible);
            }
        }, own, field);
    });

    [Fact]
    [Upstream("Select", "单选的值是空字符串时算没有选中")]
    public Task An_empty_string_is_no_selection() => WinFormsUi.Run(() =>
    {
        var select = new SelectControl { ItemsSource = Cities, Value = "", PlaceholderText = "选择城市", Clearable = true };
        WithForm(_ =>
        {
            Assert.Equal("选择城市", select.ValuePart.Text);
            Assert.Equal(TextTone.Muted, select.ValuePart.Tone);
            Assert.False(select.ClearPart.Visible);
        }, select);
    });

    [Fact]
    [Upstream("Select", "清空：单选写回 null")]
    public Task Clearing_writes_null_or_an_empty_list() => WinFormsUi.Run(() =>
    {
        var single = new SelectControl { ItemsSource = Cities, Value = "北京", Clearable = true };
        var multiple = new SelectControl { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海"], Clearable = true };
        WithForm(_ =>
        {
            single.ClearPart.PerformClick();
            multiple.ClearPart.PerformClick();
            Assert.Null(single.Value);
            Assert.Empty(multiple.Values);
        }, single, multiple);
    });

    [Fact]
    [Upstream("Select", "不可搜索时弹层没有头部")]
    public Task Only_a_searchable_select_has_a_search_box() => WinFormsUi.Run(() =>
    {
        var plain = new SelectControl { ItemsSource = Cities };
        var searchable = new SelectControl { ItemsSource = Cities, Searchable = true };
        WithForm(_ =>
        {
            Click(plain);
            WinFormsUi.Pump();
            Assert.True(plain.Surface.Visible);
            Assert.False(plain.Header.Visible);
            Assert.True(plain.Focused); // 不激活的弹出层：焦点留在触发框上
            plain.IsOpen = false;
            Click(searchable);
            WinFormsUi.Pump();
            Assert.True(searchable.Header.Visible);
            Assert.True(searchable.SearchBox.ContainsFocus);
        }, plain, searchable);
    });

    [Fact]
    [Upstream("Select", "前置图标在触发按钮之前")]
    public Task The_icon_comes_first() => WinFormsUi.Run(() =>
    {
        var select = new SelectControl { ItemsSource = Cities, Icon = Lucide.MapPin, Width = 300 };
        WithForm(_ =>
        {
            Assert.True(select.IconPart.Visible);
            Assert.True(select.IconPart.Right <= select.ValuePart.Left);
        }, select);
    });

    [Fact]
    [Upstream("Select", "多选：标签在触发按钮外")]
    public Task Multiple_shows_removable_tags_and_backspace_removes_the_last() => WinFormsUi.Run(() =>
    {
        var select = new SelectControl { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海", "广州"] };
        WithForm(_ =>
        {
            Assert.Equal(["北京", "上海", "广州"], select.TagRow.ShownTags.Select(tag => tag.Text));
            Assert.Equal("北京、上海、广州", select.AccessibilityObject.Value);
            Assert.Equal(AccessibleRole.ComboBox, select.AccessibilityObject.Role);
            Assert.False(select.ValuePart.Visible);

            var remove = select.TagRow.ShownTags.First().RemoveButton!;
            Assert.False(remove.CanSelect);
            remove.PerformClick();
            Assert.Equal(["上海", "广州"], select.Values);

            Press(select, Keys.Back);
            Assert.Equal(["上海"], select.Values);
        }, select);
    });

    [Fact]
    [Upstream("Select", "maxTagCount：只显示前几个标签")]
    public Task Max_tag_count_folds_the_rest() => WinFormsUi.Run(() =>
    {
        var select = new SelectControl { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海", "广州"], MaxTagCount = 1 };
        var readOnly = new SelectControl { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Values = ["北京", "上海"], MaxTagCount = 1, IsReadOnly = true };
        WithForm(_ =>
        {
            Assert.Equal(["北京"], select.TagRow.ShownTags.Select(tag => tag.Text));
            Assert.Equal(2, select.TagRow.HiddenCount);
            Assert.Contains(select.TagRow.Controls.OfType<TagControl>(), tag => tag.Visible && tag.Text == "+2");
            Assert.All(readOnly.TagRow.ShownTags, tag => Assert.Null(tag.RemoveButton));
        }, select, readOnly);
    });

    [Fact]
    [Upstream("Select", "creatable：输入的文字没有对应选项时")]
    public Task Creatable_offers_a_new_option() => WinFormsUi.Run(() =>
    {
        string? created = null;
        var select = new SelectControl { ItemsSource = Cities, SelectionMode = SelectionMode.Multiple, Creatable = true };
        select.OptionCreated += (_, e) => created = e.Text;
        WithForm(_ =>
        {
            Click(select);
            WinFormsUi.Pump();
            select.SearchBox.Text = "上";
            Assert.Equal(["上海", "上"], select.List.State.Options.Select(entry => entry.Option.Value));
            Assert.Equal("新建“上”", select.List.State.Options[^1].Option.Label);
            select.SearchBox.Text = "上海";
            Assert.Equal(["上海"], select.List.State.Options.Select(entry => entry.Option.Value));

            select.SearchBox.Text = "深圳";
            select.List.State.SetActive("深圳");
            Press(select.SearchBox.Input, Keys.Enter);
            Assert.Equal("深圳", created);
            Assert.Equal(["深圳"], select.Values);
            Assert.Equal("", select.SearchBox.Text);
        }, select);
    });

    [Fact]
    [Upstream("Select", "onOpenChange：点触发按钮打开是 trigger")]
    public Task Open_changes_report_why() => WinFormsUi.Run(() =>
    {
        var select = new SelectControl { ItemsSource = Cities };
        var reasons = new List<(bool, OpenChangeReason)>();
        select.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        WithForm(_ =>
        {
            Click(select);
            WinFormsUi.Pump();
            Press(select, Keys.Down);
            Press(select, Keys.Enter);
            Assert.Equal("上海", select.Value);
            Assert.False(select.IsOpen);
            select.IsOpen = true;
            select.IsOpen = false;
            Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Select)], reasons);
        }, select);
    });

    [Fact]
    public Task Typing_in_the_search_box_filters_ignoring_case_and_accents() => WinFormsUi.Run(() =>
    {
        var select = new SelectControl { ItemsSource = new[] { "Café", "Cola", "Tea" }, Searchable = true };
        WithForm(_ =>
        {
            Click(select);
            WinFormsUi.Pump();
            select.SearchBox.Text = "CAFE";
            Assert.Equal(["Café"], select.List.State.Options.Select(entry => entry.Option.Value));
            Assert.Equal("Café", select.List.State.Active);
            select.SearchBox.Text = "x";
            Assert.Empty(select.List.State.Options);
        }, select);
    });

    [Fact]
    public Task Keyboard_opens_moves_and_picks_and_typeahead_selects_while_closed() => WinFormsUi.Run(() =>
    {
        var select = new SelectControl { ItemsSource = Cities, Value = "北京" };
        WithForm(_ =>
        {
            Type(select, '广');
            Assert.Equal("广州", select.Value);
            Assert.False(select.IsOpen);

            Press(select, Keys.Up);
            Assert.True(select.IsOpen);
            Assert.Equal("广州", select.List.State.Active);
            Press(select, Keys.Up);
            Press(select, Keys.Space);
            Assert.Equal("上海", select.Value);
            Assert.False(select.IsOpen);
        }, select);
    });

    [Fact]
    public Task The_panel_matches_the_shell_width_below_it() => WinFormsUi.Run(() =>
    {
        var select = new SelectControl { ItemsSource = Cities, Width = 240 };
        WithForm(_ =>
        {
            Click(select);
            WinFormsUi.Pump();
            var panel = select.Surface.Backdrop!.Panel;
            var shell = select.RectangleToScreen(select.ClientRectangle);
            Assert.Equal(shell.Width, panel.Width);
            Assert.Equal(shell.Left, panel.Left);
            Assert.True(panel.Top > shell.Bottom);
            Assert.False(select.Surface.Form.Activates);
        }, select);
    });

    [Fact]
    [Upstream("Select", "性能：三万个选项全部选中时挂载在 1 秒内")]
    public Task Thirty_thousand_selected_options_mount_within_a_second() => WinFormsUi.Run(() =>
    {
        var items = Enumerable.Range(0, 30_000).Select(index => new ListboxOption(index.ToString(System.Globalization.CultureInfo.InvariantCulture), $"选项 {index}")).ToList();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var select = new SelectControl { ItemsSource = items, SelectionMode = SelectionMode.Multiple, Values = [.. items.Select(item => item.Value)], MaxTagCount = 1 };
        WithForm(_ => { }, select);
        Assert.InRange(clock.ElapsedMilliseconds, 0, 1000);
        Assert.Equal(29_999, select.TagRow.HiddenCount);
    });
}
