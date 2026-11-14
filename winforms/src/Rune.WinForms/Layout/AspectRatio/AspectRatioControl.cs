using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 固定宽高比的框（vue-tsx-pro 的 AspectRatio）：宽度跟着容器，高度按比例算，里面的子控件铺满它。图片、地图、嵌入的页面
/// 加载前就占好位置，加载完不把下面的内容顶开。
/// <code>
/// var cover = new AspectRatioControl { Ratio = 4d / 3 };
/// cover.Controls.Add(new PictureBox { Image = image, SizeMode = PictureBoxSizeMode.Zoom });
/// </code>
/// 宽高比是数（宽 ÷ 高，默认 16 / 9）。放在不限宽的地方时按子控件的宽度算。PictureBox 没有 CSS 的 cover 裁切，自己选 SizeMode。
/// </summary>
[ToolboxItem(true)]
[Description("固定宽高比的框：高度按宽度与比例算，子控件铺满。")]
public class AspectRatioControl : Panel
{
    readonly FillLayout engine = new();

    /// <summary>新建：自动大小（高度跟着宽度），透明底。</summary>
    public AspectRatioControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    /// <summary>宽高比（宽 ÷ 高），默认 16 / 9。</summary>
    [Category("Layout")]
    public double Ratio
    {
        get;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "宽高比要大于 0");
            field = value;
            PerformLayout();
        }
    } = 16d / 9;

    bool ShouldSerializeRatio() => Ratio != 16d / 9;

    void ResetRatio() => Ratio = 16d / 9;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    /// <summary>宽度取布局给的宽度（不限时取子控件里最宽的），高度 = 宽度 ÷ 比例。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = VerticalStack.Limit(this, proposedSize);
        if (width == int.MaxValue)
            width = Controls.Cast<Control>().Select(child => child.AutoSize ? child.GetPreferredSize(Size.Empty).Width : child.Width).DefaultIfEmpty().Max();
        return new Size(width, (int)Math.Round(width / Ratio));
    }

    /// <summary>子控件都铺满整个框。</summary>
    sealed class FillLayout : LayoutEngine
    {
        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            var owner = (Control)container;
            foreach (Control child in owner.Controls) child.Bounds = owner.ClientRectangle;
            return owner.AutoSize;
        }
    }
}
