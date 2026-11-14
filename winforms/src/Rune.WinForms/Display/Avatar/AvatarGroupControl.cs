using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 头像组（vue-tsx-pro 的 AvatarGroup）：一排头像叠放，超过 <see cref="Max"/> 的折叠成"+N"。协作者、参与人、点赞的人。
/// <code>
/// var members = new AvatarGroupControl { Max = 3, AccessibleName = "协作者" };
/// foreach (var user in users) members.Avatars.Add(new Avatar { DisplayName = user.Name, Source = user.Photo });
/// </code>
/// 头像是画出来的元素（<see cref="Avatar"/>）：叠放的头像要按顺序盖住前一个、各带一圈页面底色的描边（borderWidths.sm），子控件做不到。
/// 头像的大小随组的 <see cref="TextSize"/>（或字体）；往前叠 0.5em。"+N"和头像一样大（圆形、fill 底、正文色、xs / md 的字号、中等字重），
/// 读屏念"还有 N 人"（语言包的 AvatarMore）；<see cref="Total"/> 给了时按它算 N（列表只拿到了一部分时）。
/// 读屏里是一组（名称是 AccessibleName），子项是各个头像（名称是名字）与"+N"。大小跟着内容。
/// </summary>
public class AvatarGroupControl : Control, IElementHost, IScopeAware
{
    readonly Effect paint;
    Text moreText = new();
    TextSize textSize;

