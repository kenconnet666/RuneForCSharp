using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 开关（vue-tsx-pro 的 Switch）：立即生效的开 / 关设置，如“接收通知”；要提交才生效的选项用 Checkbox。
/// 无窗口元素，放进窗体用 <see cref="SwitchControl"/>。
/// 轨道 2.25em × 1.25em，滑块 0.875em，在 durations.sm 内滑到另一端（轨道色同时过渡），从右到左时整体镜像；
/// 轨道里的内容（<see cref="CheckedContent"/>、<see cref="UncheckedContent"/>，文字或图标）让轨道随较宽的那份加宽，切换时宽度不跳。
/// 打开时轨道是色调的实底（<see cref="ToneFill"/>），滑块是其上的颜色。外层与文字见 <see cref="ChoiceBase"/>。
/// 切换中（<see cref="IsLoading"/>）滑块里转圈（<see cref="DelayedLoading"/> 防闪烁）。
/// 部件：<see cref="ChoiceBase.PropsLabel"/>、<see cref="PropsContentText"/>（<see cref="TextProps"/>）、<see cref="PropsSpinner"/>（<see cref="IconProps"/>）；
/// 轨道、滑块、叠放层是画出来的，没有元素，TS 的 propsControl、propsThumb、propsContent 等部件记录（DESIGN §7）。
/// </summary>
public class Switch : ChoiceBase
{
    readonly Spinner spinner = new();
    readonly Signal<double> position = new(0);
    readonly DelayedLoading delayed;
    Element? checkedPart;
    Element? uncheckedPart;
    float spinnerSize;

    /// <summary>新建：关闭。</summary>
    public Switch() => delayed = new DelayedLoading(_ => Invalidate());

    /// <summary>只读：能聚焦，不能切换（查看详情、审批时）；外观照常。</summary>
    public bool IsReadOnly { get => readOnly ?? false; set => readOnly = value; }

    /// <summary>必填（外观不变，供表单用）。</summary>
    public bool IsRequired { get => required ?? false; set => required = value; }

    /// <summary>校验未通过：轨道描边是危险色。</summary>
    public bool IsInvalid { get => invalid ?? false; set { invalid = value; Invalidate(); } }

    /// <summary>切换中（如等接口返回）：滑块里转圈（防闪烁），不能切换。</summary>
    public bool IsLoading { get; set { field = value; delayed.Update(value); Invalidate(); } }

    /// <summary>打开时轨道里的文字或图标（如“开”、对勾）。</summary>
    public SwitchContent? CheckedContent { get; set { field = value; RebuildContent(); } }

    /// <summary>关闭时轨道里的文字或图标。</summary>
    public SwitchContent? UncheckedContent { get; set { field = value; RebuildContent(); } }

    /// <summary>打开时轨道的色调；不写时取作用域的默认值（<see cref="ChoiceDefaults"/>），再没有时 accent。</summary>
    public StatusTone Tone { get => ResolvedTone; set { tone = value; Invalidate(); } }

    /// <summary>字号档位：轨道、滑块、文字随之缩放；不写时取作用域的默认值，再没有时跟随宿主的字体。</summary>
    public TextSize Size { get => ResolvedSize; set { size = value; InvalidateMeasure(); } }

    /// <summary>外观覆盖（上游的 sx）：盖过作用域的 <see cref="SwitchSx"/> 与配方。</summary>
    public SwitchSx? Sx { get; set { field = value; Invalidate(); } }

    /// <summary>合并好的外观覆盖：作用域的在下，实例的在上。</summary>
    SwitchSx? ResolvedSx => Scope?.ResolveStyles(Sx) ?? Sx;

    /// <summary>轨道里文字的属性（TS 的 propsContentText）：默认 xs、半粗；图标用其中的字号与色调。</summary>
    public TextProps? PropsContentText { get; set { field = value; RebuildContent(); } }

    /// <summary>加载指示部件的属性（TS 的 propsSpinner，同 Button：图标部件的属性）。</summary>
    public IconProps? PropsSpinner { get => spinner.PropsIcon; set => spinner.PropsIcon = value; }

    /// <summary>加载指示正在显示（加载开始 200 毫秒后，至少 500 毫秒）。</summary>
    public bool ShowsSpinner => delayed.Shown;

    /// <summary>滑块的位置：0 在起始边，1 在结束边（测试用）。</summary>
    internal double Position => position.Peek();

