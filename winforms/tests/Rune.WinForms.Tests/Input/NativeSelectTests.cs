using System.Runtime.InteropServices;

namespace Rune.WinForms.Tests;

/// <summary>NativeSelectControl：字符串的值就是文字、带组的按组排（组名是选不了的标题）、没选时没有选中行；选了写回值、组名跳到下一项；只读时按键、点击换不了；放进字段取标签、说明。</summary>
public class NativeSelectTests
{
    const int WmKeyDown = 0x0100, VkDown = 0x28;

    static readonly object[] Cities =
    [
        new ListboxOption("sh", "上海") { Group = "华东" }, new ListboxOption("gz", "广州") { Group = "华南" },
        new ListboxOption("hz", "杭州") { Group = "华东" }, "其他",
        new ListboxOption("hk", "香港") { Group = "华南", IsDisabled = true },
    ];

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

    static void WithForm(Action body, params Control[] controls)
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(300, 300), Size = new Size(400, 300), FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false };
        var stack = new FlexControl { Direction = FlexDirection.Column, Dock = DockStyle.Fill };
        stack.Controls.AddRange(controls);
        form.Controls.Add(stack);
        form.Show();
        WinFormsUi.Pump();
        body();
    }

    static string[] Rows(NativeSelectControl select) => [.. select.Items.Cast<object>().Select(row => row.ToString()!)];

    [Fact]
    [Upstream("NativeSelect", "选项与分组")]
    public Task Strings_and_groups_with_a_placeholder() => WinFormsUi.Run(() =>
    {
        var select = new NativeSelectControl { Options = Cities, PlaceholderText = "选择城市" };
        WithForm(() =>
        {
            // 没有组的在前，各组按第一次出现的顺序；组名是一行选不了的标题
            Assert.Equal(["其他", "华东", "上海", "杭州", "华南", "广州", "香港"], Rows(select));
            Assert.Equal(ComboBoxStyle.DropDownList, select.DropDownStyle);
            Assert.Equal(-1, select.SelectedIndex);
            Assert.Null(select.Value);
        }, select);
    });

    [Fact]
    [Upstream("NativeSelect", "选了写回值")]
    public Task Picking_writes_the_value_and_skips_headers() => WinFormsUi.Run(() =>
    {
        var select = new NativeSelectControl { Options = Cities };
        var changes = 0;
        select.ValueChanged += (_, _) => changes++;
        WithForm(() =>
        {
            select.SelectedIndex = 0;
            Assert.Equal("其他", select.Value); // 字符串的值就是文字
            select.SelectedIndex = 1; // 往下落到组名"华东"：跳到"上海"
            Assert.Equal(2, select.SelectedIndex);
            Assert.Equal("sh", select.Value);
            select.SelectedIndex = 6; // 不可用的"香港"在最后：往回找到"广州"
            Assert.Equal("gz", select.Value);
            Assert.Equal(3, changes);
            select.Value = "hz";
            Assert.Equal(3, select.SelectedIndex);
            select.Value = null;
            Assert.Equal(-1, select.SelectedIndex);
        }, select);
    });

    [Fact]
    [Upstream("NativeSelect", "只读：别的选项选不了")]
    public Task Read_only_keeps_the_value() => WinFormsUi.Run(() =>
    {
        var own = new NativeSelectControl { Options = Cities, Value = "sh", IsReadOnly = true };
        var inField = new NativeSelectControl { Options = Cities, Value = "sh" };
        var field = new FieldControl { Label = "城市", IsReadOnly = true, Content = inField };
        WithForm(() =>
        {
            foreach (var select in new[] { own, inField })
            {
                SendMessage(select.Handle, WmKeyDown, VkDown, 0);
                WinFormsUi.Pump();
                Assert.Equal("sh", select.Value);
                select.SelectedIndex = 3;
                Assert.Equal("sh", select.Value);
                Assert.Equal(2, select.SelectedIndex);
            }
            own.IsReadOnly = false;
            SendMessage(own.Handle, WmKeyDown, VkDown, 0);
            WinFormsUi.Pump();
            Assert.Equal("hz", own.Value);
        }, own, field);
    });

    [Fact]
    [Upstream("NativeSelect", "放进 Field")]
    public Task Field_names_and_describes_it() => WinFormsUi.Run(() =>
    {
        var select = new NativeSelectControl { Options = Cities };
        var field = new FieldControl { Label = "收货城市", Error = "请选择城市", Content = select };
        var named = new NativeSelectControl { Options = Cities, AccessibleName = "自己的名字" };
        WithForm(() =>
        {
            Assert.Equal("收货城市", select.AccessibilityObject.Name);
            Assert.Contains("请选择城市", select.AccessibleDescription);
            Assert.Equal(AccessibleRole.ComboBox, select.AccessibilityObject.Role);
            field.Label = "配送城市";
            WinFormsUi.Pump();
            Assert.Equal("配送城市", select.AccessibleName);
            Assert.Equal("自己的名字", named.AccessibleName);
        }, field, named);
    });
}
