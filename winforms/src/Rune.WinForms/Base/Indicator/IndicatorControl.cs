using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 角标（vue-tsx-pro 的 Indicator）：叠在头像、图标按钮角上的计数或小圆点（未读消息、在线状态）。被标记的控件加成子控件：
/// <code>
/// var bell = new IndicatorControl { Count = unread };
/// bell.Controls.Add(new IconButtonControl { Icon = Lucide.Bell, AccessibleName = "通知" });
/// </code>
/// 角标的中心对准子控件的角，圆形子控件（Overlap = circular）上收进它宽度的 14.6%。WinForms 的控件画不到自己外面：
/// 角标是叠在子控件上面的小控件（按形状裁剪），伸出子控件的那一半由 IndicatorControl 留出位置（TS 里角标不占位置）。
/// 计数超过 <see cref="Max"/> 显示"99+"；Count 为 0（且没有 ShowZero）、也没有 Dot 时不显示角标。
/// 读屏：角标是静态文字，名称是 <see cref="Label"/>（默认取语言包的"N 条新消息""有新消息"），数字本身不念。
/// </summary>
public class IndicatorControl : Panel
{
    readonly IndicatorBadge badge;
    readonly IndicatorLayout layout;
    StatusTone tone;

    /// <summary>新建：自动大小，角标叠在最上面。</summary>
    public IndicatorControl()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        layout = new IndicatorLayout(this);
        badge = new IndicatorBadge(this);
        Controls.Add(badge);
        UpdateBadge();
    }

    /// <summary>计数；超过 Max 显示"Max+"。</summary>
    [Category("Appearance")]
    [Description("计数；超过 Max 显示\"Max+\"。")]
    [DefaultValue(null)]
    public int? Count { get; set { field = value; UpdateBadge(); } }

    /// <summary>计数的上限，默认 99。</summary>
    [Category("Appearance")]
    [Description("计数的上限。")]
    [DefaultValue(99)]
    public int Max { get; set { field = value; UpdateBadge(); } } = 99;

    /// <summary>只显示小圆点（不显示数字）。</summary>
    [Category("Appearance")]
    [Description("只显示小圆点（不显示数字）。")]
    [DefaultValue(false)]
    public bool Dot { get; set { field = value; UpdateBadge(); } }

    /// <summary>Count 为 0 时也显示。</summary>
    [Category("Appearance")]
    [Description("Count 为 0 时也显示。")]
    [DefaultValue(false)]
    public bool ShowZero { get; set { field = value; UpdateBadge(); } }

    /// <summary>色调；不写时取作用域的默认值（<see cref="IndicatorDefaults"/>），再没有时 danger。</summary>
    [Category("Appearance")]
    [Description("色调，默认 danger。")]
    public StatusTone Tone { get => tone.Or(ProviderControl.Of(this)?.ResolveDefaults<IndicatorDefaults>()?.Tone, StatusTone.Danger); set { tone = value; badge.Invalidate(); } }

    /// <summary>位置；不写时 top-end。</summary>
    [Category("Appearance")]
    [Description("角标在哪个角（按书写方向），默认 top-end。")]
    public IndicatorPlacement Placement { get => field.Value is null ? IndicatorPlacement.TopEnd : field; set { field = value; Relayout(); } }

    /// <summary>被标记控件的形状；不写时 rectangular。</summary>
    [Category("Appearance")]
    [Description("被标记控件的形状：circular 时角标收到圆的边上。")]
    public IndicatorOverlap Overlap { get => field.Value is null ? IndicatorOverlap.Rectangular : field; set { field = value; Relayout(); } }

    /// <summary>给读屏的说明；不写时取语言包。</summary>
    [Category("Accessibility")]
    [Description("给读屏的说明；不写时取语言包（\"N 条新消息\"\"有新消息\"）。")]
    [DefaultValue(null)]
    public string? Label { get; set { field = value; UpdateBadge(); } }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => layout;

    /// <summary>被标记的控件：除角标外的第一个子控件。</summary>
    internal Control? Content => Controls.Cast<Control>().FirstOrDefault(control => control != badge);

    /// <summary>角标（测试、inspect 用）。</summary>
    internal Control Badge => badge;

    /// <summary>角标里显示的文字（圆点时为空）。</summary>
    internal string BadgeText => Dot || Count is not { } n ? "" : n > Max ? $"{Max}+" : n.ToString(CultureInfo.CurrentCulture);

    /// <summary>角标显示吗：有 Dot，或计数大于 0（ShowZero 时 0 也算）。</summary>
    bool Shown => Dot || Count is { } n && (n > 0 || ShowZero);

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>角标外面一圈页面底色的宽度（borderWidths.sm）。</summary>
    int Ring => (int)MathF.Round(Tokens.BorderWidth.Sm * DeviceDpi / 96f);

    bool ShouldSerializeTone() => tone.Value is not null;

    void ResetTone() => Tone = default;

    bool ShouldSerializePlacement() => Placement != IndicatorPlacement.TopEnd;

    void ResetPlacement() => Placement = default;

    bool ShouldSerializeOverlap() => Overlap != IndicatorOverlap.Rectangular;

    void ResetOverlap() => Overlap = default;

    /// <summary>角标的大小（含外面那圈）：圆点 0.625em；计数的字号 0.75em，最小宽、高 1.6 倍，左右内边距 0.4 倍。</summary>
    internal Size BadgeSize()
    {
        if (Dot)
        {
            var dot = (int)MathF.Round(0.625f * Em) + 2 * Ring;
            return new Size(dot, dot);
        }
        var size = 0.75f * Em;
        var text = TextRenderer.MeasureText(BadgeText, CountFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        var height = (int)MathF.Round(1.6f * size);
        var width = Math.Max(height, text.Width + (int)MathF.Round(0.8f * size));
        return new Size(width + 2 * Ring, height + 2 * Ring);
    }

    /// <summary>计数的字体：0.75em、粗体（GDI 只有常规与粗体，semibold 按粗体画）。</summary>
    internal Font CountFont => Fonts.Get(Font.FontFamily.Name, Font.SizeInPoints * 0.75f, FontStyle.Bold);

    /// <summary>读屏念的说明。</summary>
    internal string Description => Label ?? (Dot ? ProviderControl.LangOf(this).IndicatorDot : ProviderControl.LangOf(this).IndicatorCount(Count ?? 0));

    void UpdateBadge()
    {
        badge.Visible = Shown;
        badge.AccessibleName = Description;
        Relayout();
    }

    void Relayout()
    {
        PerformLayout();
        badge.Invalidate();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => layout.Measure();

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        // 角标总在最上面
        badge.BringToFront();
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

    /// <summary>
    /// 排版：被标记的控件放在留出的位置后面，角标的中心对准它的角（从右到左时 end 在左边）。
    /// 留出的是角标伸出子控件的部分：中心在角上时是一半，圆形时收进去的那段抵掉一部分。
    /// </summary>
    sealed class IndicatorLayout(IndicatorControl owner) : LayoutEngine
    {
        (Size Content, Size Badge, Padding Overflow, PointF Corner) Compute()
        {
            var content = owner.Content is { } c ? c.AutoSize ? c.GetPreferredSize(Size.Empty) : c.Size : Size.Empty;
            var badge = owner.Shown ? owner.BadgeSize() : Size.Empty;
            var inset = owner.Overlap == IndicatorOverlap.Circular ? 0.146f * content.Width : 0;
            var end = owner.Placement.Value is "top-end" or "bottom-end";
            var right = end != (owner.RightToLeft == RightToLeft.Yes);
            var bottom = owner.Placement.Value is "bottom-end" or "bottom-start";
            int Over(float half) => Math.Max(0, (int)MathF.Ceiling(half - inset));
            var (h, v) = (Over(badge.Width / 2f), Over(badge.Height / 2f));
            var overflow = new Padding(right ? 0 : h, bottom ? 0 : v, right ? h : 0, bottom ? v : 0);
            var corner = new PointF(right ? content.Width - inset : inset, bottom ? content.Height - inset : inset);
            return (content, badge, overflow, corner);
        }

        public Size Measure()
        {
            var (content, _, overflow, _) = Compute();
            return content + overflow.Size;
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            var (content, badge, overflow, corner) = Compute();
            owner.Content?.SetBounds(overflow.Left, overflow.Top, content.Width, content.Height);
            var center = new PointF(overflow.Left + corner.X, overflow.Top + corner.Y);
            owner.badge.SetBounds((int)MathF.Round(center.X - badge.Width / 2f), (int)MathF.Round(center.Y - badge.Height / 2f), badge.Width, badge.Height);
            owner.badge.UpdateRegion();
            return owner.AutoSize;
        }
    }

    /// <summary>角标：按胶囊形裁剪的小控件，画外面一圈页面底色、实底色调与计数。读屏里是静态文字，名称是说明。</summary>
    sealed class IndicatorBadge : Control
    {
        readonly IndicatorControl owner;
        readonly Effect paint;

        public IndicatorBadge(IndicatorControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AccessibleRole = AccessibleRole.StaticText;
            paint = new Effect(Invalidate);
        }

        /// <summary>按当前大小裁成胶囊形（Region 没有抗锯齿：外面那圈是页面底色，锯齿不显眼）。</summary>
        public void UpdateRegion()
        {
            using var path = Drawing.RoundedRectangle(new RectangleF(0, 0, Width, Height), Height / 2f);
            var old = Region;
            Region = new Region(path);
            old?.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var _ = paint.Track();
            var colors = ProviderControl.ThemeOf(owner).Colors;
            var (background, foreground, _) = BadgeRecipe.Colors("solid", owner.Tone.Value, false, colors);
            var graphics = e.Graphics;
            graphics.Clear(colors.Background);
            var ring = owner.Ring;
            var state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var inner = new RectangleF(ring, ring, Width - 2 * ring, Height - 2 * ring);
                using var path = Drawing.RoundedRectangle(inner, inner.Height / 2);
                using var brush = new SolidBrush(background ?? colors.Text);
                graphics.FillPath(brush, path);
            }
            finally
            {
                graphics.Restore(state);
            }
            if (owner.Dot) return;
            TextRenderer.DrawText(graphics, owner.BadgeText, owner.CountFont, ClientRectangle, foreground,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) paint.Dispose();
            base.Dispose(disposing);
        }
    }
}

