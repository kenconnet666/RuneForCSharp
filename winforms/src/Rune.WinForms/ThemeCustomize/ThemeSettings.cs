namespace Rune.WinForms;

/// <summary>圆角档位（vue-tsx-pro 的 RadiusPresets）：默认圆角的倍数。</summary>
public enum ThemeRadius
{
    /// <summary>直角。</summary>
    None,

    /// <summary>小圆角（默认的 0.5 倍）。</summary>
    Small,

    /// <summary>默认。</summary>
    Medium,

    /// <summary>大圆角（默认的 1.75 倍）。</summary>
    Large,
}

/// <summary>字体搭配（vue-tsx-pro 的 FontPresets）。</summary>
public enum ThemeFont
{
    /// <summary>正文与标题都是无衬线（默认）。</summary>
    Sans,

    /// <summary>标题用衬线，正文无衬线：书卷感，长文仍好读。</summary>
    SerifHeadings,
}

/// <summary>动效速度（vue-tsx-pro 的 MotionPresets）。</summary>
public enum ThemeMotion
{
    /// <summary>关掉过渡与动画。</summary>
    None,

    /// <summary>快（默认时长的 0.6 倍）。</summary>
    Fast,

    /// <summary>默认。</summary>
    Normal,

    /// <summary>慢（默认时长的 1.5 倍）。</summary>
    Slow,
}

/// <summary>
/// 主题定制的设置（vue-tsx-pro 的 ThemeSettings）：几个旋钮（品牌色、中性色、圆角、缩放、字体、动效）；不写的旋钮沿用主题本来的取值。
/// 换算成 token 覆盖见 <see cref="ThemeSettingsMath"/>，套到界面上由各库的 ThemeCustomization 做。
/// </summary>
/// <param name="Accent">品牌色（<see cref="ThemePalette.Accents"/> 里的 Radix 色阶名，如 crimson）。</param>
/// <param name="Neutral">中性色（<see cref="ThemePalette.Neutrals"/> 里的 Radix 灰色色阶名，如 mauve）。</param>
/// <param name="Radius">圆角。</param>
/// <param name="Scaling">缩放：字号的倍数，0.9–1.1；间距、控件高度以 em 计，随字号一起缩放。</param>
/// <param name="Font">字体搭配。</param>
/// <param name="Motion">动效速度。</param>
public sealed record ThemeSettings(string? Accent = null, string? Neutral = null, ThemeRadius? Radius = null, double? Scaling = null, ThemeFont? Font = null, ThemeMotion? Motion = null);

/// <summary>
/// 主题配色的 Radix 色阶（vue-tsx-pro 的 palette.ts）：每个颜色 token 取色阶的哪一级（亮色、暗色各一级），以及按选中的色阶生成颜色覆盖。
/// 内置主题就是按这张表从 indigo（品牌色）与 slate（中性色）取的值；主题编辑器换品牌色、中性色时也按它整组替换，层级关系（底色、悬停、边框、文字）不变。
/// </summary>
public static class ThemePalette
{
    /// <summary>可选的品牌色，按色相排列（主题编辑器按这个顺序列出）。</summary>
    public static IReadOnlyList<string> Accents { get; } = [.. RadixScales.Accents.Keys];

    /// <summary>可选的中性色。</summary>
    public static IReadOnlyList<string> Neutrals { get; } = [.. RadixScales.Neutrals.Keys];

    /// <summary>品牌色 token → [亮色取第几级, 暗色取第几级]（Radix 的用途约定：9–10 实色，11 文字，3–4 浅底，7 边框）。</summary>
    public static IReadOnlyDictionary<string, (int Light, int Dark)> AccentSteps { get; } = new Dictionary<string, (int, int)>
    {
        ["accent"] = (9, 9),
        ["accentHover"] = (10, 10),
        ["accentText"] = (11, 11),
        ["accentSoft"] = (3, 3),
        ["accentSoftHover"] = (4, 4),
        ["accentBorder"] = (7, 7),
        ["focusRing"] = (9, 9),
        ["selection"] = (5, 5),
        ["codeKeyword"] = (11, 11),
        // 图表的第一个系列跟随品牌色
        ["chart1"] = (9, 9),
    };

    /// <summary>中性色 token → [亮色取第几级, 暗色取第几级]（1–2 背景，3–5 填充，6–8 边框，11–12 文字）。</summary>
    public static IReadOnlyDictionary<string, (int Light, int Dark)> NeutralSteps { get; } = new Dictionary<string, (int, int)>
    {
        // 亮色的页面着一点色（2 级），卡片、浮层是白色（见 Colors）
        ["background"] = (2, 1),
        ["surface"] = (1, 2),
        ["surfaceMuted"] = (2, 2),
        // 暗色不取第 3 级：那是 fill，滑块轨道、进度条底色、标签放进浮层会看不见；浮层靠描边与阴影分层（同 Radix Themes 的 panel）
        ["surfaceRaised"] = (1, 2),
        ["fill"] = (3, 3),
        ["fillHover"] = (4, 4),
        ["fillActive"] = (5, 5),
        ["text"] = (12, 12),
        ["textMuted"] = (11, 11),
        ["textPlaceholder"] = (11, 11),
        ["textDisabled"] = (8, 8),
        ["borderMuted"] = (6, 6),
        ["border"] = (7, 7),
        ["borderStrong"] = (8, 8),
        ["inverseSurface"] = (12, 12),
        ["inverseText"] = (1, 1),
        ["codeComment"] = (11, 11),
    };

