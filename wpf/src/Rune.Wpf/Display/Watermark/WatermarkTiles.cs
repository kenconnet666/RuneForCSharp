namespace Rune.Wpf;

/// <summary>
/// Watermark 的平铺（同上游 canvas 画的那一格）：一块平铺单元是两行两列的格子，左上、右下斜对角各放一份内容，铺开后相邻两行错开半格，
/// 铺得匀（同 antd）。一格 = 内容 + 间隙；内容绕格子中心旋转。单元从左上角 (0, 0) 起铺满，伸出单元的部分裁掉（canvas 的边界）。
/// </summary>
static class WatermarkTiles
{
    /// <summary>默认旋转角度（度）。</summary>
    public const double Rotate = -22;

    /// <summary>默认字号（px）。</summary>
    public const double FontSize = 14;

    /// <summary>默认图片宽（px）。</summary>
    public const double ImageWidth = 120;

    /// <summary>默认图片高（px）。</summary>
    public const double ImageHeight = 40;

    /// <summary>默认横向间隙（px）。</summary>
    public const double GapX = 120;

    /// <summary>默认竖向间隙（px）。</summary>
    public const double GapY = 100;

    /// <summary>默认颜色 rgba(128, 128, 128, 0.16) 的 ARGB：半透明的灰，亮色、暗色主题上都看得见、不抢内容。</summary>
    public const uint Color = 0x29808080;

    /// <summary>行高：字号的 1.4 倍。</summary>
    public static double LineHeight(double fontSize) => fontSize * 1.4;

    /// <summary>文字按换行拆成几行（多行写成"姓名\n日期"）。</summary>
    public static string[] Lines(string? text) => string.IsNullOrEmpty(text) ? [] : text.Replace("\r\n", "\n").Split('\n');

    /// <summary>第 <paramref name="index"/> 行的中心相对内容中心的竖向偏移：几行整体居中。</summary>
    public static double LineOffset(int index, int count, double lineHeight) => (index - (count - 1) / 2d) * lineHeight;

    /// <summary>铺满 width × height 的每块单元。</summary>
    /// <param name="width">铺的区域宽。</param>
    /// <param name="height">铺的区域高。</param>
    /// <param name="cellWidth">一格的宽：内容宽 + 横向间隙。</param>
    /// <param name="cellHeight">一格的高：内容高 + 竖向间隙。</param>
    public static IEnumerable<WatermarkTile> Layout(double width, double height, double cellWidth, double cellHeight)
    {
        if (cellWidth <= 0 || cellHeight <= 0) yield break;
        for (var y = 0d; y < height; y += 2 * cellHeight)
            for (var x = 0d; x < width; x += 2 * cellWidth)
                yield return new WatermarkTile(x, y, 2 * cellWidth, 2 * cellHeight,
                    x + cellWidth / 2, y + cellHeight / 2, x + cellWidth * 1.5, y + cellHeight * 1.5);
    }
}

/// <summary>一块平铺单元：矩形（裁剪用）与两份内容的中心（左上格、右下格）。</summary>
readonly record struct WatermarkTile(double X, double Y, double Width, double Height, double FirstX, double FirstY, double SecondX, double SecondY);