    /// <summary>加载指示部件（测试用）。</summary>
    internal Spinner SpinnerPart => spinner;

    /// <summary>轨道里的两份内容：打开、关闭（没有内容时为 null；测试用）。</summary>
    internal (Element? Checked, Element? Unchecked) ContentParts => (checkedPart, uncheckedPart);

    /// <inheritdoc />
    internal override bool CanToggle => !IsLoading;

    bool Wide => CheckedContent is not null || UncheckedContent is not null;

    /// <summary>停掉进行中的滑动与加载指示的等待（控件释放时）。</summary>
    internal void Stop()
    {
        Animator.Stop(position);
        delayed.Stop();
    }

    /// <summary>滑块在 durations.sm 内滑到另一端（宿主还没显示时直接到位）。</summary>
    private protected override void OnCheckedChanged(bool animate) =>
        Animator.Animate(position, IsChecked ? 1 : 0, animate ? Tokens.Duration.Sm : TimeSpan.Zero, Easing.Standard);

    /// <summary>轨道里的两份内容按文字或图标重建：文字默认 xs、半粗，不换行；图标取其中的字号（默认 xs，即 1em 的 xs 字）。</summary>
    void RebuildContent()
    {
        var props = new TextProps { Size = TextSize.Xs, Weight = TextWeight.Semibold }.Merge(PropsContentText);
        checkedPart = Build(CheckedContent);
        uncheckedPart = Build(UncheckedContent);
        InvalidateMeasure();

        Element? Build(SwitchContent? content)
        {
            switch (content)
            {
                case { Text: { } text }:
                    var part = new Text { Content = text, Wrap = TextWrap.NoWrap };
                    props.ApplyTo(part);
                    return part;
                case { Icon: { } data }:
                    var icon = new Icon { Data = data, Size = props.Size ?? TextSize.Xs };
                    if (props.Tone is { } tone) icon.Tone = tone;
                    return icon;
                default:
                    return null;
            }
        }
    }

    /// <summary>轨道 2.25em × 1.25em；轨道里有内容时宽度取较宽那份加两侧的内边距（滑块一侧 1.375em、另一侧 0.5em）。</summary>
    private protected override SizeF ControlSize(ElementContext inner)
    {
        var em = inner.Em;
        if (!Wide) return new SizeF(2.25f * em, 1.25f * em);
        var unbounded = new Size(int.MaxValue, int.MaxValue);
        var on = checkedPart?.Measure(inner, unbounded) ?? default;
        var off = uncheckedPart?.Measure(inner, unbounded) ?? default;
        return new SizeF(MathF.Max(2.25f * em, MathF.Max(on.Width, off.Width) + 1.875f * em), 1.25f * em);
    }

    /// <inheritdoc />
    private protected override bool HoverRepaints => true;

    /// <inheritdoc />
    private protected override void PaintControl(Graphics graphics, ElementContext context, ElementContext inner, RectangleF track, Func<Color, Color> dim)
    {
        var theme = context.Theme;
        var colors = theme.Colors;
        var (fill, onFill) = ToneFill.Colors(Tone, colors);
        var sx = ResolvedSx;
        var em = inner.Em;
        var progress = position.Value;
        var thumb = Thumb(context, inner, track, progress);

        // 轨道色随滑块的位置从 borderStrong 过渡到色调的实底（TS 的 background-color 过渡，同一时长与缓动）；
        // 悬停时混进一点正文色（关 15%、开 12%）：亮色里变深、暗色里变亮
        var trackColor = Drawing.Mix(sx?.TrackOffColor ?? colors.BorderStrong, sx?.TrackColor ?? fill, progress);
        if (IsHovered && IsEnabled) trackColor = Drawing.Mix(trackColor, colors.Text, IsChecked ? 0.12 : 0.15);
        using (var path = Drawing.RoundedRectangle(track, track.Height / 2))
        using (var brush = new SolidBrush(dim(trackColor))) graphics.FillPath(brush, path);
        if (Invalid)
        {
            var width = context.Scale(Tokens.BorderWidth.Sm);
            var edge = RectangleF.Inflate(track, -width / 2, -width / 2);
            using var path = Drawing.RoundedRectangle(edge, edge.Height / 2);
            using var pen = new Pen(dim(colors.Danger), width);
            graphics.DrawPath(pen, path);
        }

        // 轨道里只显示当前状态那份，在滑块另一侧的空位里居中
        if (VisibleContent is { } part)
        {
            var size = part.Measure(inner, new Size(int.MaxValue, int.MaxValue));
            var area = (Start: (IsChecked ? 0.5f : 1.375f) * em, Width: track.Width - 1.875f * em);
            var x = context.RightToLeft ? track.Right - area.Start - area.Width : track.X + area.Start;
            part.Bounds = new Rectangle((int)MathF.Round(x + (area.Width - size.Width) / 2), (int)MathF.Round(track.Y + (track.Height - size.Height) / 2), size.Width, size.Height);
            part.Paint(graphics, inner with { ForeColor = dim(IsChecked ? onFill : colors.Text) });
        }

        if (IsEnabled) Drawing.Shadow(graphics, thumb, thumb.Width / 2, theme.Shadows.Sm, inner.Scale(1));
        using (var brush = new SolidBrush(dim(sx?.ThumbColor ?? (IsChecked ? onFill : colors.Background)))) graphics.FillEllipse(brush, thumb);
        if (delayed.Shown) PaintSpinner(graphics, inner with { ForeColor = dim(fill) }, thumb);

        if (IsFocusVisible) PaintFocusRing(graphics, context, track, track.Height / 2);
    }

