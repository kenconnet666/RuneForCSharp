namespace Rune.Wpf.Tests;

/// <summary>PanZoom：以中心缩放时中心下的点不动；平移限制（大时不露空白、小时居中）；整张放进视口；滚轮、键盘、双击、缩放范围。</summary>
public class PanZoomTests
{
    [Fact]
    public void Zoom_around_keeps_the_origin_still()
    {
        var state = new PanZoomState(10, 20, 1);
        var next = PanZoom.ZoomAround(state, 2, (110, 120));
        // 缩放前 (110, 120) 下的内容点是 (100, 100)，缩放后仍在 (110, 120)
        Assert.Equal((110d, 120d), (next.X + 100 * next.Scale, next.Y + 100 * next.Scale));
    }

    [Fact]
    public void Clamp_pan_hides_the_gaps_and_centers_small_content()
    {
        Assert.Equal(new PanZoomState(-100, 0, 1), PanZoom.ClampPan(new PanZoomState(-500, 50, 1), (200, 200), (300, 300)));
        Assert.Equal(new PanZoomState(50, 50, 1), PanZoom.ClampPan(new PanZoomState(0, 0, 1), (200, 200), (100, 100)));
    }

    [Fact]
    public void Fit_transform_never_enlarges_and_keeps_the_padding()
    {
        Assert.Equal(new PanZoomState(150, 100, 1), PanZoom.FitTransform((400, 300), (100, 100), 24));
        var fitted = PanZoom.FitTransform((400, 300), (1200, 800), 24);
        Assert.Equal((400 - 48) / 1200d, fitted.Scale);
        Assert.Equal((300 - 800 * fitted.Scale) / 2, fitted.Y);
        Assert.Equal(1, PanZoom.FitTransform((0, 0), (100, 100)).Scale);
    }

    [Fact]
    public void Keys_pan_and_zoom_within_the_range()
    {
        var view = new PanZoom(() => (200, 200)) { Min = 0.5, Max = 2 };
        view.PanBy(10, 5);
        Assert.Equal(new PanZoomState(10, 5, 1), view.State);
        view.Press(PanZoomKey.Right);
        view.Press(PanZoomKey.Up);
        Assert.Equal(new PanZoomState(-30, 45, 1), view.State);
        view.Press(PanZoomKey.Reset);
        for (var i = 0; i < 10; i++) view.Press(PanZoomKey.ZoomIn);
        Assert.Equal(2, view.State.Scale);
        for (var i = 0; i < 10; i++) view.Press(PanZoomKey.ZoomOut);
        Assert.Equal(0.5, view.State.Scale);
    }

    [Fact]
    public void Double_tap_zooms_two_steps_then_resets()
    {
        var view = new PanZoom(() => (200, 200));
        view.DoubleTap((50, 50));
        Assert.Equal(1.25 * 1.25, view.State.Scale);
        Assert.Equal(50 - 50 * view.State.Scale, view.State.X);
        view.DoubleTap((50, 50));
        Assert.Equal(PanZoomState.Identity, view.State);
        Assert.Equal(0.25, view.Fit((800, 400)));
        Assert.Equal(new PanZoomState(0, 50, 0.25), view.State);
    }

    [Fact]
    public void Wheel_zooms_around_the_pointer_and_bounds_clamp_the_pan()
    {
        var view = new PanZoom(() => (200, 200)) { Bounds = () => (400, 400) };
        view.Wheel(-1, (0, 0));
        Assert.True(view.State.Scale > 1);
        Assert.Equal((0d, 0d), (view.State.X, view.State.Y));
        view.PanBy(-1000, -1000);
        // 内容 400×scale 比视口大：右下边不进到视口里
        Assert.Equal(200 - 400 * view.State.Scale, view.State.X, 6);
        view.Wheel(1, (100, 100));
        Assert.True(view.State.Scale < 1.3);
    }
}
