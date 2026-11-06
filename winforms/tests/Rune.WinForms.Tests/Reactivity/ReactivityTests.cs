namespace Rune.WinForms.Tests;

public class ReactivityTests
{
    [Fact]
    public void Only_dependents_are_notified()
    {
        var a = new Signal<int>(0);
        var b = new Signal<int>(0);
        int ranA = 0, ranB = 0;
        var effectA = new Effect(() => ranA++);
        var effectB = new Effect(() => ranB++);
        using (effectA.Track()) _ = a.Value;
        using (effectB.Track()) _ = b.Value;

        a.Value = 1;

        Assert.Equal((1, 0), (ranA, ranB));
    }

    [Fact]
    public void Writing_the_same_value_notifies_nobody()
    {
        var signal = new Signal<string>("x");
        var ran = 0;
        var effect = new Effect(() => ran++);
        using (effect.Track()) _ = signal.Value;

        signal.Value = "x";

        Assert.Equal(0, ran);
    }

    [Fact]
    public void Dependencies_are_retracked_on_every_run()
    {
        var useA = new Signal<bool>(true);
        var a = new Signal<int>(0);
        var b = new Signal<int>(0);
        var ran = 0;
        var effect = new Effect(() => ran++);
        void Read()
        {
            using (effect.Track()) _ = useA.Value ? a.Value : b.Value;
        }
        Read();

        useA.Value = false; // 分支换了：重新追踪后只依赖 b
        Read();
        ran = 0;
        a.Value = 1;
        b.Value = 1;

        Assert.Equal(1, ran);
        Assert.Equal(2, effect.DependencyCount);
    }

    [Fact]
    public void Computed_is_lazy_and_cached()
    {
        var source = new Signal<int>(2);
        var doubled = new Computed<int>(() => source.Value * 2);

        Assert.Equal(4, doubled.Value);
        Assert.Equal(4, doubled.Value);
        Assert.Equal(1, doubled.Computations);

        source.Value = 3;
        Assert.Equal(1, doubled.Computations); // 只标脏，读的时候才算
        Assert.Equal(6, doubled.Value);
        Assert.Equal(2, doubled.Computations);
    }

    [Fact]
    public void Computed_notifies_its_dependents_once_until_read()
    {
        var source = new Signal<int>(0);
        var derived = new Computed<int>(() => source.Value + 1);
        var ran = 0;
        var effect = new Effect(() => ran++);
        using (effect.Track()) _ = derived.Value;

        source.Value = 1;
        source.Value = 2; // 已经脏了，不再通知

        Assert.Equal(1, ran);
    }

    [Fact]
    public void Disposing_unsubscribes_from_long_lived_signals()
    {
        var theme = new Signal<int>(0);
        var effect = new Effect(() => { });
        var computed = new Computed<int>(() => theme.Value);
        using (effect.Track()) _ = theme.Value;
        _ = computed.Value;
        Assert.Equal(2, theme.SubscriberCount);

        effect.Dispose();
        computed.Dispose();

        Assert.Equal(0, theme.SubscriberCount);
    }
}
