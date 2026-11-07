namespace Rune.WinForms.Tests;

/// <summary>ProgressControl：读屏的进度条与取值文字；不确定进度；decimal 按原值夹住；目标线；环形；标签行。</summary>
public class ProgressTests
{
    /// <summary>中文语言包里跑（"60%，目标 80%"），跑完换回原来的。</summary>
    static Task Run(Action body) => WinFormsUi.Run(() =>
    {
        var lang = RuneLang.Current.Peek();
        RuneLang.Current.Value = Lang.ZhCn;
        try
        {
            body();
        }
        finally
        {
            RuneLang.Current.Value = lang;
        }
    });

    static Color Pixel(Control control, int x, int y)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap.GetPixel(x, y);
    }

    [Fact]
    [Upstream("Progress", "有值：role=")]
    public Task Value_is_clamped_and_spoken_as_a_percentage() => Run(() =>
    {
        var progress = new ProgressControl { Value = 150, Max = 200 };
        using var form = WinFormsUi.Show(progress);

        Assert.Equal(AccessibleRole.ProgressBar, progress.AccessibilityObject.Role);
        Assert.Equal("75%", progress.AccessibilityObject.Value);
        Assert.Equal(ProgressState.Loading, progress.State);
        Assert.Equal(ProgressState.Complete, new ProgressControl { Value = 999 }.State);
    });

    [Fact]
    [Upstream("Progress", "没有值：不确定进度")]
    public Task Without_value_it_is_indeterminate_and_value_text_is_custom() => Run(() =>
    {
        var progress = new ProgressControl();
        var custom = new ProgressControl { Value = 2, Max = 5, ValueText = (value, max) => $"{value} / {max} 个文件" };
        using var form = WinFormsUi.Show(progress, custom);

        Assert.Equal(ProgressState.Indeterminate, progress.State);
        Assert.Equal("", progress.AccessibilityObject.Value);
        Assert.Equal("2 / 5 个文件", custom.AccessibilityObject.Value);
    });

    [Fact]
    [Upstream("Progress", "bigint、Decimal：按原值比较与夹住")]
    public Task Decimal_values_compare_and_clamp_exactly() => Run(() =>
    {
        var big = new ProgressControl { Value = 9_007_199_254_740_993m, Max = 9_007_199_254_740_993m, ValueText = (value, _) => $"{value} 字节" };
        var money = new ProgressControl { Value = 150.5m, Max = 100m };

        Assert.Equal(ProgressState.Complete, big.State);
        Assert.Equal("9007199254740993 字节", big.Element.SpokenValue);
        Assert.Equal(100m, money.Element.Clamped);
        Assert.Equal("100%", money.Element.SpokenValue);
    });

    [Fact]
    [Upstream("Progress", "target：条上画目标线")]
    public Task Target_draws_a_line_and_is_spoken_after_the_value() => Run(() =>
    {
        var progress = new ProgressControl { Value = 60, Target = 80, Width = 300, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(progress);
        var middle = progress.Height / 2;
        var colors = RuneTheme.Current.Value.Colors;

        Assert.Equal("60%，目标 80%", progress.AccessibilityObject.Value);
        Assert.Equal(colors.Text.ToArgb(), Pixel(progress, (int)(0.8 * progress.Width), middle).ToArgb()); // 目标线按比例定位
        Assert.Equal(colors.Accent.ToArgb(), Pixel(progress, (int)(0.5 * progress.Width), middle).ToArgb());
        Assert.Equal(colors.Fill.ToArgb(), Pixel(progress, (int)(0.7 * progress.Width), middle).ToArgb());
    });

    [Fact]
    [Upstream("Progress", "环形：根元素是 progressbar")]
    public Task Circle_shows_the_percentage_in_the_middle_and_the_label_below() => Run(() =>
    {
        var ring = new ProgressControl { Shape = ProgressShape.Circle, Value = 72 };
        var labelled = new ProgressControl { Shape = ProgressShape.Circle, Value = 40, Target = 50, Label = "本月完成率", Top = 200 };
        using var form = WinFormsUi.Show(ring, labelled);
        var em = ring.Font.SizeInPoints * ring.DeviceDpi / 72f;

        Assert.Equal((int)MathF.Ceiling(5 * em), ring.Width); // 直径 5em
        Assert.Equal(ring.Width, ring.Height);
        Assert.Equal(["72%"], ring.Element.VisibleTexts);
        Assert.Equal(TextWeight.Semibold, ring.Element.Parts.ValueLabel.Weight);

        Assert.Equal("本月完成率", labelled.AccessibilityObject.Name);
        Assert.Equal("40%，目标 50%", labelled.AccessibilityObject.Value);
        Assert.True(labelled.Height > (int)MathF.Ceiling(5 * em)); // 标签在环的下方，占高度
        Assert.Equal(TextTone.Muted, labelled.Element.Parts.Label.Tone);

        Assert.Equal(ProgressState.Indeterminate, new ProgressControl { Shape = ProgressShape.Circle }.State);
    });

    [Fact]
    [Upstream("Progress", "label：上方的标签是名称")]
    public Task Label_row_names_the_bar_and_shows_the_value() => Run(() =>
    {
        var storage = new ProgressControl { Value = 38, Max = 50, Label = "存储空间", ValueLabel = "38 / 50 GB", PropsValueLabel = new TextProps { Tone = TextTone.Danger } };
        using var form = WinFormsUi.Show(storage);

        Assert.Equal("存储空间", storage.AccessibilityObject.Name);
        Assert.Equal(["存储空间", "38 / 50 GB"], storage.Element.VisibleTexts);
        Assert.Equal("76%", storage.AccessibilityObject.Value); // 读屏念的仍是取值文字
        var value = storage.Element.Parts.ValueLabel;
        Assert.Equal((TextTone.Danger, true), (value.Tone, value.Numeric)); // PropsValueLabel 盖在默认上，等宽数字仍在

        Assert.Equal(["资料完善度", "64%"], new ProgressControl { Value = 64, Label = "资料完善度" }.Element.VisibleTexts);
        Assert.Equal(["完善度"], new ProgressControl { Value = 64, Label = "完善度", ShowValueLabel = false }.Element.VisibleTexts);
        // 不确定进度没有数值；没有标签时只有进度条
        Assert.Equal(["加载"], new ProgressControl { Label = "加载" }.Element.VisibleTexts);
        Assert.Empty(new ProgressControl { Value = 1 }.Element.VisibleTexts);
    });
}
