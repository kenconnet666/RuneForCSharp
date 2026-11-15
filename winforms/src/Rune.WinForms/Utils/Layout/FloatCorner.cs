namespace Rune.WinForms;

/// <summary>摆在父控件客户区的哪个位置：横向 -1 起始边、0 居中、1 结束边（从右到左排版时左右对调）；竖向在顶部或底部；离边的距离（像素）。</summary>
/// <param name="Inline">横向：-1 起始边、0 居中、1 结束边。</param>
/// <param name="Top">在顶部。</param>
/// <param name="Margin">离边的距离（像素）。</param>
readonly record struct CornerSpot(int Inline, bool Top, int Margin)
{
    /// <summary>结束边与底部，外边距 lg（浮动按钮）。</summary>
    public static CornerSpot BottomEnd(Control owner) => new(1, false, (int)MathF.Round(Tokens.Space.Lg * ElementContext.Of(owner).Em));
}

/// <summary>
/// 浮在父控件上的控件（浮动按钮、通知区域）摆在父控件客户区的一角或边的中间，跟着父控件变大小、放到最上层（TS 的 position: fixed 加 inset）。
/// 位置每次现取（<see cref="CornerSpot"/>）；取到 null 时不摆（组里的浮动按钮跟着组排）。
/// </summary>
sealed class FloatCorner
{
    readonly Control owner;
    readonly Func<CornerSpot?> spot;
    Control? parent;

    public FloatCorner(Control owner, Func<CornerSpot?> spot)
    {
        this.owner = owner;
        this.spot = spot;
        owner.ParentChanged += (_, _) => Watch();
        owner.SizeChanged += (_, _) => Place();
        owner.RightToLeftChanged += (_, _) => Place();
    }

    void Watch()
    {
        if (parent is not null)
        {
            parent.Resize -= OnParentResized;
            parent.Layout -= OnParentLayout;
        }
        parent = owner.Parent;
        if (parent is null) return;
        parent.Resize += OnParentResized;
        parent.Layout += OnParentLayout;
        Place();
    }

    void OnParentResized(object? sender, EventArgs e) => Place();

    void OnParentLayout(object? sender, LayoutEventArgs e) => Place();

    public void Place()
    {
        if (parent is null || owner.Parent != parent || spot() is not { } where) return;
        var area = parent.DisplayRectangle;
        var inline = owner.RightToLeft == RightToLeft.Yes ? -where.Inline : where.Inline;
        var x = inline switch
        {
            < 0 => area.Left + where.Margin,
            0 => area.Left + (area.Width - owner.Width) / 2,
            _ => area.Right - where.Margin - owner.Width,
        };
        var y = where.Top ? area.Top + where.Margin : area.Bottom - where.Margin - owner.Height;
        var location = new Point(x, y);
        if (owner.Location != location) owner.Location = location;
        if (parent.Controls.GetChildIndex(owner) != 0) owner.BringToFront();
    }
}
