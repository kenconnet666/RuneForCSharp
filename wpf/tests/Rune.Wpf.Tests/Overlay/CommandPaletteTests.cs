using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

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

    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(child);
        return host;
    }

    static void Press(UIElement target, Key key) =>
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    static string[] Shown(CommandPalette palette) => [.. palette.List.State.Options.Select(entry => entry.Option.Value)];

    [Fact]
    [Upstream("CommandPalette", "输入写回 inputValue")]
    public Task Input_writes_back_and_remote_results_are_shown_as_is() => WpfUi.Run(() =>
    {
        var palette = new CommandPalette { ItemsSource = Pages, Filter = (_, _) => true, IsLoading = true };
        WpfUi.Show(Host(palette));
        palette.IsOpen = true;
        WpfUi.Flush();

        palette.Input.Text = "zzz";
        WpfUi.Flush();
        Assert.Equal("zzz", palette.InputText);
        Assert.Equal(["/button", "/dialog", "/select"], Shown(palette)); // 远程搜索：结果就是 ItemsSource
        Assert.True(palette.List.IsLoading);
        palette.IsOpen = false;
    });

    [Fact]
    [Upstream("CommandPalette", "默认按文字过滤；每次打开清空输入")]
    public Task Filters_by_label_and_description_and_clears_on_open() => WpfUi.Run(() =>
    {
        var palette = new CommandPalette { ItemsSource = Pages };
        WpfUi.Show(Host(palette));
        palette.IsOpen = true;
        WpfUi.Flush();

        palette.Input.Text = "DIALOG";
        Assert.Equal(["/dialog"], Shown(palette));
        palette.Input.Text = "从选项";
        Assert.Equal(["/select"], Shown(palette)); // 说明也算
        palette.IsOpen = false;
        palette.IsOpen = true;
        WpfUi.Flush();
        Assert.Equal("", palette.Input.Text);
        Assert.Equal(3, Shown(palette).Length);
        palette.IsOpen = false;
    });

    [Fact]
    [Upstream("CommandPalette", "CommandPalette：组合中的 ↓ 不移动高亮、Enter 不执行；组合结束后照常")]
    public Task Arrows_move_and_enter_invokes_and_closes() => WpfUi.Run(() =>
    {
        object? invoked = null;
        var palette = new CommandPalette { ItemsSource = Pages };
        var reasons = new List<(bool, OpenChangeReason)>();
        palette.OpenChanged += (_, e) => reasons.Add((e.IsOpen, e.Reason));
        palette.ItemInvoked += (_, e) => invoked = e.Item;
        WpfUi.Show(Host(palette));

        Assert.False(palette.OnHotkey(Key.K, ModifierKeys.None));
        Assert.True(palette.OnHotkey(Key.K, ModifierKeys.Control)); // 快捷键
        WpfUi.Flush();
        Assert.True(palette.IsOpen);
        WpfUi.Until(() => palette.Input.IsKeyboardFocusWithin);
        Assert.Equal("/button", palette.List.State.Active); // 打开时高亮第一个
        Press(palette.Input, Key.ImeProcessed); // 输入法组合中的 ↓、Enter：平台报 ImeProcessed，交给输入法
        Assert.Equal("/button", palette.List.State.Active);
        Assert.Null(invoked);
        Press(palette.Input, Key.Down);
        Press(palette.Input, Key.Enter);
        WpfUi.Flush();
        Assert.Same(Pages[1], invoked);
        Assert.False(palette.IsOpen);
        Assert.Equal([(true, OpenChangeReason.Imperative), (false, OpenChangeReason.Select)], reasons);
    });

    [Fact]
    public Task The_default_trigger_opens_and_names_itself_by_its_text() => WpfUi.Run(() =>
    {
        var palette = new CommandPalette { ItemsSource = Pages };
        WpfUi.Show(Host(palette));

        Assert.Equal("搜索文档", AutomationProperties.GetName(palette.DefaultTrigger));
        Assert.Equal("Control+K", AutomationProperties.GetAcceleratorKey(palette.DefaultTrigger));
        ((IInvokeProvider)palette.DefaultTrigger.Peer().GetPattern(PatternInterface.Invoke)).Invoke();
        WpfUi.Flush();
        Assert.True(palette.IsOpen);
        Assert.Equal("搜索", palette.Dialog.Title);
        palette.IsOpen = false;
    });
}
