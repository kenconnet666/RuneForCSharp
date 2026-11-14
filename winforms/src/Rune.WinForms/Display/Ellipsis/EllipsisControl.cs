using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 截断的文字（vue-tsx-pro 的 Ellipsis）：超出一行（或 <see cref="Lines"/> 行）时省略，真的被截断了才给出看全文的办法——悬停或聚焦时的提示，或展开按钮。
/// 表格里的长名称、卡片里的描述、列表里的备注。
/// <code>
/// var name = new EllipsisControl { Text = "杭州远山精密机械有限公司（萧山二厂）" };          // 一行，截断时悬停看全文
/// var note = new EllipsisControl { Text = description, Lines = 3, Expandable = true };   // 三行，截断时后面有"展开"
/// </code>
/// 是否截断按文字的自然大小与排到的大小比（宽度、内容变了重新量），没截断时就是普通文字：不能聚焦、没有提示、没有按钮。
/// 截断时文字能用 Tab 聚焦（聚焦也显示提示，提示也是读屏的描述），提示默认是文字本身，<see cref="Tooltip"/> 换成别的，写 null 不提示；
/// <see cref="Expandable"/> 时默认不提示，截断时后面是"展开"（accentText，悬停下划线），展开后显示全部、按钮换成"收起"（<see cref="ExpandedChanged"/> 报告）。
/// 只要省略、不需要看全文时用 TextControl 的 Truncate / LineClamp。
/// </summary>
[DefaultEvent(nameof(ExpandedChanged))]
public class EllipsisControl : FlexControl
{
    readonly EllipsisText text = new();
    readonly ToggleLink toggle = new() { Underline = LinkUnderline.Hover, Visible = false };
    readonly TooltipProvider tooltips = new();
    bool tooltipSet;
    bool truncated;

    /// <summary>新建：一行，截断时悬停看全文。</summary>
    public EllipsisControl()
    {
        Align = FlexAlign.Baseline;
        Gap = Space.Xs;
        AutoSize = true;
        // 文字缩到放得下为止（TS 的 min-width: 0），按钮不缩
        SetGrow(text, true);
        Controls.AddRange([text, toggle]);
        toggle.Click += (_, _) => IsExpanded = !IsExpanded;
        text.Layout += (_, _) => BeginCheck();
        Sync();
    }

    /// <summary>文字。</summary>
    [AllowNull]
    public override string Text { get => text.Text; set { text.Text = value; Sync(); } }

    /// <summary>最多几行，默认 1。</summary>
    [Category("Behavior")]
    [DefaultValue(1)]
    public int Lines
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
            Sync();
        }
    } = 1;

    /// <summary>截断时后面放"展开"按钮，展开后显示全部。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool Expandable { get; set { field = value; Sync(); } }

    /// <summary>展开了。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsExpanded
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>截断时悬停、聚焦显示的提示；不写时是文字本身（Expandable 时不提示），写 null 不提示。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public string? Tooltip
    {
        get;
        set
        {
            field = value;
            tooltipSet = true;
            Sync();
        }
    }

    /// <summary>真的被截断了。</summary>
    [Browsable(false)]
    public bool IsTruncated => truncated;

    /// <summary>展开、收起了。</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>截断的那段文字（测试用）。</summary>
    internal TextControl TextPart => text;

    /// <summary>展开按钮（测试用）。</summary>
    internal LinkControl Toggle => toggle;

    /// <summary>截断时的提示（测试用）。</summary>
    internal string? ShownTooltip => tooltips.GetTooltip(text);

    /// <summary>截断的方式、提示、能否聚焦与展开按钮，按现在量到的截断与否。</summary>
    void Sync()
    {
        var clamped = !IsExpanded;
        text.Truncate = clamped && Lines == 1;
        text.LineClamp = clamped && Lines > 1 ? Lines : null;
        text.Wrap = Lines > 1 || !clamped ? TextWrap.Wrap : null;
        // 多行时按钮贴着最后一行（TS 的 align-items: flex-end）
        Align = Lines > 1 ? FlexAlign.End : FlexAlign.Baseline;
        var tip = !truncated || !clamped ? null : tooltipSet ? Tooltip : Expandable ? null : text.Text;
        tooltips.SetTooltip(text, tip);
        // 截断时能聚焦：键盘用户也能看到提示里的全文
        text.Focusable = tip is not null;
        toggle.Visible = Expandable && (truncated || IsExpanded);
        var lang = ProviderControl.LangOf(this);
        toggle.Text = IsExpanded ? lang.EllipsisCollapse : lang.EllipsisExpand;
        toggle.Expanded = IsExpanded;
    }

    /// <summary>排完再量（同 TS 的 ResizeObserver 加每次渲染后）；展开时不量（展开了当然放得下）。</summary>
    void BeginCheck()
    {
        if (!IsHandleCreated || IsExpanded) return;
        BeginInvoke(() =>
        {
            if (IsDisposed || IsExpanded) return;
            var over = text.Overflows;
            if (over == truncated) return;
            truncated = over;
            Sync();
        });
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        // 按钮的文字取所在位置的语言包
        Sync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) tooltips.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>截断的那段文字：截断、有提示时能聚焦，键盘聚焦时外面一圈焦点环。</summary>
    sealed class EllipsisText : TextControl
    {
        /// <summary>能用 Tab 聚焦。</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal bool Focusable
        {
            get => TabStop;
            set
            {
                SetStyle(ControlStyles.Selectable, value);
                TabStop = value;
            }
        }

        /// <summary>省略了内容。</summary>
        internal bool Overflows => Element.Overflows(ElementContext.Of(this));

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        /// <summary>键盘聚焦时画焦点环（focusRing、2 像素、xs 圆角，同 :focus-visible）。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!Focused || !ShowFocusCues) return;
            var context = ElementContext.Of(this);
            var width = context.Scale(2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(context.Theme.Colors.FocusRing, width);
            using var path = Drawing.RoundedRectangle(new RectangleF(width / 2, width / 2, Width - width, Height - width), (Radius.Xs.In(context.Theme.Radii) ?? 0) * context.Em, default);
            e.Graphics.DrawPath(pen, path);
        }
    }

    /// <summary>展开按钮：长得像链接，读屏里是带展开状态的按钮（TS 的 button aria-expanded），空格、Enter 都能按。</summary>
    sealed class ToggleLink : LinkControl
    {
        /// <summary>展开了。</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal bool Expanded { get; set; }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || e.KeyCode != Keys.Space) return;
            PerformClick();
            e.Handled = true;
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new ToggleAccessibleObject(this);

        sealed class ToggleAccessibleObject(ToggleLink owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleRole Role => AccessibleRole.PushButton;

            public override string? Name => owner.Text;

            public override AccessibleStates State => base.State | (owner.Expanded ? AccessibleStates.Expanded : AccessibleStates.Collapsed);

            public override string DefaultAction => owner.Expanded ? "collapse" : "expand";

            public override void DoDefaultAction() => owner.PerformClick();
        }
    }
}
