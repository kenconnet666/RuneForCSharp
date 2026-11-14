using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Rune.Wpf;

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
    /// <param name="layer">波纹层（铺满宿主、不接收指针、裁成宿主的圆角）。</param>
    /// <param name="origin">圆心（波纹层的坐标）。</param>
    /// <param name="color">宿主的文字颜色（实底按钮上是浅色，描边按钮上是深色），取它的 22%。</param>
    public static Action Start(Canvas layer, Point origin, Brush? color)
    {
        var (width, height) = (layer.ActualWidth, layer.ActualHeight);
        var radius = Math.Sqrt(Math.Pow(Math.Max(origin.X, width - origin.X), 2) + Math.Pow(Math.Max(origin.Y, height - origin.Y), 2));
        var scale = new ScaleTransform(1, 1, radius, radius);
        var fill = new SolidColorBrush((color as SolidColorBrush)?.Color ?? Colors.Black) { Opacity = 0.22 };
        fill.Freeze();
        var ripple = new Ellipse { Width = 2 * radius, Height = 2 * radius, Fill = fill, IsHitTestVisible = false, RenderTransform = scale };
        Canvas.SetLeft(ripple, origin.X - radius);
        Canvas.SetTop(ripple, origin.Y - radius);
        layer.Children.Add(ripple);

        var expand = Motion.Duration(layer, ThemeKeys.Duration.Lg);
        if (expand > TimeSpan.Zero)
        {
            var grow = new DoubleAnimation(0, 1, expand) { EasingFunction = layer.Resource<IEasingFunction>(ThemeKeys.Easing.Standard) };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }

        var released = false;
        return () =>
        {
            if (released) return;
            released = true;
            var fade = Motion.Duration(layer, ThemeKeys.Duration.Md);
            if (fade <= TimeSpan.Zero)
            {
                layer.Children.Remove(ripple);
                return;
            }
            var out_ = new DoubleAnimation(0, fade) { EasingFunction = layer.Resource<IEasingFunction>(ThemeKeys.Easing.Exit) };
            out_.Completed += (_, _) => layer.Children.Remove(ripple);
            ripple.BeginAnimation(UIElement.OpacityProperty, out_);
        };
    }
}
