using System.Windows;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// “纸面”的颜色（vue-tsx-pro 的 utils/style/paper）：要被拍照、扫描、打印、导出成图片的东西（二维码、手写签名）一律浅底深色。
/// 浅色主题下跟着主题的 surface 与文字色；深色主题下仍是白底近黑，不跟着反色（反色的二维码很多扫码器认不出，导出的签名放到白纸上也看不见）。
/// 深浅按主题的 surface 亮不亮判断。
/// </summary>
static class Paper
{
    static readonly Brush DarkBackground = Frozen(new SolidColorBrush(Colors.White));
    static readonly Brush DarkInk = Frozen(new SolidColorBrush(Color.FromRgb(0x1c, 0x20, 0x24)));

    static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    /// <summary>纸面的底色。</summary>
    public static Brush Background(FrameworkElement element) =>
        element.TryFindResource(ThemeKeys.Brush.Surface) is SolidColorBrush surface && !IsDark(surface) ? surface : DarkBackground;

    /// <summary>纸面上的墨色。</summary>
    public static Brush Ink(FrameworkElement element) =>
        element.TryFindResource(ThemeKeys.Brush.Surface) is SolidColorBrush surface && !IsDark(surface) && element.TryFindResource(ThemeKeys.Brush.Text) is Brush text ? text : DarkInk;

    static bool IsDark(SolidColorBrush surface) => (0.2126 * surface.Color.R + 0.7152 * surface.Color.G + 0.0722 * surface.Color.B) / 255 < 0.5;
}
