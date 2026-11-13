using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 读屏的实时区域（vue-tsx-pro 的 announce）：看不见的一段文字，改了就发 LiveRegionChanged，读屏念出来（polite，不打断正在念的）。
/// 放进要报告的组件里（它得在可视树里，读屏才找得到它）；排版时给它零大小。
/// </summary>
sealed class LiveRegion : TextBlock
{
    public LiveRegion()
    {
        Opacity = 0;
        IsHitTestVisible = false;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    /// <summary>念一句话。</summary>
    public void Announce(string message)
    {
        Text = message;
        (UIElementAutomationPeer.FromElement(this) ?? UIElementAutomationPeer.CreatePeerForElement(this))?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
