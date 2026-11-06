namespace Rune.WinForms.Tests;

/// <summary>
/// Spinner：大小转给图标部件、PropsIcon 逐字段盖住 Spinner 给的、在画时逐帧失效且隐藏后时钟停下、
/// 有 Label 时读屏当作进度条。
/// </summary>
public class SpinnerTests
{
    static int Dip(float dip, Control control) => (int)MathF.Ceiling(dip * control.DeviceDpi / 96f);

    [Fact]
    public Task Size_goes_to_the_icon_part() => WinFormsUi.Run(() =>
    {
        var spinner = new SpinnerControl { IconSize = TextSize.Lg };
        using var form = WinFormsUi.Show(spinner);

        Assert.Equal(Lucide.LoaderCircle, spinner.Element.IconPart.Data);
        Assert.Equal(new Size(Dip(16, spinner), Dip(16, spinner)), spinner.Size);
    });

    [Fact]
    public Task Props_icon_overrides_field_by_field() => WinFormsUi.Run(() =>
    {
        using var spinner = new SpinnerControl { IconSize = TextSize.Xl, PropsIcon = new IconProps { StrokeWidth = 3, Tone = Tone.Danger } };
        var icon = spinner.Element.IconPart;
        Assert.Equal(3, icon.StrokeWidth);
        Assert.Equal(Tone.Danger, icon.Tone);
        Assert.Equal(Lucide.LoaderCircle, icon.Data); // Spinner 给的图标与大小还在
        Assert.Equal(TextSize.Xl, icon.Size);

        // 去掉 PropsIcon：回到图标自己的默认
        spinner.PropsIcon = null;
        Assert.Equal(Tokens.StrokeWidth.Md, spinner.Element.IconPart.StrokeWidth);
    });

    [Fact]
    public Task Painting_spinners_drive_the_frame_clock_and_hidden_ones_let_it_stop() => WinFormsUi.Run(() =>
    {
        var spinner = new CountingSpinner();
        using var form = WinFormsUi.Show(spinner);
        spinner.Invalidations = 0;

        WinFormsUi.Wait(TimeSpan.FromMilliseconds(100));
        Assert.True(spinner.Invalidations >= 1);

        // 离屏窗口收不到 WM_PAINT：失效之后没人重画，订阅不再续上，时钟下一拍停下
        WinFormsUi.Wait(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, FrameClock.Shared.SubscriberCount);
        Assert.False(FrameClock.Shared.IsRunning);
    });

    [Fact]
    public Task Labelled_spinners_are_progress_bars() => WinFormsUi.Run(() =>
    {
        var decorative = new SpinnerControl();
        var labelled = new SpinnerControl { Label = "正在加载订单", Left = 60 };
        using var form = WinFormsUi.Show(decorative, labelled);

        Assert.Equal(AccessibleRole.Animation, decorative.AccessibilityObject.Role);
        Assert.Null(decorative.AccessibilityObject.Name);
        Assert.Equal(AccessibleRole.ProgressBar, labelled.AccessibilityObject.Role);
        Assert.Equal("正在加载订单", labelled.AccessibilityObject.Name);
        Assert.False(labelled.CanSelect);
    });

    /// <summary>记录自己被失效了几次。</summary>
    sealed class CountingSpinner : SpinnerControl
    {
        // 字段而不是属性：控件的公开属性会被设计器序列化（WFO1000）
        public int Invalidations;

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
            Invalidations++;
        }
    }
}
