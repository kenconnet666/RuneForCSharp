using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 单选项（vue-tsx-pro 的 Radio）：放在 <see cref="RadioGroupControl"/> 里，选中由组的值决定。无窗口元素，放进窗体用 <see cref="RadioControl"/>。
/// 圆 1.125em、2px borderStrong 描边、表面底色；选中时是色调的实底（<see cref="ToneFill"/>），中间的圆点（0.45em、实底上的颜色）
/// 在 durations.sm 内从 0 放大。色调、字号、只读、出错随组；不可用写宿主的 Enabled。外层、文字与说明见 <see cref="ChoiceBase"/>。
/// </summary>
public class Radio : ChoiceBase
{
    readonly Signal<double> scale = new(0);

    /// <summary>这一项的值。</summary>
    public string? Value { get; set; }

    /// <summary>文字下方的说明（读屏的帮助文字）。</summary>
    public string? Description { get => DescriptionText; set => DescriptionText = value; }

    /// <summary>说明部件的属性（TS 的 propsDescription）。</summary>
    public TextProps? PropsDescription { get => DescriptionProps; set => DescriptionProps = value; }

    /// <summary>外观覆盖（上游的 sx）：盖过作用域的 <see cref="RadioSx"/> 与配方。</summary>
    public RadioSx? Sx { get; set { field = value; Invalidate(); } }

    RadioSx? ResolvedSx => Scope?.ResolveStyles(Sx) ?? Sx;

    /// <summary>所在的单选组（宿主沿 Parent 链找到后设）。</summary>
    internal RadioGroupControl? Group { get; set { if (field == value) return; field = value; InvalidateMeasure(); } }

    /// <inheritdoc />
    private protected override bool InGroup => Group is not null;

    /// <inheritdoc />
    internal override bool ReadOnly => Group?.ReadOnly == true;

    /// <inheritdoc />
    internal override bool Invalid => Group?.Invalid == true;

    /// <inheritdoc />
    internal override bool Required => Group?.Required == true;

    /// <summary>色调随组。</summary>
    internal override StatusTone ResolvedTone => Group?.Tone ?? base.ResolvedTone;

    /// <summary>字号档位随组。</summary>
    internal override TextSize ResolvedSize => Group?.TextSize ?? base.ResolvedSize;

    /// <summary>单选项点了只会选中。</summary>
    internal override bool NextChecked => true;

    /// <summary>圆点的缩放：0 没选中，1 选中（测试用）。</summary>
    internal double DotScale => scale.Peek();

    /// <summary>选中交给组。</summary>
    internal override void ApplyChecked(bool value)
    {
        if (Group is not null && Value is not null) Group.Value = Value;
        else base.ApplyChecked(value);
    }

    /// <summary>圆点在 durations.sm 内放大到原大或缩成 0（宿主还没显示时直接到位）。</summary>
    private protected override void OnCheckedChanged(bool animate) =>
        Animator.Animate(scale, IsChecked ? 1 : 0, animate ? Tokens.Duration.Sm : TimeSpan.Zero, Easing.Standard);

    /// <summary>停掉进行中的缩放（控件释放时）。</summary>
    internal void Stop() => Animator.Stop(scale);

    /// <inheritdoc />
    private protected override SizeF ControlSize(ElementContext inner) => new(1.125f * inner.Em, 1.125f * inner.Em);

    /// <summary>圆；中间的圆点按缩放画。</summary>
    private protected override void PaintControl(Graphics graphics, ElementContext context, ElementContext inner, RectangleF circle, Func<Color, Color> dim)
    {
        var (fill, onFill) = ToneFill.Colors(ResolvedTone, context.Theme.Colors);
        var sx = ResolvedSx;
        PaintInput(graphics, context, circle, circle.Width / 2, IsChecked, Invalid, sx?.FillColor ?? fill, sx?.BorderColor, dim);
        var side = 0.45f * inner.Em * (float)scale.Value;
        if (side > 0)
        {
            using var brush = new SolidBrush(dim(sx?.IndicatorColor ?? onFill));
            graphics.FillEllipse(brush, circle.X + (circle.Width - side) / 2, circle.Y + (circle.Height - side) / 2, side, side);
        }
        if (IsFocusVisible) PaintFocusRing(graphics, context, circle, circle.Width / 2);
    }
}

