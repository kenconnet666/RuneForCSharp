using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 开关（vue-tsx-pro 的 Switch）：立即生效的开 / 关设置，如“接收通知”；要提交才生效的选项用 Checkbox。
/// 无窗口元素，放进窗体用 <see cref="SwitchControl"/>。
/// 轨道 2.25em × 1.25em，滑块 0.875em，在 durations.sm 内滑到另一端（轨道色同时过渡），从右到左时整体镜像；
/// 轨道里的内容（<see cref="CheckedContent"/>、<see cref="UncheckedContent"/>，文字或图标）让轨道随较宽的那份加宽，切换时宽度不跳。
/// 打开时轨道是色调的实底（<see cref="ToneFill"/>），滑块是其上的颜色。文字（<see cref="Content"/>）在右边，整块可点，高度不低于 24px（WCAG 2.5.8）。
/// 切换中（<see cref="IsLoading"/>）滑块里转圈（<see cref="DelayedLoading"/> 防闪烁）。不可用时整体 opacities.disabled。
/// 部件：<see cref="PropsLabel"/>、<see cref="PropsContentText"/>（<see cref="TextProps"/>）、<see cref="PropsSpinner"/>（<see cref="IconProps"/>）；
/// 轨道、滑块、叠放层是画出来的，没有元素，TS 的 propsControl、propsThumb、propsContent 等部件记录（DESIGN §7）。
/// </summary>
public class Switch : Element
{
    Text label = new();
    readonly Spinner spinner = new();
    readonly Signal<double> position = new(0);
    readonly DelayedLoading delayed;
    Element? checkedPart;
    Element? uncheckedPart;
    float spinnerSize;

    /// <summary>新建：关闭。</summary>
    public Switch() => delayed = new DelayedLoading(_ => Invalidate());

    /// <summary>值变了（点击、空格、读屏、代码里写）：数据绑定认 <c>IsCheckedChanged</c>。</summary>
    public event EventHandler? IsCheckedChanged;

    /// <summary>是否打开；变化时滑块在 durations.sm 内滑到另一端（宿主还没显示时直接到位）。</summary>
    public bool IsChecked
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            var shown = Host is Control { IsHandleCreated: true, Visible: true };
            Animator.Animate(position, value ? 1 : 0, shown ? Tokens.Duration.Sm : TimeSpan.Zero, Easing.Standard);
            Invalidate();
            IsCheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>只读：能聚焦，不能切换（查看详情、审批时）；外观照常。</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>必填（外观不变，供表单用）。</summary>
    public bool IsRequired { get; set; }

    /// <summary>校验未通过：轨道描边是危险色。</summary>
    public bool IsInvalid { get; set { field = value; Invalidate(); } }

    /// <summary>切换中（如等接口返回）：滑块里转圈（防闪烁），不能切换。</summary>
    public bool IsLoading { get; set { field = value; delayed.Update(value); Invalidate(); } }

    /// <summary>打开时轨道里的文字或图标（如“开”、对勾）。</summary>
    public SwitchContent? CheckedContent { get; set { field = value; RebuildContent(); } }

    /// <summary>关闭时轨道里的文字或图标。</summary>
    public SwitchContent? UncheckedContent { get; set { field = value; RebuildContent(); } }

    /// <summary>文字（TS 的 children），在轨道右边（从右到左时在左边）。</summary>
    public string? Content { get; set { field = value; RebuildLabel(); } }

    /// <summary>打开时轨道的色调；不写时 accent。</summary>
    public StatusTone Tone { get => field.Value is null ? StatusTone.Accent : field; set { field = value; Invalidate(); } }

    /// <summary>字号档位：轨道、滑块、文字随之缩放；不写时跟随宿主的字体。</summary>
    public TextSize Size { get; set { field = value; InvalidateMeasure(); } }

    /// <summary>文字部件的属性（TS 的 propsLabel）。</summary>
    public TextProps? PropsLabel { get; set { field = value; RebuildLabel(); } }

    /// <summary>轨道里文字的属性（TS 的 propsContentText）：默认 xs、半粗；图标用其中的字号与色调。</summary>
    public TextProps? PropsContentText { get; set { field = value; RebuildContent(); } }

