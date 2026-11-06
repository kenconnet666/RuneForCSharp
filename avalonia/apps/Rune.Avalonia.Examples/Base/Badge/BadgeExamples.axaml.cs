using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Badge 的示例（vue-tsx-pro 文档站 base/Badge）。in-context 等 Table，statuses 等 StatusBadge，tag 等 Tag。</summary>
static class BadgeExamples
{
    public static Example[] All =>
    [
        new("Badge", "matrix", "样式与色调", "Variant 是 solid、soft（默认）、surface、outline，Tone 是六种状态色调；颜色都取主题 token，每套主题都过对比度检查。", () => new BadgeMatrix()),
        new("Badge", "shapes", "尺寸、形状与图标", "Size 默认 xs；Radius=\"full\" 是胶囊，计数常用实底；Icon 放在文字前，Dot 是同色的小圆点，表格里比整块底色更轻。", () => new BadgeShapes()),
    ];
}

/// <summary>Badge/matrix：样式与色调。</summary>
public partial class BadgeMatrix : UserControl
{
    /// <summary>加载示例。</summary>
    public BadgeMatrix() => InitializeComponent();
}

/// <summary>Badge/shapes：尺寸、形状与图标。</summary>
public partial class BadgeShapes : UserControl
{
    /// <summary>加载示例。</summary>
    public BadgeShapes() => InitializeComponent();
}