/// <summary>
/// 单选项控件（vue-tsx-pro 的 Radio）：承载 <see cref="Radio"/> 元素，放进 <see cref="RadioGroupControl"/>。
/// 点击、空格与焦点见 <see cref="ChoiceControl{T}"/>；方向键在组内移动并选中、Tab 只停在选中项由组管理。读屏是单选按钮。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Value))]
[Description("单选项：放进 RadioGroupControl，选中由组的 Value 决定。")]
public class RadioControl : ChoiceControl<Radio>
{
    /// <summary>是否选中（由组的值决定）。</summary>
    [Browsable(false)]
    public bool IsChecked => Element.IsChecked;

    /// <summary>这一项的值。</summary>
    [Category("Data")]
    [Description("这一项的值。")]
    [DefaultValue(null)]
    public string? Value { get => Element.Value; set { Element.Value = value; Element.Group?.Sync(); } }

    /// <summary>文字下方的说明。</summary>
    [Category("Appearance")]
    [Description("文字下方的说明（读屏的帮助文字）。")]
    [DefaultValue(null)]
    public string? Description { get => Element.Description; set => Element.Description = value; }

    /// <summary>说明部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsDescription { get => Element.PropsDescription; set => Element.PropsDescription = value; }

    /// <summary>外观覆盖（代码里设，上游的 sx）：盖过作用域的 <see cref="RadioSx"/> 与配方。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RadioSx? Sx { get => Element.Sx; set => Element.Sx = value; }

    /// <summary>换了位置：沿 Parent 链重新找所在的单选组。</summary>
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        var old = Element.Group;
        Element.Group = ChoiceGroupControl.Of<RadioGroupControl>(this);
        if (old != Element.Group) old?.Sync();
        Element.Group?.Sync();
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    /// <summary>方向键：交给组在组内移动并选中。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Element.Group is not { } group) return;
        var rtl = RightToLeft == RightToLeft.Yes;
        int? step = e.KeyCode switch
        {
            Keys.Down => 1,
            Keys.Up => -1,
            Keys.Right => rtl ? -1 : 1,
            Keys.Left => rtl ? 1 : -1,
            _ => null,
        };
        if (step is not { } delta) return;
        e.Handled = true;
        group.Step(this, delta);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) Element.Stop();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new RadioAccessibleObject(this);

    /// <summary>读屏：单选按钮；说明是帮助文字。</summary>
    sealed class RadioAccessibleObject(RadioControl owner) : ChoiceAccessibleObject(owner)
    {
        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole : AccessibleRole.RadioButton;

        public override string DefaultAction => "选中";
    }
}

/// <summary>Radio 的外观覆盖（上游的 <c>sx</c> 与 <c>components.Radio.styles</c>）：作用域的在下，实例的 Sx 在上，盖过色调。</summary>
public sealed record RadioSx : IScopeRecord<RadioSx>
{
    /// <summary>选中时圆的颜色。</summary>
    public Color? FillColor { get; init; }

    /// <summary>没选中时圆的描边色（出错时仍是危险色）。</summary>
    public Color? BorderColor { get; init; }

    /// <summary>圆点的颜色。</summary>
    public Color? IndicatorColor { get; init; }

    /// <inheritdoc />
    public RadioSx Merge(RadioSx? over) => over is null ? this : new() { FillColor = over.FillColor ?? FillColor, BorderColor = over.BorderColor ?? BorderColor, IndicatorColor = over.IndicatorColor ?? IndicatorColor };
}