    /// <summary>加载指示部件的属性（TS 的 propsSpinner，同 Button：图标部件的属性）。</summary>
    public IconProps? PropsSpinner { get => spinner.PropsIcon; set => spinner.PropsIcon = value; }

    /// <summary>可用（宿主的 Enabled）。</summary>
    public bool IsEnabled { get; internal set { if (field == value) return; field = value; Invalidate(); } } = true;

    /// <summary>键盘聚焦（宿主按 ShowFocusCues 设，同 :focus-visible）。</summary>
    public bool IsFocusVisible { get; internal set { if (field == value) return; field = value; Invalidate(); } }

    /// <summary>加载指示正在显示（加载开始 200 毫秒后，至少 500 毫秒）。</summary>
    public bool ShowsSpinner => delayed.Shown;

    /// <summary>滑块的位置：0 在起始边，1 在结束边（测试用）。</summary>
    internal double Position => position.Peek();

    /// <summary>加载指示部件（测试用）。</summary>
    internal Spinner SpinnerPart => spinner;

    /// <summary>轨道里的两份内容：打开、关闭（没有内容时为 null；测试用）。</summary>
    internal (Element? Checked, Element? Unchecked) ContentParts => (checkedPart, uncheckedPart);

    /// <summary>文字部件（测试用）。</summary>
    internal Text LabelPart => label;

    bool Wide => CheckedContent is not null || UncheckedContent is not null;

    bool HasLabel => !string.IsNullOrEmpty(Content);

    /// <summary>停掉进行中的滑动与加载指示的等待（控件释放时）。</summary>
    internal void Stop()
    {
        Animator.Stop(position);
        delayed.Stop();
    }

    void RebuildLabel()
    {
        label = new Text { Content = Content ?? "" };
        PropsLabel?.ApplyTo(label);
        InvalidateMeasure();
    }

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

    /// <summary>
    /// 各部分的大小（设备像素）。轨道 2.25em × 1.25em，轨道里有内容时宽度取较宽那份加两侧的内边距（滑块一侧 1.375em、另一侧 0.5em）；
    /// 第一行（轨道与第一行文字垂直居中的那一行）高 1.5em；轨道与文字间距 space.sm。
    /// </summary>
    Metrics Layout(ElementContext inner, int availableWidth)
    {
        var em = inner.Em;
        var unbounded = new Size(int.MaxValue, int.MaxValue);
        var on = checkedPart?.Measure(inner, unbounded) ?? default;
        var off = uncheckedPart?.Measure(inner, unbounded) ?? default;
        var trackWidth = Wide ? MathF.Max(2.25f * em, MathF.Max(on.Width, off.Width) + 1.875f * em) : 2.25f * em;
        var trackHeight = 1.25f * em;
        var row = MathF.Max(1.5f * em, trackHeight);
        var gap = Tokens.Space.Sm * em;
        var labelSize = default(Size);
        if (HasLabel)
        {
            var limit = availableWidth == int.MaxValue ? int.MaxValue : Math.Max(0, availableWidth - (int)MathF.Ceiling(trackWidth + gap));
            labelSize = label.Measure(inner, new Size(limit, int.MaxValue));
        }
        // 文字的行高按字体算（不是 TS 的 1.5em）：在第一行里居中，多行时上下各留一份
        var labelTop = MathF.Max(0, (row - Fonts.LineHeight(LabelFont(inner))) / 2);
        return new Metrics(trackWidth, trackHeight, row, gap, on, off, labelSize, labelTop);
    }

    Font LabelFont(ElementContext inner) =>
        label.Size.Pixels is { } pixels ? Fonts.Get(inner.Font.FontFamily.Name, pixels * 72 / 96, inner.Font.Style) : inner.Font;

    /// <inheritdoc />
    protected internal override Size Measure(ElementContext context, Size available)
    {
        var inner = context.Sized(Size);
        var metrics = Layout(inner, available.Width);
        var width = metrics.Track.Width + (HasLabel ? metrics.Gap + metrics.Label.Width : 0);
        var labelHeight = HasLabel ? metrics.LabelTop * 2 + metrics.Label.Height : 0;
        var height = MathF.Max(context.Scale(Tokens.Size.MinTarget), MathF.Max(metrics.Row, labelHeight));
        return new Size((int)MathF.Ceiling(width), (int)MathF.Ceiling(height));
    }

