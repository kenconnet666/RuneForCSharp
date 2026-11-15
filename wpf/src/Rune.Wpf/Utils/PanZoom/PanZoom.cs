namespace Rune.Wpf;

/// <summary>平移与缩放的状态（vue-tsx-pro 的 PanZoomState）：内容先缩放、再平移（变换原点在左上角）。</summary>
/// <param name="X">横向平移（像素）。</param>
/// <param name="Y">纵向平移（像素）。</param>
/// <param name="Scale">缩放比例，1 为原大。</param>
public readonly record struct PanZoomState(double X, double Y, double Scale)
{
    /// <summary>不平移、原大。</summary>
    public static PanZoomState Identity { get; } = new(0, 0, 1);
}

/// <summary>视口里的一块（vue-tsx-pro 的 PanZoomRegion，视口内坐标，像素）：裁剪框。</summary>
/// <param name="X">左边。</param>
/// <param name="Y">上边。</param>
/// <param name="Width">宽。</param>
/// <param name="Height">高。</param>
public readonly record struct PanZoomRegion(double X, double Y, double Width, double Height);

/// <summary>平移、缩放能识别的按键（平台的键换成它）。</summary>
public enum PanZoomKey
{
    /// <summary>放大一档（+、=）。</summary>
    ZoomIn,

    /// <summary>缩小一档（−）。</summary>
    ZoomOut,

    /// <summary>回到初始（0）。</summary>
    Reset,

    /// <summary>内容往右挪（←：看左边）。</summary>
    Left,

    /// <summary>内容往左挪（→）。</summary>
    Right,

    /// <summary>内容往下挪（↑）。</summary>
    Up,

    /// <summary>内容往上挪（↓）。</summary>
    Down,
}

/// <summary>
/// 平移与缩放（vue-tsx-pro 的 usePanZoom，三个库共用一份）：状态只有平移 X、Y 与缩放 Scale，内容自己按它变换。
/// 拖动平移、滚轮以指针为中心缩放、双击放大一档（已放大时回到初始）、键盘 + / − / 0 / 方向键；给了内容大小（<see cref="Bounds"/>）时限制平移：
/// 内容比视口（或 <see cref="Frame"/>，裁剪框）大就不露出边外的空白，比它小就居中；<see cref="Cover"/> 时缩放下限是内容盖满 Frame 所需的比例（裁剪图片：框里永远是图）。
/// 平台的控件把事件换成这里的调用，坐标都是视口内的（像素）。
/// </summary>
/// <param name="viewport">视口的大小（像素）。</param>
public sealed class PanZoom(Func<(double Width, double Height)> viewport)
{
    /// <summary>键盘方向键每次平移多少像素。</summary>
    public const double KeyPan = 40;

    /// <summary>当前状态。</summary>
    public PanZoomState State { get; private set; } = PanZoomState.Identity;

    /// <summary>最小缩放，默认 0.25。</summary>
    public double Min { get; set; } = 0.25;

    /// <summary>最大缩放，默认 4。</summary>
    public double Max { get; set; } = 4;

    /// <summary>每一档缩放的倍数（按钮、键盘、双击），默认 1.25。</summary>
    public double Step { get; set; } = 1.25;

    /// <summary>初始状态（Reset 回到它）。</summary>
    public PanZoomState Initial { get; set; } = PanZoomState.Identity;

    /// <summary>内容的大小（缩放前）：给了就限制平移；null 时不限制。</summary>
    public Func<(double Width, double Height)?>? Bounds { get; set; }

    /// <summary>平移限制按视口里的这一块算（裁剪框）；null 时是整个视口。</summary>
    public Func<PanZoomRegion?>? Frame { get; set; }

    /// <summary>内容至少盖满 <see cref="Frame"/>（配合 <see cref="Bounds"/>）：缩放下限取盖满所需的比例与 <see cref="Min"/> 里大的那个。</summary>
    public bool Cover { get; set; }

    /// <summary>平移限制的区域：<see cref="Frame"/>，没有时是整个视口。</summary>
    PanZoomRegion Region
    {
        get
        {
            if (Frame?.Invoke() is { } frame) return frame;
            var (width, height) = viewport();
            return new PanZoomRegion(0, 0, width, height);
        }
    }

    /// <summary>实际的缩放下限：<see cref="Cover"/> 时还要盖满 Frame。</summary>
    public double MinScale
    {
        get
        {
            if (!Cover || Bounds?.Invoke() is not { Width: > 0, Height: > 0 } content) return Min;
            var region = Region;
            return Math.Max(Min, Math.Max(region.Width / content.Width, region.Height / content.Height));
        }
    }

    /// <summary>实际的缩放上限（不小于下限）。</summary>
    public double MaxScale => Math.Max(Max, MinScale);

    /// <summary>状态变了：控件重新排内容、更新缩放的百分比。</summary>
    public event Action? Changed;

    /// <summary>视口的中心（缩放按钮、键盘的缩放中心）。</summary>
    public (double X, double Y) Center
    {
        get
        {
            var (width, height) = viewport();
            return (width / 2, height / 2);
        }
    }

