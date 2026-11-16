using System.Globalization;
using System.Text.RegularExpressions;

namespace Rune.WinForms;

/// <summary>sRGB 颜色：各通道 0–1（解析出来的可能超出，格式化时夹回），不透明度 0–1。</summary>
/// <param name="R">红。</param>
/// <param name="G">绿。</param>
/// <param name="B">蓝。</param>
/// <param name="A">不透明度。</param>
public readonly record struct Rgba(double R, double G, double B, double A = 1);

/// <summary>取色器用的颜色：色相 0–360、饱和度与明度 0–1、不透明度 0–1。</summary>
/// <param name="H">色相（度）。</param>
/// <param name="S">饱和度。</param>
/// <param name="V">明度。</param>
/// <param name="A">不透明度。</param>
public readonly record struct Hsva(double H, double S, double V, double A = 1);

/// <summary>颜色的几种写法。</summary>
public enum ColorFormat
{
    /// <summary>#rrggbb / #rrggbbaa。</summary>
    Hex,

    /// <summary>rgb() / rgba()。</summary>
    Rgb,

    /// <summary>hsl() / hsla()。</summary>
    Hsl,
}

/// <summary>
/// 颜色工具（vue-tsx-pro 的 utils/data/color，上游用 culori）：解析 CSS 颜色（#hex、rgb()、hsl()、颜色名）、格式化（输出与 culori 逐字相同）、
/// HSV 互转、半透明颜色的叠加、WCAG 对比度、按对比度挑文字色。三个库各一份（孪生文件）。
/// <code>
/// ColorMath.ContrastRatio("#3e63dd", "#ffffff")   // 4.6
/// ColorMath.ReadableText("#ffc53d")               // 深色文字（亮黄底上白字达不到 4.5:1）
/// </code>
/// </summary>
public static class ColorMath
{
    /// <summary>WCAG 2.2 AA：正文文字的对比度要求。</summary>
    public const double ContrastText = 4.5;

    /// <summary>WCAG 2.2 AA：大字、图形与界面部件的对比度要求。</summary>
    public const double ContrastUi = 3;

    const string Number = @"([+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?)";
    const string Comma = @"\s*,\s*";
    const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    static readonly Regex Hex = new(@"^#([0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})$", Options);
    static readonly Regex RgbLegacy = new($@"^rgba?\(\s*{Number}(%?){Comma}{Number}(%?){Comma}{Number}(%?)\s*(?:,\s*{Number}(%?)\s*)?\)$", Options);
    static readonly Regex RgbModern = new($@"^rgba?\(\s*{Number}(%?)\s+{Number}(%?)\s+{Number}(%?)\s*(?:/\s*{Number}(%?)\s*)?\)$", Options);
    static readonly Regex HslLegacy = new($@"^hsla?\(\s*{Number}(deg|rad|grad|turn)?{Comma}{Number}%{Comma}{Number}%\s*(?:,\s*{Number}(%?)\s*)?\)$", Options);
    static readonly Regex HslModern = new($@"^hsla?\(\s*{Number}(deg|rad|grad|turn)?\s+{Number}%?\s+{Number}%?\s*(?:/\s*{Number}(%?)\s*)?\)$", Options);