    /// <inheritdoc />
    protected internal override int? Baseline(ElementContext context) =>
        HasLabel ? (int)MathF.Round(Layout(context.Sized(Size), Bounds.Width).LabelTop) + label.Baseline(context.Sized(Size)) : null;

    /// <inheritdoc />
    protected internal override void Paint(Graphics graphics, ElementContext context)
    {
        var inner = context.Sized(Size);
        var theme = context.Theme;
        var colors = theme.Colors;
        var (fill, onFill) = ToneFill.Colors(Tone, colors);
        var metrics = Layout(inner, Bounds.Width);
        var back = IsEnabled ? default : BackColor(colors);
        Color Dim(Color color) => IsEnabled ? color : Drawing.Mix(back, color, Tokens.Opacity.Disabled);
        float X(float start, float width) => FromStart(context, start, width);

        var em = inner.Em;
        var progress = position.Value;
        var (track, thumb) = Place(context, inner, metrics, progress);
        var (trackWidth, trackHeight) = (track.Width, track.Height);
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // 轨道色随滑块的位置从 borderStrong 过渡到色调的实底（TS 的 background-color 过渡，同一时长与缓动）
            using (var path = Drawing.RoundedRectangle(track, trackHeight / 2))
            using (var brush = new SolidBrush(Dim(Drawing.Mix(colors.BorderStrong, fill, progress)))) graphics.FillPath(brush, path);
            if (IsInvalid)
            {
                var width = context.Scale(Tokens.BorderWidth.Sm);
                var edge = RectangleF.Inflate(track, -width / 2, -width / 2);
                using var path = Drawing.RoundedRectangle(edge, edge.Height / 2);
                using var pen = new Pen(Dim(colors.Danger), width);
                graphics.DrawPath(pen, path);
            }

            // 轨道里只显示当前状态那份，在滑块另一侧的空位里居中
            if (VisibleContent is { } part)
            {
                var size = IsChecked ? metrics.Checked : metrics.Unchecked;
                var area = (Start: (IsChecked ? 0.5f : 1.375f) * em, Width: trackWidth - 1.875f * em);
                part.Bounds = new Rectangle(
                    (int)MathF.Round(X(area.Start, area.Width) + (area.Width - size.Width) / 2),
                    (int)MathF.Round(track.Y + (trackHeight - size.Height) / 2), size.Width, size.Height);
                part.Paint(graphics, inner with { ForeColor = Dim(IsChecked ? onFill : colors.Text) });
            }

            if (IsEnabled) Drawing.Shadow(graphics, thumb, thumb.Width / 2, theme.Shadows.Sm, inner.Scale(1));
            using (var brush = new SolidBrush(Dim(IsChecked ? onFill : colors.Background))) graphics.FillEllipse(brush, thumb);
            if (delayed.Shown) PaintSpinner(graphics, inner with { ForeColor = Dim(fill) }, thumb);

            if (IsFocusVisible)
            {
                // focusRingStyles.tight：贴着轨道，外扩 4、宽 2（画在宿主留出的 Padding 里）
                var (reach, width) = (context.Scale(4), context.Scale(Tokens.BorderWidth.Sm));
                var ring = RectangleF.Inflate(track, reach - width / 2, reach - width / 2);
                using var path = Drawing.RoundedRectangle(ring, ring.Height / 2);
                using var pen = new Pen(colors.FocusRing, width);
                graphics.DrawPath(pen, path);
            }
        }
        finally
        {
            graphics.Restore(state);
        }

