using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Rune.Avalonia;

/// <summary>
/// “纸面”的颜色（vue-tsx-pro 的 utils/style/paper）：要被拍照、扫描、打印、导出成图片的东西（二维码、手写签名）一律浅底深色。
/// 浅色主题下跟着主题的 surface 与文字色；深色主题下仍是白底近黑，不跟着反色（反色的二维码很多扫码器认不出，导出的签名放到白纸上也看不见）。
/// </summary>
static class Paper
{
    static readonly IBrush DarkBackground = new ImmutableSolidColorBrush(Colors.White);
    static readonly IBrush DarkInk = new ImmutableSolidColorBrush(Color.Parse("#1c2024"));

    /// <summary>纸面的底色。</summary>
    public static IBrush Background(Control control) => IsDark(control) ? DarkBackground : control.Resource<IBrush>(ThemeKeys.Brush.Surface);

    /// <summary>纸面上的墨色。</summary>
    public static IBrush Ink(Control control) => IsDark(control) ? DarkInk : control.Resource<IBrush>(ThemeKeys.Brush.Text);

    static bool IsDark(Control control)
    {
        // 还没放进窗口时没有主题变体：按浅色
        ThemeVariant? variant = control.ActualThemeVariant;
        while (variant?.InheritVariant is ThemeVariant parent) variant = parent;
        return variant == ThemeVariant.Dark;
    }
}
