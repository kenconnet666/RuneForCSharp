using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 一组日期分段（vue-tsx-pro 的 DateSegmentGroup）：年 / 月 / 日、时 : 分，状态由调用方的 <see cref="DateSegments"/> 给；
/// DateField、TimeField 放在输入框外壳里用，日期区间在一个外壳里放两组。
/// WinForms 里是一个自己画的控件（同 OtpFieldControl）：焦点在控件上时 DateSegments.FocusedSegment 是"聚焦的那一段"，
/// Tab / Shift+Tab 先在各段之间走（同上游每段都在 Tab 顺序里），走到两端才离开控件；←→ 也能换段，输入满了自动跳到下一段。
/// 打的字交给 DateSegments.Type，↑↓、Page Up / Down、Home / End、Backspace 交给 HandleKey；Enter 写回打了一半的年份，再提交所在的 FormControl。
/// 读屏：这一组是 Grouping，每段是子对象 SpinButton（名称"组名 段名"，没填的念"未填写"）。
/// 外观：上下 xs、数字等宽；每段左右 0.1em、xs 圆角，聚焦时 accent 底、accentContrast 字；没填的段与分隔符是 textMuted。
/// </summary>
[ToolboxItem(false)]
public class DateSegmentGroupControl : Control, IBaseline
{
    /// <summary>新建：能聚焦、自己画。</summary>
    public DateSegmentGroupControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        TabStop = true;
        Cursor = Cursors.IBeam;
        AccessibleRole = AccessibleRole.Grouping;
    }

    /// <summary>分段状态。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateSegments? Segments { get; set { field = value; Refresh(); } }

    /// <summary>只读：能聚焦、能换段，不能改。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsReadOnly { get; set { field = value; Refresh(); } }

    /// <summary>必填（读屏）。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsRequired { get; set { field = value; Refresh(); } }

    /// <summary>校验未通过。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsInvalid { get; set { field = value; Refresh(); } }

    /// <summary>显示的文字（测试用）：各段与分隔符连起来。</summary>
    internal string SegmentText => Segments is null ? "" : string.Concat(Segments.Segments.Select(segment => segment.Text));

    float Em => this.EmPixels();

    /// <summary>各段文字的基线：上边留的 xs 加字体的上伸部（Field 的标签按它对齐）。</summary>
    int? IBaseline.Baseline
    {
        get
        {
            var family = Font.FontFamily;
            return (int)MathF.Round(Tokens.Space.Xs * Em) + (int)MathF.Round(Em * family.GetCellAscent(Font.Style) / family.GetEmHeight(Font.Style));
        }
    }

    /// <summary>值或状态变了：重排、重画。外面改了值（调过 DateSegments.Sync）后调它。</summary>
    public override void Refresh()
    {
        if (Parent is not null) Parent.PerformLayout();
        Size = GetPreferredSize(Size.Empty);
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }

    /// <summary>各段的位置（相对控件）。</summary>
    List<(DateSegment Segment, Rectangle Bounds)> Arrange()
    {
        var result = new List<(DateSegment, Rectangle)>();
        if (Segments is null) return result;
        var pad = (int)MathF.Round(0.1f * Em);
        var top = (int)MathF.Round(Tokens.Space.Xs * Em);
        var height = Font.Height;
        var x = 0;
        foreach (var segment in Segments.Segments)
        {
            var width = TextRenderer.MeasureText(segment.Text.Length > 0 ? segment.Text : " ", Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            var box = segment.IsLiteral ? width : width + 2 * pad;
            result.Add((segment, new Rectangle(x, top, box, height)));
            x += box;
        }
        return result;
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var layout = Arrange();
        var top = (int)MathF.Round(Tokens.Space.Xs * Em);
        return new Size(layout.Count == 0 ? 0 : layout[^1].Bounds.Right, Font.Height + 2 * top);
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Segments is not { } state) return;
        var theme = ProviderControl.ThemeOf(this);
        var colors = theme.Colors;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var (segment, bounds) in Arrange())
        {
            var focused = Focused && segment.Type is { } type && state.FocusedSegment == type;
            if (focused)
            {
                using var path = Drawing.RoundedRectangle(bounds, theme.Radii.Xs * Em);
                using var fill = new SolidBrush(colors.Accent);
                e.Graphics.FillPath(fill, path);
            }
            var color = focused ? colors.AccentContrast : segment.IsLiteral || segment.IsPlaceholder ? colors.TextMuted : ForeColor;
            if (!Enabled) color = Color.FromArgb((int)(255 * Tokens.Opacity.Disabled), color);
            TextRenderer.DrawText(e.Graphics, segment.Text, Font, bounds, color, TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>字号变了（外壳的 TextSize、窗体的字体）：重新量各段。</summary>
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Refresh();
    }

    /// <summary>Tab 进来时聚焦第一段，Shift+Tab 进来时聚焦最后一段。</summary>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (Segments is { FocusedSegment: null } state && state.Editable is { Count: > 0 } editable)
            state.SetFocused((ModifierKeys & Keys.Shift) != 0 ? editable[^1].Type : editable[0].Type);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Segments?.SetFocused(null);
        Refresh();
    }

    /// <summary>点哪一段聚焦哪一段。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (Segments is not { } state || !Enabled) return;
        var hit = Arrange().Where(entry => !entry.Segment.IsLiteral).OrderBy(entry => Math.Abs(entry.Bounds.Left + entry.Bounds.Width / 2 - e.X)).FirstOrDefault();
        if (hit.Segment?.Type is { } type) state.SetFocused(type);
        Focus();
        Refresh();
    }

    /// <summary>Tab / Shift+Tab 先在各段之间走，到两端才交给窗体换控件。</summary>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (Segments is { } state && keyData is Keys.Tab or (Keys.Tab | Keys.Shift))
        {
            var direction = keyData == Keys.Tab ? 1 : -1;
            var editable = state.Editable;
            var index = editable.ToList().FindIndex(segment => segment.Type == state.FocusedSegment);
            if (index + direction >= 0 && index + direction < editable.Count)
            {
                state.SetFocused(editable[index + direction].Type);
                Refresh();
                return true;
            }
        }
        return base.ProcessDialogKey(keyData);
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (char.IsControl(e.KeyChar)) return;
        e.Handled = true;
        TypeText(e.KeyChar.ToString());
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Press(e.KeyData)) e.Handled = e.SuppressKeyPress = true;
    }

    /// <summary>在聚焦的那一段输入文字（数字、a / p）。</summary>
    /// <param name="text">文字。</param>
    internal void TypeText(string text)
    {
        if (Segments is not { } state || !Enabled || IsReadOnly) return;
        state.Type(text);
        Refresh();
    }

    /// <summary>按键：Enter 写回并提交所在的表单；只读时只能换段。返回是否处理了。</summary>
    /// <param name="keys">按键（含修饰键）。</param>
    internal bool Press(Keys keys)
    {
        if (Segments is not { } state || !Enabled || (keys & (Keys.Control | Keys.Alt)) != 0) return false;
        if ((keys & Keys.KeyCode) == Keys.Enter)
        {
            // 同输入框：写回打了一半的年份，再提交所在的表单（查询栏、对话框里的表单）
            state.Commit();
            Refresh();
            if (FormControl.Of(this) is { } form) _ = form.SubmitAsync();
            return true;
        }
        DateSegmentKey? key = (keys & Keys.KeyCode) switch
        {
            Keys.Up => DateSegmentKey.Up,
            Keys.Down => DateSegmentKey.Down,
            Keys.PageUp => DateSegmentKey.PageUp,
            Keys.PageDown => DateSegmentKey.PageDown,
            Keys.Home => DateSegmentKey.Home,
            Keys.End => DateSegmentKey.End,
            Keys.Back or Keys.Delete => DateSegmentKey.Backspace,
            Keys.Right => DateSegmentKey.Right,
            Keys.Left => DateSegmentKey.Left,
            _ => null,
        };
        if (key is not { } pressed || IsReadOnly && pressed is not (DateSegmentKey.Left or DateSegmentKey.Right)) return false;
        if (!state.HandleKey(pressed, RightToLeft == RightToLeft.Yes)) return false;
        Refresh();
        return true;
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new GroupAccessibleObject(this);

    sealed class GroupAccessibleObject(DateSegmentGroupControl owner) : ControlAccessibleObject(owner)
    {
        IReadOnlyList<DateSegment> Editable => owner.Segments?.Editable ?? [];

        public override int GetChildCount() => Editable.Count;

        public override AccessibleObject? GetChild(int index) => index >= 0 && index < Editable.Count ? new SegmentAccessibleObject(owner, this, Editable[index]) : null;

        public override AccessibleObject? GetFocused() =>
            owner.Focused && Editable.FirstOrDefault(segment => segment.Type == owner.Segments?.FocusedSegment) is { } focused ? new SegmentAccessibleObject(owner, this, focused) : base.GetFocused();
    }

    sealed class SegmentAccessibleObject(DateSegmentGroupControl owner, AccessibleObject parent, DateSegment segment) : AccessibleObject
    {
        public override AccessibleObject Parent => parent;

        public override AccessibleRole Role => AccessibleRole.SpinButton;

        public override string Name => owner.AccessibleName is { Length: > 0 } name ? $"{name} {segment.Label}" : segment.Label;

        public override string Value => segment.IsPlaceholder ? ProviderControl.LangOf(owner).DateFieldEmpty : segment.Text;

        public override AccessibleStates State =>
            AccessibleStates.Focusable | (owner.Focused && owner.Segments?.FocusedSegment == segment.Type ? AccessibleStates.Focused : 0) | (owner.IsReadOnly ? AccessibleStates.ReadOnly : 0);

        public override Rectangle Bounds =>
            owner.Arrange().FirstOrDefault(entry => entry.Segment.Type == segment.Type) is { Segment: not null } hit ? owner.RectangleToScreen(hit.Bounds) : Rectangle.Empty;
    }
}
