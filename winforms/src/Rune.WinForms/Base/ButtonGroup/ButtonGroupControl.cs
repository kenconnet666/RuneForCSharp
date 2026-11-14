using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 按钮组（vue-tsx-pro 的 ButtonGroup）：几个相关的按钮连成一条，共用外观。拆分按钮、翻页、缩放、对齐方式这类工具栏。
/// <code>
/// var align = new ButtonGroupControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, AccessibleName = "文字对齐" };
/// align.Controls.AddRange([new IconButtonControl { Icon = Lucide.TextAlignStart, AccessibleName = "左对齐" }, new IconButtonControl { Icon = Lucide.TextAlignCenter, AccessibleName = "居中" }]);
/// </code>
/// 组里的按钮去掉相邻一侧的圆角、边框叠在一起，交叉轴拉齐（一样高，竖排时一样宽）；悬停、聚焦的那个浮到上面，边框不被邻居盖住。
/// 组上写的 Variant、Tone、TextSize、Radius 是组里按钮的默认值，按钮自己写的优先；没写 Tone 时组里的按钮都是 accent（图标按钮也是，拆分按钮的两半同色）。
/// 按钮四周给焦点环留的边距在相邻一侧去掉（邻居紧贴着），那一侧的焦点环画在按钮里面。只是排一排、不相连的按钮用 FlexControl；选中其中一项用 ToggleGroupControl。
/// </summary>
[ToolboxItem(true)]
[Description("按钮组：相连的一排（或一列）按钮，共用外观。")]
public class ButtonGroupControl : AttachedGroupControl
{
    /// <summary>组里按钮的样式；不写时按钮用自己的默认。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public ButtonVariant? Variant { get; set { field = value; Restyle(); } }

    /// <summary>组里按钮的色调；不写时是 accent。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public StatusTone? Tone { get; set { field = value; Restyle(); } }

    /// <summary>组里按钮的字号档位。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public TextSize? TextSize { get; set { field = value; Restyle(); } }

    /// <summary>组里按钮的圆角（两端的按钮外侧用它）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public Radius? Radius { get; set { field = value; Restyle(); } }

    /// <summary>外侧照旧给焦点环留位置（画在按钮外面），相邻一侧不留（邻居紧贴着，那一侧的环画在按钮里面）。</summary>
    protected override Padding RingRoom(Control button, bool first, bool last)
    {
        var ring = button.LogicalToDeviceUnits(RingSpace);
        return Orientation.IsVertical
            ? new Padding(ring, first ? ring : 0, ring, last ? ring : 0)
            : new Padding(first ? ring : 0, ring, last ? ring : 0, ring);
    }
}

/// <summary>能放进相连的一组的按钮（按钮家族的宿主）：组经它写去掉圆角的角、让按钮按组的外观重排。</summary>
interface IGroupButton
{
    /// <summary>按钮元素。</summary>
    ButtonCore Core { get; }
}
