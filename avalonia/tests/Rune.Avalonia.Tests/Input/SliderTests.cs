using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;

namespace Rune.Avalonia.Tests;

/// <summary>Slider：读屏的滑块与取值范围、键盘、对齐步长、点击轨道、不可用、范围选择、刻度。</summary>
public class SliderTests
{
    static void Press(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

    static IRangeValueProvider Range(SliderThumb thumb) => (IRangeValueProvider)thumb.Peer();

    /// <summary>点轨道上 ratio 处（0–1）：轨道两端各缩进半个滑块。</summary>
    static void Click(Window window, Slider slider, double ratio)
    {
        var half = slider.Thumbs[0].Bounds.Width / 2;
        var x = half + ratio * (slider.Bounds.Width - 2 * half);
        var at = slider.TranslatePoint(new Point(x, slider.Thumbs[0].Bounds.Center.Y), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }

    [Fact]
    [Upstream("Slider", "滑块是 role=")]
    public Task Thumb_is_a_slider_with_range_and_name() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Value = 30, ValueText = v => $"{v}%" };
        AutomationProperties.SetName(slider, "音量");
        HeadlessUi.Show(slider);

        var thumb = slider.Thumbs.Single();
        var peer = thumb.Peer();
        Assert.Equal(AutomationControlType.Slider, peer.GetAutomationControlType());
        Assert.Equal("音量", peer.GetName());
        Assert.Equal((0d, 100d, 30d), (Range(thumb).Minimum, Range(thumb).Maximum, Range(thumb).Value));
        Assert.Equal("30%", AutomationProperties.GetItemStatus(thumb));
        Assert.True(thumb.Focusable);
        Assert.False(slider.Peer().IsControlElement());
    });

    [Fact]
    [Upstream("Slider", "键盘：方向键 ±step、Page 键 ±bigStep、Home/End 到两端，夹在范围内；每次都提交")]
    public Task Keys_step_page_and_jump_and_commit_each_time() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Value = 50, Step = 5 };
        var commits = new List<double>();
        slider.ValueCommitted += (_, _) => commits.Add(slider.Value);
        HeadlessUi.Show(slider);
        var thumb = slider.Thumbs[0];

        Press(thumb, Key.Right);
        Assert.Equal(55, slider.Value);
        Press(thumb, Key.Down);
        Assert.Equal(50, slider.Value);
        Press(thumb, Key.PageUp);
        Assert.Equal(60, slider.Value);
        Press(thumb, Key.End);
        Press(thumb, Key.Up);
        Assert.Equal(100, slider.Value);
        Press(thumb, Key.Home);
        Assert.Equal(0, slider.Value);
        Assert.Equal(0, commits[^1]);
        Assert.Equal(0, Range(thumb).Value);
    });

    [Fact]
    [Upstream("Slider", "小数步长不带浮点误差")]
    public Task Decimal_steps_have_no_floating_error() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Value = 0.1, Min = 0, Max = 1, Step = 0.1 };
        HeadlessUi.Show(slider);
        Press(slider.Thumbs[0], Key.Right);
        Press(slider.Thumbs[0], Key.Right);
        Assert.Equal(0.3, slider.Value);
    });

    [Fact]
    [Upstream("Slider", "点击轨道跳到对应位置")]
    public Task Clicking_the_track_jumps_there_snapped_to_the_step() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Step = 10, Width = 224 };
        var window = HeadlessUi.Show(slider);
        Click(window, slider, 0.68);
        Assert.Equal(70, slider.Value);
        Assert.True(slider.Thumbs[0].IsFocused);
    });

    [Fact]
    [Upstream("Slider", "不可用：不能聚焦，键盘与指针都不改值")]
    public Task Disabled_does_not_focus_or_change() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Value = 20, IsEnabled = false, Width = 224 };
        var window = HeadlessUi.Show(slider);
        Assert.False(slider.Thumbs[0].IsEffectivelyEnabled);
        Assert.True(Range(slider.Thumbs[0]).IsReadOnly);
        Press(slider.Thumbs[0], Key.Right);
        Click(window, slider, 0.9);
        Assert.Equal(20, slider.Value);
    });

    [Fact]
    public Task Read_only_focuses_but_does_not_change() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Value = 20, IsReadOnly = true };
        HeadlessUi.Show(slider);
        Assert.True(slider.Thumbs[0].Focusable);
        Press(slider.Thumbs[0], Key.Right);
        Assert.Equal(20, slider.Value);
        Assert.True(Range(slider.Thumbs[0]).IsReadOnly);
    });

    [Fact]
    [Upstream("Slider", "父级传入超出范围的值时按范围内显示，滑块不跑出轨道")]
    public Task Out_of_range_values_show_clamped() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Value = 150, Width = 224 };
        HeadlessUi.Show(slider);
        Assert.Equal(100, Range(slider.Thumbs[0]).Value);
        Assert.True(slider.Thumbs[0].Bounds.Right <= slider.Bounds.Width + 0.5);
        Assert.Equal(150, slider.Value);
    });

    [Fact]
    [Upstream("Slider", "两个滑块：名称加上最小值 / 最大值，取值范围互相限制，键盘不能交叉")]
    public Task Range_thumbs_are_named_and_cannot_cross() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Range = new SliderRange(20, 60), Step = 10 };
        AutomationProperties.SetName(slider, "价格");
        var commits = new List<SliderRange?>();
        slider.ValueCommitted += (_, _) => commits.Add(slider.Range);
        HeadlessUi.Show(slider);

        var (low, high) = (slider.Thumbs[0], slider.Thumbs[1]);
        Assert.Equal(["价格 最小值", "价格 最大值"], slider.Thumbs.Select(thumb => thumb.Peer().GetName()));
        Assert.Equal((60d, 20d), (Range(low).Maximum, Range(high).Minimum));
        Press(low, Key.End);
        Assert.Equal(new SliderRange(60, 60), slider.Range);
        Assert.Equal(new SliderRange(60, 60), commits[^1]);
        Press(high, Key.Home);
        Assert.Equal(new SliderRange(60, 60), slider.Range);
        Press(high, Key.Right);
        Assert.Equal(new SliderRange(60, 70), slider.Range);
    });

    [Fact]
    [Upstream("Slider", "点击轨道移动离得近的那个滑块")]
    public Task Clicking_moves_the_nearer_thumb() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Range = new SliderRange(20, 60), Width = 224 };
        var window = HeadlessUi.Show(slider);
        Click(window, slider, 0.9);
        Assert.Equal(new SliderRange(20, 90), slider.Range);
    });

    [Fact]
    [Upstream("Slider", "在 Field 里：名称连上隐藏的")]
    public Task In_a_field_the_label_names_both_thumbs() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Range = new SliderRange(1, 2) };
        HeadlessUi.Show(new Field { Label = "价格", Content = slider });
        Assert.Equal(["价格 最小值", "价格 最大值"], slider.Thumbs.Select(thumb => thumb.Peer().GetName()));
    });

    [Fact]
    [Upstream("Slider", "每个刻度一个点，在已选范围里的点标记出来；有文字的刻度显示文字")]
    public Task Marks_with_labels_show_their_text() => HeadlessUi.Run(() =>
    {
        var slider = new Slider { Value = 50, Marks = [0, new SliderMark(50, "一半"), 100] };
        HeadlessUi.Show(slider);
        Assert.Equal(["一半"], slider.MarkLabels.Select(label => label.Text));
        // 刻度文字留出位置：比没有文字时高
        var plain = new Slider { Value = 50, Marks = [0, 50, 100] };
        HeadlessUi.Show(plain);
        Assert.True(slider.DesiredSize.Height > plain.DesiredSize.Height);
    });

    [Fact]
    public void Scale_snaps_bounds_and_picks_the_nearer_thumb()
    {
        Assert.Equal(0.3, SliderScale.Snap(0.30000000000000004, 0.1, 0, 1));
        Assert.Equal((0d, 60d), SliderScale.Bounds(0, [20, 60], 0, 100));
        Assert.Equal(1, SliderScale.Nearest(70, [60, 60]));
        Assert.Equal(0, SliderScale.Nearest(50, [60, 60]));
        Assert.Equal(10, SliderScale.BigStep(null, 1, 0, 100));
    }
}
