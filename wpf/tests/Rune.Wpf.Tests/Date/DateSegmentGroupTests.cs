using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>DateSegmentGroup：每段能聚焦、打字逐段输入且焦点跟着跳；↑↓ 交给 DateSegments；只读时只能换段；读屏是"组名 段名"的 spinbutton，没填的念"未填写"。</summary>
public class DateSegmentGroupTests
{
    static (DateSegmentGroup Group, Func<DateFieldValues?> Value) Create(bool readOnly = false)
    {
        DateFieldValues? value = null;
        var state = new DateSegments([DateSegmentType.Year, DateSegmentType.Month, DateSegmentType.Day])
        {
            Culture = CultureInfo.GetCultureInfo("zh-CN"),
            Value = () => value,
            Changed = next => value = next,
            PlaceholderValue = () => new DateFieldValues { Year = 2026, Month = 10, Day = 20 },
            Placeholders = Lang.ZhCn.DateFieldPlaceholders,
        };
        var group = new DateSegmentGroup { Segments = state, IsReadOnly = readOnly };
        AutomationProperties.SetName(group, "出生日期");
        Provider.SetLang(group, Lang.ZhCn);
        group.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(group);
        WpfUi.Flush();
        return (group, () => value);
    }

    static void Type(string text)
    {
        var target = (UIElement)Keyboard.FocusedElement;
        target.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, target, text)) { RoutedEvent = TextCompositionManager.TextInputEvent });
    }

    static void Press(Key key)
    {
        var target = (UIElement)Keyboard.FocusedElement;
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    [Fact]
    public Task Typing_fills_segments_and_focus_follows() => WpfUi.Run(() =>
    {
        var (group, value) = Create();
        Assert.Equal("年/月/日", group.Text);
        group.Boxes[0].Focus();
        Type("2026");
        Assert.True(group.Boxes[1].IsKeyboardFocused);
        Type("3");
        Assert.True(group.Boxes[2].IsKeyboardFocused);
        Type("15");
        Assert.Equal(new DateFieldValues { Year = 2026, Month = 3, Day = 15 }, value());
        Assert.Equal("2026/03/15", group.Text);
        Press(Key.Up);
        Assert.Equal(16, value()?.Day);
    });

    [Fact]
    public Task Segments_read_as_named_spin_buttons() => WpfUi.Run(() =>
    {
        var (group, _) = Create();
        var peer = group.Boxes[0].Peer();
        Assert.Equal((AutomationControlType.Spinner, "出生日期 年"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal("未填写", ((IValueProvider)peer.GetPattern(PatternInterface.Value)).Value);
        var range = (IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue);
        Assert.Equal((1d, 9999d), (range.Minimum, range.Maximum));
    });

    [Fact]
    public Task Read_only_groups_only_move_between_segments() => WpfUi.Run(() =>
    {
        var (group, value) = Create(readOnly: true);
        group.Boxes[0].Focus();
        Type("2026");
        Press(Key.Up);
        Assert.Null(value());
        Assert.Equal("年/月/日", group.Text);
        Press(Key.Right);
        Assert.True(group.Boxes[1].IsKeyboardFocused);
    });
}
