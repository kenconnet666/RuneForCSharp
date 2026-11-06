namespace Rune.WinForms;

/// <summary>
/// 波纹（vue-tsx-pro 的 utils/dom/ripple）的全局开关：按下时从指针位置扩散出一圈半透明的文字颜色，松开后淡出。
/// 元素自己画波纹（扩散进度与不透明度是 <see cref="Animator"/> 驱动的信号，变了只重画那个元素）；组件的 Ripple 属性单独覆盖这个开关。
/// </summary>
public static class Ripple
{
    /// <summary>全局开关（vue-tsx-pro 的 createUi / Provider 的 ripple，默认开启）。</summary>
    public static bool Enabled { get; set; } = true;
}