    /// <summary>滑块：0.875em，离轨道两端 0.1875em，随 <paramref name="progress"/> 从起始边滑到结束边。</summary>
    static RectangleF Thumb(ElementContext context, ElementContext inner, RectangleF track, double progress)
    {
        var side = 0.875f * inner.Em;
        var (start, end) = (0.1875f * inner.Em, track.Width - 1.0625f * inner.Em);
        var offset = start + (end - start) * (float)progress;
        var x = context.RightToLeft ? track.Right - offset - side : track.X + offset;
        return new RectangleF(x, track.Y + (track.Height - side) / 2, side, side);
    }

    /// <summary>轨道与滑块的位置（测试用）。</summary>
    internal (RectangleF Track, RectangleF Thumb) Parts(ElementContext context, double progress)
    {
        var track = ControlBounds(context);
        return (track, Thumb(context, context.Sized(Size), track, progress));
    }

    /// <summary>轨道里正在显示的那份内容：只显示当前状态的，另一份只占位。</summary>
    internal Element? VisibleContent => IsChecked ? checkedPart : uncheckedPart;

    /// <summary>滑块里的加载指示：0.75em，居中，颜色是色调的实底。</summary>
    void PaintSpinner(Graphics graphics, ElementContext inner, RectangleF thumb)
    {
        var size = 0.75f * inner.Em * 96 / inner.Dpi;
        if (size != spinnerSize) spinner.Size = spinnerSize = size;
        var measured = spinner.Measure(inner, new Size(int.MaxValue, int.MaxValue));
        spinner.Bounds = new Rectangle((int)MathF.Round(thumb.X + (thumb.Width - measured.Width) / 2), (int)MathF.Round(thumb.Y + (thumb.Height - measured.Height) / 2), measured.Width, measured.Height);
        spinner.Paint(graphics, inner);
    }
}

/// <summary>
/// 开关控件（vue-tsx-pro 的 Switch）：承载 <see cref="Switch"/> 元素。
/// <code>
/// var notify = new SwitchControl { Text = "接收通知", IsChecked = true, AccessibleName = "接收通知" };
/// var dark = new SwitchControl { CheckedContent = Lucide.Moon, UncheckedContent = Lucide.Sun, TextSize = TextSize.Lg };
/// // 切换要等接口：Toggling 里拦下并显示加载，接口回来再写 IsChecked
/// sync.Toggling += (_, e) => { e.Cancel = true; sync.IsLoading = true; Save(e.IsChecked, () => { sync.IsChecked = e.IsChecked; sync.IsLoading = false; }); };
/// </code>
/// 点击、空格与焦点见 <see cref="ChoiceControl{T}"/>；切换中点击无效。
/// 读屏是复选按钮，名称是 AccessibleName 或文字，状态是选中 / 未选中（切换时通知读屏），切换中带“忙”，只读带“只读”。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(IsCheckedChanged))]
[DefaultProperty(nameof(IsChecked))]
[Description("开关：立即生效的开 / 关设置，轨道里可以放文字或图标。")]
public class SwitchControl : ChoiceControl<Switch>
{
    /// <summary>是否打开。</summary>
    [Bindable(true)]
    [Category("Appearance")]
    [Description("是否打开。")]
    [DefaultValue(false)]
    public bool IsChecked { get => Element.IsChecked; set => Element.IsChecked = value; }

