using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// MenuControl：触发器打开，↓ 聚焦第一项、↑ 聚焦最后一项；方向键循环移动、按字母跳；执行一项后关闭（原因 select），复选项不关；Esc 关闭并回到触发器，Tab 关闭往后走；
/// 快捷键在菜单关着时也执行；说明是描述；浮层以触发器为名称（写了名称时用它）；分组以标题为名称，单选组跟着 Value 勾选、点了写回；子菜单；右键菜单。
/// </summary>
public class MenuTests
{
    /// <summary>屏幕上的窗体（浮层按屏幕定位、要激活，屏幕外的窗体核对不了）。</summary>
    static void WithForm(Action<Form> body, params Control[] controls)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(300, 300), Size = new Size(400, 300),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        flow.Controls.AddRange(controls);
        form.Controls.Add(flow);
        form.Show();
        form.Activate();
        WinFormsUi.Pump();
        try
        {
            body(form);
        }
        finally
        {
            foreach (var menu in controls.OfType<MenuControl>()) menu.IsOpen = false;
        }
    }

    static void Raise(Control control, string method, EventArgs args)
    {
        control.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, [args.GetType()])!.Invoke(control, [args]);
        WinFormsUi.Pump();
    }

    static void Press(Control control, Keys key) => Raise(control, "OnKeyDown", new KeyEventArgs(key));

    /// <summary>对话框键（Esc、Tab）：经消息预处理交给控件、窗体。</summary>
    static void DialogKey(Control control, Keys key)
    {
        var message = Message.Create(control.Handle, 0x100, (IntPtr)key, IntPtr.Zero);
        control.PreProcessMessage(ref message);
        WinFormsUi.Pump();
    }

    [Fact]
    [Upstream("Menu", "有说明时名称指向文字、描述指向说明；没有说明时两者都不加")]
    public Task Descriptions_are_descriptions_not_names() => WinFormsUi.Run(() =>
    {
        var document = new MenuItemControl { Text = "文档", Description = "空白的文字文档" };
        var code = new MenuCheckboxItemControl { Text = "代码视图", Description = "显示行号与折叠" };
        var help = new MenuLinkItemControl { Text = "帮助", Description = "使用说明", Href = new Uri("https://example.com/help") };
        var folder = new MenuItemControl { Text = "文件夹" };
        var menu = new MenuControl { Trigger = new ButtonControl { Text = "新建" } };
        menu.Items.AddRange([document, code, help, folder]);
        WithForm(_ =>
        {
            Assert.Equal(
                [("文档", "空白的文字文档"), ("代码视图", "显示行号与折叠"), ("帮助", "使用说明"), ("文件夹", null)],
                new MenuItemControlBase[] { document, code, help, folder }.Select(item => (item.AccessibilityObject.Name, item.AccessibilityObject.Description)));
            Assert.Equal(AccessibleRole.MenuItem, document.AccessibilityObject.Role);
        }, menu);
    });

    [Fact]
    [Upstream("Menu", "默认以触发器的文字为名称；写了 aria-label 时不再加 aria-labelledby（它会盖过 aria-label）")]
    public Task The_popup_is_named_by_the_trigger_unless_named() => WinFormsUi.Run(() =>
    {
        var byTrigger = new MenuControl { Trigger = new ButtonControl { Text = "更多" } };
        byTrigger.Items.Add(new MenuItemControl { Text = "复制" });
        var named = new MenuControl { Trigger = new ButtonControl { Text = "…" }, AccessibleName = "行操作" };
        named.Items.Add(new MenuItemControl { Text = "删除" });
        WithForm(_ =>
        {
            byTrigger.IsOpen = true;
            named.IsOpen = true;
            WinFormsUi.Pump();
            Assert.Equal(AccessibleRole.MenuPopup, byTrigger.Surface.AccessibleRole);
            Assert.Equal(("更多", "行操作"), (byTrigger.Surface.AccessibilityObject.Name, named.Surface.AccessibilityObject.Name));
        }, byTrigger, named);
    });

    [Fact]
    [Upstream("MenuGroup", "分组以标题为名称；单选项跟着 _value 勾选，点另一项写回")]
    public Task Groups_are_named_and_radio_items_follow_the_value() => WinFormsUi.Run(() =>
    {
        var refresh = new MenuGroupControl { Label = "操作" };
        refresh.Controls.Add(new MenuItemControl { Text = "刷新" });
        var byName = new MenuRadioItemControl { Text = "名称", Value = "name" };
        var byDate = new MenuRadioItemControl { Text = "日期", Value = "date" };
        var sort = new MenuRadioGroupControl { Label = "排序", Value = "name" };
        sort.Controls.AddRange([byName, byDate]);
        var menu = new MenuControl { Trigger = new ButtonControl { Text = "视图" } };
        menu.Items.AddRange([refresh, sort]);
        WithForm(_ =>
        {
            Assert.Equal(["操作", "排序"], new MenuGroupControl[] { refresh, sort }.Select(group => group.AccessibilityObject.Name));
            Assert.Equal(AccessibleRole.Grouping, sort.AccessibilityObject.Role);
            bool Checked(MenuRadioItemControl item) => item.AccessibilityObject.State.HasFlag(AccessibleStates.Checked);
            Assert.Equal([true, false], new[] { byName, byDate }.Select(Checked));
            byDate.AccessibilityObject.DoDefaultAction();
            Assert.Equal("date", sort.Value);
            Assert.Equal([false, true], new[] { byName, byDate }.Select(Checked));
        }, menu);
    });

    [Fact]
    [Upstream("Menu", "Menu：执行一项是 select")]
    public Task Running_an_item_closes_with_select_but_checkboxes_stay() => WinFormsUi.Run(() =>
    {
        var ran = 0;
        var rename = new MenuItemControl { Text = "重命名" };
        rename.Click += (_, _) => ran++;
        var grid = new MenuCheckboxItemControl { Text = "显示网格" };
        var menu = new MenuControl { Trigger = new ButtonControl { Text = "操作" } };
        menu.Items.AddRange([grid, rename]);
        var reasons = new List<(bool, OpenChangeReason)>();
        menu.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        WithForm(_ =>
        {
            menu.IsOpen = true;
            WinFormsUi.Pump();
            grid.PerformClick();
            Assert.True(grid.IsChecked);
            Assert.True(menu.IsOpen); // 复选项默认不关
            rename.PerformClick();
            Assert.Equal(1, ran);
            Assert.False(menu.IsOpen);
            Assert.Equal([(false, OpenChangeReason.Select)], reasons);
        }, menu);
    });

    [Fact]
    public Task Keyboard_opens_moves_types_ahead_and_closes() => WinFormsUi.Run(() =>
    {
        var trigger = new ButtonControl { Text = "操作" };
        var (copy, disabled, export, remove) = (new MenuItemControl { Text = "复制" }, new MenuItemControl { Text = "粘贴", Enabled = false }, new MenuItemControl { Text = "导出" }, new MenuItemControl { Text = "删除", Tone = MenuItemTone.Danger });
        var menu = new MenuControl { Trigger = trigger };
        menu.Items.AddRange([copy, disabled, export, remove]);
        var reasons = new List<(bool, OpenChangeReason)>();
        menu.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        var next = new ButtonControl { Text = "下一个" };
        WithForm(_ =>
        {
            trigger.Focus();
            Raise(trigger, "OnKeyDown", new KeyEventArgs(Keys.Up)); // ↑ 打开并聚焦最后一项
            Assert.True(remove.Focused);
            Press(remove, Keys.Down); // 循环到第一项
            Assert.True(copy.Focused);
            Press(copy, Keys.Down); // 跳过不可用的
            Assert.True(export.Focused);
            Raise(export, "OnKeyPress", new KeyPressEventArgs('删')); // 按字（母）跳到以它开头的项
            Assert.True(remove.Focused);
            Assert.Equal(ProviderControl.ThemeOf(remove).Colors.DangerText, remove.ForeColor); // 破坏性操作是危险色

            DialogKey(remove, Keys.Escape); // Esc 关闭，焦点回到触发器
            Assert.False(menu.IsOpen);
            Assert.True(trigger.Focused);

            Raise(trigger, "OnKeyDown", new KeyEventArgs(Keys.Down)); // ↓ 打开并聚焦第一项
            Assert.True(copy.Focused);
            DialogKey(copy, Keys.Tab); // Tab 关闭，焦点往后走
            Assert.False(menu.IsOpen);
            Assert.True(next.Focused);
            Assert.Equal([(true, OpenChangeReason.Trigger), (false, OpenChangeReason.Escape), (true, OpenChangeReason.Trigger), (false, OpenChangeReason.Focus)], reasons);
        }, menu, next);
    });

    [Fact]
    public Task Shortcuts_run_items_while_the_menu_is_closed() => WinFormsUi.Run(() =>
    {
        var copies = 0;
        var copy = new MenuItemControl { Text = "复制", Shortcut = "Ctrl+D" };
        copy.Click += (_, _) => copies++;
        var grid = new MenuCheckboxItemControl { Text = "显示网格", Shortcut = "Ctrl+G", Enabled = false };
        var menu = new MenuControl { Trigger = new ButtonControl { Text = "操作" } };
        menu.Items.AddRange([copy, grid]);
        WithForm(_ =>
        {
            Assert.True(menu.OnHotkey(Keys.D | Keys.Control));
            Assert.False(menu.OnHotkey(Keys.G | Keys.Control)); // 不可用的不执行
            Assert.Equal((1, false, false), (copies, grid.IsChecked, menu.IsOpen));
            Assert.Equal("Control+D", copy.AccessibilityObject.KeyboardShortcut); // 读屏从快捷键念
        }, menu);
    });

    [Fact]
    [Upstream("MenuSub", "是带子菜单的项")]
    public Task Submenus_expand_and_keys_go_in_and_out() => WinFormsUi.Run(() =>
    {
        var pdf = new MenuItemControl { Text = "PDF" };
        var excel = new MenuItemControl { Text = "Excel" };
        var export = new MenuSubControl { Text = "导出为" };
        export.Items.AddRange([pdf, excel]);
        var menu = new MenuControl { Trigger = new ButtonControl { Text = "更多" } };
        menu.Items.AddRange([new MenuItemControl { Text = "复制" }, export]);
        WithForm(_ =>
        {
            menu.IsOpen = true;
            WinFormsUi.Pump();
            var states = export.AccessibilityObject.State;
            Assert.True(states.HasFlag(AccessibleStates.HasPopup) && states.HasFlag(AccessibleStates.Collapsed));
            Assert.Equal("导出为", export.AccessibilityObject.Name);

            export.PerformClick(); // 点了展开，焦点进到第一项；外面的菜单不关
            WinFormsUi.Pump();
            Assert.True(export.IsOpen);
            Assert.True(export.AccessibilityObject.State.HasFlag(AccessibleStates.Expanded));
            Assert.True(menu.IsOpen);
            Assert.True(pdf.Focused);
            Assert.Equal("导出为", export.Surface.AccessibilityObject.Name); // 子菜单以这一项为名称

            Press(pdf, Keys.Left); // ← 只关子菜单，回到这一项
            Assert.Equal((false, true), (export.IsOpen, menu.IsOpen));
            Assert.True(export.Focused);

            Press(export, Keys.Right); // → 打开并聚焦第一项
            Assert.True(pdf.Focused);
            excel.PerformClick(); // 子菜单里执行一项：整串都关
            Assert.Equal((false, false), (export.IsOpen, menu.IsOpen));
        }, menu);
    });

    [Fact]
    [Upstream("ContextMenu", "在区域里右键打开菜单（阻止浏览器自己的）；在菜单上右键不再打开")]
    public Task Context_menus_open_on_request_and_give_focus_back() => WinFormsUi.Run(() =>
    {
        var file = new ButtonControl { Text = "文件" };
        var rename = new MenuItemControl { Text = "重命名" };
        var region = new ContextMenuControl { Content = file, Size = new Size(200, 80) };
        region.Items.Add(rename);
        var reasons = new List<(bool, OpenChangeReason)>();
        region.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        WithForm(_ =>
        {
            file.Focus();
            Assert.True(region.Request(file, atPointer: false)); // 键盘的菜单键：从获得焦点的控件下方弹出
            WinFormsUi.Pump();
            Assert.True(region.IsOpen);
            Assert.Equal("更多操作", region.Menu.Surface.AccessibilityObject.Name); // 菜单的名称默认取语言包
            Assert.True(rename.Focused);
            Assert.False(region.Request(rename, atPointer: true)); // 在菜单上右键不再打开
            Assert.Equal([(true, OpenChangeReason.Trigger)], reasons);

            DialogKey(rename, Keys.Escape);
            Assert.False(region.IsOpen);
            Assert.True(file.Focused); // 关闭后焦点回到打开前的控件
        }, region);
    });

    [Fact]
    [Upstream("ContextMenu", "disabled 时不弹出，浏览器的右键菜单照常")]
    public Task Disabled_context_menus_stay_closed() => WinFormsUi.Run(() =>
    {
        var file = new SmallControl { Text = "文件" };
        var region = new ContextMenuControl { Content = file, Enabled = false, Size = new Size(200, 80) };
        region.Items.Add(new MenuItemControl { Text = "重命名" });
        WithForm(_ =>
        {
            Assert.False(region.Request(file, atPointer: true)); // 没拦下：平台自己的右键菜单照常
            Assert.False(region.IsOpen);
        }, region);
    });
}
