using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 滑块（vue-tsx-pro 的 Slider）：在一个范围里拖动选值（音量、进度、价格）。
/// <code>
/// var volume = new SliderControl { Value = 60, AccessibleName = "音量" };
/// var price = new SliderControl { Range = new SliderRange(800, 3500), Max = 5000, Step = 100 };   // 范围：两个滑块
/// price.ValueCommitted += (_, _) =&gt; Query(price.Range);
/// </code>
/// 方向键 ±Step，Page Up / Page Down ±BigStep，Home / End 到两端；从右到左排版时横向的方向键反过来。点击轨道任意位置直接跳过去并接着拖动。
/// <see cref="Range"/> 不为 null 时是范围选择：两个滑块，Tab 在两个之间走（再按 Tab 离开），不能交叉；点击轨道移动离得近的那个。
/// 读屏：单值时控件自己是滑块；范围时是一组、两个滑块子节点，名称后面加"最小值""最大值"。
/// 一次操作结束（松开、按键）时触发 <see cref="ValueCommitted"/>。整个控件自己画：轨道 fill 底、已选部分是色调的实底、
/// 滑块 surface 底与色调描边、刻度点与刻度文字。横向时默认占满给的宽度（AutoSize：在 FlexControl 里拉伸）。不可用时整体半透明。
/// </summary>
[DefaultProperty(nameof(Value))]
[DefaultEvent(nameof(ValueCommitted))]
[Description("滑块：在一个范围里拖动选值；可以选范围。")]
public class SliderControl : Control, IFieldControl, IScopeAware
{
    // 键盘所在的滑块（范围时 0 是低的一端）
    int focusIndex;
    int dragging = -1;

