using Avalonia.Automation;
using Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 读屏的实时区域（vue-tsx-pro 的 announce）：看不见的一段文字，改了读屏就念（polite，不打断正在念的）。
/// 放进要报告的组件里（它得在可视树与读屏的树里，读屏才收得到）；排版时给它零大小。
/// </summary>
sealed class LiveRegion : TextBlock
{
    public LiveRegion()
    {
        Opacity = 0;
        IsHitTestVisible = false;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    /// <summary>念一句话：同一句话连着报两次也念（先清空，文字变了才算更新）。</summary>
    public void Announce(string message)
    {
        if (Text == message) Text = "";
        Text = message;
    }
}
