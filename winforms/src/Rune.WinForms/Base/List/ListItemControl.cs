using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Rune.WinForms;

/// <summary>
/// 列表项（vue-tsx-pro 的 ListItem）：<see cref="Control.Text"/> 是项的文字（按宽度换行），子控件（嵌套的列表、段落）排在文字下面。
/// 记号（圆点、编号）在起始边的 1.5em 里靠末端，文字与它隔 space.xs；带图标时图标在前（上边距 0.2em），文字隔 space.sm，不缩进。
/// 记号、图标、文字是画的（元素），读屏里是列表项，名称是文字。
/// </summary>
public class ListItemControl : Panel, IElementHost
{
    readonly Effect paint;
    readonly Text text = new();
    readonly Text marker = new() { Wrap = TextWrap.NoWrap, Align = TextAlign.End };
    Icon? icon;
    string markerText = "";
    IconData? listIcon;
    IconProps? listPropsIcon;

    /// <summary>新建：自动大小，记号、文字自己画。</summary>
    public ListItemControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleRole = AccessibleRole.ListItem;
        paint = new Effect(Invalidate);
        text.Host = this;
        marker.Host = this;
    }

    /// <summary>项的文字。</summary>
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [Category("Appearance")]
    [Description("项的文字（按宽度换行）；嵌套的列表加成子控件。")]
    [AllowNull]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <summary>这一项的图标，覆盖列表的 Icon（有序列表里不用）。</summary>
    [Category("Appearance")]
    [Description("这一项的图标，覆盖列表的 Icon。")]
    [DefaultValue(null)]
    public IconData? Icon { get; set { field = value; UpdateIcon(); } }

    /// <summary>图标的部件属性，合并在列表的 PropsIcon 上面。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIcon { get; set { field = value; UpdateIcon(); } }

    /// <summary>画出来的记号（圆点、编号）；带图标时不画。</summary>
    internal string Marker => icon is null ? markerText : "";

    /// <summary>画出来的图标（合并后的部件属性已套上）。</summary>
    internal Icon? IconElement => icon;

    /// <summary>文字的位置。</summary>
    internal Rectangle TextBounds => text.Bounds;

    /// <summary>列表给的记号、默认图标与它的部件属性。</summary>
    internal void SetMarker(string text, IconData? icon, IconProps? propsIcon)
    {
        (markerText, listIcon, listPropsIcon) = (text, icon, propsIcon);
        marker.Content = text;
        UpdateIcon();
    }

    void UpdateIcon()
    {
        var shown = Icon ?? listIcon;
        if (shown is null) icon = null;
        else
        {
            icon = new Icon { Host = this };
            new IconProps { Data = shown }.Merge(listPropsIcon).Merge(PropsIcon).ApplyTo(icon);
        }
        Relayout();
    }

    ElementContext Context => new(DeviceDpi, Font, ForeColor, RightToLeft == RightToLeft.Yes);

    /// <summary>文字的起始边离控件起始边多远：记号的 1.5em + space.xs，或图标 + space.sm。</summary>
    int Indent(ElementContext context) => icon is null
        ? (int)MathF.Round((1.5f + Tokens.Space.Xs) * context.Em)
        : icon.Measure(context, Size.Empty).Width + (int)MathF.Round(Tokens.Space.Sm * context.Em);

    /// <summary>文字的高度（没有文字时 0）。</summary>
    int TextHeight(ElementContext context, int width) => Text.Length == 0 ? 0 : text.Measure(context, new Size(Math.Max(1, width), int.MaxValue)).Height;

    /// <summary>想要的大小（同 Label）：文字不换行时的宽度，不超过布局给的宽度与 MaximumSize；高度按这个宽度换行。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var context = Context;
        var indent = Indent(context);
        var children = VerticalStack.Shown(this);
        var natural = Math.Max(Text.Length == 0 ? 0 : text.Measure(context, new Size(int.MaxValue, int.MaxValue)).Width, VerticalStack.Measure(children, 0, 0).Width);
        var width = Math.Max(0, Math.Min(natural, VerticalStack.Limit(this, proposedSize) - Padding.Horizontal - indent));
        var height = TextHeight(context, width) + VerticalStack.Measure(children, width, 0).Height;
        var markerHeight = icon is null ? marker.Measure(context, Size.Empty).Height : (int)MathF.Round(0.2f * context.Em) + icon.Measure(context, Size.Empty).Height;
        return new Size(indent + width + Padding.Horizontal, Math.Max(height, markerHeight) + Padding.Vertical);
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        var context = Context;
        var area = DisplayRectangle;
        var indent = Indent(context);
        var content = new Rectangle(area.X + indent, area.Y, Math.Max(0, area.Width - indent), area.Height);
        var textHeight = TextHeight(context, content.Width);
        text.Bounds = Mirror(content with { Height = textHeight });
        var children = VerticalStack.Shown(this);
        VerticalStack.Arrange(children, content with { Y = content.Y + textHeight }, 0);
        if (RightToLeft == RightToLeft.Yes)
            foreach (var child in children) child.Left = ClientSize.Width - child.Right;
        if (icon is not null)
        {
            var size = icon.Measure(context, Size.Empty);
            icon.Bounds = Mirror(new Rectangle(area.X, area.Y + (int)MathF.Round(0.2f * context.Em), size.Width, size.Height));
        }
        // 记号靠 1.5em 的末端，再空出约一个空格（CSS 的 outside 记号后面跟着空格）
        var markerSize = marker.Measure(context, Size.Empty);
        marker.Bounds = Mirror(new Rectangle(area.X, area.Y, (int)MathF.Round(1.2f * context.Em), markerSize.Height));
        Invalidate();
    }

    Rectangle Mirror(Rectangle bounds) => RightToLeft == RightToLeft.Yes ? bounds with { X = ClientSize.Width - bounds.Right } : bounds;

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // 绘制读到的主题 token 变了时重画（同 ElementControl）
        using var _ = paint.Track();
        var context = Context;
        if (icon is not null) icon.Paint(e.Graphics, context);
        else if (markerText.Length > 0) marker.Paint(e.Graphics, context);
        if (Text.Length > 0) text.Paint(e.Graphics, context);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        text.Content = Text;
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Relayout();
    }

    /// <summary>大小可能变了：让列表重排（它按想要的高度放项），再排自己。</summary>
    void Relayout()
    {
        if (Parent is { } parent) parent.PerformLayout(this, nameof(PreferredSize));
        else if (AutoSize) Size = GetPreferredSize(Size.Empty);
        PerformLayout();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    void IElementHost.InvalidateElement(Element element) => Invalidate(element.Bounds);

    void IElementHost.InvalidateLayout(Element element) => Relayout();
}