    /// <summary>新建：0–100、步长 1 的单值滑块。</summary>
    public SliderControl()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    /// <summary>单值。</summary>
    [Category("Behavior")]
    [DefaultValue(0d)]
    public double Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>范围选择的取值；不为 null 时是两个滑块。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SliderRange? Range
    {
        get;
        set
        {
            if (field == value) return;
            var shape = (field is null) != (value is null);
            field = value;
            if (shape) focusIndex = 0;
            Changed();
            RangeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>最小值，默认 0。</summary>
    [Category("Behavior")]
    [DefaultValue(0d)]
    public double Min { get; set { field = value; Changed(); } }

    /// <summary>最大值，默认 100。</summary>
    [Category("Behavior")]
    [DefaultValue(100d)]
    public double Max { get; set { field = value; Changed(); } } = 100;

    /// <summary>步长，默认 1；取值总是对齐到 Min + n × Step。</summary>
    [Category("Behavior")]
    [DefaultValue(1d)]
    public double Step
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 1;

    /// <summary>Page Up / Page Down 的步长；不写时为范围的十分之一（至少一个 Step）。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public double? BigStep { get; set; }

    /// <summary>方向，默认横向。</summary>
    [Category("Appearance")]
    public SliderOrientation Orientation { get => field.Value is null ? SliderOrientation.Horizontal : field; set { field = value; Relayout(); } }

    /// <summary>只读：能聚焦，拖动与方向键不改值；不写时跟着所在的字段。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public bool? IsReadOnly { get; set { field = value; Invalidate(); } }

    /// <summary>字号档位：滑块与轨道随之缩放；不写时跟着窗体。</summary>
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

    /// <summary>已选范围与滑块描边的色调，默认 accent。</summary>
    [Category("Appearance")]
    public StatusTone Tone { get; set { field = value; Invalidate(); } }

    /// <summary>刻度：值或带文字的刻度；有文字时下方（纵向时右侧）留出位置。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<SliderMark>? Marks { get; set { field = value; Relayout(); } }

    /// <summary>读屏念的取值文字（TS 的 aria-valuetext），如 <c>v =&gt; $"{v}%"</c>；不写时念数字。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<double, string>? ValueText { get; set; }

    bool ShouldSerializeOrientation() => Orientation != SliderOrientation.Horizontal;

    bool ShouldSerializeTextSize() => TextSize.Value is not null;

    bool ShouldSerializeTone() => Tone.Value is not null;

    /// <summary>单值变了（拖动中也会）。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>范围变了（拖动中也会）。</summary>
    public event EventHandler? RangeChanged;

    /// <summary>一次操作结束（松开指针、按键）时触发。</summary>
    public event EventHandler? ValueCommitted;

    internal bool IsRange => Range is not null;

    internal bool ReadOnly => IsReadOnly ?? FieldControl.Of(this)?.IsReadOnly ?? false;

    internal bool Vertical => Orientation.Value == "vertical";

    /// <summary>键盘所在的滑块（测试用）。</summary>
    internal int FocusIndex => focusIndex;

    /// <summary>各滑块的值（单值时一个）。</summary>
    internal double[] Values => Range is { } range ? [range.Low, range.High] : [Value];

    /// <summary>显示的值：父级给了超出范围的值时按范围内显示（不改父级的值）。</summary>
    internal double Shown(int index) => Math.Clamp(Values[index], Min, Math.Max(Min, Max));

    /// <summary>第 index 个滑块能取的范围。</summary>
    internal (double Lower, double Upper) BoundsOf(int index) => SliderScale.Bounds(index, Values, Min, Max);

    double BigStepValue => SliderScale.BigStep(BigStep, Step, Min, Max);

    bool Editable => Enabled && !ReadOnly;

    float Em => this.EmPixels();

    float Px(float pixels) => pixels * DeviceDpi / 96f;

    float ThumbSize => (Tokens.Space.Lg + Tokens.Space.Xs) * Em;

    float TrackThickness => Tokens.Space.Xs * 1.5f * Em;

    bool Labelled => Marks?.Any(mark => mark.Label is not null) == true;

    float Reserve => Labelled ? (Vertical ? 3 : 1.5f) * Em : 0;

    void IFieldControl.FieldChanged() => Invalidate();

    void IScopeAware.ScopeChanged() => Invalidate();

    void Changed()
    {
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }

    void Relayout()
    {
        if (AutoSize) Size = GetPreferredSize(Size);
        Invalidate();
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

    /// <summary>横向时宽度跟着给的（FlexControl 拉伸时给的是一行的宽），没给时 10em；纵向默认 10em 高。高（纵向时宽）至少 minTarget。</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var cross = (int)Math.Ceiling(Math.Max(Px(Tokens.Size.MinTarget), ThumbSize) + Reserve);
        var length = (int)Math.Ceiling(10 * Em);
        return Vertical
            ? new Size(cross, proposedSize.Height is > 1 and < int.MaxValue ? proposedSize.Height : length)
            : new Size(proposedSize.Width is > 1 and < int.MaxValue ? proposedSize.Width : Math.Max(Width, length), cross);
    }

    /// <summary>轨道的矩形（滑块中心能到的范围）。</summary>
    RectangleF TrackRect()
    {
        var half = ThumbSize / 2;
        var thickness = TrackThickness;
        var box = Vertical ? new RectangleF(0, 0, Math.Max(0, Width - Reserve), Height) : new RectangleF(0, 0, Width, Math.Max(0, Height - Reserve));
        return Vertical
            ? new RectangleF(box.X + (box.Width - thickness) / 2, box.Y + half, thickness, Math.Max(0, box.Height - 2 * half))
            : new RectangleF(box.X + half, box.Y + (box.Height - thickness) / 2, Math.Max(0, box.Width - 2 * half), thickness);
    }

    bool RightToLeftLayout => RightToLeft == RightToLeft.Yes && !Vertical;

    /// <summary>值在轨道上的中心点；从右到左排版时横向的起点在右边。</summary>
    internal PointF PointOf(double value)
    {
        var track = TrackRect();
        var ratio = (float)SliderScale.Ratio(value, Min, Max);
        if (RightToLeftLayout) ratio = 1 - ratio;
        return Vertical ? new PointF(track.X + track.Width / 2, track.Bottom - ratio * track.Height) : new PointF(track.X + ratio * track.Width, track.Y + track.Height / 2);
    }

    double ValueAt(Point point)
    {
        var track = TrackRect();
        var ratio = Vertical ? (track.Bottom - point.Y) / Math.Max(1, track.Height) : (point.X - track.X) / Math.Max(1, track.Width);
        if (RightToLeftLayout) ratio = 1 - ratio;
        return SliderScale.ValueAt(ratio, Min, Max, Step);
    }

    /// <summary>改第 index 个滑块的值（对齐步长、不越过另一个）。</summary>
    internal void Set(int index, double raw)
    {
        var next = SliderScale.Set(Values, index, raw, Min, Max, Step);
        if (IsRange) Range = new SliderRange(next[0], next[1]);
        else Value = next[0];
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    /// <summary>范围时 Tab 先在两个滑块之间走（同网页里两个可聚焦的滑块），到头了再离开控件。</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (IsRange && Focused && (keyData & ~Keys.Shift) == Keys.Tab)
        {
            var next = (keyData & Keys.Shift) == Keys.Shift ? focusIndex - 1 : focusIndex + 1;
            if (next is 0 or 1)
            {
                focusIndex = next;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.Focus, next);
                return true;
            }
        }
        return base.ProcessDialogKey(keyData);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var rtl = RightToLeftLayout;
        SliderKey? action = e.KeyCode switch
        {
            Keys.Right => rtl ? SliderKey.Decrease : SliderKey.Increase,
            Keys.Left => rtl ? SliderKey.Increase : SliderKey.Decrease,
            Keys.Up => SliderKey.Increase,
            Keys.Down => SliderKey.Decrease,
            Keys.PageUp => SliderKey.PageUp,
            Keys.PageDown => SliderKey.PageDown,
            Keys.Home => SliderKey.Home,
            Keys.End => SliderKey.End,
            _ => null,
        };
        if (action is not { } pressed) return;
        e.Handled = true;
        if (!Editable) return;
        var (lower, upper) = BoundsOf(focusIndex);
        Set(focusIndex, SliderScale.Target(pressed, Values[focusIndex], Step, BigStepValue, lower, upper));
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>按下（点轨道）：移动离得近的那个滑块到这里、键盘跟到它，之后的移动拖着它。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Editable) return;
        Focus();
        var target = ValueAt(e.Location);
        dragging = focusIndex = SliderScale.Nearest(target, Values);
        Set(dragging, target);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging >= 0) Set(dragging, ValueAt(e.Location));
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (dragging < 0) return;
        dragging = -1;
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <summary>画轨道、已选范围、刻度点与文字、滑块；键盘聚焦时聚焦的滑块外面一圈焦点环。不可用时整体半透明。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var track = TrackRect();
        if (track.Width <= 0 || track.Height <= 0) return;
        var colors = ProviderControl.ThemeOf(this).Colors;
        var alpha = Enabled ? 255 : (int)(255 * Tokens.Opacity.Disabled);
        Color Dim(Color color) => Color.FromArgb(color.A * alpha / 255, color);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var (fill, onFill) = ToneFill.Colors(Tone, colors);
        var round = Math.Min(track.Width, track.Height) / 2;
        using (var trackPath = Drawing.RoundedRectangle(track, round))
        {
            using (var brush = new SolidBrush(Dim(colors.Fill))) graphics.FillPath(brush, trackPath);
            var values = Enumerable.Range(0, Values.Length).Select(Shown).ToArray();
            var (from, to) = values.Length > 1 ? (values[0], values[1]) : (Min, values[0]);
            var (start, end) = (PointOf(from), PointOf(to));
            var selected = Vertical
                ? RectangleF.FromLTRB(track.Left, Math.Min(start.Y, end.Y), track.Right, Math.Max(start.Y, end.Y))
                : RectangleF.FromLTRB(Math.Min(start.X, end.X), track.Top, Math.Max(start.X, end.X), track.Bottom);
            var state = graphics.Save();
            graphics.SetClip(trackPath);
            using (var brush = new SolidBrush(Dim(fill))) graphics.FillRectangle(brush, selected);
            graphics.Restore(state);
            var dot = Tokens.Space.Xs * Em;
            foreach (var mark in Marks ?? [])
            {
                // 在已选范围里的点换成实底上的颜色
                var center = PointOf(mark.Value);
                using var brush = new SolidBrush(Dim(mark.Value >= from && mark.Value <= to ? onFill : colors.BorderStrong));
                graphics.FillEllipse(brush, center.X - dot / 2, center.Y - dot / 2, dot, dot);
            }
        }
        PaintLabels(graphics, track, Dim(colors.TextMuted));
        var size = ThumbSize;
        var border = Px(Tokens.BorderWidth.Sm);
        for (var index = 0; index < Values.Length; index++)
        {
            var center = PointOf(Shown(index));
            var circle = new RectangleF(center.X - size / 2, center.Y - size / 2, size, size);
            // sm 阴影：下方一圈淡淡的投影
            using (var shadow = new SolidBrush(Color.FromArgb(alpha / 10, Color.Black))) graphics.FillEllipse(shadow, RectangleF.Inflate(circle with { Y = circle.Y + Px(1.5f) }, Px(1), Px(1)));
            // 底色不透明：网页是整体半透明，滑块照样盖住轨道
            using (var surface = new SolidBrush(colors.Surface)) graphics.FillEllipse(surface, circle);
            using (var pen = new Pen(Dim(fill), border)) graphics.DrawEllipse(pen, RectangleF.Inflate(circle, -border / 2, -border / 2));
            if (Focused && ShowFocusCues && index == focusIndex)
            {
                using var ring = new Pen(colors.FocusRing, Px(2));
                graphics.DrawEllipse(ring, RectangleF.Inflate(circle, Px(2), Px(2)));
            }
        }
    }

    /// <summary>刻度文字：sm、textMuted，在轨道下方（纵向时右侧），横向时中心对准刻度；两端的不伸出控件（子控件画不到自己外面）。</summary>
    void PaintLabels(Graphics graphics, RectangleF track, Color color)
    {
        if (!Labelled) return;
        using var font = Fonts.Get(Font.FamilyName(), Tokens.FontSize.Sm * 72 / 96 * (Font.SizeInPoints * 96 / 72 / Tokens.FontSize.Md), Font.Style);
        foreach (var mark in Marks!)
        {
            if (mark.Label is not { } text) continue;
            var size = TextRenderer.MeasureText(graphics, text, font, Size.Empty, TextFormatFlags.NoPadding);
            var center = PointOf(mark.Value);
            var origin = Vertical
                ? new Point((int)(track.X + track.Width / 2 + Tokens.Space.Md * Em), (int)(center.Y - size.Height / 2f))
                : new Point((int)Math.Clamp(center.X - size.Width / 2f, 0, Math.Max(0, Width - size.Width)), (int)(track.Y + track.Height / 2 + Tokens.Space.Sm * Em));
            TextRenderer.DrawText(graphics, text, font, origin, color, TextFormatFlags.NoPadding);
        }
    }

    /// <summary>读屏念的值：取值文字，没有时是数字。</summary>
    internal string ValueTextOf(int index) => ValueText?.Invoke(Shown(index)) ?? Shown(index).ToString(CultureInfo.CurrentCulture);

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibleObject(this);

    /// <summary>名称：自己的名称或字段标签。</summary>
    internal string? OwnName => AccessibleName is { Length: > 0 } name ? name : FieldControl.Of(this) is { } scope && scope.Owns(this) ? scope.Label : null;

    /// <summary>读屏：单值时控件自己是滑块（值是取值文字）；范围时是一组，子节点是两个滑块。说明与错误是描述。</summary>
    sealed class SliderAccessibleObject(SliderControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => owner.IsRange ? AccessibleRole.Grouping : AccessibleRole.Slider;

        public override string? Name => owner.OwnName;

        public override string? Description => FieldControl.DescribedBy(owner, owner.AccessibleDescription);

        public override string? Value
        {
            get => owner.IsRange ? null : owner.ValueTextOf(0);
            set
            {
                if (!owner.IsRange && owner.Editable && double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)) owner.Set(0, number);
            }
        }

        public override AccessibleStates State => base.State | (owner.ReadOnly ? AccessibleStates.ReadOnly : AccessibleStates.None);

        public override int GetChildCount() => owner.IsRange ? 2 : 0;

        public override AccessibleObject? GetChild(int index) => owner.IsRange && index is 0 or 1 ? new ThumbAccessibleObject(owner, this, index) : null;

        public override AccessibleObject? GetFocused() => owner.IsRange && owner.Focused ? GetChild(owner.focusIndex) : base.GetFocused();
    }

    /// <summary>范围里的一个滑块：名称后面加"最小值""最大值"，值是取值文字。</summary>
    sealed class ThumbAccessibleObject(SliderControl owner, AccessibleObject parent, int index) : AccessibleObject
    {
        public override AccessibleRole Role => AccessibleRole.Slider;

        public override string Name
        {
            get
            {
                var lang = ProviderControl.LangOf(owner);
                var part = index == 0 ? lang.SliderMin : lang.SliderMax;
                return owner.OwnName is { Length: > 0 } own ? $"{own} {part}" : part;
            }
        }

        public override string? Value
        {
            get => owner.ValueTextOf(index);
            set
            {
                if (owner.Editable && double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)) owner.Set(index, number);
            }
        }

        public override AccessibleObject Parent => parent;

        public override AccessibleStates State =>
            AccessibleStates.Focusable
            | (owner.Focused && owner.focusIndex == index ? AccessibleStates.Focused : AccessibleStates.None)
            | (owner.ReadOnly ? AccessibleStates.ReadOnly : AccessibleStates.None)
            | (owner.Enabled ? AccessibleStates.None : AccessibleStates.Unavailable);

        public override Rectangle Bounds
        {
            get
            {
                var center = owner.PointOf(owner.Shown(index));
                var size = owner.ThumbSize;
                return owner.RectangleToScreen(Rectangle.Round(new RectangleF(center.X - size / 2, center.Y - size / 2, size, size)));
            }
        }
    }
}
