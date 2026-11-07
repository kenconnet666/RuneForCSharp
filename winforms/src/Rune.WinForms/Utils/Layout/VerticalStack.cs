using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 竖着排子控件（按 Controls 的顺序，撑满宽度），间距由宿主给；从右到左时只是镜像（竖排不受影响）。
/// 自动大小的子控件按给它的宽度取想要的高度（文字按宽度换行）。
/// </summary>
sealed class VerticalStack(Func<int> gap) : LayoutEngine
{
    /// <summary>
    /// 想要的大小（同 Label）：最宽的子控件的宽度，不超过布局给的宽度与 MaximumSize；高度按这个宽度排（文字换行后的）。
    /// </summary>
    public Size Measure(Control container, Size proposed)
    {
        var padding = container.Padding.Size;
        var children = Shown(container);
        var width = Math.Min(Measure(children, 0, 0).Width, Limit(container, proposed) - padding.Width);
        return new Size(width, Measure(children, width, gap()).Height) + padding;
    }

    /// <summary>布局给的宽度与 MaximumSize 里小的那个（布局引擎用 0、1 或 int.MaxValue 表示不限）。</summary>
    public static int Limit(Control container, Size proposed)
    {
        var limit = proposed.Width is > 1 and < int.MaxValue ? proposed.Width : int.MaxValue;
        return container.MaximumSize.Width > 0 ? Math.Min(limit, container.MaximumSize.Width) : limit;
    }

    /// <summary>一组子控件竖排的大小（宽度 0 时不限宽）。</summary>
    public static Size Measure(IReadOnlyList<Control> children, int width, int gap)
    {
        var (widest, height) = (0, 0);
        foreach (var child in children)
        {
            var size = SizeOf(child, width);
            widest = Math.Max(widest, size.Width);
            height += size.Height;
        }
        return new Size(widest, height + Math.Max(0, children.Count - 1) * gap);
    }

    /// <summary>从 area 的顶上依次放下，返回用掉的高度。</summary>
    public static int Arrange(IReadOnlyList<Control> children, Rectangle area, int gap)
    {
        var y = area.Y;
        foreach (var child in children)
        {
            var height = SizeOf(child, area.Width).Height;
            child.SetBounds(area.X, y, area.Width, height);
            y += height + gap;
        }
        return children.Count == 0 ? 0 : y - gap - area.Y;
    }

    /// <summary>参与排版的子控件：容器还没显示时都算（那时子控件的 Visible 都是 false）。</summary>
    public static List<Control> Shown(Control container) =>
        [.. container.Controls.Cast<Control>().Where(child => child.Visible || !container.Visible)];

    static Size SizeOf(Control child, int width) => child.AutoSize ? child.GetPreferredSize(new Size(width, 0)) : child.Size;

    /// <inheritdoc />
    public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
    {
        var parent = (Control)container;
        Arrange(Shown(parent), parent.DisplayRectangle, gap());
        // 自动大小时让父容器重排（大小可能跟着子控件变了）
        return parent.AutoSize;
    }
}
