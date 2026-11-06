namespace Rune.WinForms;

/// <summary>
/// 阴影的一层（CSS box-shadow 的一项，长度是设备无关像素）。vue-tsx-pro 的阴影 token 由两三层叠成，主题里原样保留各层，
/// 绘制时由下往上逐层画。
/// </summary>
/// <param name="X">水平偏移。</param>
/// <param name="Y">垂直偏移。</param>
/// <param name="Blur">模糊半径。</param>
/// <param name="Spread">扩展（负数是收缩）。</param>
/// <param name="Color">颜色（带透明度）。</param>
/// <param name="Inset">内阴影。</param>
public readonly record struct ShadowLayer(float X, float Y, float Blur, float Spread, Color Color, bool Inset = false);
