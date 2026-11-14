using System.ComponentModel;
using System.Windows.Forms.Layout;

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
public class ButtonGroupControl : Control
{
    readonly GroupLayout engine;
    readonly PreferredSizeCache preferred;
    // 排列的顺序：浮到上面要调 z 序，会改 Controls 的顺序
    readonly List<Control> order = [];

    /// <summary>新建：横排，读屏里是一个组。</summary>
    public ButtonGroupControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        engine = new GroupLayout(this);
        preferred = new PreferredSizeCache(this);
    }

    /// <summary>排列方向，默认 horizontal。</summary>
    [Category("Layout")]
    public AttachedOrientation Orientation { get; set { field = value; Restyle(); } } = AttachedOrientation.Horizontal;

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

    bool ShouldSerializeOrientation() => Orientation != AttachedOrientation.Horizontal;

    void ResetOrientation() => Orientation = AttachedOrientation.Horizontal;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => preferred.Get(proposedSize, _ => engine.Measure());

    /// <summary>按排列的顺序（不是 z 序）的子控件（测试用）。</summary>
    internal IReadOnlyList<Control> Items => order;

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is not { } child) return;
        order.Add(child);
        child.MouseEnter += OnChildState;
        child.MouseLeave += OnChildState;
        child.Enter += OnChildState;
        child.Leave += OnChildState;
        if (child is IGroupButton button) button.Core.Restyle();
    }

    /// <inheritdoc />
    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        if (e.Control is not { } child) return;
        order.Remove(child);
        child.MouseEnter -= OnChildState;
        child.MouseLeave -= OnChildState;
        child.Enter -= OnChildState;
        child.Leave -= OnChildState;
        if (child is not IGroupButton button) return;
        button.Core.Attached = AttachedCorners.None;
        child.Padding = new Padding(child.LogicalToDeviceUnits(RingSpace));
        button.Core.Restyle();
    }

    /// <summary>按钮给焦点环留的边距（ButtonControlBase 的默认 Padding，逻辑像素）。</summary>
    const int RingSpace = 4;

    void Restyle()
    {
        foreach (var button in order.OfType<IGroupButton>()) button.Core.Restyle();
        PerformLayout();
        Invalidate();
    }

    /// <summary>悬停的浮到上面，聚焦的再高一层（CSS 的 z-index 1、2）：聚焦的最后放到最前面。</summary>
    void OnChildState(object? sender, EventArgs e)
    {
        if (sender is Control child && child.ClientRectangle.Contains(child.PointToClient(MousePosition))) child.BringToFront();
        order.FirstOrDefault(child => child.ContainsFocus)?.BringToFront();
    }

    /// <summary>沿主轴排，相邻的叠一条边框（按钮相邻一侧不留焦点环的边距）；交叉轴拉齐。</summary>
    sealed class GroupLayout(ButtonGroupControl owner) : LayoutEngine
    {
        List<Control> Shown() => owner.order.Where(child => child.Visible || !owner.Visible).ToList();

        int Overlap => (int)Math.Round(AttachedRules.Overlap * owner.DeviceDpi / 96d);

        /// <summary>给每个按钮写上去掉圆角的角与边距：相邻一侧不留焦点环的位置，邻居紧贴着。</summary>
        /// <summary>WinForms 从右到左时不镜像绘制：横排的第一个在右边，左右两侧的角与边距对调。</summary>
        void Attach(List<Control> shown)
        {
            var vertical = owner.Orientation.IsVertical;
            var rtl = owner.RightToLeft == RightToLeft.Yes && !vertical;
            for (var i = 0; i < shown.Count; i++)
            {
                if (shown[i] is not IGroupButton button) continue;
                var (first, last) = (i == 0, i == shown.Count - 1);
                // 下面按从左到右算：从右到左时靠右的（第一个）是"最后一个"
                if (rtl) (first, last) = (last, first);
                button.Core.Attached = AttachedRules.Squared(owner.Orientation, first, last);
                var ring = shown[i].LogicalToDeviceUnits(RingSpace);
                var padding = vertical
                    ? new Padding(ring, first ? ring : 0, ring, last ? ring : 0)
                    : new Padding(first ? ring : 0, ring, last ? ring : 0, ring);
                if (shown[i].Padding != padding) shown[i].Padding = padding;
            }
        }

        public Size Measure()
        {
            var shown = Shown();
            Attach(shown);
            var vertical = owner.Orientation.IsVertical;
            int main = 0, cross = 0;
            foreach (var child in shown)
            {
                var size = child.AutoSize ? child.GetPreferredSize(Size.Empty) : child.Size;
                main += vertical ? size.Height : size.Width;
                cross = Math.Max(cross, vertical ? size.Width : size.Height);
            }
            main -= Math.Max(0, shown.Count - 1) * Overlap;
            return vertical ? new Size(cross, main) : new Size(main, cross);
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            var shown = Shown();
            Attach(shown);
            var vertical = owner.Orientation.IsVertical;
            var cross = vertical ? owner.ClientSize.Width : owner.ClientSize.Height;
            var rtl = owner.RightToLeft == RightToLeft.Yes && !vertical;
            var offset = 0;
            foreach (var child in shown)
            {
                var size = child.AutoSize ? child.GetPreferredSize(Size.Empty) : child.Size;
                if (vertical) child.SetBounds(0, offset, cross, size.Height);
                else child.SetBounds(rtl ? owner.ClientSize.Width - offset - size.Width : offset, 0, size.Width, cross);
                offset += (vertical ? size.Height : size.Width) - Overlap;
            }
            return owner.AutoSize;
        }
    }
}

/// <summary>能放进 ButtonGroupControl 相连的按钮（按钮家族的宿主）：组经它写去掉圆角的角、让按钮按组的外观重排。</summary>
interface IGroupButton
{
    /// <summary>按钮元素。</summary>
    ButtonCore Core { get; }
}
