namespace Rune.WinForms;

/// <summary>
/// 勾选类控件（Switch、Checkbox、Radio）的色调实底（vue-tsx-pro 的 toneFillStyles：<c>--tone-fill</c> 与其上的 <c>--tone-on-fill</c>）。
/// 和 Badge 的调色板不同：中性用反色表面，状态色用文字色而不是实色——黄、绿的实色在表面上不到 3:1，图形要的是与周围的对比。
/// </summary>
public static class ToneFill
{
    /// <summary>色调的实底，与实底上的对勾、圆点、滑块色。</summary>
    /// <param name="tone">色调；default 按 accent。</param>
    /// <param name="colors">当前主题的颜色。</param>
    public static (Color Fill, Color OnFill) Colors(StatusTone tone, ThemeColors colors) => tone.Value switch
    {
        "neutral" => (colors.InverseSurface, colors.InverseText),
        "info" => (colors.InfoText, colors.Background),
        "success" => (colors.SuccessText, colors.Background),
        "warning" => (colors.WarningText, colors.Background),
        "danger" => (colors.DangerText, colors.Background),
        _ => (colors.Accent, colors.AccentContrast),
    };
}