    /// <summary>CSS 的颜色名（同 culori 的 named）。</summary>
    static readonly Dictionary<string, int> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = 0xf0f8ff, ["antiquewhite"] = 0xfaebd7, ["aqua"] = 0x00ffff, ["aquamarine"] = 0x7fffd4, ["azure"] = 0xf0ffff, ["beige"] = 0xf5f5dc,
        ["bisque"] = 0xffe4c4, ["black"] = 0x000000, ["blanchedalmond"] = 0xffebcd, ["blue"] = 0x0000ff, ["blueviolet"] = 0x8a2be2, ["brown"] = 0xa52a2a,
        ["burlywood"] = 0xdeb887, ["cadetblue"] = 0x5f9ea0, ["chartreuse"] = 0x7fff00, ["chocolate"] = 0xd2691e, ["coral"] = 0xff7f50,
        ["cornflowerblue"] = 0x6495ed, ["cornsilk"] = 0xfff8dc, ["crimson"] = 0xdc143c, ["cyan"] = 0x00ffff, ["darkblue"] = 0x00008b, ["darkcyan"] = 0x008b8b,
        ["darkgoldenrod"] = 0xb8860b, ["darkgray"] = 0xa9a9a9, ["darkgreen"] = 0x006400, ["darkgrey"] = 0xa9a9a9, ["darkkhaki"] = 0xbdb76b,
        ["darkmagenta"] = 0x8b008b, ["darkolivegreen"] = 0x556b2f, ["darkorange"] = 0xff8c00, ["darkorchid"] = 0x9932cc, ["darkred"] = 0x8b0000,
        ["darksalmon"] = 0xe9967a, ["darkseagreen"] = 0x8fbc8f, ["darkslateblue"] = 0x483d8b, ["darkslategray"] = 0x2f4f4f, ["darkslategrey"] = 0x2f4f4f,
        ["darkturquoise"] = 0x00ced1, ["darkviolet"] = 0x9400d3, ["deeppink"] = 0xff1493, ["deepskyblue"] = 0x00bfff, ["dimgray"] = 0x696969,
        ["dimgrey"] = 0x696969, ["dodgerblue"] = 0x1e90ff, ["firebrick"] = 0xb22222, ["floralwhite"] = 0xfffaf0, ["forestgreen"] = 0x228b22,
        ["fuchsia"] = 0xff00ff, ["gainsboro"] = 0xdcdcdc, ["ghostwhite"] = 0xf8f8ff, ["gold"] = 0xffd700, ["goldenrod"] = 0xdaa520, ["gray"] = 0x808080,
        ["green"] = 0x008000, ["greenyellow"] = 0xadff2f, ["grey"] = 0x808080, ["honeydew"] = 0xf0fff0, ["hotpink"] = 0xff69b4, ["indianred"] = 0xcd5c5c,
        ["indigo"] = 0x4b0082, ["ivory"] = 0xfffff0, ["khaki"] = 0xf0e68c, ["lavender"] = 0xe6e6fa, ["lavenderblush"] = 0xfff0f5, ["lawngreen"] = 0x7cfc00,
        ["lemonchiffon"] = 0xfffacd, ["lightblue"] = 0xadd8e6, ["lightcoral"] = 0xf08080, ["lightcyan"] = 0xe0ffff, ["lightgoldenrodyellow"] = 0xfafad2,
        ["lightgray"] = 0xd3d3d3, ["lightgreen"] = 0x90ee90, ["lightgrey"] = 0xd3d3d3, ["lightpink"] = 0xffb6c1, ["lightsalmon"] = 0xffa07a,
        ["lightseagreen"] = 0x20b2aa, ["lightskyblue"] = 0x87cefa, ["lightslategray"] = 0x778899, ["lightslategrey"] = 0x778899, ["lightsteelblue"] = 0xb0c4de,
        ["lightyellow"] = 0xffffe0, ["lime"] = 0x00ff00, ["limegreen"] = 0x32cd32, ["linen"] = 0xfaf0e6, ["magenta"] = 0xff00ff, ["maroon"] = 0x800000,
        ["mediumaquamarine"] = 0x66cdaa, ["mediumblue"] = 0x0000cd, ["mediumorchid"] = 0xba55d3, ["mediumpurple"] = 0x9370db, ["mediumseagreen"] = 0x3cb371,
        ["mediumslateblue"] = 0x7b68ee, ["mediumspringgreen"] = 0x00fa9a, ["mediumturquoise"] = 0x48d1cc, ["mediumvioletred"] = 0xc71585,
        ["midnightblue"] = 0x191970, ["mintcream"] = 0xf5fffa, ["mistyrose"] = 0xffe4e1, ["moccasin"] = 0xffe4b5, ["navajowhite"] = 0xffdead,
        ["navy"] = 0x000080, ["oldlace"] = 0xfdf5e6, ["olive"] = 0x808000, ["olivedrab"] = 0x6b8e23, ["orange"] = 0xffa500, ["orangered"] = 0xff4500,
        ["orchid"] = 0xda70d6, ["palegoldenrod"] = 0xeee8aa, ["palegreen"] = 0x98fb98, ["paleturquoise"] = 0xafeeee, ["palevioletred"] = 0xdb7093,
        ["papayawhip"] = 0xffefd5, ["peachpuff"] = 0xffdab9, ["peru"] = 0xcd853f, ["pink"] = 0xffc0cb, ["plum"] = 0xdda0dd, ["powderblue"] = 0xb0e0e6,
        ["purple"] = 0x800080, ["rebeccapurple"] = 0x663399, ["red"] = 0xff0000, ["rosybrown"] = 0xbc8f8f, ["royalblue"] = 0x4169e1, ["saddlebrown"] = 0x8b4513,
        ["salmon"] = 0xfa8072, ["sandybrown"] = 0xf4a460, ["seagreen"] = 0x2e8b57, ["seashell"] = 0xfff5ee, ["sienna"] = 0xa0522d, ["silver"] = 0xc0c0c0,
        ["skyblue"] = 0x87ceeb, ["slateblue"] = 0x6a5acd, ["slategray"] = 0x708090, ["slategrey"] = 0x708090, ["snow"] = 0xfffafa, ["springgreen"] = 0x00ff7f,
        ["steelblue"] = 0x4682b4, ["tan"] = 0xd2b48c, ["teal"] = 0x008080, ["thistle"] = 0xd8bfd8, ["tomato"] = 0xff6347, ["turquoise"] = 0x40e0d0,
        ["violet"] = 0xee82ee, ["wheat"] = 0xf5deb3, ["white"] = 0xffffff, ["whitesmoke"] = 0xf5f5f5, ["yellow"] = 0xffff00, ["yellowgreen"] = 0x9acd32,
    };

    static double Parse(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>解析 CSS 颜色（#hex、rgb() / rgba()、hsl() / hsla()、颜色名、transparent）；解析不了时返回 null。</summary>
    public static Rgba? ParseColor(string value)
    {
        var text = value.Trim();
        if (text.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return new Rgba(0, 0, 0, 0);
        if (Named.TryGetValue(text, out var named)) return new Rgba(((named >> 16) & 0xff) / 255d, ((named >> 8) & 0xff) / 255d, (named & 0xff) / 255d);
        if (Hex.Match(text) is { Success: true } hex) return FromHex(hex.Groups[1].Value);
        if (RgbLegacy.Match(text) is { Success: true } legacy)
        {
            // 旧写法的三个通道要么都是数、要么都是百分比
            if (legacy.Groups[2].Value != legacy.Groups[4].Value || legacy.Groups[4].Value != legacy.Groups[6].Value) return null;
            return Rgb(legacy);
        }
        if (RgbModern.Match(text) is { Success: true } modern) return Rgb(modern);
        if ((HslLegacy.Match(text) is { Success: true } old ? old : HslModern.Match(text)) is { Success: true } hsl)
        {
            var unit = hsl.Groups[2].Value.ToLowerInvariant() switch { "rad" => 180 / Math.PI, "grad" => 0.9, "turn" => 360, _ => 1 };
            return HslToRgb(Parse(hsl.Groups[1].Value) * unit, Parse(hsl.Groups[3].Value) / 100, Parse(hsl.Groups[4].Value) / 100, Alpha(hsl.Groups[5], hsl.Groups[6]));
        }
        return null;
    }

    static Rgba Rgb(Match match)
    {
        double Channel(int index) => match.Groups[index * 2 + 2].Value == "%" ? Parse(match.Groups[index * 2 + 1].Value) / 100 : Parse(match.Groups[index * 2 + 1].Value) / 255;
        return new Rgba(Channel(0), Channel(1), Channel(2), Alpha(match.Groups[7], match.Groups[8]));
    }

    static double Alpha(Group value, Group percent) => !value.Success ? 1 : percent.Value == "%" ? Parse(value.Value) / 100 : Parse(value.Value);

    static Rgba FromHex(string digits)
    {
        if (digits.Length <= 4) digits = string.Concat(digits.Select(digit => $"{digit}{digit}"));
        int Byte(int index) => int.Parse(digits.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Rgba(Byte(0) / 255d, Byte(1) / 255d, Byte(2) / 255d, digits.Length == 8 ? Byte(3) / 255d : 1);
    }

    /// <summary>JS 的 Math.round：.5 往正无穷取（C# 默认是银行家舍入，格式化的结果会和 culori 差一）。</summary>
    static double Round(double value) => Math.Floor(value + 0.5);

    static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

    static int Fixup(double value) => (int)Round(Clamp01(value) * 255);

    /// <summary>保留两位小数并写成 JS 数字的样子（0.8、225.9、226）。</summary>
    static string TwoDecimals(double value) => (Round(value * 100) / 100).ToString(CultureInfo.InvariantCulture);

    /// <summary>格式化成 #rrggbb；有透明度时是 #rrggbbaa。</summary>
    public static string ToHex(Rgba color)
    {
        var hex = string.Create(CultureInfo.InvariantCulture, $"#{Fixup(color.R):x2}{Fixup(color.G):x2}{Fixup(color.B):x2}");
        return color.A < 1 ? hex + Fixup(color.A).ToString("x2", CultureInfo.InvariantCulture) : hex;
    }

    /// <summary>格式化成 #rrggbb（有透明度时 #rrggbbaa）；解析不了时返回 null。</summary>
    public static string? ToHex(string value) => ParseColor(value) is { } color ? ToHex(color) : null;

    /// <summary>rgb(r, g, b)，不透明度不满时 rgba(r, g, b, a)（同 culori 的 formatRgb）。</summary>
    public static string FormatRgb(Rgba color) => color.A >= 1
        ? $"rgb({Fixup(color.R)}, {Fixup(color.G)}, {Fixup(color.B)})"
        : $"rgba({Fixup(color.R)}, {Fixup(color.G)}, {Fixup(color.B)}, {TwoDecimals(Clamp01(color.A))})";

    /// <summary>hsl(h, s%, l%)，不透明度不满时 hsla(…, a)（同 culori 的 formatHsl：两位小数）。</summary>
    public static string FormatHsl(Rgba color)
    {
        var (h, s, l) = RgbToHsl(color);
        var body = $"{TwoDecimals(h)}, {TwoDecimals(Clamp01(s) * 100)}%, {TwoDecimals(Clamp01(l) * 100)}%";
        return color.A >= 1 ? $"hsl({body})" : $"hsla({body}, {TwoDecimals(Clamp01(color.A))})";
    }

    /// <summary>按格式写出颜色；解析不了时原样返回。</summary>
    public static string FormatColor(string value, ColorFormat format) => ParseColor(value) is not { } color ? value : format switch
    {
        ColorFormat.Rgb => FormatRgb(color),
        ColorFormat.Hsl => FormatHsl(color),
        _ => ToHex(color),
    };

    /// <summary>半透明的前景叠到不透明的背景上，得到人眼看到的颜色。</summary>
    static Rgba Over(Rgba foreground, Rgba background) => new(
        foreground.R * foreground.A + background.R * (1 - foreground.A),
        foreground.G * foreground.A + background.G * (1 - foreground.A),
        foreground.B * foreground.A + background.B * (1 - foreground.A));

    /// <summary>WCAG 的相对亮度（线性 sRGB 加权）。</summary>
    static double Luminance(Rgba color)
    {
        static double Linear(double channel)
        {
            var magnitude = Math.Abs(channel);
            return magnitude <= 0.04045 ? channel / 12.92 : Math.Sign(channel) * Math.Pow((magnitude + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }

    static Rgba Required(string value) => ParseColor(value) ?? throw new ArgumentException($"无法解析的颜色：{value}", nameof(value));

    /// <summary>
    /// 前景与背景的 WCAG 对比度（1–21）。半透明的颜色按实际看到的算：背景先叠到页面底色（<paramref name="page"/>）上，前景再叠到背景上。
    /// 颜色解析不了时抛 ArgumentException。
    /// </summary>
    public static double ContrastRatio(string foreground, string background, string page = "#ffffff")
    {
        var solidBackground = Over(Required(background), Required(page) with { A = 1 });
        var solidForeground = Over(Required(foreground), solidBackground);
        var (a, b) = (Luminance(solidForeground), Luminance(solidBackground));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>实色背景上的文字色：按候选的顺序，第一个达到正文对比度（4.5:1）的就用它；都达不到时取对比度最高的。</summary>
    public static string ReadableText(string background, IReadOnlyList<string>? candidates = null)
    {
        candidates ??= ["#ffffff", "#1c2024", "#000000"];
        var (best, bestRatio) = (candidates[0], 0d);
        foreach (var candidate in candidates)
        {
            var ratio = ContrastRatio(candidate, background);
            if (ratio >= ContrastText) return candidate;
            if (ratio > bestRatio) (best, bestRatio) = (candidate, ratio);
        }
        return best;
    }

    /// <summary>两个颜色在 sRGB 里按比例混合（<paramref name="amount"/> 是 b 的占比，0–1），输出 #rrggbb；解析不了时抛 ArgumentException。</summary>
    public static string MixColors(string a, string b, double amount = 0.5)
    {
        var (from, to) = (Required(a), Required(b));
        return ToHex(new Rgba(from.R + (to.R - from.R) * amount, from.G + (to.G - from.G) * amount, from.B + (to.B - from.B) * amount));
    }

    /// <summary>同一个颜色换成给定的不透明度（0–1），输出 rgba()；解析不了时抛 ArgumentException。</summary>
    public static string WithAlpha(string value, double alpha) => FormatRgb(Required(value) with { A = alpha });

    /// <summary>解析成 HSV（灰色没有色相，按 0）；解析不了时返回 null。</summary>
    public static Hsva? ToHsva(string value)
    {
        if (ParseColor(value) is not { } color) return null;
        var (max, min) = (Math.Max(color.R, Math.Max(color.G, color.B)), Math.Min(color.R, Math.Min(color.G, color.B)));
        return new Hsva(Hue(color, max, min), max == 0 ? 0 : 1 - min / max, max, color.A);
    }

    /// <summary>色相（同 culori：最大与最小相等时没有色相，记 0）。</summary>
    static double Hue(Rgba color, double max, double min)
    {
        if (max == min) return 0;
        var delta = max - min;
        return (max == color.R ? (color.G - color.B) / delta + (color.G < color.B ? 6 : 0)
            : max == color.G ? (color.B - color.R) / delta + 2
            : (color.R - color.G) / delta + 4) * 60;
    }

    static (double H, double S, double L) RgbToHsl(Rgba color)
    {
        var (max, min) = (Math.Max(color.R, Math.Max(color.G, color.B)), Math.Min(color.R, Math.Min(color.G, color.B)));
        return (Hue(color, max, min), max == min ? 0 : (max - min) / (1 - Math.Abs(max + min - 1)), 0.5 * (max + min));
    }

    /// <summary>色相规范到 0–360。</summary>
    static double NormalizeHue(double hue) => (hue %= 360) < 0 ? hue + 360 : hue;

    static Rgba HslToRgb(double h, double s, double l, double alpha)
    {
        h = NormalizeHue(h);
        var m1 = l + s * (l < 0.5 ? l : 1 - l);
        var m2 = m1 - (m1 - l) * 2 * Math.Abs(h / 60 % 2 - 1);
        var (r, g, b) = Math.Floor(h / 60) switch
        {
            0 => (m1, m2, 2 * l - m1),
            1 => (m2, m1, 2 * l - m1),
            2 => (2 * l - m1, m1, m2),
            3 => (2 * l - m1, m2, m1),
            4 => (m2, 2 * l - m1, m1),
            5 => (m1, 2 * l - m1, m2),
            _ => (2 * l - m1, 2 * l - m1, 2 * l - m1),
        };
        return new Rgba(r, g, b, alpha);
    }

    /// <summary>HSV → sRGB。</summary>
    public static Rgba HsvaToRgba(Hsva color)
    {
        var (h, s, v) = (NormalizeHue(color.H), color.S, color.V);
        var f = Math.Abs(h / 60 % 2 - 1);
        var (r, g, b) = Math.Floor(h / 60) switch
        {
            0 => (v, v * (1 - s * f), v * (1 - s)),
            1 => (v * (1 - s * f), v, v * (1 - s)),
            2 => (v * (1 - s), v, v * (1 - s * f)),
            3 => (v * (1 - s), v * (1 - s * f), v),
            4 => (v * (1 - s * f), v * (1 - s), v),
            5 => (v, v * (1 - s), v * (1 - s * f)),
            _ => (v * (1 - s), v * (1 - s), v * (1 - s)),
        };
        return new Rgba(r, g, b, color.A);
    }

    /// <summary>HSV 写成 #rrggbb，不透明度小于 1 时 #rrggbbaa。</summary>
    public static string HsvaToHex(Hsva color) => ToHex(HsvaToRgba(color));
}
