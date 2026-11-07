using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Rune.WinForms;

/// <summary>
/// 表单标签（vue-tsx-pro 的 Label；WinForms 自带 Label，这里叫 FieldLabel）：md 字号、中等字重、行高按字体、正文色；
/// 必填时文字后有一个星号（0.7em、dangerText，贴着第一行的顶边）；不可用时整体 opacities.disabled。
/// 无窗口元素，放进窗体用 <see cref="FieldLabelControl"/>；<see cref="FieldControl"/> 里自动关联。
/// </summary>
public class FieldLabel : Element
{
    readonly Text text = new() { Size = TextSize.Md, Weight = TextWeight.Medium };
    readonly Icon required = new() { Data = Lucide.Asterisk };

    /// <summary>文字。</summary>
    public string? Content { get => text.Content; set { text.Content = value ?? ""; InvalidateMeasure(); } }

    /// <summary>必填：文字后加星号。</summary>
    public bool IsRequired { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>可用（宿主的 Enabled）。</summary>
    public bool IsEnabled { get; internal set { if (field == value) return; field = value; Invalidate(); } } = true;

    /// <summary>文字部件（测试用）。</summary>
    internal Text TextPart => text;

    /// <summary>星号的边长与间距（设备像素）：0.7em、space.xs。</summary>
    (float Side, float Gap) Mark(ElementContext inner) => (0.7f * inner.Em, Tokens.Space.Xs * inner.Em);

    ElementContext Inner(ElementContext context) => context.Sized(TextSize.Md);

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var inner = Inner(context);
        var (side, gap) = Mark(inner);
        var extra = IsRequired ? (int)MathF.Ceiling(side + gap) : 0;
        var size = text.Measure(inner, new Size(available.Width == int.MaxValue ? int.MaxValue : Math.Max(0, available.Width - extra), available.Height));
        return new Size(size.Width + extra, Math.Max(size.Height, IsRequired ? (int)MathF.Ceiling(side) : 0));
    }

    /// <inheritdoc />
    protected internal override int? Baseline(ElementContext context) => text.Baseline(Inner(context));

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var inner = Inner(context);
        var colors = context.Theme.Colors;
        var back = IsEnabled ? default : BackColor(colors);
        Color Dim(Color color) => IsEnabled ? color : Drawing.Mix(back, color, Tokens.Opacity.Disabled);
        var (side, gap) = Mark(inner);
        var size = text.Measure(inner, new Size(Math.Max(0, Bounds.Width - (IsRequired ? (int)MathF.Ceiling(side + gap) : 0)), int.MaxValue));
        var x = context.RightToLeft ? Bounds.Right - size.Width : Bounds.X;
        text.Bounds = new Rectangle(x, Bounds.Y, size.Width, size.Height);
        text.Paint(graphics, inner with { ForeColor = Dim(colors.Text) });
        if (!IsRequired) return;
        required.Size = side * 96 / inner.Dpi;
        var mark = required.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        var markX = context.RightToLeft ? x - gap - mark.Width : x + size.Width + gap;
        required.Bounds = new Rectangle((int)MathF.Round(markX), Bounds.Y, mark.Width, mark.Height);
        required.Paint(graphics, inner with { ForeColor = Dim(colors.DangerText) });
    }

    /// <summary>宿主往上第一个不透明的背景色（不可用时把颜色混进它，等于整体半透明）。</summary>
    Color BackColor(ThemeColors colors)
    {
        for (var control = Host as Control; control is not null; control = control.Parent)
            if (control.BackColor.A == 255) return control.BackColor;
        return colors.Background;
    }
}

/// <summary>
/// 表单标签控件（vue-tsx-pro 的 Label）：承载 <see cref="FieldLabel"/> 元素。Text 是文字；点它聚焦 <see cref="Target"/>
/// （是一组时聚焦组里的 Tab 停靠项，同点原生输入框的标签）。读屏是静态文字。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Text))]
[Description("表单标签：必填时文字后有星号，点它聚焦关联的控件。")]
public class FieldLabelControl : ElementControl<FieldLabel>
{
    /// <summary>新建：不进 Tab 顺序。</summary>
    public FieldLabelControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    /// <summary>文字。</summary>
    [AllowNull]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [Category("Appearance")]
    [Description("标签的文字。")]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <summary>必填。</summary>
    [Category("Appearance")]
    [Description("必填：文字后加星号（读屏的必填由控件告诉）。")]
    [DefaultValue(false)]
    public bool IsRequired { get => Element.IsRequired; set => Element.IsRequired = value; }

    /// <summary>点标签时聚焦的控件。</summary>
    [Category("Behavior")]
    [Description("点标签时聚焦的控件。")]
    [DefaultValue(null)]
    public Control? Target { get; set; }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Element.Content = Text;
    }

    /// <inheritdoc />
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Element.IsEnabled = Enabled;
    }

    /// <summary>点标签：聚焦目标。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && Target is { } target) FieldControl.FocusControl(target);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new FieldLabelAccessibleObject(this);

    sealed class FieldLabelAccessibleObject(FieldLabelControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole : AccessibleRole.StaticText;

        public override string? Name
        {
            get => base.Name is { Length: > 0 } name ? name : owner.Text;
            set => base.Name = value;
        }
    }
}
