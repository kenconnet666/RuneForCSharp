using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>
/// CommandPaletteControl：输入写回 InputText、远程搜索时 Filter 全放行、加载中列表换骨架；默认按文字与说明过滤、每次打开清空输入；
/// ↑↓ 移动、Enter 执行（报告那一项并关闭）；快捷键 Ctrl+K 开关；默认触发按钮的名称与快捷键；面板停在窗体上部。
/// </summary>
public class CommandPaletteTests
{
    static readonly ListboxOption[] Pages =
    [
        new("/button", "Button 按钮") { Description = "触发操作" },
        new("/dialog", "Dialog 对话框") { Description = "模态窗口" },
        new("/select", "Select 下拉选择") { Description = "从选项里选" },
    ];

    static void Press(TextBox input, Keys key) =>
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(input, [new KeyEventArgs(key)]);

    static string[] Shown(CommandPaletteControl palette) => [.. palette.List.State.Options.Select(entry => entry.Option.Value)];

    /// <summary>屏幕上的窗体（遮罩与面板按屏幕定位）。</summary>
    static void WithForm(CommandPaletteControl palette, Action<Form> body)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(200, 200), Size = new Size(800, 600),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        form.Controls.Add(palette);
        form.Show();
        form.Activate();
        WinFormsUi.Pump();
        try
        {
            body(form);
        }
        finally
        {
            palette.IsOpen = false;
        }
    }

    [Fact]
    [Upstream("CommandPalette", "输入写回 inputValue")]
    public Task Input_writes_back_and_remote_results_are_shown_as_is() => WinFormsUi.Run(() =>
    {
        var palette = new CommandPaletteControl { ItemsSource = Pages, Filter = (_, _) => true, IsLoading = true };
        var changes = 0;
        palette.InputTextChanged += (_, _) => changes++;
        WithForm(palette, _ =>
        {
            palette.IsOpen = true;
            WinFormsUi.Pump();
            palette.Input.Text = "zzz";
            Assert.Equal("zzz", palette.InputText);
            Assert.Equal(1, changes);
            Assert.Equal(["/button", "/dialog", "/select"], Shown(palette)); // 远程搜索：结果就是 ItemsSource
            Assert.True(palette.List.IsLoading);
        });
    });

    [Fact]
    [Upstream("CommandPalette", "默认按文字过滤；每次打开清空输入")]
    public Task Filters_by_label_and_description_and_clears_on_open() => WinFormsUi.Run(() =>
    {
        var palette = new CommandPaletteControl { ItemsSource = Pages };
        WithForm(palette, _ =>
        {
            palette.IsOpen = true;
            WinFormsUi.Pump();
            palette.Input.Text = "DIALOG";
            Assert.Equal(["/dialog"], Shown(palette));
            palette.Input.Text = "从选项";
            Assert.Equal(["/select"], Shown(palette)); // 说明也算
            palette.IsOpen = false;
            palette.IsOpen = true;
            WinFormsUi.Pump();
            Assert.Equal("", palette.Input.Text);
            Assert.Equal(3, Shown(palette).Length);
        });
    });

    [Fact]
    public Task Arrows_move_and_enter_invokes_and_closes() => WinFormsUi.Run(() =>
    {
        object? invoked = null;
        var palette = new CommandPaletteControl { ItemsSource = Pages };
        var reasons = new List<(bool, OpenChangeReason)>();
        palette.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        palette.ItemInvoked += (_, e) => invoked = e.Item;
        WithForm(palette, _ =>
        {
            Assert.False(palette.OnHotkey(Keys.K));
            Assert.True(palette.OnHotkey(Keys.K | Keys.Control)); // 快捷键
            WinFormsUi.Pump();
            Assert.True(palette.IsOpen);
            Assert.True(palette.Input.Input.Focused);
            Assert.Equal("/button", palette.List.State.Active); // 打开时高亮第一个
            Press(palette.Input.Input, Keys.Down);
            Press(palette.Input.Input, Keys.Enter);
            WinFormsUi.Pump();
            Assert.Same(Pages[1], invoked);
            Assert.False(palette.IsOpen);
        });
        Assert.Equal([(true, OpenChangeReason.Imperative), (false, OpenChangeReason.Select)], reasons);
    });

    [Fact]
    public Task The_default_trigger_opens_names_itself_by_its_text_and_the_panel_sits_high() => WinFormsUi.Run(() =>
    {
        var palette = new CommandPaletteControl { ItemsSource = Pages };
        WithForm(palette, form =>
        {
            Assert.Equal("搜索文档", palette.DefaultTrigger.AccessibilityObject.Name);
            Assert.Equal("Control+K", palette.DefaultTrigger.AccessibilityObject.KeyboardShortcut);
            palette.DefaultTrigger.PerformClick();
            WinFormsUi.Pump();
            Assert.True(palette.IsOpen);
            Assert.Equal("搜索", palette.Dialog.Title);
            var area = form.RectangleToScreen(form.ClientRectangle);
            Assert.Equal(area.Top + (int)(area.Height * 0.12f), palette.Dialog.Surface.Backdrop!.Panel.Top);
        });
    });
}
