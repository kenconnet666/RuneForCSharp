using System.Globalization;

namespace Rune.WinForms.Tests;

/// <summary>DateSegmentGroupControl：打字逐段输入且聚焦的段跟着跳；Tab 先在段之间走；↑↓ 交给 DateSegments；只读时只能换段；每段是"组名 段名"的 SpinButton，没填的念"未填写"。</summary>
public class DateSegmentGroupTests
{
    static (DateSegmentGroupControl Group, Func<DateFieldValues?> Value, Form Window) Create(bool readOnly = false)
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
        var group = new DateSegmentGroupControl { Segments = state, IsReadOnly = readOnly, AccessibleName = "出生日期" };
        var provider = new ProviderControl { Lang = Lang.ZhCn, Dock = DockStyle.Fill };
        provider.Controls.Add(group);
        var window = WinFormsUi.Show(provider);
        group.Focus();
        WinFormsUi.Pump();
        return (group, () => value, window);
    }

    [Fact]
    public Task Typing_fills_segments_and_the_focused_segment_follows() => WinFormsUi.Run(() =>
    {
        var (group, value, window) = Create();
        using var _ = window;
        Assert.Equal("年/月/日", group.SegmentText);
        Assert.Equal(DateSegmentType.Year, group.Segments!.FocusedSegment);
        group.TypeText("2026");
        Assert.Equal(DateSegmentType.Month, group.Segments.FocusedSegment);
        group.TypeText("3");
        group.TypeText("15");
        Assert.Equal(new DateFieldValues { Year = 2026, Month = 3, Day = 15 }, value());
        Assert.Equal("2026/03/15", group.SegmentText);
        group.Press(Keys.Up);
        Assert.Equal(16, value()?.Day);
    });

    [Fact]
    public Task Tab_walks_the_segments_before_leaving() => WinFormsUi.Run(() =>
    {
        var (group, _, window) = Create();
        using var _ = window;
        var process = typeof(Control).GetMethod("ProcessDialogKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.True((bool)process.Invoke(group, [Keys.Tab])!);
        Assert.Equal(DateSegmentType.Month, group.Segments!.FocusedSegment);
        Assert.True((bool)process.Invoke(group, [Keys.Tab | Keys.Shift])!);
        Assert.Equal(DateSegmentType.Year, group.Segments.FocusedSegment);
    });

    [Fact]
    public Task Segments_read_as_named_spin_buttons() => WinFormsUi.Run(() =>
    {
        var (group, _, window) = Create();
        using var _ = window;
        var year = group.AccessibilityObject.GetChild(0)!;
        Assert.Equal((AccessibleRole.SpinButton, "出生日期 年", "未填写"), (year.Role, year.Name, year.Value));
        Assert.Equal(3, group.AccessibilityObject.GetChildCount());
    });

    [Fact]
    public Task Read_only_groups_only_move_between_segments() => WinFormsUi.Run(() =>
    {
        var (group, value, window) = Create(readOnly: true);
        using var _ = window;
        group.TypeText("2026");
        Assert.False(group.Press(Keys.Up));
        Assert.Null(value());
        Assert.True(group.Press(Keys.Right));
        Assert.Equal(DateSegmentType.Month, group.Segments!.FocusedSegment);
    });
}
