namespace Rune.WinForms.Tests;

/// <summary>绘制 effect：OnPaint 里读到的信号变了，只让读到它的控件失效。</summary>
public class PaintEffectTests
{
    [Fact]
    public Task Changing_a_signal_invalidates_only_its_readers() => WinFormsUi.Run(() =>
    {
        var shared = new Signal<Color>(Color.Blue);
        var reader = new PaintProbe(shared);
        var other = new PaintProbe(new Signal<Color>(Color.Blue)) { Left = 50 };
        using var form = WinFormsUi.Show(reader, other);
        reader.Invalidations = other.Invalidations = 0;

        shared.Value = Color.Red;
        WinFormsUi.Pump();

        Assert.Equal(1, reader.Invalidations);
        Assert.Equal(0, other.Invalidations);
    });

    [Fact]
    public Task Code_version_bump_invalidates_controls_that_read_it() => WinFormsUi.Run(() =>
    {
        // 热重载的通路：读过 CodeVersion 的派生值标脏，依赖它的绘制 effect 让控件失效
        var color = new Computed<Color>(() =>
        {
            _ = HotReload.CodeVersion.Value;
            return Color.Blue;
        });
        var effect = new Effect(() => effectRuns++);
        using (effect.Track()) _ = color.Value;

        HotReload.Raise();

        Assert.Equal(1, effectRuns);
        effect.Dispose();
        color.Dispose();
    });

    static int effectRuns;
}
