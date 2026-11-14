using global::Avalonia;
using global::Avalonia.Animation;
using global::Avalonia.Animation.Easings;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Shapes;
using global::Avalonia.Media;
using global::Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 波纹（vue-tsx-pro 的 utils/dom/ripple）：按下时从指针位置扩散出一圈半透明的文字颜色，松开后淡出；
/// 键盘按下 Enter、空格时从中心扩散。扩散用 durations.lg 与标准缓动，淡出用 durations.md 与离场缓动；
/// 系统关闭动画时没有扩散过程，按下时直接盖满。按钮、导航项、菜单项等可点击的组件共用。
/// </summary>
public static class Ripple
{
    /// <summary>全局开关（vue-tsx-pro 的 createUi / Provider 的 ripple，默认开启）；组件的 Ripple 属性单独覆盖。</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// 在 <paramref name="layer"/> 上从 <paramref name="origin"/> 开始一个波纹，返回松开时调用的淡出。
    /// 半径是圆心到最远的角的距离：扩散完正好盖满整个宿主。
    /// </summary>
    /// <param name="layer">波纹层（铺满宿主、不接收指针、随宿主圆角裁切）。</param>
    /// <param name="origin">圆心（波纹层的坐标）。</param>
    /// <param name="color">宿主的文字颜色（实底按钮上是浅色，描边按钮上是深色），取它的 22%。</param>
    public static Action Start(Canvas layer, Point origin, IBrush? color)
    {
        var (width, height) = (layer.Bounds.Width, layer.Bounds.Height);
        var radius = Math.Sqrt(Math.Pow(Math.Max(origin.X, width - origin.X), 2) + Math.Pow(Math.Max(origin.Y, height - origin.Y), 2));
        var expand = Motion.Duration(layer, ThemeKeys.Duration.Lg);
        var scale = new ScaleTransform(expand > TimeSpan.Zero ? 0 : 1, expand > TimeSpan.Zero ? 0 : 1);
        var ripple = new Ellipse
        {
            Width = 2 * radius,
            Height = 2 * radius,
            Fill = new SolidColorBrush((color as ISolidColorBrush)?.Color ?? Colors.Black, 0.22),
            IsHitTestVisible = false,
            RenderTransform = scale,
        };
        Canvas.SetLeft(ripple, origin.X - radius);
        Canvas.SetTop(ripple, origin.Y - radius);
        layer.Children.Add(ripple);
        if (expand > TimeSpan.Zero)
        {
            var easing = layer.Resource<Easing>(ThemeKeys.Easing.Standard);
            scale.Transitions = [Transition(ScaleTransform.ScaleXProperty, expand, easing), Transition(ScaleTransform.ScaleYProperty, expand, easing)];
            // 加进树之后再改值，过渡才会播
            Dispatcher.UIThread.Post(() => scale.ScaleX = scale.ScaleY = 1, DispatcherPriority.Background);
        }

        var released = false;
        return () =>
        {
            if (released) return;
            released = true;
            var fade = Motion.Duration(layer, ThemeKeys.Duration.Md);
            ripple.Transitions = [Transition(Visual.OpacityProperty, fade, layer.Resource<Easing>(ThemeKeys.Easing.Exit))];
            ripple.Opacity = 0;
            DispatcherTimer.RunOnce(() => layer.Children.Remove(ripple), fade + TimeSpan.FromMilliseconds(50));
        };
    }

    static DoubleTransition Transition(AvaloniaProperty property, TimeSpan duration, Easing easing) =>
        new() { Property = property, Duration = duration, Easing = easing };


}
