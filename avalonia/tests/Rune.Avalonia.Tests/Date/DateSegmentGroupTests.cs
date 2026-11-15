using System.Globalization;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>DateSegmentGroup：每段能聚焦、打字逐段输入且焦点跟着跳；↑↓ 交给 DateSegments；只读时只能换段；读屏是"组名 段名"的 spinbutton，没填的念"未填写"。</summary>
public class DateSegmentGroupTests
{
    static (DateSegmentGroup Group, Func<DateFieldValues?> Value, global::Avalonia.Controls.Window Window) Create(bool readOnly = false)
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
        var window = HeadlessUi.Show(group);
        return (group, () => value, window);
    }

    [Fact]
    public Task Typing_fills_segments_and_focus_follows() => HeadlessUi.Run(() =>
    {
        var (group, value, window) = Create();
        Assert.Equal("年/月/日", group.Text);
        group.Boxes[0].Focus();
        window.KeyTextInput("2026");
        Assert.True(group.Boxes[1].IsFocused);
        window.KeyTextInput("3");
        Assert.True(group.Boxes[2].IsFocused);
        window.KeyTextInput("15");
        Assert.Equal(new DateFieldValues { Year = 2026, Month = 3, Day = 15 }, value());
        Assert.Equal("2026/03/15", group.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Equal(16, value()?.Day);
    });

    [Fact]
    public Task Segments_read_as_named_spin_buttons() => HeadlessUi.Run(() =>
    {
        var (group, _, _) = Create();
        var peer = group.Boxes[0].Peer();
        Assert.Equal((AutomationControlType.Spinner, "出生日期 年"), (peer.GetAutomationControlType(), peer.GetName()));
        Assert.Equal("未填写", ((IValueProvider)peer).Value);
        Assert.Equal((1d, 9999d), (((IRangeValueProvider)peer).Minimum, ((IRangeValueProvider)peer).Maximum));
    });

    [Fact]
    public Task Read_only_groups_only_move_between_segments() => HeadlessUi.Run(() =>
    {
        var (group, value, window) = Create(readOnly: true);
        group.Boxes[0].Focus();
        window.KeyTextInput("2026");
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        Assert.Null(value());
        Assert.Equal("年/月/日", group.Text);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.True(group.Boxes[1].IsFocused);
    });
}
