namespace Rune.Avalonia;

/// <summary>
/// 只有图标的按钮（vue-tsx-pro 的 IconButton）：宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色），其余外观同 <see cref="Button"/>。
/// <code>
/// &lt;rune:IconButton Icon="{x:Static rune:Lucide.Settings}" AutomationProperties.Name="设置" /&gt;
/// </code>
/// 没有文字，必须写 <c>AutomationProperties.Name</c>（TS 的 aria-label），读屏才知道它是做什么的。
/// 部件（模板里的名字，见 <see cref="IconButtonParts"/>）：内容层、图标、加载指示，用 <c>/template/</c> 样式改。
/// </summary>
public class IconButton : ButtonCore;

/// <summary>图标按钮的部件名：样式里用 <c>rune|IconButton /template/ rune|Icon#PART_Icon</c> 选中。</summary>
public static class IconButtonParts
{
    /// <summary>内容层（包着图标，TS 的 propsContent）。</summary>
    public const string Content = "PART_Content";

    /// <summary>图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>加载指示（TS 的 propsSpinner）。</summary>
    public const string Spinner = "PART_Spinner";
}
