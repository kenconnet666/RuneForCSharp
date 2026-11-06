using System.Windows;

namespace Rune.Wpf;

/// <summary>
/// 组件库的资源入口：合并到 <c>Application.Resources</c>，带上不随主题变的 token 与当前主题的 token（tools/token-gen 生成）。
/// <code>
/// &lt;Application.Resources&gt;
///   &lt;rune:RuneTheme Theme="dark" /&gt;
/// &lt;/Application.Resources&gt;
/// </code>
/// 换 <see cref="Theme"/> 就换掉合并进来的主题字典，用 DynamicResource 的地方随之更新（3000 个控件约 360 ms，DESIGN §9）。
/// 组件自己的主题样式在 Themes/Generic.xaml，按 DefaultStyleKey 生效，不经过这里。
/// </summary>
public partial class RuneTheme : ResourceDictionary
{
    // 相对的组件 URI 不依赖 pack 协议：Application 建好之前读 BuiltinThemes 也不会抛异常
    static readonly Uri TokensSource = new("/Rune.Wpf;component/Config/Generated/Tokens.g.xaml", UriKind.Relative);

    ResourceDictionary? current;
    string theme = "";

    /// <summary>带上 token，主题默认亮色。</summary>
    public RuneTheme()
    {
        MergedDictionaries.Add(new ResourceDictionary { Source = TokensSource });
        Theme = "light";
    }

    /// <summary>内置主题的名字（同 vue-tsx-pro：light、dark、paper……，见 <see cref="BuiltinThemes"/>）。</summary>
    public string Theme
    {
        get => theme;
        set
        {
            if (value == theme) return;
            var entry = BuiltinThemes.FirstOrDefault(t => t.Name == value)
                ?? throw new ArgumentException($"没有内置主题 {value}；有：{string.Join("、", BuiltinThemes.Select(t => t.Name))}", nameof(value));
            var next = new ResourceDictionary { Source = entry.Source };
            if (current is null) MergedDictionaries.Add(next);
            else MergedDictionaries[MergedDictionaries.IndexOf(current)] = next;
            current = next;
            theme = value;
        }
    }
}

/// <summary>一套内置主题。</summary>
/// <param name="Name">名字，同 vue-tsx-pro 的 builtinThemes（light、high-contrast……）。</param>
/// <param name="Label">中文名。</param>
/// <param name="IsDark">是不是暗色主题。</param>
/// <param name="Source">主题字典（颜色、阴影、字体、圆角）的地址。</param>
public sealed record BuiltinTheme(string Name, string Label, bool IsDark, Uri Source);