    /// <summary>只读。</summary>
    [Category("Behavior")]
    [Description("只读：能聚焦，不能切换；外观照常。")]
    [DefaultValue(false)]
    public bool IsReadOnly { get => Element.IsReadOnly; set { Element.IsReadOnly = value; AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); } }

    /// <summary>必填。</summary>
    [Category("Behavior")]
    [Description("必填（外观不变，供表单用）。")]
    [DefaultValue(false)]
    public bool IsRequired { get => Element.IsRequired; set => Element.IsRequired = value; }

    /// <summary>校验未通过。</summary>
    [Category("Appearance")]
    [Description("校验未通过：轨道描边是危险色。")]
    [DefaultValue(false)]
    public bool IsInvalid { get => Element.IsInvalid; set => Element.IsInvalid = value; }

    /// <summary>切换中。</summary>
    [Category("Behavior")]
    [Description("切换中：滑块里转圈，点击无效。")]
    [DefaultValue(false)]
    public bool IsLoading { get => Element.IsLoading; set { Element.IsLoading = value; AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); } }

    /// <summary>打开时轨道里的文字或图标。</summary>
    [Category("Appearance")]
    [Description("打开时轨道里的文字或图标（代码里可直接写 Lucide.Moon）；给了它或 UncheckedContent，轨道随内容加宽。")]
    [DefaultValue(null)]
    public SwitchContent? CheckedContent { get => Element.CheckedContent; set => Element.CheckedContent = value; }

    /// <summary>关闭时轨道里的文字或图标。</summary>
    [Category("Appearance")]
    [Description("关闭时轨道里的文字或图标。")]
    [DefaultValue(null)]
    public SwitchContent? UncheckedContent { get => Element.UncheckedContent; set => Element.UncheckedContent = value; }

    /// <summary>打开时轨道的色调。</summary>
    [Category("Appearance")]
    [Description("打开时轨道的色调：neutral、accent（默认）、info、success、warning、danger。")]
    public StatusTone Tone { get => Element.Tone; set => Element.Tone = value; }

    /// <summary>字号档位。</summary>
    [Category("Appearance")]
    [Description("字号：xs–xl 或像素数；轨道、滑块随之缩放，空着时跟随字体。")]
    public TextSize TextSize { get => Element.Size; set => Element.Size = value; }

    /// <summary>外观覆盖（代码里设，上游的 sx）：盖过作用域的 <see cref="SwitchSx"/> 与配方。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SwitchSx? Sx { get => Element.Sx; set => Element.Sx = value; }

    /// <summary>轨道里文字的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsContentText { get => Element.PropsContentText; set => Element.PropsContentText = value; }

    /// <summary>加载指示部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsSpinner { get => Element.PropsSpinner; set => Element.PropsSpinner = value; }

    bool ShouldSerializeTone() => Element.Explicit.Tone;

    void ResetTone() => Tone = default;

    bool ShouldSerializeTextSize() => Element.Explicit.Size;

    void ResetTextSize() => TextSize = default;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) Element.Stop();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new SwitchAccessibleObject(this);

    /// <summary>读屏：复选按钮；切换中带“忙”。</summary>
    sealed class SwitchAccessibleObject(SwitchControl owner) : ChoiceAccessibleObject(owner)
    {
        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole : AccessibleRole.CheckButton;

        public override AccessibleStates State => owner.IsLoading ? base.State | AccessibleStates.Busy : base.State;

        public override string DefaultAction => "切换";
    }
}

/// <summary>Switch 的外观覆盖（上游的 <c>sx</c> 与 <c>components.Switch.styles</c>）：作用域的在下，实例的 Sx 在上，盖过色调。</summary>
public sealed record SwitchSx : IScopeRecord<SwitchSx>
{
    /// <summary>打开时轨道的颜色。</summary>
    public Color? TrackColor { get; init; }

    /// <summary>关着时轨道的颜色。</summary>
    public Color? TrackOffColor { get; init; }

    /// <summary>滑块的颜色（开、关一样）。</summary>
    public Color? ThumbColor { get; init; }

    /// <inheritdoc />
    public SwitchSx Merge(SwitchSx? over) => over is null ? this : new() { TrackColor = over.TrackColor ?? TrackColor, TrackOffColor = over.TrackOffColor ?? TrackOffColor, ThumbColor = over.ThumbColor ?? ThumbColor };
}
