namespace Rune.Wpf;

/// <summary>
/// 勾选类控件（Switch、Checkbox、Radio）的色调实底（vue-tsx-pro 的 toneFillStyles：<c>--tone-fill</c> 与其上的 <c>--tone-on-fill</c>）。
/// 和 Badge 的调色板不同：中性用反色表面，状态色用文字色而不是实色——黄、绿的实色在表面上不到 3:1，图形要的是与周围的对比。
/// </summary>
public static class ToneFill
{
    /// <summary>色调对应的画刷资源键：实底，与实底上的对勾、圆点、滑块色。</summary>
    /// <param name="tone">色调；default 按 accent。</param>
    public static (string Fill, string OnFill) Keys(StatusTone tone) => tone.Value switch
    {
        "neutral" => ("Rune.Brush.InverseSurface", "Rune.Brush.InverseText"),
        "info" => ("Rune.Brush.InfoText", "Rune.Brush.Background"),
        "success" => ("Rune.Brush.SuccessText", "Rune.Brush.Background"),
        "warning" => ("Rune.Brush.WarningText", "Rune.Brush.Background"),
        "danger" => ("Rune.Brush.DangerText", "Rune.Brush.Background"),
        _ => ("Rune.Brush.Accent", "Rune.Brush.AccentContrast"),
    };
}
