using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 评分（vue-tsx-pro 的 Rating）：几颗星（或别的图标）里选一个分数，供应商评价、满意度、优先级。可以半星，可以只读显示。
/// <code>
/// var quality = new RatingControl { Value = 4.5, AllowHalf = true };
/// quality.ValueChanged += (_, _) => Save(quality.Value);
/// field.Content = quality;
/// var summary = new RatingControl { Value = 4.5, IsReadOnly = true, AccessibleName = "综合" };
/// </code>
/// 可编辑时是单选组：一个能聚焦的控件，读屏的子节点是各个分数（单选项，名称"3 星"）；方向键换分数（到头绕回），空格选中聚焦的分数。
/// 指针悬停时预览将要选的分数，再点一次已选的分数清空（<see cref="Clearable"/>，默认开）；<see cref="AllowHalf"/> 时每颗星左右各一半。
/// 只读（<see cref="IsReadOnly"/> 或所在字段只读）时是一张图，读屏名称是自己的名称（或字段标签）接上"4.5 星，满分 5 星"。
/// 星是 1.35em 的方块、隔 0.15em：空的是 borderStrong 的描线，选中的按比例填上色调的实色（默认 warning）；指针下的那颗放大到 1.12 倍。
/// </summary>
[DefaultEvent(nameof(ValueChanged))]
public class RatingControl : Control, IFieldControl, IScopeAware
{
    double? hover;
    // 键盘所在的分数（读屏的焦点、空格选中它）：跟着分数，清空后留在原处
    double focusScore = 1;

