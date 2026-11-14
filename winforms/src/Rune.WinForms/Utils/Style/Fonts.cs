using System.Collections.Concurrent;
using System.Drawing.Text;
using System.Runtime.CompilerServices;

namespace Rune.WinForms;

/// <summary>
/// 主题的字体是一串候选（同 CSS 的 font-family），<see cref="Installed"/> 取第一个装了的：
/// <code>
/// form.Font = new Font(Fonts.Installed(RuneTheme.Current.Value.Fonts.Sans), Tokens.FontSize.Md * 72 / 96);
/// </code>
/// 元素绘制用的字体按（字体、磅值、样式）共用一个 Font，不随元素创建与释放（数量有限：几种字体 × 几档字号 × 几种样式）。
/// </summary>
public static class Fonts
{
    static readonly ConcurrentDictionary<(string Family, float Points, FontStyle Style), Font> fonts = new();
    static readonly ConcurrentDictionary<IReadOnlyList<string>, string> installed = new();

    /// <summary>候选里第一个装了的字体；都没装时用系统的界面字体。</summary>
    /// <param name="candidates">主题里的一串字体（<see cref="ThemeFonts.Sans"/>、<see cref="ThemeFonts.Mono"/>……）。</param>
    public static string Installed(IReadOnlyList<string> candidates) => installed.GetOrAdd(candidates, list =>
    {
        using var collection = new InstalledFontCollection();
        var names = collection.Families.Select(family => family.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return list.FirstOrDefault(names.Contains) ?? SystemFonts.MessageBoxFont!.FontFamily.Name;
    });

    /// <summary>共用的字体（不要 Dispose）。</summary>
    internal static Font Get(string family, float points, FontStyle style) =>
        fonts.GetOrAdd((family, points, style), key => new Font(key.Family, key.Points, key.Style));

    /// <summary>1em 是多少设备像素：字号（磅）按 DPI 换算。</summary>
    internal static float EmPixels(this Font font, int dpi) => font.SizeInPoints * dpi / 72f;

    /// <summary>控件的 1em（设备像素）：按控件的字体与所在显示器的 DPI。</summary>
    internal static float EmPixels(this Control control) => control.Font.EmPixels(control.DeviceDpi);

    static readonly ConditionalWeakTable<Font, StrongBox<int>> lineHeights = [];

    /// <summary>
    /// 一行字的高度：按 TextRenderer 量出来的算（比 Font.Height 多一两个像素），与多行文字量出来的高度一致。
    /// 按字体缓存：每个文字元素每次测量都要它，量一次要一次 GDI 调用。宿主的字体可能被释放，弱引用缓存不延长它的寿命。
    /// </summary>
    internal static int LineHeight(Font font) => lineHeights.GetValue(font, key =>
        new StrongBox<int>(TextRenderer.MeasureText("Ag", key, default(System.Drawing.Size), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height)).Value;
}
