using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace Rune.Wpf.Tests;

/// <summary>测试里常用的查找：按名字或部件样式键找部件、主题颜色、画刷的颜色、读屏节点。</summary>
static class Lookup
{
    /// <summary>可视树里按名字找部件（PART_Xxx）。</summary>
    public static T Part<T>(this FrameworkElement owner, string name) where T : FrameworkElement => WpfUi.Descendants(owner).OfType<T>().First(part => part.Name == name);

    /// <summary>可视树里用这个部件样式键的元素（样式是同一个引用），只能有一个。</summary>
    public static T Part<T>(this FrameworkElement owner, PartKey key) where T : FrameworkElement
    {
        var style = (Style)owner.FindResource(key);
        return Assert.Single(WpfUi.Descendants(owner).OfType<T>(), part => ReferenceEquals(part.Style, style));
    }

    /// <summary>主题里的画刷颜色。</summary>
    public static Color ThemeColor(this FrameworkElement owner, string key) => ColorOf(owner.FindResource(key));

    /// <summary>纯色画刷的颜色；不是纯色时为 default。</summary>
    public static Color ColorOf(object? brush) => brush is SolidColorBrush solid ? solid.Color : default;

    /// <summary>元素的读屏节点。</summary>
    public static AutomationPeer Peer(this UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element);
}