    /// <summary>新建：五颗星的单选组。</summary>
    public RatingControl()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    /// <summary>分数；没有评分时是 null。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public double? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (value is { } score) focusScore = score;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>一共几颗星，默认 5。</summary>
    [Category("Behavior")]
    [DefaultValue(5)]
    public int Count
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
            Relayout();
        }
    } = 5;

    /// <summary>可以选半颗。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool AllowHalf { get; set { field = value; Relayout(); } }

    /// <summary>再点一次已选的分数时清空，默认 true。</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    public bool Clearable { get; set; } = true;

    /// <summary>图标，默认星星。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconData Icon { get; set { field = value; Invalidate(); } } = Lucide.Star;

    /// <summary>选中的颜色，默认 warning（金黄）。</summary>
    [Category("Appearance")]
    public StatusTone Tone { get => field.Value is null ? StatusTone.Warning : field; set { field = value; Invalidate(); } }

    /// <summary>字号档位：星随之缩放；不写时跟着窗体。</summary>
    [Category("Appearance")]
    public TextSize TextSize
    {
        get;
        set
        {
            field = value;
            if (value.Pixels is { } pixels) Font = Fonts.Get(Font.FamilyName(), pixels * 72 / 96, Font.Style);
            else ResetFont();
        }
    }

    /// <summary>只读：只显示分数；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsReadOnly { get; set { field = value; Relayout(); } }

    bool ShouldSerializeTone() => Tone != StatusTone.Warning;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    /// <summary>分数变了（点选、方向键、清空）。</summary>
    public event EventHandler? ValueChanged;

    internal bool ReadOnly => IsReadOnly ?? FieldControl.ReadOnlyOf(this);

    /// <summary>能选的分数；只读时没有。</summary>
    internal double[] Scores => ReadOnly ? [] : RatingScale.Scores(Count, AllowHalf);

    /// <summary>第 <paramref name="index"/> 颗星（从 1 开始）按分数填了多少：0、50、100（测试用，悬停预览不算）。</summary>
    internal int Filled(int index) => RatingScale.Filled(Value, index);

    float Em => this.EmPixels();

    float StarSize => (float)(RatingScale.StarEm * Em);

    float Pitch => (float)((RatingScale.StarEm + RatingScale.GapEm) * Em);

    void IFieldControl.FieldChanged() => Relayout();

    void IScopeAware.ScopeChanged() => Invalidate();

    void Relayout()
    {
        // 只读时不能聚焦（是一张图）
        SetStyle(ControlStyles.Selectable, !ReadOnly);
        TabStop = !ReadOnly;
        Cursor = ReadOnly ? Cursors.Default : Cursors.Hand;
        if (AutoSize) Size = GetPreferredSize(Size.Empty);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Relayout();
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) =>
        new((int)Math.Ceiling(Count * StarSize + (Count - 1) * RatingScale.GapEm * Em), (int)Math.Ceiling(StarSize));

    /// <summary>第 <paramref name="index"/> 颗星（从 1 开始）的方块；从右往左排版时第一颗在右边。</summary>
    internal RectangleF StarBounds(int index)
    {
        var x = (index - 1) * Pitch;
        if (RightToLeft == RightToLeft.Yes) x = Width - x - StarSize;
        return new RectangleF(x, (Height - StarSize) / 2, StarSize, StarSize);
    }

    /// <summary>分数的点击区域：整颗，半星时起始边一半是 n - 0.5、结束边一半是 n。</summary>
    internal RectangleF ScoreBounds(double score)
    {
        var star = (int)Math.Ceiling(score);
        var bounds = StarBounds(star);
        if (!AllowHalf) return bounds;
        var startHalf = score != star;
        var left = startHalf == (RightToLeft != RightToLeft.Yes);
        return new RectangleF(left ? bounds.X : bounds.X + bounds.Width / 2, bounds.Y, bounds.Width / 2, bounds.Height);
    }

    double? ScoreAt(Point point) => Scores.Cast<double?>().FirstOrDefault(score => ScoreBounds(score!.Value).Contains(point));

    /// <summary>点了一个分数（指针、空格、读屏）：可清空时再点已选的清空。</summary>
    internal void Pick(double score)
    {
        if (ReadOnly || !Enabled) return;
        focusScore = score;
        hover = null;
        Value = RatingScale.Pick(Value, score, Clearable);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var next = Enabled ? ScoreAt(e.Location) : null;
        if (next == hover) return;
        hover = next;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = null;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || ScoreAt(e.Location) is not { } score) return;
        Focus();
        Pick(score);
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    /// <summary>方向键：换到前一个、后一个分数并选中（到头绕回）；↓ 同 →；从右往左排版时左右对调。空格选中聚焦的分数。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || ReadOnly || !Enabled) return;
        if (e.KeyCode == Keys.Space)
        {
            Pick(focusScore);
            e.Handled = true;
            return;
        }
        var rtl = RightToLeft == RightToLeft.Yes;
        var delta = e.KeyCode == Keys.Down || e.KeyCode == (rtl ? Keys.Left : Keys.Right) ? 1 : e.KeyCode == Keys.Up || e.KeyCode == (rtl ? Keys.Right : Keys.Left) ? -1 : 0;
        if (delta == 0) return;
        // 还没评分时从聚焦的那个（第一个）算起，同原生单选组
        var next = RatingScale.Step(Scores, Value ?? focusScore, delta);
        focusScore = next;
        Value = next;
        AccessibilityNotifyClients(AccessibleEvents.Focus, Array.IndexOf(Scores, next));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        // Tab 落在已选的分数上，没选时第一个
        focusScore = Value ?? Scores.FirstOrDefault(1);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <summary>
    /// 画星：空心的 borderStrong 描线打底，填色的那层按比例裁出（实色，填满图标）；指针下的那颗放大；
    /// 键盘聚焦时聚焦的那颗外面一圈 2 像素的焦点环（压进 1 像素）。不可用时整排半透明。
    /// </summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var colors = ProviderControl.ThemeOf(this).Colors;
        var alpha = Enabled ? 255 : (int)(255 * Tokens.Opacity.Disabled);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var shown = hover ?? Value;
        var hoveredStar = hover is { } preview ? (int)Math.Ceiling(preview) : 0;
        using var outline = Icon.CreatePath();
        using var empty = new Pen(Color.FromArgb(alpha, colors.BorderStrong), 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var tone = Color.FromArgb(alpha, ToneColor(colors, Tone));
        using var fill = new SolidBrush(tone);
        using var stroke = new Pen(tone, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        for (var index = 1; index <= Count; index++)
        {
            var bounds = StarBounds(index);
            var state = graphics.Save();
            var zoom = !ReadOnly && index == hoveredStar ? (float)RatingScale.HoverScale : 1;
            var center = new PointF(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            graphics.TranslateTransform(center.X, center.Y);
            graphics.ScaleTransform(zoom, zoom);
            graphics.TranslateTransform(-center.X, -center.Y);
            if (Focused && ShowFocusCues && !ReadOnly && (int)Math.Ceiling(focusScore) == index)
            {
                using var ring = new Pen(colors.FocusRing, 2);
                using var path = Drawing.RoundedRectangle(RectangleF.Inflate(bounds, 1, 1), (Radius.Sm.In(ProviderControl.ThemeOf(this).Radii) ?? 0) * Em, default);
                graphics.DrawPath(ring, path);
            }
            var place = graphics.Save();
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / IconData.Size, bounds.Height / IconData.Size);
            graphics.DrawPath(empty, outline);
            graphics.Restore(place);
            var percent = RatingScale.Filled(shown, index);
            if (percent > 0)
            {
                var width = bounds.Width * percent / 100f;
                // 从起始边裁起：从右往左排版时从右边
                graphics.SetClip(new RectangleF(RightToLeft == RightToLeft.Yes ? bounds.Right - width : bounds.X, bounds.Y - 2, width, bounds.Height + 4), CombineMode.Intersect);
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / IconData.Size, bounds.Height / IconData.Size);
                graphics.FillPath(fill, outline);
                graphics.DrawPath(stroke, outline);
            }
            graphics.Restore(state);
        }
    }

    /// <summary>选中的颜色：各色调的实色（金黄的星、红色的心），neutral 是 textMuted。实心与空心靠形状区分，不只靠颜色。</summary>
    static Color ToneColor(ThemeColors colors, StatusTone tone) => tone.Value switch
    {
        "accent" => colors.Accent,
        "info" => colors.Info,
        "success" => colors.Success,
        "danger" => colors.Danger,
        "neutral" => colors.TextMuted,
        _ => colors.Warning,
    };

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new RatingAccessibleObject(this);

    /// <summary>
    /// 读屏：可编辑时是单选组（名称取所在字段的标签，说明与错误是描述），子节点是各个分数；
    /// 只读时是一张图，名称里总有分数：自己的名称或字段标签后面接上"4.5 星，满分 5 星"。
    /// </summary>
    sealed class RatingAccessibleObject(RatingControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => owner.ReadOnly ? AccessibleRole.Graphic : AccessibleRole.Grouping;

        public override string? Description => FieldControl.DescribedBy(owner, owner.AccessibleDescription);

        public override string? Name
        {
            get
            {
                var own = owner.AccessibleName is { Length: > 0 } name ? name : FieldControl.Of(owner) is { } scope && scope.Owns(owner) ? scope.Label : null;
                if (!owner.ReadOnly) return own;
                var summary = ProviderControl.LangOf(owner).RatingSummary(owner.Value, owner.Count);
                return string.IsNullOrEmpty(own) ? summary : $"{own} {summary}";
            }
        }

        public override int GetChildCount() => owner.Scores.Length;

        public override AccessibleObject? GetChild(int index) => index >= 0 && index < owner.Scores.Length ? new OptionAccessibleObject(owner, this, owner.Scores[index]) : null;

        public override AccessibleObject? GetFocused() =>
            owner.Focused && Array.IndexOf(owner.Scores, owner.focusScore) is >= 0 and var index ? GetChild(index) : base.GetFocused();
    }

    /// <summary>一个分数：单选项，名称是"3 星"，选中与否同分数；默认动作是选它。</summary>
    sealed class OptionAccessibleObject(RatingControl owner, AccessibleObject parent, double score) : AccessibleObject
    {
        public override AccessibleRole Role => AccessibleRole.RadioButton;

        public override string Name => ProviderControl.LangOf(owner).RatingStar(score);

        public override AccessibleObject Parent => parent;

        public override AccessibleStates State =>
            AccessibleStates.Focusable | AccessibleStates.Selectable
            | (owner.Value == score ? AccessibleStates.Checked : AccessibleStates.None)
            | (owner.Focused && owner.focusScore == score ? AccessibleStates.Focused : AccessibleStates.None)
            | (owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);

        public override Rectangle Bounds => owner.RectangleToScreen(Rectangle.Round(owner.ScoreBounds(score)));

        public override string DefaultAction => "select";

        public override void DoDefaultAction()
        {
            if (owner.Value != score) owner.Pick(score);
        }
    }
}
