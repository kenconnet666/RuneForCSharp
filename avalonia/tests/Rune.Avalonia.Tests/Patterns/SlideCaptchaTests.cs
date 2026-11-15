using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;

namespace Rune.Avalonia.Tests;

/// <summary>SlideCaptcha：加载中、键盘微调后松手交出位置与轨迹、各状态的提示与锁定、换图回到起点、换一张。</summary>
public class SlideCaptchaTests
{
    static SlideChallenge Challenge(string id = "c1") => new(id, new RenderTargetBitmap(new PixelSize(320, 160)), new RenderTargetBitmap(new PixelSize(57, 160)), 320, 160, 57);

    static SlideCaptcha Show(SlideCaptcha captcha)
    {
        Provider.SetLang(captcha, Lang.ZhCn);
        var window = HeadlessUi.Show(captcha);
        HeadlessUi.Flush(window);
        return captcha;
    }

    static void Press(SlideCaptcha captcha, Key key) => captcha.Handle.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    static IRangeValueProvider Slider(SlideCaptcha captcha) => (IRangeValueProvider)captcha.Handle.Peer();

    static IEnumerable<string> Texts(SlideCaptcha captcha) => captcha.GetLogicalDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? "");

    [Fact]
    [Upstream("SlideCaptcha", "还没拿到图时显示加载中，滑块不能操作")]
    public Task Loading_without_a_challenge() => HeadlessUi.Run(() =>
    {
        var captcha = Show(new SlideCaptcha());
        Assert.Contains(Texts(captcha), text => text.Contains("加载中"));
        Assert.DoesNotContain(captcha.GetLogicalDescendants().OfType<global::Avalonia.Controls.Image>(), image => image.IsEffectivelyVisible);
        Assert.False(captcha.Handle.Peer().IsEnabled());
        Assert.False(captcha.Handle.IsTabStop);
    });

    [Fact]
    [Upstream("SlideCaptcha", "键盘微调后按 Enter 松手：交出位置、轨迹和时长")]
    public Task Keys_nudge_and_enter_releases() => HeadlessUi.Run(() =>
    {
        var captcha = Show(new SlideCaptcha { Challenge = Challenge() });
        var released = new List<SlideResult>();
        captcha.Released += (_, e) => released.Add(e.Result);
        Assert.Equal(320 - 57, Slider(captcha).Maximum);
        for (var i = 0; i < 3; i++) Press(captcha, Key.Right);
        Assert.Equal(6, Slider(captcha).Value);
        Press(captcha, Key.Enter);
        Assert.Single(released);
        Assert.Equal(6, released[0].X);
        Assert.Equal(3, released[0].Track.Count);
        Assert.True(released[0].Duration >= 0);
    });

    [Fact]
    [Upstream("SlideCaptcha", "Home / End 到两端；没动过就按 Enter 不松手")]
    public Task Home_and_end_and_no_release_without_moving() => HeadlessUi.Run(() =>
    {
        var captcha = Show(new SlideCaptcha { Challenge = Challenge() });
        var released = 0;
        captcha.Released += (_, _) => released++;
        Press(captcha, Key.Enter);
        Assert.Equal(0, released);
        Press(captcha, Key.End);
        Assert.Equal(320 - 57, Slider(captcha).Value);
        Press(captcha, Key.Home);
        Assert.Equal(0, Slider(captcha).Value);
    });

    [Fact]
    [Upstream("SlideCaptcha", "验证中、通过时锁定；没对准时显示提示，设回 idle 滑块回到起点")]
    public Task Statuses_lock_show_text_and_reset() => HeadlessUi.Run(() =>
    {
        var captcha = Show(new SlideCaptcha { Challenge = Challenge(), ErrorText = "位置差太多" });
        Press(captcha, Key.Right);
        Assert.Equal(2, Slider(captcha).Value);
        captcha.Status = SlideStatus.Verifying;
        Assert.Contains(":verifying", captcha.Classes);
        Assert.False(captcha.Handle.Peer().IsEnabled());
        captcha.Status = SlideStatus.Error;
        Assert.Contains("位置差太多", Texts(captcha));
        captcha.Status = SlideStatus.Idle;
        Assert.Equal(0, Slider(captcha).Value);
        Assert.True(captcha.Handle.Peer().IsEnabled());
        captcha.Status = SlideStatus.Success;
        Assert.Contains("验证通过", Texts(captcha));
        Assert.False(captcha.Handle.Peer().IsEnabled());
    });

    [Fact]
    [Upstream("SlideCaptcha", "换了新图，滑块回到起点；“换一张”触发 onRefresh")]
    public Task New_challenge_resets_and_refresh_asks_for_another() => HeadlessUi.Run(() =>
    {
        var captcha = Show(new SlideCaptcha { Challenge = Challenge() });
        var refreshed = 0;
        captcha.RefreshRequested += (_, _) => refreshed++;
        Press(captcha, Key.Right);
        captcha.Challenge = Challenge("c2");
        Assert.Equal(0, Slider(captcha).Value);
        Assert.Equal("换一张", global::Avalonia.Automation.AutomationProperties.GetName(captcha.RefreshButton));
        captcha.RefreshButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, refreshed);
    });

    [Fact]
    [Upstream("SlideCaptcha", "方向键、Home / End 移动滑块，Enter 松手交给判定")]
    public Task Real_keys_move_and_enter_hands_over() => HeadlessUi.Run(() =>
    {
        var captcha = new SlideCaptcha { Challenge = Challenge() };
        Provider.SetLang(captcha, Lang.ZhCn);
        var window = HeadlessUi.Show(captcha);
        HeadlessUi.Flush(window);
        SlideResult? result = null;
        // 使用方收到结果就进入判定
        captcha.Released += (_, e) =>
        {
            result = e.Result;
            captcha.Status = SlideStatus.Verifying;
        };
        var max = Slider(captcha).Maximum;
        captcha.Handle.Focus();
        void Key(PhysicalKey key) => window.KeyPressQwerty(key, RawInputModifiers.None);
        Key(PhysicalKey.End);
        Assert.Equal(max, Slider(captcha).Value);
        Key(PhysicalKey.ArrowLeft);
        Assert.Equal(max - 2, Slider(captcha).Value);
        Key(PhysicalKey.Home);
        Assert.Equal(0, Slider(captcha).Value);
        Key(PhysicalKey.ArrowRight);
        Key(PhysicalKey.ArrowRight);
        Assert.Equal(4, Slider(captcha).Value);
        Key(PhysicalKey.Enter);
        Assert.Equal(4, result!.X);
        // 判定中：滑块不可用、离开 Tab 顺序
        Assert.False(captcha.Handle.Peer().IsEnabled());
        Assert.False(captcha.Handle.IsTabStop);
    });
}