        if (HasLabel)
        {
            var size = metrics.Label;
            label.Bounds = new Rectangle((int)MathF.Round(X(trackWidth + metrics.Gap, size.Width)), (int)MathF.Round(Bounds.Y + metrics.LabelTop), size.Width, size.Height);
            label.Paint(graphics, inner with { ForeColor = Dim(inner.ForeColor) });
        }
    }

    /// <summary>从右到左时整体镜像：布局都按起始边算，落到画布上时换算。</summary>
    float FromStart(ElementContext context, float start, float width) => context.RightToLeft ? Bounds.Right - start - width : Bounds.X + start;

    /// <summary>轨道与滑块在宿主里的位置（设备像素）：滑块 0.875em，离轨道两端 0.1875em，随 <paramref name="progress"/> 从起始边滑到结束边。</summary>
    (RectangleF Track, RectangleF Thumb) Place(ElementContext context, ElementContext inner, Metrics metrics, double progress)
    {
        var (width, height) = (metrics.TrackWidth, metrics.TrackHeight);
        var track = new RectangleF(FromStart(context, 0, width), Bounds.Y + (metrics.Row - height) / 2, width, height);
        var side = 0.875f * inner.Em;
        var (start, end) = (0.1875f * inner.Em, width - 1.0625f * inner.Em);
        var thumb = new RectangleF(FromStart(context, start + (end - start) * (float)progress, side), track.Y + (height - side) / 2, side, side);
        return (track, thumb);
    }

    /// <summary>轨道与滑块的位置（测试用）。</summary>
    internal (RectangleF Track, RectangleF Thumb) Parts(ElementContext context, double progress)
    {
        var inner = context.Sized(Size);
        return Place(context, inner, Layout(inner, Bounds.Width), progress);
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

    /// <summary>宿主往上第一个不透明的背景色（不可用时把颜色按 opacities.disabled 混进它，等于整体半透明）。</summary>
    Color BackColor(ThemeColors colors)
    {
        for (var control = Host as Control; control is not null; control = control.Parent)
            if (control.BackColor.A == 255) return control.BackColor;
        return colors.Background;
    }

    /// <summary>各部分的大小：轨道、第一行（轨道在里面垂直居中）、轨道与文字的间距、轨道里两份内容、文字、文字离上边的距离。</summary>
    readonly record struct Metrics(float TrackWidth, float TrackHeight, float Row, float Gap, Size Checked, Size Unchecked, Size Label, float LabelTop)
    {
        public SizeF Track => new(TrackWidth, TrackHeight);
    }
}

/// <summary>Switch 要切换时的参数：<see cref="IsChecked"/> 是要切到的值，设 Cancel 不改。</summary>
/// <param name="isChecked">要切到的值。</param>
public sealed class SwitchTogglingEventArgs(bool isChecked) : CancelEventArgs
{
    /// <summary>要切到的值。</summary>
    public bool IsChecked { get; } = isChecked;
}

