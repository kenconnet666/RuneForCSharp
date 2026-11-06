using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 单元素宿主：把一个元素包成一个控件，能放进设计器、在属性窗口里改（决定 12）。每个组件有一个具体的子类
/// （<c>IconControl : ElementControl&lt;Icon&gt;</c>），设计器的工具箱认不了泛型。
/// 默认跟随元素想要的大小（AutoSize，同 Label）。绘制在一个 <see cref="Effect"/> 里追踪：元素读到的信号（主题）变了
/// 只让这个控件失效；释放控件时退订。
/// </summary>
/// <typeparam name="T">元素的类型。</typeparam>
[ToolboxItem(false)]
public abstract class ElementControl<T> : Control, IElementHost where T : Element, new()
{
    readonly Effect paint;

    /// <summary>建好元素并接到自己身上。</summary>
    protected ElementControl()
    {
        paint = new Effect(Invalidate);
        Element = new T { Host = this };
        // 背景色是环境属性，默认跟随父控件；父控件有背景图时可以设成 Transparent
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        AutoSize = true;
    }

    /// <summary>承载的元素：代码里直接改它的属性。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public T Element { get; }

    /// <summary>跟随元素想要的大小（默认开着）。</summary>
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(true)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    ElementContext Context => new(DeviceDpi, Font, ForeColor, RightToLeft == RightToLeft.Yes);

    /// <summary>元素想要的大小加上 Padding。宽度受布局给的建议宽度与 MaximumSize 限制（同 Label：要换行就设 MaximumSize 或关掉 AutoSize）。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        // 布局引擎用 0、1 或 int.MaxValue 表示"不限"
        var width = proposedSize.Width is > 1 and < int.MaxValue ? proposedSize.Width : int.MaxValue;
        if (MaximumSize.Width > 0) width = Math.Min(width, MaximumSize.Width);
        var available = new Size(width == int.MaxValue ? width : Math.Max(0, width - Padding.Horizontal), int.MaxValue);
        return Element.Measure(Context, available) + Padding.Size;
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Element.Bounds = new Rectangle(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        Element.Paint(e.Graphics, Context);
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RefreshLayout();
    }

    /// <inheritdoc />
    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        RefreshLayout();
    }

    /// <inheritdoc />
    protected override void OnAutoSizeChanged(EventArgs e)
    {
        base.OnAutoSizeChanged(e);
        RefreshLayout();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        RefreshLayout();
    }

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        Invalidate();
    }

    void IElementHost.InvalidateElement(Element element) => Invalidate(element.Bounds);

    void IElementHost.InvalidateLayout(Element element) => RefreshLayout();

    void RefreshLayout()
    {
        if (AutoSize) Size = GetPreferredSize(Size.Empty);
        PerformLayout();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }
}