    /// <summary>以 <paramref name="origin"/> 为中心缩放到 <paramref name="next"/>：origin 下的内容点缩放前后不动。</summary>
    public static PanZoomState ZoomAround(PanZoomState state, double next, (double X, double Y) origin)
    {
        var ratio = next / state.Scale;
        return new PanZoomState(origin.X - (origin.X - state.X) * ratio, origin.Y - (origin.Y - state.Y) * ratio, next);
    }

    /// <summary>平移的限制：内容比区域（视口或裁剪框）大时边缘不进到区域里，比区域小时在区域里居中。</summary>
    public static PanZoomState ClampPan(PanZoomState state, PanZoomRegion region, (double Width, double Height) content)
    {
        static double Axis(double offset, double start, double room, double size) => size <= room ? start + (room - size) / 2 : Math.Clamp(offset, start + room - size, start);
        return state with
        {
            X = Axis(state.X, region.X, region.Width, content.Width * state.Scale),
            Y = Axis(state.Y, region.Y, region.Height, content.Height * state.Scale),
        };
    }

    /// <summary>
    /// 把内容整个放进视口并居中的状态：缩放取放得下的最大值（不超过原大，再限制在 min–max），四周留出 padding；
    /// 视口或内容的宽高为 0 时缩放按 1 算。
    /// </summary>
    public static PanZoomState FitTransform((double Width, double Height) viewport, (double Width, double Height) content, double padding = 0, double min = 0, double max = double.PositiveInfinity)
    {
        static double Fits(double room, double size) => size > 0 && room > 0 ? room / size : 1;
        var scale = Math.Clamp(Math.Min(Math.Min(Fits(viewport.Width - padding * 2, content.Width), Fits(viewport.Height - padding * 2, content.Height)), 1), min, Math.Max(min, max));
        return new PanZoomState((viewport.Width - content.Width * scale) / 2, (viewport.Height - content.Height * scale) / 2, scale);
    }

    /// <summary>换成新状态（夹在缩放范围里；有内容大小时限制平移）。</summary>
    public void Apply(PanZoomState next)
    {
        if (Bounds?.Invoke() is { } content)
            next = ClampPan(next with { Scale = Math.Clamp(next.Scale, MinScale, MaxScale) }, Region, content);
        if (next == State) return;
        State = next;
        Changed?.Invoke();
    }

    /// <summary>缩放到 <paramref name="scale"/>（夹在 min–max）；origin 是缩放中心（视口内坐标），默认视口中心。</summary>
    public void ZoomTo(double scale, (double X, double Y)? origin = null)
    {
        var next = Math.Clamp(scale, MinScale, MaxScale);
        if (next == State.Scale) return;
        Apply(ZoomAround(State, next, origin ?? Center));
    }

    /// <summary>放大一档。</summary>
    public void ZoomIn((double X, double Y)? origin = null) => ZoomTo(State.Scale * Step, origin);

    /// <summary>缩小一档。</summary>
    public void ZoomOut((double X, double Y)? origin = null) => ZoomTo(State.Scale / Step, origin);

    /// <summary>平移一段（拖动的增量）。</summary>
    public void PanBy(double dx, double dy) => Apply(State with { X = State.X + dx, Y = State.Y + dy });

    /// <summary>回到初始。</summary>
    public void Reset() => Apply(Initial);

    /// <summary>把内容整个放进视口并居中（不超过原大），四周留出 padding；返回用到的缩放。</summary>
    public double Fit((double Width, double Height) content, double padding = 0)
    {
        var next = FitTransform(viewport(), content, padding, Min, Max);
        if (next != State)
        {
            State = next;
            Changed?.Invoke();
        }
        return next.Scale;
    }

    /// <summary>
    /// 滚轮：以指针为中心连续缩放。<paramref name="delta"/> 是平台报的滚动量（向下为正，鼠标滚轮一格约 1；触控板的捏合小得多），
    /// 按 e 的指数换成倍数，两种都顺手（同上游按 deltaY 连续缩放）。
    /// </summary>
    public void Wheel(double delta, (double X, double Y) origin) => ZoomTo(State.Scale * Math.Exp(-delta * 0.2), origin);

    /// <summary>双击：已放大时回到初始，否则以那一点为中心放大两档。</summary>
    public void DoubleTap((double X, double Y) origin)
    {
        if (State.Scale > Initial.Scale) Apply(Initial);
        else ZoomTo(State.Scale * Step * Step, origin);
    }

    /// <summary>键盘：缩放、回到初始、方向键平移。</summary>
    public void Press(PanZoomKey key)
    {
        switch (key)
        {
            case PanZoomKey.ZoomIn: ZoomIn(); break;
            case PanZoomKey.ZoomOut: ZoomOut(); break;
            case PanZoomKey.Reset: Reset(); break;
            case PanZoomKey.Left: PanBy(KeyPan, 0); break;
            case PanZoomKey.Right: PanBy(-KeyPan, 0); break;
            case PanZoomKey.Up: PanBy(0, KeyPan); break;
            case PanZoomKey.Down: PanBy(0, -KeyPan); break;
        }
    }
}
