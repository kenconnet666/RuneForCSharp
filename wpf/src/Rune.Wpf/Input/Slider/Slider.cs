using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 滑块（vue-tsx-pro 的 Slider）：在一个范围里拖动选值（音量、进度、价格）。
/// <code>
/// &lt;rune:Slider Value="{Binding Volume}" AutomationProperties.Name="音量" /&gt;
/// &lt;rune:Slider Value="{Binding Price}" Min="0" Max="1000" Step="50" /&gt;
/// &lt;rune:Slider Range="{Binding PriceRange}" /&gt;      &lt;!-- 范围：两个滑块 --&gt;
/// </code>
/// 方向键 ±Step，Page Up / Page Down ±BigStep，Home / End 到两端；从右到左排版时横向的方向键反过来。点击轨道任意位置直接跳过去并接着拖动。
/// <see cref="Range"/> 不为 null 时是范围选择：两个滑块各自可聚焦、可拖动，不能交叉；点击轨道移动离得近的那个，读屏名称后面加"最小值""最大值"。
/// 一次操作结束（松开、按键）时触发 <see cref="ValueCommitted"/>：保存、发请求放在这里，不必每次移动都发。
/// 轨道 fill 底、1.5 倍 space.xs 粗；已选部分是色调的实底；滑块是 space.lg + space.xs 的圆，surface 底、色调描边、sm 阴影。
/// 点击区域至少 sizes.minTarget；滑块中心对准取值，两端各留半个滑块（滑块在两端时不伸出控件）。不可用时整体半透明。
/// </summary>
public class Slider : FrameworkElement, IFieldControl
{
    /// <summary>单值（默认双向绑定）。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(Slider),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    /// <summary>范围选择的取值（默认双向绑定）；不为 null 时是两个滑块。</summary>
    public static readonly DependencyProperty RangeProperty = DependencyProperty.Register(nameof(Range), typeof(SliderRange?), typeof(Slider),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) => ((Slider)d).OnRangeChanged(e)));

    /// <summary>最小值，默认 0。</summary>
    public static readonly DependencyProperty MinProperty = DependencyProperty.Register(nameof(Min), typeof(double), typeof(Slider),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    /// <summary>最大值，默认 100。</summary>
    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(nameof(Max), typeof(double), typeof(Slider),
        new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    /// <summary>步长，默认 1；取值总是对齐到 Min + n × Step。</summary>
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(Slider), new FrameworkPropertyMetadata(1d), step => (double)step > 0);

    /// <summary>Page Up / Page Down 的步长；不写时为范围的十分之一（至少一个 Step）。</summary>
    public static readonly DependencyProperty BigStepProperty = DependencyProperty.Register(nameof(BigStep), typeof(double?), typeof(Slider), new FrameworkPropertyMetadata(null));

    /// <summary>方向，默认横向。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(SliderOrientation), typeof(Slider),
        new FrameworkPropertyMetadata(default(SliderOrientation), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>只读：滑块能聚焦，拖动与方向键不改值；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool?), typeof(Slider), new FrameworkPropertyMetadata(null));

    /// <summary>字号档位：滑块与轨道随之缩放；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(Slider),
        new FrameworkPropertyMetadata(default(TextSize), (d, _) => ((Slider)d).ApplySize()));

    /// <summary>已选范围与滑块描边的色调，默认 accent。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(Slider),
        new FrameworkPropertyMetadata(default(StatusTone), FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Slider)d).RefreshThumbs()));

    /// <summary>刻度：值或带文字的刻度；有文字时下方（纵向时右侧）留出位置。</summary>
    public static readonly DependencyProperty MarksProperty = DependencyProperty.Register(nameof(Marks), typeof(IReadOnlyList<SliderMark>), typeof(Slider),
        new FrameworkPropertyMetadata(null, (d, _) => ((Slider)d).Rebuild()));

    /// <summary>字号（尺寸按它算）：同 TextElement.FontSize，继承外面的。</summary>
    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(Slider),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    readonly VisualCollection children;
    readonly List<SliderThumb> thumbs = [];
    readonly List<Text> labels = [];
    Func<double, string>? valueText;
    int dragging = -1;

    static Slider() => IsEnabledProperty.OverrideMetadata(typeof(Slider), new UIPropertyMetadata(true, (d, _) => ((Slider)d).ApplyEnabled()));

    /// <summary>新建：0–100、步长 1 的单值滑块。</summary>
    public Slider()
    {
        children = new VisualCollection(this);
        Cursor = Cursors.Hand;
        Rebuild();
    }

    /// <summary>单值。</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>范围选择的取值。</summary>
    public SliderRange? Range { get => (SliderRange?)GetValue(RangeProperty); set => SetValue(RangeProperty, value); }

    /// <summary>最小值。</summary>
    public double Min { get => (double)GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最大值。</summary>
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>步长。</summary>
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>Page 键的步长。</summary>
    public double? BigStep { get => (double?)GetValue(BigStepProperty); set => SetValue(BigStepProperty, value); }

    /// <summary>方向。</summary>
    public SliderOrientation Orientation { get => (SliderOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => (bool?)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>刻度。</summary>
    public IReadOnlyList<SliderMark>? Marks { get => (IReadOnlyList<SliderMark>?)GetValue(MarksProperty); set => SetValue(MarksProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>读屏念的取值文字（TS 的 aria-valuetext），如 <c>v =&gt; $"{v}%"</c>；不写时念数字。</summary>
    public Func<double, string>? ValueText
    {
        get => valueText;
        set
        {
            valueText = value;
            RefreshThumbs();
        }
    }

    /// <summary>一次操作结束（松开指针、按键）时触发。</summary>
    public event EventHandler? ValueCommitted;

    /// <summary>滑块（测试用）：单值时一个，范围时两个。</summary>
    internal IReadOnlyList<SliderThumb> Thumbs => thumbs;

    /// <summary>刻度文字（测试用）。</summary>
    internal IReadOnlyList<Text> MarkLabels => labels;

    internal bool IsRange => Range is not null;

    internal bool ReadOnly => IsReadOnly ?? Field.Of(this)?.IsReadOnly ?? false;

    internal bool Vertical => Orientation.Value == "vertical";

    /// <summary>各滑块的值（单值时一个）。</summary>
    internal double[] Values => Range is { } range ? [range.Low, range.High] : [Value];

    /// <summary>显示的值：父级给了超出范围的值时按范围内显示（不改父级的值）。</summary>
    internal double Shown(int index) => Math.Clamp(Values[index], Min, Math.Max(Min, Max));

    /// <summary>第 index 个滑块能取的范围。</summary>
    internal (double Lower, double Upper) BoundsOf(int index) => SliderScale.Bounds(index, Values, Min, Max);

    internal string? ValueTextOf(double value) => valueText?.Invoke(value);

    internal double BigStepValue => SliderScale.BigStep(BigStep, Step, Min, Max);

    double Em => FontSize;

    double ThumbSize => (this.Resource<double>(ThemeKeys.Space.Lg) + this.Resource<double>(ThemeKeys.Space.Xs)) * Em;

    double TrackThickness => this.Resource<double>(ThemeKeys.Space.Xs) * 1.5 * Em;

    bool Labelled => Marks?.Any(mark => mark.Label is not null) == true;

    bool Editable => IsEnabled && !ReadOnly;

    void IFieldControl.FieldChanged()
    {
        RefreshThumbs();
        InvalidateVisual();
    }

    static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Slider)d).RefreshThumbs();

    void OnRangeChanged(DependencyPropertyChangedEventArgs e)
    {
        if ((e.OldValue is null) != (e.NewValue is null)) Rebuild();
        else RefreshThumbs();
    }

    void RefreshThumbs()
    {
        foreach (var thumb in thumbs) thumb.Refresh();
    }

    /// <summary>字号档位换成主题的字号资源（像素值直接用）；不写时继承外面的。</summary>
    void ApplySize()
    {
        if (Size.Pixels is { } pixels) SetValue(FontSizeProperty, pixels);
        else if (Size.ResourceKey is { } key) SetResourceReference(FontSizeProperty, key);
        else ClearValue(FontSizeProperty);
    }

    /// <summary>不可用时整体（含滑块、刻度文字）半透明。</summary>
    void ApplyEnabled()
    {
        if (IsEnabled) ClearValue(OpacityProperty);
        else SetResourceReference(OpacityProperty, ThemeKeys.Opacity.Disabled);
        RefreshThumbs();
    }

    /// <summary>按单值 / 范围建滑块，按刻度建文字。</summary>
    void Rebuild()
    {
        foreach (var child in children) RemoveLogicalChild(child);
        children.Clear();
        thumbs.Clear();
        labels.Clear();
        foreach (var mark in Marks ?? [])
        {
            if (mark.Label is null) continue;
            var label = new Text { Text = mark.Label, Size = TextSize.Sm, Tone = TextTone.Muted, Wrap = TextWrap.NoWrap, IsHitTestVisible = false };
            labels.Add(label);
            children.Add(label);
            AddLogicalChild(label);
        }
        for (var index = 0; index < Values.Length; index++)
        {
            var thumb = new SliderThumb(this, index);
            thumbs.Add(thumb);
            children.Add(thumb);
            AddLogicalChild(thumb);
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>改第 index 个滑块的值（对齐步长、不越过另一个）。</summary>
    internal void Set(int index, double raw)
    {
        var next = SliderScale.Set(Values, index, raw, Min, Max, Step);
        if (IsRange) SetCurrentValue(RangeProperty, new SliderRange(next[0], next[1]));
        else SetCurrentValue(ValueProperty, next[0]);
    }

    /// <summary>键盘：按键换成增减、到两端，改值并提交。</summary>
    internal bool Press(int index, Key key)
    {
        var rtl = FlowDirection == FlowDirection.RightToLeft && !Vertical;
        SliderKey? action = key switch
        {
            Key.Right => rtl ? SliderKey.Decrease : SliderKey.Increase,
            Key.Left => rtl ? SliderKey.Increase : SliderKey.Decrease,
            Key.Up => SliderKey.Increase,
            Key.Down => SliderKey.Decrease,
            Key.PageUp => SliderKey.PageUp,
            Key.PageDown => SliderKey.PageDown,
            Key.Home => SliderKey.Home,
            Key.End => SliderKey.End,
            _ => null,
        };
        if (action is not { } pressed) return false;
        if (!Editable) return true;
        var (lower, upper) = BoundsOf(index);
        Set(index, SliderScale.Target(pressed, Values[index], Step, BigStepValue, lower, upper));
        ValueCommitted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>轨道的矩形（滑块中心能到的范围）。</summary>
    Rect TrackRect(System.Windows.Size size)
    {
        var half = ThumbSize / 2;
        var thickness = TrackThickness;
        var reserve = Labelled ? (Vertical ? 3 : 1.5) * Em : 0;
        var box = Vertical ? new Rect(0, 0, Math.Max(0, size.Width - reserve), size.Height) : new Rect(0, 0, size.Width, Math.Max(0, size.Height - reserve));
        return Vertical
            ? new Rect(box.X + (box.Width - thickness) / 2, box.Y + half, thickness, Math.Max(0, box.Height - 2 * half))
            : new Rect(box.X + half, box.Y + (box.Height - thickness) / 2, Math.Max(0, box.Width - 2 * half), thickness);
    }

    /// <summary>值在轨道上的中心点。</summary>
    Point PointOf(double value, System.Windows.Size size)
    {
        var track = TrackRect(size);
        var ratio = SliderScale.Ratio(value, Min, Max);
        return Vertical ? new Point(track.X + track.Width / 2, track.Bottom - ratio * track.Height) : new Point(track.X + ratio * track.Width, track.Y + track.Height / 2);
    }

    /// <summary>指针位置对应的值（WPF 从右到左排版时整个镜像，这里按从左往右算）。</summary>
    double ValueAt(Point point)
    {
        var track = TrackRect(RenderSize);
        var ratio = Vertical ? (track.Bottom - point.Y) / Math.Max(1, track.Height) : (point.X - track.X) / Math.Max(1, track.Width);
        return SliderScale.ValueAt(ratio, Min, Max, Step);
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!PointerDown(e.GetPosition(this))) return;
        CaptureMouse();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        PointerMove(e.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (PointerUp()) ReleaseMouseCapture();
    }

    /// <summary>按下（点轨道）：移动离得近的那个滑块到这里、焦点交给它（键盘接着可以微调），之后的移动拖着它。不能改值时返回 false。</summary>
    internal bool PointerDown(Point point)
    {
        if (!Editable) return false;
        var target = ValueAt(point);
        dragging = SliderScale.Nearest(target, Values);
        thumbs[dragging].Focus();
        Set(dragging, target);
        return true;
    }

    internal void PointerMove(Point point)
    {
        if (dragging >= 0) Set(dragging, ValueAt(point));
    }

    /// <summary>松开：一次操作结束，提交。</summary>
    internal bool PointerUp()
    {
        if (dragging < 0) return false;
        dragging = -1;
        ValueCommitted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        dragging = -1;
    }

    /// <inheritdoc />
    protected override int VisualChildrenCount => children.Count;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => children[index];

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => children.GetEnumerator();

    /// <inheritdoc />
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        foreach (var label in labels) label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        foreach (var thumb in thumbs) thumb.Measure(new System.Windows.Size(ThumbSize, ThumbSize));
        var cross = Math.Max(this.Resource<double>(ThemeKeys.Size.MinTarget), ThumbSize) + (Labelled ? (Vertical ? 3 : 1.5) * Em : 0);
        // 横向时尽量占满给的宽度（同块级元素）；纵向默认 10em 高
        return Vertical
            ? new System.Windows.Size(cross, double.IsNaN(Height) ? 10 * Em : Height)
            : new System.Windows.Size(double.IsInfinity(availableSize.Width) ? 10 * Em : availableSize.Width, cross);
    }

    /// <inheritdoc />
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        var size = ThumbSize;
        foreach (var thumb in thumbs)
        {
            var center = PointOf(Shown(thumb.Index), finalSize);
            thumb.Arrange(new Rect(center.X - size / 2, center.Y - size / 2, size, size));
        }
        var track = TrackRect(finalSize);
        var labelMarks = (Marks ?? []).Where(mark => mark.Label is not null).ToList();
        for (var i = 0; i < labels.Count; i++)
        {
            var center = PointOf(labelMarks[i].Value, finalSize);
            var label = labels[i].DesiredSize;
            // 文字在轨道下方（纵向时右侧）：横向时中心对准刻度
            var origin = Vertical
                ? new Point(track.X + track.Width / 2 + this.Resource<double>(ThemeKeys.Space.Md) * Em, center.Y - label.Height / 2)
                : new Point(center.X - label.Width / 2, track.Y + track.Height / 2 + this.Resource<double>(ThemeKeys.Space.Sm) * Em);
            labels[i].Arrange(new Rect(origin, label));
        }
        return finalSize;
    }

    /// <summary>画轨道、已选范围与刻度点；滑块是子元素自己画。整块透明的底（含两端）都能点：轨道很细。</summary>
    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var track = TrackRect(RenderSize);
        if (track.Width <= 0 || track.Height <= 0) return;
        var (fillKey, onFillKey) = ToneFill.Keys(Tone);
        var round = Math.Min(track.Width, track.Height) / 2;
        context.DrawRoundedRectangle(this.Resource<Brush>(ThemeKeys.Brush.Fill), null, track, round, round);
        var values = Enumerable.Range(0, Values.Length).Select(Shown).ToArray();
        var (from, to) = values.Length > 1 ? (values[0], values[1]) : (Min, values[0]);
        var (start, end) = (PointOf(from, RenderSize), PointOf(to, RenderSize));
        var selected = Vertical ? new Rect(track.X, end.Y, track.Width, Math.Max(0, start.Y - end.Y)) : new Rect(start.X, track.Y, Math.Max(0, end.X - start.X), track.Height);
        context.PushClip(new RectangleGeometry(track, round, round));
        context.DrawRectangle(this.Resource<Brush>(fillKey), null, selected);
        context.Pop();
        var dot = this.Resource<double>(ThemeKeys.Space.Xs) * Em;
        foreach (var mark in Marks ?? [])
        {
            // 在已选范围里的点换成实底上的颜色
            var active = mark.Value >= from && mark.Value <= to;
            context.DrawEllipse(this.Resource<Brush>(active ? onFillKey : ThemeKeys.Brush.BorderStrong), null, PointOf(mark.Value, RenderSize), dot / 2, dot / 2);
        }
        // 键盘聚焦的滑块外面一圈焦点环
        foreach (var thumb in thumbs.Where(thumb => thumb.ShowsFocusRing))
        {
            var ring = new Rect(thumb.TranslatePoint(new Point(0, 0), this), thumb.RenderSize);
            ring.Inflate(2, 2);
            context.DrawEllipse(null, new Pen(this.Resource<Brush>(ThemeKeys.Brush.FocusRing), 2), new Point(ring.X + ring.Width / 2, ring.Y + ring.Height / 2), ring.Width / 2, ring.Height / 2);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SliderPeer(this);

    /// <summary>外层不占读屏节点：滑块才是（同 TS：外层是普通的 div）。</summary>
    sealed class SliderPeer(Slider owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>滑块上能拖、能聚焦的那个圆（TS 的 role="slider"）：surface 底、色调描边、sm 阴影；读屏是滑块，取值范围是它能取的范围。</summary>
public sealed class SliderThumb : FrameworkElement
{
    readonly Slider owner;
    bool focusVisible;

    internal SliderThumb(Slider owner, int index)
    {
        this.owner = owner;
        Index = index;
        Focusable = true;
        FocusVisualStyle = null;
        Cursor = Cursors.Hand;
        SetResourceReference(EffectProperty, ThemeKeys.Shadow.Sm);
        Refresh();
    }

    /// <summary>第几个滑块（范围时 0 是低的一端）。</summary>
    public int Index { get; }

    /// <summary>显示的值。</summary>
    public double Value => owner.Shown(Index);

    /// <summary>键盘聚焦着（Slider 画焦点环）。</summary>
    internal bool ShowsFocusRing => focusVisible && IsKeyboardFocused;

    /// <summary>重画（色调跟着滑块）；读屏的取值文字跟着值。</summary>
    internal void Refresh()
    {
        AutomationProperties.SetItemStatus(this, owner.ValueTextOf(Value) ?? "");
        InvalidateVisual();
    }

    /// <summary>surface 底、borderWidths.sm 的色调描边的圆。</summary>
    protected override void OnRender(DrawingContext context)
    {
        var (fill, _) = ToneFill.Keys(owner.Tone);
        var width = this.Resource<double>(ThemeKeys.BorderWidth.Sm);
        var radius = Math.Max(0, RenderSize.Width / 2 - width / 2);
        context.DrawEllipse(this.Resource<Brush>(ThemeKeys.Brush.Surface), new Pen(this.Resource<Brush>(fill), width), new Point(RenderSize.Width / 2, RenderSize.Height / 2), radius, radius);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && owner.Press(Index, e.Key)) e.Handled = true;
    }

    /// <summary>键盘聚焦时画焦点环（指针点进来不画，同 :focus-visible）。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        focusVisible = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        owner.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        focusVisible = false;
        owner.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ThumbPeer(this);

    /// <summary>
    /// 读屏：滑块，取值范围是这个滑块能取的（范围时互相限制）；名称是滑块的名称或字段标签，范围时后面接"最小值""最大值"；
    /// 取值文字（ValueText）在条目状态里；说明与错误是帮助文字。
    /// </summary>
    sealed class ThumbPeer(SliderThumb thumb) : FrameworkElementAutomationPeer(thumb), IRangeValueProvider
    {
        Slider Slider => thumb.owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string GetClassNameCore() => nameof(Slider);

        protected override string GetHelpTextCore() => Field.DescribedBy(Slider, base.GetHelpTextCore());

        protected override string GetNameCore()
        {
            var own = AutomationProperties.GetName(Slider) is { Length: > 0 } name ? name : Field.Of(Slider) is { } field && field.Owns(Slider) ? field.LabelText : null;
            if (!Slider.IsRange) return own ?? "";
            var lang = Provider.GetLang(Slider);
            var part = thumb.Index == 0 ? lang.SliderMin : lang.SliderMax;
            return string.IsNullOrEmpty(own) ? part : $"{own} {part}";
        }

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

        public bool IsReadOnly => Slider.ReadOnly || !Slider.IsEnabled;

        public double Value => thumb.Value;

        public double Minimum => Slider.BoundsOf(thumb.Index).Lower;

        public double Maximum => Slider.BoundsOf(thumb.Index).Upper;

        public double SmallChange => Slider.Step;

        public double LargeChange => Slider.BigStepValue;

        public void SetValue(double value)
        {
            if (IsReadOnly) return;
            Slider.Set(thumb.Index, value);
        }
    }
}
