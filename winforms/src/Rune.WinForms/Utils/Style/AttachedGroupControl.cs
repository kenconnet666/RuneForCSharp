using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 相连的一组（ButtonGroupControl、ControlGroupControl 共用）：沿主轴排，相邻的叠一条边框，交叉轴拉齐；给每个控件写上要去掉圆角的角
/// （跳过隐藏的），悬停的浮到上面、聚焦的再高一层。子控件是窗口，浮到上面要调 z 序（会改 Controls 的顺序），排列的顺序另外记。
/// 按钮四周给焦点环留的边距由派生类定（<see cref="RingRoom"/>）：没留位置的一侧焦点环画在按钮里面。读屏里是一个组。
/// </summary>
[ToolboxItem(false)]
public abstract class AttachedGroupControl : Control
{
    /// <summary>按钮给焦点环留的边距（ButtonControlBase 的默认 Padding，逻辑像素）。</summary>
    protected const int RingSpace = 4;

    readonly GroupLayout engine;
    readonly PreferredSizeCache preferred;
    readonly List<Control> order = [];

    /// <summary>新建：横排、透明底，读屏里是一个组。</summary>
    protected AttachedGroupControl()
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

    bool ShouldSerializeOrientation() => Orientation != AttachedOrientation.Horizontal;

    void ResetOrientation() => Orientation = AttachedOrientation.Horizontal;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => preferred.Get(proposedSize, engine.Measure);

    /// <summary>按排列的顺序（不是 z 序）的子控件（测试用）。</summary>
    internal IReadOnlyList<Control> Items => order;

    /// <summary>主轴上分剩余空间的控件（CSS 的 flex-grow: 1）；只在横排、给了宽度时生效。</summary>
    protected virtual bool Grows(Control child) => false;

    /// <summary>不按内容宽、占满给的宽度（ControlGroup 默认占满一行）。</summary>
    protected virtual bool Fills => false;

    /// <summary>按钮四周给焦点环留的边距：first、last 是排在两端（从右到左时已对调成从左到右）。</summary>
    protected abstract Padding RingRoom(Control button, bool first, bool last);

    /// <summary>外观变了：按钮重新量、重画，组重排。</summary>
    protected void Restyle()
    {
        foreach (var button in order.OfType<IGroupButton>()) button.Core.Restyle();
        PerformLayout();
        Invalidate();
    }

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
        Attached.Set(child, AttachedCorners.None);
        if (child is not IGroupButton button) return;
        child.Padding = new Padding(child.LogicalToDeviceUnits(RingSpace));
        button.Core.Restyle();
    }

    /// <summary>悬停的浮到上面，聚焦的再高一层（CSS 的 z-index 1、2）：聚焦的最后放到最前面。</summary>
    void OnChildState(object? sender, EventArgs e)
    {
        if (sender is Control child && child.ClientRectangle.Contains(child.PointToClient(MousePosition))) child.BringToFront();
        order.FirstOrDefault(control => control.ContainsFocus)?.BringToFront();
    }

    /// <summary>沿主轴排，相邻的叠一条边框；交叉轴拉齐。</summary>
    sealed class GroupLayout(AttachedGroupControl owner) : LayoutEngine
    {
        List<Control> Shown() => owner.order.Where(child => child.Visible || !owner.Visible).ToList();

        int Overlap => (int)Math.Round(AttachedRules.Overlap * owner.DeviceDpi / 96d);

        /// <summary>
        /// 给每个控件写上去掉圆角的角，给按钮定焦点环的边距。WinForms 从右到左时不镜像绘制：横排的第一个在右边，按从左到右算时对调首尾。
        /// </summary>
        void Attach(List<Control> shown)
        {
            var rtl = owner.RightToLeft == RightToLeft.Yes && !owner.Orientation.IsVertical;
            for (var i = 0; i < shown.Count; i++)
            {
                var (first, last) = (i == 0, i == shown.Count - 1);
                if (rtl) (first, last) = (last, first);
                Attached.Set(shown[i], AttachedRules.Squared(owner.Orientation, first, last));
                if (shown[i] is not IGroupButton) continue;
                var padding = owner.RingRoom(shown[i], first, last);
                if (shown[i].Padding != padding) shown[i].Padding = padding;
            }
        }

        static Size Natural(Control child) => child.AutoSize ? child.GetPreferredSize(Size.Empty) : child.Size;

        /// <summary>各控件的主轴长度：按内容；横排且给了宽度时，多出来的平分给要分剩余空间的控件（基准是内容宽）。</summary>
        List<(Control Child, int Length)> Lengths(List<Control> shown, int width)
        {
            var vertical = owner.Orientation.IsVertical;
            var lengths = shown.Select(child => (child, vertical ? Natural(child).Height : Natural(child).Width)).ToList();
            var growing = vertical ? 0 : shown.Count(owner.Grows);
            if (growing == 0 || width <= 0) return lengths;
            var extra = width - (lengths.Sum(item => item.Item2) - Math.Max(0, shown.Count - 1) * Overlap);
            if (extra <= 0) return lengths;
            var share = extra / growing;
            return lengths.Select(item => owner.Grows(item.child) ? (item.child, item.Item2 + share) : item).ToList();
        }

        public Size Measure(Size proposed)
        {
            var shown = Shown();
            Attach(shown);
            var vertical = owner.Orientation.IsVertical;
            var bounded = proposed.Width is > 1 and < int.MaxValue;
            var lengths = Lengths(shown, owner.Fills && bounded ? proposed.Width : 0);
            var main = lengths.Sum(item => item.Length) - Math.Max(0, shown.Count - 1) * Overlap;
            var cross = shown.Select(child => vertical ? Natural(child).Width : Natural(child).Height).DefaultIfEmpty().Max();
            if (owner.Fills && bounded && !vertical) main = Math.Max(main, proposed.Width);
            if (owner.Fills && bounded && vertical) cross = Math.Max(cross, proposed.Width);
            return vertical ? new Size(cross, main) : new Size(main, cross);
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            var shown = Shown();
            Attach(shown);
            var vertical = owner.Orientation.IsVertical;
            var client = owner.ClientSize;
            var cross = vertical ? client.Width : client.Height;
            var rtl = owner.RightToLeft == RightToLeft.Yes && !vertical;
            var offset = 0;
            foreach (var (child, length) in Lengths(shown, owner.Fills ? client.Width : 0))
            {
                if (vertical) child.SetBounds(0, offset, cross, length);
                else child.SetBounds(rtl ? client.Width - offset - length : offset, 0, length, cross);
                offset += length - Overlap;
            }
            return owner.AutoSize;
        }
    }
}
