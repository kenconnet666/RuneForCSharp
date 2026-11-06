using System.ComponentModel;

namespace Rune.WinForms.Tests;

/// <summary>
/// Text 家族：各组件的默认值（不写进设计器代码）；字号、字重、等宽改变大小；色调取当前主题且只有写了色调的文字换主题时重画；
/// 截断与限制行数；读屏把它当作静态文字。
/// </summary>
public class TextTests
{
    static Size Preferred(Control control) => control.GetPreferredSize(Size.Empty);

    [Fact]
    public Task Components_have_their_defaults_and_do_not_serialize_them() => WinFormsUi.Run(() =>
    {
        using var strong = new StrongControl { Text = "重要" };
        using var small = new SmallControl { Text = "附注" };
        using var paragraph = new ParagraphControl { Text = "段落" };
        Assert.Equal(TextWeight.Semibold, strong.Weight);
        Assert.Equal(TextSize.Xs, small.TextSize);
        Assert.Equal(TextWrap.Pretty, paragraph.Wrap);

        Assert.False(TypeDescriptor.GetProperties(strong)[nameof(StrongControl.Weight)]!.ShouldSerializeValue(strong));
        Assert.False(TypeDescriptor.GetProperties(small)[nameof(SmallControl.TextSize)]!.ShouldSerializeValue(small));
        strong.Weight = TextWeight.Normal;
        Assert.True(TypeDescriptor.GetProperties(strong)[nameof(StrongControl.Weight)]!.ShouldSerializeValue(strong));
        // 回到不写：取组件的默认值
        strong.Weight = default;
        Assert.Equal(TextWeight.Semibold, strong.Weight);
    });

    [Fact]
    public Task Size_weight_and_mono_change_the_measured_size() => WinFormsUi.Run(() =>
    {
        var plain = new TextControl { Text = "iiii 1234" };
        var large = new TextControl { Text = "iiii 1234", TextSize = TextSize.Xl };
        var bold = new TextControl { Text = "iiii 1234", Weight = TextWeight.Bold };
        var mono = new TextControl { Text = "iiii 1234", Mono = true };
        using var form = WinFormsUi.Show(plain, large, bold, mono);

        Assert.True(Preferred(large).Height > Preferred(plain).Height);
        Assert.True(Preferred(bold).Width > Preferred(plain).Width);
        Assert.True(Preferred(mono).Width > Preferred(plain).Width); // 等宽字体里 i 和数字一样宽
        Assert.Equal(plain.Size, Preferred(plain)); // AutoSize
    });

    [Fact]
    public Task Tone_reads_the_theme_only_when_set() => WinFormsUi.Run(() =>
    {
        // 实心方块：中间的像素整个被字形盖住，颜色不受抗锯齿影响
        var plain = new CountingText { Text = "██", TextSize = 32, ForeColor = Color.Purple };
        var danger = new CountingText { Text = "██", TextSize = 32, Tone = TextTone.Danger, Left = 200 };
        using var form = WinFormsUi.Show(plain, danger);
        try
        {
            Assert.Equal(BuiltinThemes.Light.Colors.DangerText.ToArgb(), Center(danger).ToArgb());
            Assert.Equal(Color.Purple.ToArgb(), Center(plain).ToArgb());
            plain.Invalidations = danger.Invalidations = 0;

            RuneTheme.Current.Value = BuiltinThemes.Dark;
            WinFormsUi.Pump();

            Assert.Equal(1, danger.Invalidations);
            Assert.Equal(0, plain.Invalidations);
            Assert.Equal(BuiltinThemes.Dark.Colors.DangerText.ToArgb(), Center(danger).ToArgb());
        }
        finally
        {
            RuneTheme.Current.Value = BuiltinThemes.Light;
        }

        static Color Center(Control control)
        {
            using var bitmap = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
            return bitmap.GetPixel(control.Width / 4, control.Height / 2);
        }
    });

    [Fact]
    public Task Truncate_and_line_clamp_limit_the_size() => WinFormsUi.Run(() =>
    {
        const string Long = "符文是编译期的标记：组件里写 count++，编译后变成对响应式引用的读写；模板里直接用 count，不需要 .value，也不会丢失响应性。";
        var wrapped = new ParagraphControl { Text = Long, MaximumSize = new Size(200, 0) };
        var clamped = new ParagraphControl { Text = Long, MaximumSize = new Size(200, 0), LineClamp = 2 };
        var truncated = new TextControl { Text = Long, MaximumSize = new Size(200, 0), Truncate = true };
        var oneLine = new TextControl { Text = "一行" };
        using var form = WinFormsUi.Show(wrapped, clamped, truncated, oneLine);

        var line = Preferred(oneLine).Height;
        Assert.True(Preferred(wrapped).Height > 2 * line);
        Assert.Equal(2 * line, Preferred(clamped).Height);
        Assert.Equal(line, Preferred(truncated).Height);
        Assert.True(Preferred(truncated).Width <= 200);
    });

    [Fact]
    public Task Screen_readers_see_static_text() => WinFormsUi.Run(() =>
    {
        var text = new TextControl { Text = "更新于 3 分钟前" };
        using var form = WinFormsUi.Show(text);

        Assert.Equal(AccessibleRole.StaticText, text.AccessibilityObject.Role);
        Assert.Equal("更新于 3 分钟前", text.AccessibilityObject.Name);
        Assert.False(text.CanSelect);
    });

    /// <summary>记录自己被失效了几次。</summary>
    sealed class CountingText : TextControl
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