    /// <summary>新建：没有头像。</summary>
    public AvatarGroupControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        AutoSize = true;
        AccessibleRole = AccessibleRole.Grouping;
        paint = new Effect(Invalidate);
        Avatars = new AvatarList(this);
        RebuildMoreText();
    }

    /// <summary>头像。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Collection<Avatar> Avatars { get; }

    /// <summary>最多显示几个头像，其余折叠成"+N"；不写时全部显示。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public int? Max { get; set { field = value; Relayout(); } }

    /// <summary>总人数：列表只拿到前几个时给出，"+N"按它算。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public int? Total { get; set { field = value; Relayout(); } }

    /// <summary>"+N"文字的部件属性（TS 的 propsMoreText），逐字段盖在默认的中等字重上；字号默认 xs / md。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsMoreText { get; set { field = value; RebuildMoreText(); } }

    /// <summary>新建"+N"的文字元素，套上合并后的部件属性（没写的字段回到元素自己的默认）。</summary>
    void RebuildMoreText()
    {
        moreText = new Text { Host = this, Wrap = TextWrap.NoWrap };
        new TextProps { Weight = TextWeight.Medium }.Merge(PropsMoreText).ApplyTo(moreText);
        Relayout();
    }

    /// <summary>字号档位，头像边长是它的 2.5 倍；不写时跟随字体。</summary>
    [Category("Appearance")]
    public TextSize TextSize { get => textSize; set { textSize = value; ApplyTextSize(); } }

    bool ShouldSerializeTextSize() => textSize.Value is not null;

    /// <inheritdoc />
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [DefaultValue(true)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    ElementContext Context => ElementContext.Of(this);

    /// <summary>显示的头像。</summary>
    internal IReadOnlyList<Avatar> Shown => Max is { } max && Avatars.Count > max ? [.. Avatars.Take(Math.Max(0, max))] : [.. Avatars];

    /// <summary>折叠了几个（"+N"的 N）；不折叠时 0。</summary>
    internal int Hidden => Math.Max(0, (Total ?? Avatars.Count) - Shown.Count);

    /// <summary>"+N"的位置（测试用）；不折叠时为空。</summary>
    internal Rectangle MoreBounds { get; private set; }

    /// <summary>"+N"的文字元素（测试用）。</summary>
    internal Text MoreText => moreText;

    void ApplyTextSize()
    {
        if (TextSize.Pixels is { } pixels)
        {
            var outer = Parent?.Font ?? Font;
            Font = Fonts.Get(outer.FamilyName(), pixels * 72 / 96, outer.Style);
        }
        else ResetFont();
        Relayout();
    }

    void IScopeAware.ScopeChanged()
    {
        ApplyTextSize();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        ((IScopeAware)this).ScopeChanged();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    void Relayout()
    {
        if (AutoSize) Size = GetPreferredSize(Size.Empty);
        Arrange();
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
    }

    /// <summary>一排：后一个往前叠 0.5em（按它自己的字号，头像可以写自己的 Size），竖直居中。</summary>
    Size Arrange()
    {
        var context = Context;
        var items = Shown.Select(avatar => (Element: (Element?)avatar, Size: avatar.Measure(context, Size.Empty), Em: context.Sized(avatar.Size).Em)).ToList();
        var hidden = Hidden;
        if (hidden > 0)
        {
            var side = (int)MathF.Round(2.5f * context.Em);
            items.Add((null, new Size(side, side), context.Em));
        }
        var height = items.Count == 0 ? 0 : items.Max(item => item.Size.Height);
        var x = 0;
        MoreBounds = Rectangle.Empty;
        for (var index = 0; index < items.Count; index++)
        {
            var (element, size, em) = items[index];
            if (index > 0) x -= (int)MathF.Round(0.5f * em);
            var bounds = new Rectangle(x, (height - size.Height) / 2, size.Width, size.Height);
            if (element is not null) element.Bounds = bounds;
            else MoreBounds = bounds;
            x += size.Width;
        }
        moreText.Content = hidden > 0 ? $"+{hidden}" : "";
        // 字号缩在文字上："+N"的圆仍和头像一样大
        if (PropsMoreText?.Size is null) moreText.Size = Tokens.FontSize.Xs / Tokens.FontSize.Md * context.Em * 96f / DeviceDpi;
        return new Size(Math.Max(0, x), height);
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Arrange();

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Arrange();
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var _ = paint.Track();
        var context = Context;
        var graphics = e.Graphics;
        var colors = context.Theme.Colors;
        var ring = context.Scale(Tokens.BorderWidth.Sm);
        // 按顺序画：每一项先画一圈页面底色（TS 的 box-shadow: 0 0 0 borderWidths.sm background），后一项盖在前一项上，描边把相邻的分开
        foreach (var avatar in Shown)
        {
            var side = Math.Min(avatar.Bounds.Width, avatar.Bounds.Height);
            Ring(avatar.Bounds, avatar.CornerRadius(context, side));
            avatar.Paint(graphics, context);
        }
        if (MoreBounds.IsEmpty) return;
        Ring(MoreBounds, MoreBounds.Width / 2f);
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var fill = new SolidBrush(colors.Fill)) graphics.FillEllipse(fill, MoreBounds);
        graphics.Restore(state);
        var text = moreText.Measure(context, MoreBounds.Size);
        moreText.Bounds = new Rectangle(MoreBounds.X + (MoreBounds.Width - text.Width) / 2, MoreBounds.Y + (MoreBounds.Height - text.Height) / 2, text.Width, text.Height);
        moreText.Paint(graphics, context with { ForeColor = colors.Text });

        void Ring(Rectangle bounds, float radius)
        {
            var inner = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var shape = Drawing.RoundedRectangle(RectangleF.Inflate(bounds, ring, ring), radius + ring);
            using var brush = new SolidBrush(colors.Background);
            graphics.FillPath(brush, shape);
            graphics.Restore(inner);
        }
    }

    void IElementHost.InvalidateElement(Element element) => Invalidate();

    void IElementHost.InvalidateLayout(Element element) => Relayout();

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new GroupAccessibleObject(this);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) paint.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>头像加进来时挂到组上，增删都重新排版。</summary>
    sealed class AvatarList(AvatarGroupControl owner) : Collection<Avatar>
    {
        protected override void InsertItem(int index, Avatar item)
        {
            item.Host = owner;
            base.InsertItem(index, item);
            owner.Relayout();
        }

        protected override void SetItem(int index, Avatar item)
        {
            this[index].Host = null;
            item.Host = owner;
            base.SetItem(index, item);
            owner.Relayout();
        }

        protected override void RemoveItem(int index)
        {
            this[index].Host = null;
            base.RemoveItem(index);
            owner.Relayout();
        }

        protected override void ClearItems()
        {
            foreach (var item in this) item.Host = null;
            base.ClearItems();
            owner.Relayout();
        }
    }

    /// <summary>读屏：一组，子项是显示的头像（有名字的才有名称）与"+N"（"还有 N 人"）。</summary>
    sealed class GroupAccessibleObject(AvatarGroupControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Grouping;

        public override int GetChildCount() => owner.Shown.Count + (owner.Hidden > 0 ? 1 : 0);

        public override AccessibleObject? GetChild(int index)
        {
            var shown = owner.Shown;
            if (index >= 0 && index < shown.Count) return new ItemAccessibleObject(this, owner, shown[index].DisplayName ?? "", shown[index].Bounds);
            return index == shown.Count && owner.Hidden > 0 ? new ItemAccessibleObject(this, owner, owner.Context.Lang.AvatarMore(owner.Hidden), owner.MoreBounds) : null;
        }
    }

    /// <summary>组里的一项：一张图片。</summary>
    sealed class ItemAccessibleObject(AccessibleObject parent, AvatarGroupControl owner, string name, Rectangle bounds) : AccessibleObject
    {
        public override AccessibleObject Parent => parent;

        public override AccessibleRole Role => AccessibleRole.Graphic;

        public override string Name => name;

        public override Rectangle Bounds => owner.RectangleToScreen(bounds);

        public override AccessibleStates State => AccessibleStates.ReadOnly;
    }
}
