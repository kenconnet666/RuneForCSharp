using global::Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 色调调色板（DESIGN §4 决定 11）：一种状态色调在当前主题里的一组画刷。每套主题六份（<c>Rune.Palette.Accent</c>……），
/// 由 tools/token-gen 生成在主题字典里。组件的色调样式只设调色板，样式档位绑定调色板的字段，组合档位不再逐条写：
/// <code>
/// &lt;Style Selector="^:danger"&gt;&lt;Setter Property="Palette" Value="{DynamicResource Rune.Palette.Danger}" /&gt;&lt;/Style&gt;
/// &lt;Style Selector="^:solid"&gt;&lt;Setter Property="Background" Value="{Binding $self.Palette.Solid}" /&gt;&lt;/Style&gt;
/// </code>
/// Provider 能在一片区域里换掉某个色调的调色板；使用方要改单个组件的颜色，照常在带伪类的选择器里设 Background。
/// </summary>
public sealed class TonePalette
{
    /// <summary>实底的底色。</summary>
    public IBrush? Solid { get; set; }

    /// <summary>实底悬停的底色。</summary>
    public IBrush? SolidHover { get; set; }

    /// <summary>实底上的文字与图标。</summary>
    public IBrush? Contrast { get; set; }

    /// <summary>浅色底。</summary>
    public IBrush? Soft { get; set; }

    /// <summary>浅色底悬停。</summary>
    public IBrush? SoftHover { get; set; }

    /// <summary>浅色底加描边（surface 样式）的底色。</summary>
    public IBrush? Surface { get; set; }

    /// <summary>透明底（outline、ghost）悬停的底色。</summary>
    public IBrush? SubtleHover { get; set; }

    /// <summary>浅色底、描边、透明底上的文字与图标。</summary>
    public IBrush? Text { get; set; }

    /// <summary>描边。</summary>
    public IBrush? Border { get; set; }
}
