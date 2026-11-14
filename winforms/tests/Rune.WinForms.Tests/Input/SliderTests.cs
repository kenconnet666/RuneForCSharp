using System.Reflection;

namespace Rune.WinForms.Tests;

/// <summary>SliderControl：读屏的滑块与取值、键盘、对齐步长、点击轨道、不可用、范围选择（Tab 在两个滑块之间）、刻度。</summary>
public class SliderTests
{
    static void Press(Control control, Keys key) =>
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [new KeyEventArgs(key)]);

    static void Mouse(Control control, string handler, int x, int y) =>
        typeof(Control).GetMethod(handler, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);

    /// <summary>点轨道上 ratio 处（0–1）：轨道两端各缩进半个滑块。</summary>
    static void Click(SliderControl slider, double ratio)
    {
        var start = slider.PointOf(slider.Min);
        var end = slider.PointOf(slider.Max);
        var x = (int)Math.Round(start.X + ratio * (end.X - start.X));
        Mouse(slider, "OnMouseDown", x, (int)start.Y);
        Mouse(slider, "OnMouseUp", x, (int)start.Y);
    }

    static ProviderControl Host(params Control[] children)
    {
        var host = new ProviderControl { Dock = DockStyle.Fill, Lang = Lang.ZhCn };
        host.Controls.AddRange(children);
        return host;
    }

    [Fact]
    [Upstream("Slider", "滑块是 role=")]
    public Task Control_is_a_slider_with_value_text_and_name() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Value = 30, ValueText = v => $"{v}%", AccessibleName = "音量", Width = 200 };
        using var form = WinFormsUi.Show(Host(slider));

        var accessible = slider.AccessibilityObject;
        Assert.Equal(AccessibleRole.Slider, accessible.Role);
        Assert.Equal("音量", accessible.Name);
        Assert.Equal("30%", accessible.Value);
        Assert.True(slider.TabStop);
    });

    [Fact]
    [Upstream("Slider", "键盘：方向键 ±step、Page 键 ±bigStep、Home/End 到两端，夹在范围内；每次都提交")]
    public Task Keys_step_page_and_jump_and_commit_each_time() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Value = 50, Step = 5 };
        var commits = new List<double>();
        slider.ValueCommitted += (_, _) => commits.Add(slider.Value);
        using var form = WinFormsUi.Show(Host(slider));

        Press(slider, Keys.Right);
        Assert.Equal(55, slider.Value);
        Press(slider, Keys.Down);
        Assert.Equal(50, slider.Value);
        Press(slider, Keys.PageUp);
        Assert.Equal(60, slider.Value);
        Press(slider, Keys.End);
        Press(slider, Keys.Up);
        Assert.Equal(100, slider.Value);
        Press(slider, Keys.Home);
        Assert.Equal(0, slider.Value);
        Assert.Equal(0, commits[^1]);
    });

    [Fact]
    [Upstream("Slider", "小数步长不带浮点误差")]
    public Task Decimal_steps_have_no_floating_error() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Value = 0.1, Min = 0, Max = 1, Step = 0.1 };
        using var form = WinFormsUi.Show(Host(slider));
        Press(slider, Keys.Right);
        Press(slider, Keys.Right);
        Assert.Equal(0.3, slider.Value);
    });

    [Fact]
    [Upstream("Slider", "点击轨道跳到对应位置")]
    public Task Clicking_the_track_jumps_there_snapped_to_the_step() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Step = 10, AutoSize = false, Size = new Size(224, 40) };
        using var form = WinFormsUi.Show(Host(slider));
        Click(slider, 0.68);
        Assert.Equal(70, slider.Value);
    });

    [Fact]
    [Upstream("Slider", "不可用：不能聚焦，键盘与指针都不改值")]
    public Task Disabled_does_not_change() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Value = 20, Enabled = false, AutoSize = false, Size = new Size(224, 40) };
        using var form = WinFormsUi.Show(Host(slider));
        Assert.False(slider.CanFocus);
        Assert.True(slider.AccessibilityObject.State.HasFlag(AccessibleStates.Unavailable));
        Press(slider, Keys.Right);
        Click(slider, 0.9);
        Assert.Equal(20, slider.Value);
    });

    [Fact]
    public Task Read_only_does_not_change() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Value = 20, IsReadOnly = true };
        using var form = WinFormsUi.Show(Host(slider));
        Press(slider, Keys.Right);
        Assert.Equal(20, slider.Value);
        Assert.True(slider.AccessibilityObject.State.HasFlag(AccessibleStates.ReadOnly));
    });

    [Fact]
    [Upstream("Slider", "父级传入超出范围的值时按范围内显示，滑块不跑出轨道")]
    public Task Out_of_range_values_show_clamped() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Value = 150, AutoSize = false, Size = new Size(224, 40) };
        using var form = WinFormsUi.Show(Host(slider));
        Assert.Equal("100", slider.AccessibilityObject.Value);
        Assert.True(slider.PointOf(slider.Shown(0)).X < slider.Width);
        Assert.Equal(150, slider.Value);
    });

    [Fact]
    [Upstream("Slider", "两个滑块：名称加上最小值 / 最大值，取值范围互相限制，键盘不能交叉")]
    public Task Range_thumbs_are_named_and_cannot_cross() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Range = new SliderRange(20, 60), Step = 10, AccessibleName = "价格" };
        var commits = new List<SliderRange?>();
        slider.ValueCommitted += (_, _) => commits.Add(slider.Range);
        using var form = WinFormsUi.Show(Host(slider));

        var accessible = slider.AccessibilityObject;
        Assert.Equal(AccessibleRole.Grouping, accessible.Role);
        Assert.Equal(["价格 最小值", "价格 最大值"], Enumerable.Range(0, accessible.GetChildCount()).Select(i => accessible.GetChild(i)!.Name));
        Assert.Equal((60d, 20d), (slider.BoundsOf(0).Upper, slider.BoundsOf(1).Lower));
        Press(slider, Keys.End);
        Assert.Equal(new SliderRange(60, 60), slider.Range);
        Assert.Equal(new SliderRange(60, 60), commits[^1]);
        // Tab 先走到第二个滑块
        slider.Focus();
        Assert.True((bool)typeof(SliderControl).GetMethod("ProcessDialogKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(slider, [Keys.Tab])!);
        Assert.Equal(1, slider.FocusIndex);
        Press(slider, Keys.Home);
        Assert.Equal(new SliderRange(60, 60), slider.Range);
        Press(slider, Keys.Right);
        Assert.Equal(new SliderRange(60, 70), slider.Range);
    });

    [Fact]
    [Upstream("Slider", "点击轨道移动离得近的那个滑块")]
    public Task Clicking_moves_the_nearer_thumb() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Range = new SliderRange(20, 60), AutoSize = false, Size = new Size(224, 40) };
        using var form = WinFormsUi.Show(Host(slider));
        Click(slider, 0.9);
        Assert.Equal(new SliderRange(20, 90), slider.Range);
        Assert.Equal(1, slider.FocusIndex);
    });

    [Fact]
    [Upstream("Slider", "在 Field 里：名称连上隐藏的")]
    public Task In_a_field_the_label_names_both_thumbs() => WinFormsUi.Run(() =>
    {
        var slider = new SliderControl { Range = new SliderRange(1, 2) };
        using var form = WinFormsUi.Show(Host(new FieldControl { Label = "价格", Content = slider }));
        var accessible = slider.AccessibilityObject;
        Assert.Equal(["价格 最小值", "价格 最大值"], Enumerable.Range(0, 2).Select(i => accessible.GetChild(i)!.Name));
    });

    [Fact]
    [Upstream("Slider", "每个刻度一个点，在已选范围里的点标记出来；有文字的刻度显示文字")]
    public Task Marks_with_labels_reserve_room() => WinFormsUi.Run(() =>
    {
        var labelled = new SliderControl { Value = 50, Marks = [0, new SliderMark(50, "一半"), 100] };
        var plain = new SliderControl { Value = 50, Marks = [0, 50, 100] };
        using var form = WinFormsUi.Show(Host(labelled, plain));
        Assert.True(labelled.GetPreferredSize(new Size(200, 0)).Height > plain.GetPreferredSize(new Size(200, 0)).Height);
        WinFormsUi.Paint(labelled);
    });
}