    /// <summary>半透明表面（粘性顶栏）：背景色加 80% 不透明度。</summary>
    const double TranslucentAlpha = 0.8;

    /// <summary>色阶的第 level 级（1–12）。</summary>
    /// <param name="scale">色阶名（品牌色或中性色）。</param>
    /// <param name="level">第几级。</param>
    /// <param name="dark">取暗色色阶。</param>
    public static string Step(string scale, int level, bool dark)
    {
        var pair = RadixScales.Accents.TryGetValue(scale, out var accent) ? accent
            : RadixScales.Neutrals.TryGetValue(scale, out var neutral) ? neutral
            : throw new ArgumentException($"没有 Radix 色阶 {scale}", nameof(scale));
        return (dark ? pair.Dark : pair.Light)[level - 1];
    }

    /// <summary>色阶的代表色（第 9 级实色）：主题编辑器里选色阶的色块。</summary>
    public static string Swatch(string scale, bool dark) => Step(scale, 9, dark);

    /// <summary>按选中的色阶生成颜色 token 的覆盖（token 名 → #rrggbb 或 rgba(…)）；accent、neutral 都不写时为空。</summary>
    /// <param name="accent">品牌色；null 时不改品牌色。</param>
    /// <param name="neutral">中性色；null 时不改中性色。</param>
    /// <param name="dark">取暗色色阶。</param>
    public static Dictionary<string, string> Colors(string? accent, string? neutral, bool dark)
    {
        var colors = new Dictionary<string, string>();
        void Pick(string scale, IReadOnlyDictionary<string, (int Light, int Dark)> steps)
        {
            foreach (var (token, levels) in steps) colors[token] = Step(scale, dark ? levels.Dark : levels.Light, dark);
        }
        if (accent is not null)
        {
            Pick(accent, AccentSteps);
            // 实色按钮上的文字：亮黄、青柠这类浅色实色配深色字，其余配白字
            colors["accentContrast"] = ColorMath.ReadableText(colors["accent"]);
        }
        if (neutral is not null)
        {
            Pick(neutral, NeutralSteps);
            if (!dark)
            {
                // 着色的页面 + 白色的卡片与浮层；弱化的区域取 2、3 级的中间，放在页面上也看得出
                colors["surface"] = colors["surfaceRaised"] = "#ffffff";
                colors["surfaceMuted"] = ColorMath.MixColors(colors["background"], colors["fill"]);
            }
            colors["surfaceTranslucent"] = ColorMath.WithAlpha(colors["background"], TranslucentAlpha);
        }
        return colors;
    }
}

/// <summary>设置换算成 token 覆盖要用的倍数（vue-tsx-pro customize.ts 的 RADIUS_FACTORS、MOTION_FACTORS 与缩放规则）。</summary>
public static class ThemeSettingsMath
{
    /// <summary>圆角档位的倍数：直角 0、小 0.5、默认 1、大 1.75。</summary>
    public static double RadiusFactor(ThemeRadius radius) => radius switch
    {
        ThemeRadius.None => 0,
        ThemeRadius.Small => 0.5,
        ThemeRadius.Large => 1.75,
        _ => 1,
    };

    /// <summary>动效的倍数：关 0、快 0.6、默认 1、慢 1.5。</summary>
    public static double MotionFactor(ThemeMotion motion) => motion switch
    {
        ThemeMotion.None => 0,
        ThemeMotion.Fast => 0.6,
        ThemeMotion.Slow => 1.5,
        _ => 1,
    };

    /// <summary>一个数值乘倍数，保留三位小数（同上游 scale：`0.4em` × 1.75 → `0.7em`）。</summary>
    public static double Scale(double value, double factor) => Math.Round(value * factor * 1000, MidpointRounding.AwayFromZero) / 1000;

    /// <summary>设置里有没有要覆盖的：都是默认档位（或没写）时为 false，主题原样用。</summary>
    public static bool HasOverrides(ThemeSettings settings, bool reducedMotion = false) =>
        settings.Accent is not null || settings.Neutral is not null || settings.Radius is { } radius && radius != ThemeRadius.Medium
        || settings.Scaling is { } scaling && scaling != 1 || settings.Font == ThemeFont.SerifHeadings
        || !reducedMotion && settings.Motion is { } motion && motion != ThemeMotion.Normal;
}