/// <summary>角标在被标记控件的哪个角（vue-tsx-pro 的 IndicatorPlacements），按书写方向。</summary>
/// <param name="Value">取值（top-end、bottom-end、top-start、bottom-start）。</param>
[TypeConverter(typeof(VariantConverter<IndicatorPlacement>))]
public readonly record struct IndicatorPlacement(string Value) : IVariant<IndicatorPlacement>
{
    /// <summary>右上（默认；从右到左排版时左上）。</summary>
    public static readonly IndicatorPlacement TopEnd = new("top-end");

    /// <summary>右下：头像的在线状态。</summary>
    public static readonly IndicatorPlacement BottomEnd = new("bottom-end");

    /// <summary>左上。</summary>
    public static readonly IndicatorPlacement TopStart = new("top-start");

    /// <summary>左下。</summary>
    public static readonly IndicatorPlacement BottomStart = new("bottom-start");

    /// <inheritdoc />
    public static IReadOnlyList<IndicatorPlacement> Known { get; } = [TopEnd, BottomEnd, TopStart, BottomStart];

    /// <inheritdoc />
    public static IndicatorPlacement Parse(string value)
    {
        var placement = new IndicatorPlacement(value);
        Variants.WarnIfUnknown(placement);
        return placement;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>被标记控件的形状（vue-tsx-pro 的 IndicatorOverlaps）：决定角标往里收多少。</summary>
/// <param name="Value">取值（rectangular、circular）。</param>
[TypeConverter(typeof(VariantConverter<IndicatorOverlap>))]
public readonly record struct IndicatorOverlap(string Value) : IVariant<IndicatorOverlap>
{
    /// <summary>方形（默认）：角标中心在角上。</summary>
    public static readonly IndicatorOverlap Rectangular = new("rectangular");

    /// <summary>圆形（圆形头像）：角标收到圆的边上。</summary>
    public static readonly IndicatorOverlap Circular = new("circular");

    /// <inheritdoc />
    public static IReadOnlyList<IndicatorOverlap> Known { get; } = [Rectangular, Circular];

    /// <inheritdoc />
    public static IndicatorOverlap Parse(string value)
    {
        var overlap = new IndicatorOverlap(value);
        Variants.WarnIfUnknown(overlap);
        return overlap;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Indicator 在作用域里的默认值（上游的 <c>defaults.Indicator</c>）。</summary>
public sealed record IndicatorDefaults : IScopeRecord<IndicatorDefaults>
{
    /// <summary>色调。</summary>
    public StatusTone? Tone { get; init; }

    /// <inheritdoc />
    public IndicatorDefaults Merge(IndicatorDefaults? over) => over is null ? this : new() { Tone = over.Tone ?? Tone };
}
