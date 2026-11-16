using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia;

/// <summary>
/// 把主题定制的设置套到界面上（vue-tsx-pro 的 customizeTheme、themeSettingsOverrides、themeSettingsCode）：设置换算成一组资源覆盖，
/// 放进某个元素（窗口、Provider、应用）的 Resources，里面的控件就按新的 token 显示；每个旋钮只改相关的一组 token，层级关系不变：
/// 换品牌色、中性色按 Radix 的级别整组替换（<see cref="ThemePalette"/>），圆角按这套主题自己的圆角等比缩放，字号按默认值缩放，动效按倍数缩放时长。
/// <code>
/// ThemeCustomization.Apply(window, new ThemeSettings(Accent: "crimson", Radius: ThemeRadius.Large));
/// </code>
/// 资源覆盖不分亮暗：换了亮暗主题要再 Apply 一次（品牌色、中性色取对应的色阶）。
/// </summary>
public static class ThemeCustomization
{
    // 放进 Resources 的覆盖字典认得出来：再 Apply 时换掉它，不动使用方自己的资源
    static readonly object Marker = new();

    /// <summary>设置换算成资源覆盖（键是 ThemeKeys 的键）；没有要覆盖的时为空字典。</summary>
    /// <param name="settings">设置。</param>
    /// <param name="variant">套在哪套主题上：决定取亮色还是暗色色阶，圆角按它自己的取值缩放。</param>
    /// <param name="reducedMotion">用户开启了"减少动态效果"：不覆盖动效时长。</param>
    public static ResourceDictionary Resources(ThemeSettings settings, ThemeVariant variant, bool reducedMotion = false)
    {
        var resources = new ResourceDictionary();
        foreach (var (key, value) in Overrides(settings, variant, reducedMotion)) resources[key] = value;
        return resources;
    }

    /// <summary>设置换算成 资源键 → 值 的覆盖（颜色给 Color 与 Brush 两个键）。</summary>
    internal static List<KeyValuePair<string, object>> Overrides(ThemeSettings settings, ThemeVariant variant, bool reducedMotion = false)
    {
        var overrides = new List<KeyValuePair<string, object>>();
        void Add(string key, object value) => overrides.Add(new(key, value));
        foreach (var (token, css) in ThemePalette.Colors(settings.Accent, settings.Neutral, IsDark(variant)))
        {
            var color = ToColor(css);
            Add($"Rune.Color.{Pascal(token)}", color);
            Add($"Rune.Brush.{Pascal(token)}", new SolidColorBrush(color));
        }
        if (settings.Radius is { } radius && radius != ThemeRadius.Medium)
        {
            var factor = ThemeSettingsMath.RadiusFactor(radius);
            foreach (var key in Keys(typeof(ThemeKeys.Radius)).Where(key => key is not (ThemeKeys.Radius.None or ThemeKeys.Radius.Full)))
                if (FindDouble(key, variant) is { } value) Add(key, ThemeSettingsMath.Scale(value, factor));
        }
        if (settings.Scaling is { } scaling && scaling != 1)
            foreach (var key in Keys(typeof(ThemeKeys.FontSize)))
                if (ThemeKeys.Fallback(key) is double value) Add(key, ThemeSettingsMath.Scale(value, scaling));
        if (settings.Font == ThemeFont.SerifHeadings && Find<FontFamily>(ThemeKeys.Font.Serif, variant) is { } serif) Add(ThemeKeys.Font.Heading, serif);
        if (!reducedMotion && settings.Motion is { } motion && motion != ThemeMotion.Normal)
        {
            var factor = ThemeSettingsMath.MotionFactor(motion);
            foreach (var key in Keys(typeof(ThemeKeys.Duration)).Where(key => key != ThemeKeys.Duration.None))
                if (ThemeKeys.Fallback(key) is TimeSpan value) Add(key, TimeSpan.FromMilliseconds(ThemeSettingsMath.Scale(value.TotalMilliseconds, factor)));
        }
        return overrides;
    }

