using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 不排版的布局引擎：子控件全由宿主在 OnLayout 里自己摆。默认引擎会先把自动大小（AutoSize）的子控件缩成它想要的大小，
/// 宿主随后又撑回去，子树每次都按两种宽度各排一遍，套几层就成倍放大（文档页的示例卡片因此要几十秒才排出来）。
/// </summary>
sealed class ManualLayout : LayoutEngine
{
    public static readonly ManualLayout Instance = new();

    /// <summary>自动大小的宿主要父级重排（同默认引擎）：内容变大时外层才会重新量它。</summary>
    public override bool Layout(object container, LayoutEventArgs layoutEventArgs) => container is Control { AutoSize: true };
}
