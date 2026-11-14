using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>
/// CommandPalette：输入写回 InputText、远程搜索时 Filter 全放行、加载中列表换骨架；默认按文字与说明过滤、每次打开清空输入；
/// ↑↓ 移动、Enter 执行（报告那一项并关闭）；快捷键 Ctrl+K 开关；默认触发按钮的名称与快捷键。
/// </summary>
public class CommandPaletteTests
{
    static readonly ListboxOption[] Pages =
    [
        new("/button", "Button 按钮") { Description = "触发操作" },
        new("/dialog", "Dialog 对话框") { Description = "模态窗口" },
        new("/select", "Select 下拉选择") { Description = "从选项里选" },
    ];

    static void Press(Control control, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = control });

    static string[] Shown(CommandPalette palette) => [.. palette.List.State.Options.Select(entry => entry.Option.Value)];

    [Fact]
    [Upstream("CommandPalette", "输入写回 inputValue")]
    public Task Input_writes_back_and_remote_results_are_shown_as_is() => HeadlessUi.Run(() =>
    {
        var palette = new CommandPalette { ItemsSource = Pages, Filter = (_, _) => true, IsLoading = true };
        var window = HeadlessUi.Show(palette);
        palette.IsOpen = true;
        HeadlessUi.Flush(window);

        palette.Input.Text = "zzz";
        HeadlessUi.Flush(window);
        Assert.Equal("zzz", palette.InputText);
        Assert.Equal(["/button", "/dialog", "/select"], Shown(palette)); // 远程搜索：结果就是 ItemsSource
        Assert.True(palette.List.IsLoading);
        palette.IsOpen = false;
    });

    [Fact]
    [Upstream("CommandPalette", "默认按文字过滤；每次打开清空输入")]
    public Task Filters_by_label_and_description_and_clears_on_open() => HeadlessUi.Run(() =>
    {
        var palette = new CommandPalette { ItemsSource = Pages };
        var window = HeadlessUi.Show(palette);
        palette.IsOpen = true;
        HeadlessUi.Flush(window);

        palette.Input.Text = "DIALOG";
        HeadlessUi.Flush(window);
        Assert.Equal(["/dialog"], Shown(palette));
        palette.Input.Text = "从选项";
        HeadlessUi.Flush(window);
        Assert.Equal(["/select"], Shown(palette)); // 说明也算
        palette.IsOpen = false;
        palette.IsOpen = true;
        HeadlessUi.Flush(window);
        Assert.Equal("", palette.Input.Text);
        Assert.Equal(3, Shown(palette).Length);
        palette.IsOpen = false;
    });

    [Fact]
    [Upstream("CommandPalette", "CommandPalette：组合中的 ↓ 不移动高亮、Enter 不执行；组合结束后照常")]
    public Task Arrows_move_and_enter_invokes_and_closes() => HeadlessUi.Run(() =>
    {
        object? invoked = null;
        var palette = new CommandPalette { ItemsSource = Pages };
        var reasons = new List<(bool, OpenChangeReason)>();
        palette.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        palette.ItemInvoked += (_, e) => invoked = e.Item;
        var window = HeadlessUi.Show(palette);

        Press(palette, Key.K, KeyModifiers.Control); // 快捷键
        HeadlessUi.Flush(window);
        Assert.True(palette.IsOpen);
        HeadlessUi.Until(() => palette.Input.IsKeyboardFocusWithin || palette.Input.IsFocused);
        Assert.Equal("/button", palette.List.State.Active); // 打开时高亮第一个
        Press(palette.Input, Key.ImeProcessed); // 输入法组合中的 ↓、Enter：平台报 ImeProcessed，交给输入法
        Assert.Equal("/button", palette.List.State.Active);
        Assert.Null(invoked);
        Press(palette.Input, Key.Down);
        Press(palette.Input, Key.Enter);
        HeadlessUi.Flush(window);
        Assert.Same(Pages[1], invoked);
        Assert.False(palette.IsOpen);
        Assert.Equal([(true, OpenChangeReason.Imperative), (false, OpenChangeReason.Select)], reasons);
    });

    [Fact]
    public Task The_default_trigger_opens_and_names_itself_by_its_text() => HeadlessUi.Run(() =>
    {
        var palette = new CommandPalette { ItemsSource = Pages };
        var window = HeadlessUi.Show(palette);

        Assert.Equal("搜索文档", AutomationProperties.GetName(palette.DefaultTrigger));
        Assert.Equal("Control+K", AutomationProperties.GetAcceleratorKey(palette.DefaultTrigger));
        ((IInvokeProvider)palette.DefaultTrigger.Peer()).Invoke();
        HeadlessUi.Flush(window);
        Assert.True(palette.IsOpen);
        Assert.Equal("搜索", palette.Dialog.Title);
        palette.IsOpen = false;
    });

    [Fact]
    public Task Item_command_runs_with_the_item_unless_the_event_handles_it() => HeadlessUi.Run(() =>
    {
        var command = new TestCommand();
        var palette = new CommandPalette { ItemsSource = Pages, ItemCommand = command };
        var window = HeadlessUi.Show(palette);
        palette.IsOpen = true;
        HeadlessUi.Flush(window);

        palette.List.RaiseEvent(new ListboxPickedEventArgs(Listbox.PickedEvent, "/dialog"));
        Assert.Same(Pages[1], Assert.Single(command.Executed));

        palette.ItemInvoked += (_, e) => e.Handled = true; // 事件处理了：不再调命令
        palette.IsOpen = true;
        HeadlessUi.Flush(window);
        palette.List.RaiseEvent(new ListboxPickedEventArgs(Listbox.PickedEvent, "/button"));
        Assert.Single(command.Executed);
    });
}
