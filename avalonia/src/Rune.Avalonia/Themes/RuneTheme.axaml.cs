using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Rune.Avalonia;

/// <summary>
/// 组件库的样式入口：加到 <c>Application.Styles</c>，放在 FluentTheme 之后。
/// <code>
/// &lt;Application.Styles&gt;
///   &lt;FluentTheme /&gt;
///   &lt;rune:RuneTheme /&gt;
/// &lt;/Application.Styles&gt;
/// </code>
/// </summary>
public class RuneTheme : Styles
{
    /// <summary>加载主题里的 tokens 与组件样式；装上提示的默认值与键盘行为（<see cref="Tooltip"/>）。</summary>
    public RuneTheme()
    {
        Tooltip.Install();
        AvaloniaXamlLoader.Load(this);
    }
}