/// <summary>
/// 开关控件（vue-tsx-pro 的 Switch）：承载 <see cref="Switch"/> 元素。
/// <code>
/// var notify = new SwitchControl { Text = "接收通知", IsChecked = true, AccessibleName = "接收通知" };
/// var dark = new SwitchControl { CheckedContent = Lucide.Moon, UncheckedContent = Lucide.Sun, TextSize = TextSize.Lg };
/// // 切换要等接口：Toggling 里拦下并显示加载，接口回来再写 IsChecked
/// sync.Toggling += (_, e) => { e.Cancel = true; sync.IsLoading = true; Save(e.IsChecked, () => { sync.IsChecked = e.IsChecked; sync.IsLoading = false; }); };
/// </code>
/// 点击、空格（松开时）切换；只读时能聚焦、不能切换；切换中、不可用时点击无效。焦点环只在键盘提示开着时显示，画在宿主留出的 4 像素 Padding 里。
/// 读屏是复选按钮，名称是 AccessibleName 或文字，状态是选中 / 未选中（切换时通知读屏），切换中带“忙”，只读带“只读”。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(IsCheckedChanged))]
[DefaultProperty(nameof(IsChecked))]
[Description("开关：立即生效的开 / 关设置，轨道里可以放文字或图标。")]
public class SwitchControl : ElementControl<Switch>
{
    /// <summary>新建：能获得焦点、进 Tab 顺序。</summary>
    public SwitchControl()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        Padding = new Padding(LogicalToDeviceUnits(4));
        Cursor = Cursors.Hand;
        Element.IsCheckedChanged += (_, _) => OnIsCheckedChanged(EventArgs.Empty);
    }

    /// <summary>要切换时（点击、空格、读屏、<see cref="Toggle"/>）先发：设 Cancel 不改值——切换要等接口时拦下，接口回来再写 IsChecked。</summary>
    [Category("Behavior")]
    [Description("要切换时先发；设 Cancel 不改值（受控用法：接口回来再写 IsChecked）。")]
    public event EventHandler<SwitchTogglingEventArgs>? Toggling;

    /// <summary>值变了；数据绑定认它。</summary>
    [Category("Property Changed")]
    [Description("IsChecked 变了。")]
    public event EventHandler? IsCheckedChanged;

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

    /// <summary>文字，也是读屏名称（没写 AccessibleName 时）。</summary>
    [AllowNull]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    [Category("Appearance")]
    [Description("轨道右边的文字，也是读屏名称。")]
    public override string Text { get => base.Text; set => base.Text = value; }

    /// <summary>文字部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsLabel { get => Element.PropsLabel; set => Element.PropsLabel = value; }

    /// <summary>轨道里文字的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextProps? PropsContentText { get => Element.PropsContentText; set => Element.PropsContentText = value; }

    /// <summary>加载指示部件的属性（代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconProps? PropsSpinner { get => Element.PropsSpinner; set => Element.PropsSpinner = value; }

    bool ShouldSerializeTone() => Tone != StatusTone.Accent;

    void ResetTone() => Tone = default;

    bool ShouldSerializeTextSize() => TextSize != default;

    void ResetTextSize() => TextSize = default;

    /// <summary>
    /// 切换，同点击：只读、切换中、不可用时不变；先发 <see cref="Toggling"/>，没被拦下才写 <see cref="IsChecked"/>。
    /// 要直接设值用 <see cref="IsChecked"/>。
    /// </summary>
    public void Toggle()
    {
        if (IsReadOnly || IsLoading || !Enabled) return;
        var args = new SwitchTogglingEventArgs(!IsChecked);
        OnToggling(args);
        if (!args.Cancel) IsChecked = args.IsChecked;
    }

    /// <summary>引发 <see cref="Toggling"/>。</summary>
    /// <param name="e">参数。</param>
    protected virtual void OnToggling(SwitchTogglingEventArgs e) => Toggling?.Invoke(this, e);

    /// <summary>引发 <see cref="IsCheckedChanged"/> 并通知读屏状态变了。</summary>
    /// <param name="e">参数。</param>
    protected virtual void OnIsCheckedChanged(EventArgs e)
    {
        IsCheckedChanged?.Invoke(this, e);
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
    }

    /// <inheritdoc />
    protected override void OnClick(EventArgs e)
    {
        Toggle();
        base.OnClick(e);
    }

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

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) Focus();
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Space) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    /// <summary>空格松开时切换（同原生 checkbox）。</summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode == Keys.Space) Toggle();
    }

    /// <summary>焦点环只在键盘提示开着时显示（用鼠标点出来的焦点不显示，同 :focus-visible）。</summary>
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Element.IsFocusVisible = ShowFocusCues;
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Element.IsFocusVisible = false;
    }

    /// <inheritdoc />
    protected override void OnChangeUICues(UICuesEventArgs e)
    {
        base.OnChangeUICues(e);
        Element.IsFocusVisible = Focused && ShowFocusCues;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) Element.Stop();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new SwitchAccessibleObject(this);

    /// <summary>读屏：复选按钮，名称取 AccessibleName 或文字；状态是选中 / 未选中，切换中带“忙”，只读带“只读”；默认动作是切换。</summary>
    sealed class SwitchAccessibleObject(SwitchControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => Owner is { AccessibleRole: not AccessibleRole.Default } control ? control.AccessibleRole : AccessibleRole.CheckButton;

        public override string? Name
        {
            get => base.Name is { Length: > 0 } name ? name : owner.Text;
            set => base.Name = value;
        }

        public override AccessibleStates State
        {
            get
            {
                var state = base.State;
                if (owner.IsChecked) state |= AccessibleStates.Checked;
                if (owner.IsReadOnly) state |= AccessibleStates.ReadOnly;
                if (owner.IsLoading) state |= AccessibleStates.Busy;
                return state;
            }
        }

        public override string DefaultAction => "切换";

        public override void DoDefaultAction() => owner.Toggle();
    }
}
