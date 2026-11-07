using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia.Tests;

/// <summary>测试里常用的查找：按名字找部件、主题颜色、画刷的颜色、读屏节点。</summary>
static class Lookup
{
    /// <summary>逻辑树里按名字找部件（PART_Xxx）。</summary>
    public static T Part<T>(this Control owner, string name) where T : Control => owner.GetLogicalDescendants().OfType<T>().First(part => part.Name == name);

    /// <summary>主题里的画刷颜色（按控件的主题变体查）；查不到时为 default。</summary>
    public static Color ThemeColor(this Control owner, string key) =>
        owner.TryFindResource(key, owner.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : default;

    /// <summary>主题资源（按控件的主题变体查）；查不到或类型不对时用 <paramref name="fallback"/>。</summary>
    public static T Resource<T>(this Control owner, string key, T fallback) =>
        owner.TryFindResource(key, owner.ActualThemeVariant, out var value) && value is T typed ? typed : fallback;

    /// <summary>纯色画刷的颜色；不是纯色时为 default。</summary>
    public static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : default;

    /// <summary>控件的读屏节点。</summary>
    public static AutomationPeer Peer(this Control control) => ControlAutomationPeer.CreatePeerForElement(control);
}
