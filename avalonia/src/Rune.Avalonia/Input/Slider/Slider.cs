using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 滑块（vue-tsx-pro 的 Slider）：在一个范围里拖动选值（音量、进度、价格）。
/// <code>
/// &lt;rune:Slider Value="{Binding Volume}" AutomationProperties.Name="音量" /&gt;
/// &lt;rune:Slider Value="{Binding Price}" Min="0" Max="1000" Step="50" /&gt;
/// &lt;rune:Slider Range="{Binding PriceRange}" Marks="0,500,1000" /&gt;      &lt;!-- 范围：两个滑块 --&gt;
/// </code>
/// 方向键 ±Step，Page Up / Page Down ±BigStep，Home / End 到两端；从右到左排版时横向的方向键反过来。点击轨道任意位置直接跳过去并接着拖动。
/// <see cref="Range"/> 不为 null 时是范围选择：两个滑块各自可聚焦、可拖动，不能交叉；点击轨道移动离得近的那个，读屏名称后面加"最小值""最大值"。
/// 一次操作结束（松开、按键）时触发 <see cref="ValueCommitted"/>：保存、发请求放在这里，不必每次移动都发。
/// 轨道 fill 底、1.5 倍 space.xs 粗；已选部分是色调的实底；滑块是 space.lg + space.xs 的圆，surface 底、色调描边、sm 阴影。
/// 点击区域至少 sizes.minTarget；滑块中心对准取值，两端各留半个滑块（滑块在两端时不伸出控件）。
/// </summary>
public class Slider : Control, IFieldControl, global::Avalonia.Rendering.ICustomHitTest
{
    /// <summary>单值（默认双向绑定）。</summary>
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<Slider, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>范围选择的取值（默认双向绑定）；不为 null 时是两个滑块。</summary>
    public static readonly StyledProperty<SliderRange?> RangeProperty = AvaloniaProperty.Register<Slider, SliderRange?>(nameof(Range), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>最小值，默认 0。</summary>
    public static readonly StyledProperty<double> MinProperty = AvaloniaProperty.Register<Slider, double>(nameof(Min));

    /// <summary>最大值，默认 100。</summary>
    public static readonly StyledProperty<double> MaxProperty = AvaloniaProperty.Register<Slider, double>(nameof(Max), 100);

    /// <summary>步长，默认 1；取值总是对齐到 Min + n × Step。</summary>
    public static readonly StyledProperty<double> StepProperty = AvaloniaProperty.Register<Slider, double>(nameof(Step), 1, validate: step => step > 0);

    /// <summary>Page Up / Page Down 的步长；不写时为范围的十分之一（至少一个 Step）。</summary>
    public static readonly StyledProperty<double?> BigStepProperty = AvaloniaProperty.Register<Slider, double?>(nameof(BigStep));

    /// <summary>方向，默认横向。</summary>
    public static readonly StyledProperty<SliderOrientation> OrientationProperty = AvaloniaProperty.Register<Slider, SliderOrientation>(nameof(Orientation));

    /// <summary>只读：滑块能聚焦，拖动与方向键不改值；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<Slider, bool?>(nameof(IsReadOnly));

    /// <summary>字号档位：滑块与轨道随之缩放；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Slider, TextSize>(nameof(Size));

    /// <summary>已选范围与滑块描边的色调，默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Slider, StatusTone>(nameof(Tone));

    /// <summary>刻度：值或带文字的刻度；有文字时下方（纵向时右侧）留出位置。</summary>
    public static readonly StyledProperty<IReadOnlyList<SliderMark>?> MarksProperty = AvaloniaProperty.Register<Slider, IReadOnlyList<SliderMark>?>(nameof(Marks));

    /// <summary>字号（尺寸按它算）：同 TextElement.FontSize，继承外面的。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Slider>();

    readonly TemplateResources resources;
    readonly List<SliderThumb> thumbs = [];
    readonly List<Text> labels = [];
    Func<double, string>? valueText;
    int dragging = -1;

    static Slider()
    {
        AffectsRender<Slider>(ValueProperty, RangeProperty, MinProperty, MaxProperty, ToneProperty);
        AffectsMeasure<Slider>(FontSizeProperty, OrientationProperty, MarksProperty);
        AffectsArrange<Slider>(ValueProperty, RangeProperty, MinProperty, MaxProperty);
    }

    /// <summary>新建：0–100、步长 1 的单值滑块。</summary>
    public Slider()
    {
        resources = new TemplateResources(this);
        this.RedrawOnResourceChanges(this);
        Cursor = new Cursor(StandardCursorType.Hand);
        Rebuild();
    }

    /// <summary>单值。</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>范围选择的取值。</summary>
    public SliderRange? Range { get => GetValue(RangeProperty); set => SetValue(RangeProperty, value); }

    /// <summary>最小值。</summary>
    public double Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最大值。</summary>
    public double Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>步长。</summary>
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>Page 键的步长。</summary>
    public double? BigStep { get => GetValue(BigStepProperty); set => SetValue(BigStepProperty, value); }

    /// <summary>方向。</summary>
    public SliderOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>刻度。</summary>
    public IReadOnlyList<SliderMark>? Marks { get => GetValue(MarksProperty); set => SetValue(MarksProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>读屏念的取值文字（TS 的 aria-valuetext），如 <c>v =&gt; $"{v}%"</c>；不写时念数字。</summary>
    public Func<double, string>? ValueText
    {
        get => valueText;
        set
        {
            valueText = value;
            foreach (var thumb in thumbs) thumb.Refresh();
        }
    }

    /// <summary>一次操作结束（松开指针、按键）时触发。</summary>
    public event EventHandler? ValueCommitted;

    /// <summary>滑块（测试用）：单值时一个，范围时两个。</summary>
    internal IReadOnlyList<SliderThumb> Thumbs => thumbs;

    /// <summary>刻度文字（测试用）。</summary>
    internal IReadOnlyList<Text> MarkLabels => labels;

    internal bool IsRange => Range is not null;

    internal bool ReadOnly => IsReadOnly ?? Field.ReadOnlyOf(this);

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

    void IFieldControl.FieldChanged()
    {
        foreach (var thumb in thumbs) thumb.Refresh();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RangeProperty && (change.OldValue is null) != (change.NewValue is null) || change.Property == MarksProperty) Rebuild();
        else if (change.Property == ValueProperty || change.Property == RangeProperty || change.Property == MinProperty || change.Property == MaxProperty)
            foreach (var thumb in thumbs) thumb.Refresh();
        else if (change.Property == ToneProperty || change.Property == IsReadOnlyProperty || change.Property == Field.OwnerProperty)
            foreach (var thumb in thumbs) thumb.Refresh();
        else if (change.Property == SizeProperty)
        {
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        // 不可用时整体（含滑块、刻度文字）半透明
        else if (change.Property == IsEffectivelyEnabledProperty) resources.Bind(OpacityProperty, IsEffectivelyEnabled ? null : ThemeKeys.Opacity.Disabled);
    }

    /// <summary>按单值 / 范围建滑块，按刻度建文字。</summary>
    void Rebuild()
    {
        VisualChildren.Clear();
        LogicalChildren.Clear();
        thumbs.Clear();
        labels.Clear();
        foreach (var mark in Marks ?? [])
        {
            if (mark.Label is null) continue;
            var label = new Text { Text = mark.Label, Size = TextSize.Sm, Tone = TextTone.Muted, Wrap = TextWrap.NoWrap, IsHitTestVisible = false };
            labels.Add(label);
            VisualChildren.Add(label);
            LogicalChildren.Add(label);
        }
        for (var index = 0; index < Values.Length; index++)
        {
            var thumb = new SliderThumb(this, index);
            thumbs.Add(thumb);
            VisualChildren.Add(thumb);
            LogicalChildren.Add(thumb);
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

    bool Editable => IsEffectivelyEnabled && !ReadOnly;

    /// <summary>轨道的矩形（滑块中心能到的范围）；排版时按要排的尺寸（那时 Bounds 还是上一次的）。</summary>
    Rect TrackRect(LayoutSize? size = null)
    {
        var half = ThumbSize / 2;
        var thickness = TrackThickness;
        var box = ContentBox(size ?? Bounds.Size);
        return Vertical
            ? new Rect(box.X + (box.Width - thickness) / 2, box.Y + half, thickness, Math.Max(0, box.Height - 2 * half))
            : new Rect(box.X + half, box.Y + (box.Height - thickness) / 2, Math.Max(0, box.Width - 2 * half), thickness);
    }

    /// <summary>滑块与轨道所在的区域（去掉刻度文字留的位置）。</summary>
    Rect ContentBox(LayoutSize size)
    {
        var reserve = Labelled ? (Vertical ? 3 : 1.5) * Em : 0;
        return Vertical ? new Rect(0, 0, Math.Max(0, size.Width - reserve), size.Height) : new Rect(0, 0, size.Width, Math.Max(0, size.Height - reserve));
    }

    /// <summary>值在轨道上的中心点。</summary>
    Point PointOf(double value, LayoutSize? size = null)
    {
        var track = TrackRect(size);
        var ratio = SliderScale.Ratio(value, Min, Max);
        return Vertical ? new Point(track.Center.X, track.Bottom - ratio * track.Height) : new Point(track.X + ratio * track.Width, track.Center.Y);
    }

    /// <summary>指针位置对应的值（Avalonia 从右到左排版时整个镜像，这里按从左往右算）。</summary>
    double ValueAt(Point point)
    {
        var track = TrackRect();
        var ratio = Vertical ? (track.Bottom - point.Y) / Math.Max(1, track.Height) : (point.X - track.X) / Math.Max(1, track.Width);
        return SliderScale.ValueAt(ratio, Min, Max, Step);
    }

    /// <summary>整块（含两端与刻度文字的位置）都能点：轨道很细，只按画出来的部分命中就很难点中。</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!Editable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var target = ValueAt(e.GetPosition(this));
        dragging = SliderScale.Nearest(target, Values);
        // 焦点交给滑块，键盘接着可以微调
        thumbs[dragging].Focus(NavigationMethod.Pointer);
        Set(dragging, target);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (dragging >= 0) Set(dragging, ValueAt(e.GetPosition(this)));
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (dragging < 0) return;
        dragging = -1;
        e.Pointer.Capture(null);
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        dragging = -1;
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        foreach (var label in labels) label.Measure(LayoutSize.Infinity);
        foreach (var thumb in thumbs) thumb.Measure(new LayoutSize(ThumbSize, ThumbSize));
        var cross = Math.Max(this.Resource<double>(ThemeKeys.Size.MinTarget), ThumbSize) + (Labelled ? (Vertical ? 3 : 1.5) * Em : 0);
        // 横向时尽量占满给的宽度（同块级元素）；纵向默认 10em 高
        return Vertical
            ? new LayoutSize(cross, double.IsNaN(Height) ? 10 * Em : Height)
            : new LayoutSize(double.IsInfinity(availableSize.Width) ? 10 * Em : availableSize.Width, cross);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
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
                ? new Point(track.Center.X + this.Resource<double>(ThemeKeys.Space.Md) * Em, center.Y - label.Height / 2)
                : new Point(center.X - label.Width / 2, track.Center.Y + this.Resource<double>(ThemeKeys.Space.Sm) * Em);
            labels[i].Arrange(new Rect(origin, label));
        }
        return finalSize;
    }

    /// <summary>画轨道、已选范围与刻度点；滑块是子元素自己画。</summary>
    public override void Render(DrawingContext context)
    {
        var track = TrackRect();
        if (track.Width <= 0 || track.Height <= 0) return;
        var (fillKey, onFillKey) = ToneFill.Keys(Tone);
        var round = Math.Min(track.Width, track.Height) / 2;
        context.DrawRectangle(this.Resource<IBrush>(ThemeKeys.Brush.Fill), null, new RoundedRect(track, round));
        var values = Enumerable.Range(0, Values.Length).Select(Shown).ToArray();
        var (from, to) = values.Length > 1 ? (values[0], values[1]) : (Min, values[0]);
        var (start, end) = (PointOf(from), PointOf(to));
        var selected = Vertical ? new Rect(track.X, end.Y, track.Width, Math.Max(0, start.Y - end.Y)) : new Rect(start.X, track.Y, Math.Max(0, end.X - start.X), track.Height);
        using (context.PushClip(new RoundedRect(track, round)))
            context.FillRectangle(this.Resource<IBrush>(fillKey), selected);
        var dot = this.Resource<double>(ThemeKeys.Space.Xs) * Em;
        foreach (var mark in Marks ?? [])
        {
            var center = PointOf(mark.Value);
            // 在已选范围里的点换成实底上的颜色
            var active = mark.Value >= from && mark.Value <= to;
            context.DrawEllipse(this.Resource<IBrush>(active ? onFillKey : ThemeKeys.Brush.BorderStrong), null, center, dot / 2, dot / 2);
        }
        // 键盘聚焦的滑块外面一圈焦点环（滑块是子元素，画在它下面也露在外圈）
        foreach (var thumb in thumbs.Where(thumb => thumb.IsFocused && thumb.ShowsFocusRing))
        {
            var ring = thumb.Bounds.Inflate(2);
            context.DrawEllipse(null, new Pen(this.Resource<IBrush>(ThemeKeys.Brush.FocusRing), 2), ring.Center, ring.Width / 2, ring.Height / 2);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SliderPeer(this);

    /// <summary>外层不占读屏节点：滑块才是（同 TS：外层是普通的 div）。</summary>
    sealed class SliderPeer(Slider owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>滑块上能拖、能聚焦的那个圆（TS 的 role="slider"）：surface 底、色调描边、sm 阴影；读屏是滑块，取值范围是它能取的范围。</summary>
public sealed class SliderThumb : Border
{
    readonly Slider owner;
    readonly TemplateResources resources;
    bool focusVisible;

    internal SliderThumb(Slider owner, int index)
    {
        this.owner = owner;
        Index = index;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        resources = new TemplateResources(this);
        resources.Bind(BackgroundProperty, ThemeKeys.Brush.Surface);
        resources.Bind(BoxShadowProperty, ThemeKeys.Shadow.Sm);
        Refresh();
    }

    /// <summary>第几个滑块（范围时 0 是低的一端）。</summary>
    public int Index { get; }

    /// <summary>显示的值。</summary>
    public double Value => owner.Shown(Index);

    /// <summary>描边色调、粗细与圆角跟着滑块的色调与字号；读屏的取值跟着值。</summary>
    internal void Refresh()
    {
        var (fill, _) = ToneFill.Keys(owner.Tone);
        resources.Bind(BorderBrushProperty, fill);
        var width = this.Resource<double>(ThemeKeys.BorderWidth.Sm);
        BorderThickness = new Thickness(width);
        AutomationProperties.SetItemStatus(this, owner.ValueTextOf(Value));
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void ArrangeCore(Rect finalRect)
    {
        CornerRadius = new CornerRadius(finalRect.Width / 2);
        base.ArrangeCore(finalRect);
    }

    /// <summary>键盘聚焦（Slider 在外圈画焦点环：Border 的 Render 是密封的）。</summary>
    internal bool ShowsFocusRing => focusVisible;

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && owner.Press(Index, e.Key)) e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        owner.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        focusVisible = false;
        owner.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ThumbPeer(this);

    /// <summary>
    /// 读屏：滑块，取值范围是这个滑块能取的（范围时互相限制）；名称是滑块的名称或字段标签，范围时后面接"最小值""最大值"；
    /// 取值文字（ValueText）在条目状态里；说明与错误是帮助文字。
    /// </summary>
    sealed class ThumbPeer(SliderThumb thumb) : ControlAutomationPeer(thumb), IRangeValueProvider
    {
        Slider Slider => thumb.owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

        protected override string GetClassNameCore() => nameof(Slider);

        protected override string? GetHelpTextCore() => Field.DescribedBy(Slider, base.GetHelpTextCore());

        protected override string? GetNameCore()
        {
            var own = AutomationProperties.GetName(Slider) is { Length: > 0 } name ? name : Field.Of(Slider) is { } field && field.Owns(Slider) ? field.LabelText : null;
            if (!Slider.IsRange) return own;
            var lang = Provider.GetLang(Slider);
            var part = thumb.Index == 0 ? lang.SliderMin : lang.SliderMax;
            return string.IsNullOrEmpty(own) ? part : $"{own} {part}";
        }

        public bool IsReadOnly => Slider.ReadOnly || !Slider.IsEffectivelyEnabled;

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
