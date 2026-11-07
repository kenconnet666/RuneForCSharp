using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 复选框（vue-tsx-pro 的 Checkbox）：要提交才生效的勾选；立即生效的设置用 Switch。无窗口元素，放进窗体用 <see cref="CheckboxControl"/>。
/// 方框 1.125em、圆角 radii.sm、2px borderStrong 描边、表面底色；勾上或部分勾选时是色调的实底（<see cref="ToneFill"/>），
/// 里面是对勾或横线（0.875em、描线 3、实底上的颜色）；出错时描边是危险色。放在 <see cref="CheckboxGroupControl"/> 里时勾选由组的值决定，
/// 只读、出错随组。外层、文字与说明见 <see cref="ChoiceBase"/>；部件：<see cref="PropsIndicator"/>（<see cref="IconProps"/>）、文字、说明。
/// </summary>
public class Checkbox : ChoiceBase
{
    readonly Icon indicator = new() { StrokeWidth = 3 };

    /// <summary>只读：能聚焦、读屏念"只读"，勾选状态不能改；外观照常。</summary>
    public bool IsReadOnly { get => readOnly; set => readOnly = value; }

    /// <summary>必填（如"同意条款"）。</summary>
    public bool IsRequired { get => required; set => required = value; }

    /// <summary>校验未通过：方框的描边是危险色。</summary>
    public bool IsInvalid { get => invalid; set { invalid = value; Invalidate(); } }

    /// <summary>部分勾选（"全选"下面只选了一部分）：显示横线，读屏念"部分选中"。</summary>
    public bool IsIndeterminate { get; set { field = value; Invalidate(); } }

    /// <summary>在复选组里的值。</summary>
    public string? Value { get; set; }

    /// <summary>选中时的色调；不写时取作用域的默认值（<see cref="ChoiceDefaults"/>），再没有时 accent。</summary>
    public StatusTone Tone { get => ResolvedTone; set { tone = value; Invalidate(); } }

    /// <summary>字号档位：方框随之缩放；不写时取作用域的默认值，再没有时跟随宿主的字体。</summary>
    public TextSize Size { get => ResolvedSize; set { size = value; InvalidateMeasure(); } }

    /// <summary>文字下方的说明（读屏的帮助文字）。</summary>
    public string? Description { get => DescriptionText; set => DescriptionText = value; }

    /// <summary>说明部件的属性（TS 的 propsDescription）。</summary>
    public TextProps? PropsDescription { get => DescriptionProps; set => DescriptionProps = value; }

    /// <summary>对勾 / 横线部件的属性（TS 的 propsIndicator）。</summary>
    public IconProps? PropsIndicator { get; set { field = value; Invalidate(); } }

    /// <summary>外观覆盖（上游的 sx）：盖过作用域的 <see cref="CheckboxSx"/> 与配方。</summary>
    public CheckboxSx? Sx { get; set { field = value; Invalidate(); } }

    CheckboxSx? ResolvedSx => Scope?.ResolveStyles(Sx) ?? Sx;

    /// <summary>所在的复选组（宿主沿 Parent 链找到后设）。</summary>
    internal CheckboxGroupControl? Group { get; set { if (field == value) return; field = value; Invalidate(); } }

    /// <inheritdoc />
    internal override bool ReadOnly => base.ReadOnly || Group?.IsReadOnly == true;

    /// <inheritdoc />
    internal override bool Invalid => base.Invalid || Group?.IsInvalid == true;

    /// <summary>对勾 / 横线部件（测试用）。</summary>
    internal Icon IndicatorPart => indicator;

    /// <summary>在复选组里时勾选交给组。</summary>
    internal override void ApplyChecked(bool value)
    {
        if (Group is not null && Value is not null) Group.Toggle(Value);
        else base.ApplyChecked(value);
    }

    /// <inheritdoc />
    private protected override SizeF ControlSize(ElementContext inner) => new(1.125f * inner.Em, 1.125f * inner.Em);

    /// <summary>方框；勾上或部分勾选时里面画对勾或横线。</summary>
    private protected override void PaintControl(Graphics graphics, ElementContext context, ElementContext inner, RectangleF box, Func<Color, Color> dim)
    {
        var (fill, onFill) = ToneFill.Colors(Tone, context.Theme.Colors);
        var sx = ResolvedSx;
        var radius = (Radius.Sm.In(context.Theme.Radii) ?? 0) * inner.Em;
        var marked = IsChecked || IsIndeterminate;
        PaintInput(graphics, context, box, radius, marked, Invalid, sx?.FillColor ?? fill, sx?.BorderColor, dim);
        if (marked)
        {
            indicator.Data = IsIndeterminate ? Lucide.Minus : Lucide.Check;
            indicator.Size = 0.875f * inner.Em * 96 / inner.Dpi;
            PropsIndicator?.ApplyTo(indicator);
            var mark = indicator.Measure(inner, new Size(int.MaxValue, int.MaxValue));
            indicator.Bounds = new Rectangle((int)MathF.Round(box.X + (box.Width - mark.Width) / 2), (int)MathF.Round(box.Y + (box.Height - mark.Height) / 2), mark.Width, mark.Height);
            indicator.Paint(graphics, inner with { ForeColor = dim(sx?.IndicatorColor ?? onFill) });
        }
        if (IsFocusVisible) PaintFocusRing(graphics, context, box, radius);
    }
}