    /// <summary>把设置套到 target 上（换掉上一次 Apply 放进去的覆盖）；设置全是默认档位时拿掉覆盖。</summary>
    /// <param name="target">窗口、Provider 或别的元素（覆盖对它和它里面的控件生效）。</param>
    /// <param name="settings">设置。</param>
    /// <param name="variant">套在哪套主题上；null 时取 target 当前实际的主题变体。</param>
    public static void Apply(StyledElement target, ThemeSettings settings, ThemeVariant? variant = null)
    {
        var merged = target.Resources.MergedDictionaries;
        foreach (var old in merged.OfType<ResourceDictionary>().Where(item => item.TryGetValue(Marker, out _)).ToList()) merged.Remove(old);
        if (!ThemeSettingsMath.HasOverrides(settings)) return;
        var resources = Resources(settings, variant ?? target.ActualThemeVariant);
        resources[Marker] = true;
        merged.Add(resources);
    }

    /// <summary>
    /// 生成套用这些设置的 XAML（上游生成 defineTheme 的 TS 代码）：一个 ResourceDictionary，可以合并进 App.axaml 或某个窗口的 Resources。
    /// </summary>
    /// <param name="settings">设置。</param>
    /// <param name="variant">套在哪套主题上。</param>
    public static string Code(ThemeSettings settings, ThemeVariant variant)
    {
        var lines = new StringBuilder();
        lines.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"");
        lines.AppendLine("                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
        lines.AppendLine("                    xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\">");
        foreach (var (key, value) in Overrides(settings, variant))
            lines.AppendLine(value switch
            {
                Color color => $"  <Color x:Key=\"{key}\">{color}</Color>",
                SolidColorBrush brush => $"  <SolidColorBrush x:Key=\"{key}\" Color=\"{brush.Color}\" />",
                double number => $"  <x:Double x:Key=\"{key}\">{number.ToString(CultureInfo.InvariantCulture)}</x:Double>",
                TimeSpan time => $"  <sys:TimeSpan x:Key=\"{key}\">{time.ToString("c", CultureInfo.InvariantCulture)}</sys:TimeSpan>",
                FontFamily font => $"  <FontFamily x:Key=\"{key}\">{System.Security.SecurityElement.Escape(string.Join(", ", font.FamilyNames))}</FontFamily>",
                _ => $"  <!-- {key}: {value} -->",
            });
        lines.AppendLine("</ResourceDictionary>");
        return lines.ToString();
    }

    /// <summary>
    /// 某个元素上实际生效的颜色做对比度检查（上游读 contrastTarget 上算出来的 CSS 变量）：按 ThemeContrast 的组合，返回不够的那些。
    /// </summary>
    /// <param name="target">从哪个元素上读资源（它的 Resources、外层、主题）。</param>
    public static IReadOnlyList<ContrastIssue> CheckContrast(StyledElement target) =>
        ThemeContrast.Check(token => target.TryFindResource($"Rune.Color.{Pascal(token)}", target.ActualThemeVariant, out var value) && value is Color color
            ? color.A == 255 ? $"#{color.R:x2}{color.G:x2}{color.B:x2}" : string.Create(CultureInfo.InvariantCulture, $"rgba({color.R}, {color.G}, {color.B}, {color.A / 255d})")
            : "#000000");

    /// <summary>主题变体是不是暗色（自定义的变体看它所属的亮或暗）。</summary>
    internal static bool IsDark(ThemeVariant variant)
    {
        for (ThemeVariant? current = variant; current is not null; current = current.InheritVariant)
            if (current == ThemeVariant.Dark) return true;
        return false;
    }

    /// <summary>一组 token 的全部键（ThemeKeys 里那个类的常量）。</summary>
    static IEnumerable<string> Keys(Type group) =>
        group.GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!);

    /// <summary>这套主题里一个资源的取值（应用的资源，按变体）。</summary>
    static T? Find<T>(string key, ThemeVariant variant) where T : class =>
        Application.Current?.TryGetResource(key, variant, out var value) == true ? value as T : null;

    static double? FindDouble(string key, ThemeVariant variant) =>
        Application.Current?.TryGetResource(key, variant, out var value) == true && value is double number ? number : null;

    static Color ToColor(string css) => ColorMath.ParseColor(css) is { } rgba
        ? Color.FromArgb(Channel(rgba.A), Channel(rgba.R), Channel(rgba.G), Channel(rgba.B))
        : throw new FormatException($"不是颜色：{css}");

    // ColorMath 的通道是 0–1
    static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);

    static string Pascal(string token) => char.ToUpperInvariant(token[0]) + token[1..];
}
