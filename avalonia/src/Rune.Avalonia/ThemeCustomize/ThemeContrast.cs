namespace Rune.Avalonia;

/// <summary>要检查的一对颜色 token（vue-tsx-pro 的 ContrastPair）。</summary>
/// <param name="Foreground">前景 token。</param>
/// <param name="Background">背景 token。</param>
/// <param name="Minimum">最低对比度。</param>
/// <param name="Label">用途说明。</param>
public sealed record ContrastPair(string Foreground, string Background, double Minimum, string Label);

/// <summary>一处对比度不够的组合（vue-tsx-pro 的 ContrastIssue）。</summary>
/// <param name="Foreground">前景 token。</param>
/// <param name="Background">背景 token。</param>
/// <param name="Ratio">实际对比度。</param>
/// <param name="Minimum">要求的最低对比度。</param>
/// <param name="Label">用途说明。</param>
public sealed record ContrastIssue(string Foreground, string Background, double Ratio, double Minimum, string Label);

/// <summary>
/// 主题颜色要满足的对比度（vue-tsx-pro 的 theme-customize/contrast，WCAG 2.2 AA）：哪些文字放在哪些底色上、各要多少。主题编辑器按它实时提示改出来的配色。
/// 文字 4.5:1；大字、图形与界面部件（焦点环、选中的勾选框）3:1。
/// </summary>
public static class ThemeContrast
{
    const double Text = 4.5;
    const double Ui = 3;

    static IEnumerable<ContrastPair> On(string foreground, IEnumerable<string> backgrounds, double minimum, string label) =>
        backgrounds.Select(background => new ContrastPair(foreground, background, minimum, label));

    /// <summary>要检查的颜色组合（同上游 CONTRAST_PAIRS 的顺序）。</summary>
    public static IReadOnlyList<ContrastPair> Pairs { get; } =
    [
        .. On("text", ["background", "surface", "surfaceMuted", "surfaceRaised", "fill", "fillHover", "fillActive"], Text, "正文"),
        .. On("textMuted", ["background", "surface", "surfaceMuted", "surfaceRaised", "fill"], Text, "次要文字"),
        new("textPlaceholder", "surface", Ui, "占位文字"),
        new("accentContrast", "accent", Text, "主按钮"),
        new("accentContrast", "accentHover", Text, "主按钮悬停"),
        .. On("accentText", ["background", "surface", "surfaceRaised", "accentSoft", "accentSoftHover"], Text, "链接、选中项"),
        new("dangerContrast", "danger", Text, "危险按钮"),
        new("dangerContrast", "dangerHover", Text, "危险按钮悬停"),
        .. On("dangerText", ["background", "surface", "dangerSoft", "dangerSoftHover"], Text, "错误文字"),
        .. new[] { ("successText", "successSoft"), ("warningText", "warningSoft"), ("infoText", "infoSoft") }.SelectMany(pair => new ContrastPair[]
        {
            new(pair.Item1, pair.Item2, Text, "提示条"),
            new(pair.Item1, "background", Text, "提示文字"),
            new(pair.Item1, "surface", Text, "提示文字"),
        }),
        .. new[] { "success", "warning", "info" }.SelectMany(tone => new ContrastPair[]
        {
            new($"{tone}Contrast", tone, Text, "状态实色（按钮、实底徽标）"),
            new($"{tone}Contrast", $"{tone}Hover", Text, "状态实色悬停"),
            new($"{tone}Text", $"{tone}SoftHover", Text, "状态浅底悬停"),
        }),
        new("inverseText", "inverseSurface", Text, "反色表面"),
        // 占位文字也是要读的文字：表面色的输入框与浅底（soft）的输入框
        new("textPlaceholder", "surface", Text, "占位文字"),
        new("textPlaceholder", "fill", Text, "占位文字（浅底输入框）"),
        // 遮罩是半透明的：按叠在页面背景上算（盖在图片上时由遮罩本身保证够深）
        new("onScrim", "scrim", Text, "图片上的遮罩"),
        // 代码块（surfaceMuted）、行内代码与高亮行（fill）、diff 的增删行上都有高亮的代码
        .. new[] { "codeKeyword", "codeString", "codeNumber", "codeFunction", "codeType", "codeComment", "codeTag", "codeAttribute" }
            .SelectMany(foreground => On(foreground, ["surfaceMuted", "fill", "codeInserted", "codeDeleted"], Text, "代码高亮")),
        new("text", "codeInserted", Text, "diff 新增行"),
        new("text", "codeDeleted", Text, "diff 删除行"),
        new("text", "selection", Text, "选中的文字"),
        new("text", "highlight", Text, "高亮标记"),
        new("focusRing", "background", Ui, "焦点环"),
        new("focusRing", "surface", Ui, "焦点环"),
        // 选中的勾选框、开关、滑块与进度条的实底色：在表面与轨道上
        .. new[] { "accent", "inverseSurface", "infoText", "successText", "warningText", "dangerText" }.SelectMany(foreground => new ContrastPair[]
        {
            new(foreground, "surface", Ui, "选中的勾选框、开关"),
            new(foreground, "fill", Ui, "滑块、进度条"),
        }),
        .. Enumerable.Range(1, 8).SelectMany(index => On($"chart{index}", ["background", "surface"], Ui, "图表系列")),
    ];

    /// <summary>按 <see cref="Pairs"/> 检查一套配色；read 读出 token 的取值（CSS 颜色文字）。半透明的背景叠在 background（页面背景）上算。</summary>
    public static IReadOnlyList<ContrastIssue> Check(Func<string, string> read)
    {
        var page = read("background");
        return [.. Pairs.Select(pair => (pair, ratio: ColorMath.ContrastRatio(read(pair.Foreground), read(pair.Background), page)))
            .Where(item => item.ratio < item.pair.Minimum)
            .Select(item => new ContrastIssue(item.pair.Foreground, item.pair.Background, item.ratio, item.pair.Minimum, item.pair.Label))];
    }
}