/// <summary>
/// 复选框控件（vue-tsx-pro 的 Checkbox）：承载 <see cref="Checkbox"/> 元素。
/// <code>
/// var agree = new CheckboxControl { Text = "我已阅读并同意", Tone = StatusTone.Success };
/// // 全选：部分勾选时 IsIndeterminate；Toggling 里拦下，自己改整组
/// all.Toggling += (_, e) => { e.Cancel = true; group.Value = allChecked ? [] : everything; };
/// </code>
/// 点击、空格与焦点见 <see cref="ChoiceControl{T}"/>。读屏是复选框，部分勾选时状态是"混合"，名称是 AccessibleName 或文字，说明是帮助文字。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(IsCheckedChanged))]
[DefaultProperty(nameof(IsChecked))]
[Description("复选框：要提交才生效的勾选，可以部分勾选；放进 CheckboxGroupControl 成组。")]
public class CheckboxControl : ChoiceControl<Checkbox>
{
    /// <summary>是否勾选；在复选组里时由组的值决定。</summary>
    [Bindable(true)]
    [Category("Appearance")]
    [Description("是否勾选；在复选组里时由组的值决定。")]
    [DefaultValue(false)]
    public bool IsChecked { get => Element.IsChecked; set => Element.IsChecked = value; }

    /// <summary>部分勾选。</summary>
    [Category("Appearance")]
    [Description("部分勾选（全选下面只选了一部分）：显示横线。")]
    [DefaultValue(false)]
    public bool IsIndeterminate { get => Element.IsIndeterminate; set { Element.IsIndeterminate = value; AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); } }

    /// <summary>在复选组里的值。</summary>
    [Category("Data")]
    [Description("在复选组里的值。")]
    [DefaultValue(null)]
    public string? Value { get => Element.Value; set { Element.Value = value; Element.Group?.Sync(); } }

    /// <summary>只读。</summary>
    [Category("Behavior")]
    [Description("只读：能聚焦，勾选状态不能改；外观照常。")]
    [DefaultValue(false)]
    public bool IsReadOnly { get => Element.IsReadOnly; set { Element.IsReadOnly = value; AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); } }

    /// <summary>必填。</summary>
    [Category("Behavior")]
    [Description("必填（如同意条款）。")]
    [DefaultValue(false)]
    public bool IsRequired { get => Element.IsRequired; set => Element.IsRequired = value; }

    /// <summary>校验未通过。</summary>
    [Category("Appearance")]
    [Description("校验未通过：方框的描边是危险色。")]
    [DefaultValue(false)]
    public bool IsInvalid { get => Element.IsInvalid; set => Element.IsInvalid = value; }

    /// <summary>选中时的色调。</summary>
    [Category("Appearance")]
    [Description("勾上时的色调：neutral、accent（默认）、info、success、warning、danger。")]
    public StatusTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>字号档位。</summary>
    [Category("Appearance")]
    [Description("字号：xs–xl 或像素数；方框随之缩放，空着时跟随字体。")]
    public TextSize TextSize { get => Element.Size; set => Element.Size = value; }

    /// <summary>文字下方的说明。</summary>
    [Category("Appearance")]
    [Description("文字下方的说明（读屏的帮助文字）。")]
    [DefaultValue(null)]
    public string? Description { get => Element.Description; set => Element.Description = value; }

    /// <summary>说明部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsDescription { get => Element.PropsDescription; set => Element.PropsDescription = value; }

    /// <summary>对勾 / 横线部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsIndicator { get => Element.PropsIndicator; set => Element.PropsIndicator = value; }

    /// <summary>外观覆盖（代码里设，上游的 sx）：盖过作用域的 <see cref="CheckboxSx"/> 与配方。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CheckboxSx? Sx { get => Element.Sx; set => Element.Sx = value; }

    bool ShouldSerializeTone() => Element.Explicit.Tone;

    void ResetTone() => Tone = default;

    bool ShouldSerializeTextSize() => Element.Explicit.Size;

    void ResetTextSize() => TextSize = default;

    /// <summary>换了位置：沿 Parent 链重新找所在的复选组。</summary>
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        var old = Element.Group;
        Element.Group = ChoiceGroupControl.Of<CheckboxGroupControl>(this);
        if (old != Element.Group) old?.Sync();
        Element.Group?.Sync();
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new CheckboxAccessibleObject(this);

    /// <summary>读屏：复选框；部分勾选时是"混合"；说明是帮助文字。</summary>
    sealed class CheckboxAccessibleObject(CheckboxControl owner) : ChoiceAccessibleObject(owner)
    {
        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole : AccessibleRole.CheckButton;

        public override AccessibleStates State => owner.IsIndeterminate ? (base.State & ~AccessibleStates.Checked) | AccessibleStates.Mixed : base.State;

        public override string? Help => base.Help ?? owner.Description;

        public override string DefaultAction => owner.IsChecked ? "取消勾选" : "勾选";
    }
}

/// <summary>Checkbox 的外观覆盖（上游的 <c>sx</c> 与 <c>components.Checkbox.styles</c>）：作用域的在下，实例的 Sx 在上，盖过色调。</summary>
public sealed record CheckboxSx : IScopeRecord<CheckboxSx>
{
    /// <summary>勾上、部分勾选时方框的颜色。</summary>
    public Color? FillColor { get; init; }

    /// <summary>没勾时方框的描边色（出错时仍是危险色）。</summary>
    public Color? BorderColor { get; init; }

    /// <summary>对勾、横线的颜色。</summary>
    public Color? IndicatorColor { get; init; }

    /// <inheritdoc />
    public CheckboxSx Merge(CheckboxSx? over) => over is null ? this : new() { FillColor = over.FillColor ?? FillColor, BorderColor = over.BorderColor ?? BorderColor, IndicatorColor = over.IndicatorColor ?? IndicatorColor };
}
