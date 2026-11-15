namespace Rune.WinForms;

/// <summary>
/// “纸面”的颜色（vue-tsx-pro 的 utils/style/paper）：要被拍照、扫描、打印、导出成图片的东西（二维码、手写签名）一律浅底深色。
/// 浅色主题下跟着主题的 surface 与文字色；深色主题下仍是白底近黑，不跟着反色（反色的二维码很多扫码器认不出，导出的签名放到白纸上也看不见）。
/// </summary>
static class Paper
{
    static readonly Color DarkInk = Color.FromArgb(0x1c, 0x20, 0x24);

    /// <summary>纸面的底色。</summary>
    public static Color Background(Control control) => ProviderControl.ThemeOf(control) is { IsDark: false } theme ? theme.Colors.Surface : Color.White;

    /// <summary>纸面上的墨色。</summary>
    public static Color Ink(Control control) => ProviderControl.ThemeOf(control) is { IsDark: false } theme ? theme.Colors.Text : DarkInk;
}
