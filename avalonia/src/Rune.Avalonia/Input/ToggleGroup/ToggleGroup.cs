using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 分段控件（vue-tsx-pro 的 ToggleGroup）：一组切换按钮 <see cref="ToggleItem"/>，单选像分段控件（视图切换、时间范围），多选像格式按钮（加粗、斜体）。
/// <code>
/// &lt;rune:ToggleGroup Value="{Binding View}" IsRequired="True" AutomationProperties.Name="视图"&gt;
///   &lt;rune:ToggleItem Value="list" Icon="{x:Static rune:Lucide.List}"&gt;列表&lt;/rune:ToggleItem&gt;
///   &lt;rune:ToggleItem Value="grid" Icon="{x:Static rune:Lucide.LayoutGrid}"&gt;卡片&lt;/rune:ToggleItem&gt;
/// &lt;/rune:ToggleGroup&gt;
/// </code>
/// 单选时 <see cref="Value"/> 是选中项的值，选中项下面一块 accentSoft 底、accentBorder 描边的滑块（切换时滑过去）；
/// 多选（<see cref="SelectionMode"/>=multiple）时 <see cref="Values"/> 是选中项的值的列表，每项各自开关（选中的是 surface 外观）。
/// <see cref="IsRequired"/> 时至少保留一项。外观：fill 底、md 圆角、四周与项之间 space.xs。整组一个 Tab 停靠点，方向键在组内移动（漫游焦点）。
/// 放进 Field 时以标签为名称，沿用字段的必填、只读、不可用。读屏里每项是切换按钮（TS 的 aria-pressed）。
/// </summary>
public class ToggleGroup : Flex, IFieldControl
{
    /// <summary>选择方式，默认 single。</summary>
    public static readonly StyledProperty<SelectionMode> SelectionModeProperty = AvaloniaProperty.Register<ToggleGroup, SelectionMode>(nameof(SelectionMode));

    /// <summary>单选：选中项的值（默认双向绑定）；没有选中时为空。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<ToggleGroup, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>多选：选中项的值（默认双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ValuesProperty =
        AvaloniaProperty.Register<ToggleGroup, IReadOnlyList<string>?>(nameof(Values), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>必须有选中项：最后一项不能取消（分段控件通常要求）；在必填的 Field 里默认开启。</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AvaloniaProperty.Register<ToggleGroup, bool>(nameof(IsRequired));

    /// <summary>整组只读：选中项照常显示、按钮能聚焦，点了不切换。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<ToggleGroup, bool>(nameof(IsReadOnly));

    /// <summary>方向，默认 horizontal。</summary>
    public static readonly StyledProperty<ToggleGroupOrientation> OrientationProperty = AvaloniaProperty.Register<ToggleGroup, ToggleGroupOrientation>(nameof(Orientation));

    /// <summary>字号档位，按钮随之缩放；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<ToggleGroup, TextSize>(nameof(Size));

    /// <summary>按下时的波纹；不写时沿用全局配置。</summary>
    public static readonly StyledProperty<bool?> RippleProperty = AvaloniaProperty.Register<ToggleGroup, bool?>(nameof(Ripple));

    /// <summary>滑块的位置与大小（相对组）。</summary>
    static readonly StyledProperty<Rect> IndicatorProperty = AvaloniaProperty.Register<ToggleGroup, Rect>("Indicator");

    RovingFocus? focus;
    readonly Decoration decoration;
    bool placed;

    /// <summary>新建：横排、间距 space.xs；底色与滑块画在最底下一层（不参加 Flex 的排版）。</summary>
    public ToggleGroup()
    {
        Gap = Space.Xs;
        Align = FlexAlign.Stretch;
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left;
        // ZIndex -1：画在所有项下面（Panel 按 Children 的下标插入可视子元素，下标会错开一位）
        decoration = new Decoration(this) { ZIndex = -1, IsHitTestVisible = false };
        VisualChildren.Insert(0, decoration);
        LayoutUpdated += (_, _) => PlaceIndicator();
    }

    /// <summary>选择方式。</summary>
    public SelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>单选的选中值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>多选的选中值。</summary>
    public IReadOnlyList<string>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>必须有选中项。</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>整组只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>方向。</summary>
    public ToggleGroupOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>按下时的波纹。</summary>
    public bool? Ripple { get => GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    bool Multiple => SelectionMode == SelectionMode.Multiple;

    /// <summary>单选：选中底色由滑块画，项本身不画。</summary>
    internal bool Sliding => !Multiple;

    /// <summary>必选：自己写了的优先，其次所在字段的。</summary>
    internal bool Required => IsSet(IsRequiredProperty) ? IsRequired : Field.Of(this)?.IsRequired ?? false;

    /// <summary>只读：自己写了的优先，其次所在字段的。</summary>
    internal bool ReadOnly => IsSet(IsReadOnlyProperty) ? IsReadOnly : Field.Of(this)?.IsReadOnly ?? false;

    /// <summary>选中的值（单选时最多一个；单选的空字符串算没有选中）。</summary>
    public IReadOnlyList<string> SelectedValues => Multiple ? Values ?? [] : string.IsNullOrEmpty(Value) ? [] : [Value];

    Rect Indicator { get => GetValue(IndicatorProperty); set => SetValue(IndicatorProperty, value); }

    IEnumerable<ToggleItem> Items => Children.OfType<ToggleItem>();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OrientationProperty)
        {
            var vertical = Orientation == ToggleGroupOrientation.Vertical;
            Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
            if (focus is not null) focus.Orientation = vertical ? "vertical" : "horizontal";
            focus?.Sync();
        }
        if (change.Property == SelectionModeProperty || change.Property == ValueProperty || change.Property == ValuesProperty || change.Property == SizeProperty
            || change.Property == RippleProperty || change.Property == IsReadOnlyProperty || change.Property == Field.OwnerProperty || change.Property == IsEnabledProperty)
            Sync();
        else if (change.Property == IndicatorProperty || change.Property == TextElement.FontSizeProperty) decoration.InvalidateVisual();
    }

    void IFieldControl.FieldChanged() => Sync();

    /// <summary>整组一个 Tab 停靠点、组内方向键移动；在工具栏里时焦点交给工具栏（整栏一个停靠点，方向键跨过组内外的按钮）。</summary>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (focus is null && this.FindLogicalAncestorOfType<Toolbar>() is null)
            focus = new RovingFocus(this, control => control is ToggleItem)
            {
                Loop = true,
                Orientation = Orientation == ToggleGroupOrientation.Vertical ? "vertical" : "horizontal",
                // Tab 进来时落在选中项上
                Preferred = control => control is ToggleItem { IsSelected: true },
            };
        Sync();
    }

    /// <summary>选中状态、外观交给各项；滑块对准选中项。</summary>
    internal void Sync()
    {
        var selected = SelectedValues;
        foreach (var item in Items) item.Apply(this, item.Value is { } value && selected.Contains(value));
        focus?.Sync();
        decoration.InvalidateVisual();
    }

    /// <summary>切换一项（单选、多选的规则见 <see cref="Selection.Toggle"/>）；只读时不切换。</summary>
    internal void Toggle(string value)
    {
        if (ReadOnly) return;
        var selected = SelectedValues;
        var next = Selection.Toggle(selected, value, SelectionMode, Required);
        if (ReferenceEquals(next, selected)) return;
        if (Multiple) SetCurrentValue(ValuesProperty, next);
        else SetCurrentValue(ValueProperty, next.FirstOrDefault());
    }

    /// <summary>滑块对准选中项：第一次直接到位，之后滑过去（durations.md、standard，系统关闭动画时直接到位）。</summary>
    void PlaceIndicator()
    {
        if (!Sliding || Items.FirstOrDefault(item => item.IsSelected) is not { IsArrangeValid: true } selected) return;
        var bounds = selected.Bounds;
        if (bounds == Indicator) return;
        if (!placed)
        {
            placed = true;
            Transitions = null;
        }
        else if (Transitions is null)
        {
            var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
            if (duration > TimeSpan.Zero)
                Transitions = [new RectTransition { Property = IndicatorProperty, Duration = duration, Easing = this.Resource<Easing>(ThemeKeys.Easing.Standard) }];
        }
        Indicator = bounds;
    }

    /// <summary>四周 space.xs 的内边距（Flex 没有 Padding：量之前收进去、排完再挪过去）。</summary>
    Thickness Inset => new(0.25 * TextElement.GetFontSize(this));

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Inset;
        decoration.Measure(availableSize);
        return base.MeasureOverride(availableSize.Deflate(inset)).Inflate(inset);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = Inset;
        decoration.Arrange(new Rect(finalSize));
        base.ArrangeOverride(finalSize.Deflate(inset));
        foreach (var child in Children) child.Arrange(child.Bounds.Translate(new Vector(inset.Left, inset.Top)));
        return finalSize;
    }

    /// <summary>fill 底、md 圆角；单选时选中项下面 accentSoft 底、xs 宽的 accentBorder 描边、sm 圆角的滑块。</summary>
    void Paint(DrawingContext context)
    {
        var em = TextElement.GetFontSize(this);
        if (this.Resource<IBrush?>(ThemeKeys.Brush.Fill) is { } fill)
            context.DrawRectangle(fill, null, new RoundedRect(new Rect(Bounds.Size), this.Resource<double>(ThemeKeys.Radius.Md) * em));
        if (!Sliding || SelectedValues.Count == 0 || Indicator.Width <= 0) return;
        var pen = this.Resource<IBrush?>(ThemeKeys.Brush.AccentBorder) is { } border ? new Pen(border, 1) : null;
        var radius = Items.FirstOrDefault(item => item.IsSelected)?.CornerRadius.TopLeft ?? this.Resource<double>(ThemeKeys.Radius.Sm) * em;
        context.DrawRectangle(this.Resource<IBrush?>(ThemeKeys.Brush.AccentSoft), pen, new RoundedRect(Snap(Indicator).Deflate(0.5), Math.Max(0, radius - 0.5)));
    }

    /// <summary>
    /// 滑块的四边吸附到设备像素（同浏览器画盒子的边）：组的内边距 0.25em 带小数，不吸附时 1 像素的描边压在两行之间发虚。
    /// 按到根的偏移算，组自己可能不在整像素上。
    /// </summary>
    Rect Snap(Rect rect)
    {
        if (VisualRoot is not Visual root || this.TranslatePoint(default, root) is not { } offset) return rect;
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        double Round(double value, double at) => Math.Round((value + at) * scale, MidpointRounding.AwayFromZero) / scale - at;
        var (left, top) = (Round(rect.Left, offset.X), Round(rect.Top, offset.Y));
        return new Rect(left, top, Math.Max(0, Round(rect.Right, offset.X) - left), Math.Max(0, Round(rect.Bottom, offset.Y) - top));
    }

    /// <summary>最底下一层：只画组的底色与滑块（Panel 的 Render 是密封的）。</summary>
    sealed class Decoration(ToggleGroup owner) : Control
    {
        public override void Render(DrawingContext context) => owner.Paint(context);
    }

    /// <summary>滑块的位置与大小一起过渡（平台没有 Rect 的过渡）。</summary>
    sealed class RectTransition : InterpolatingTransitionBase<Rect>
    {
        protected override Rect Interpolate(double progress, Rect from, Rect to) => new(
            from.X + (to.X - from.X) * progress, from.Y + (to.Y - from.Y) * progress,
            from.Width + (to.Width - from.Width) * progress, from.Height + (to.Height - from.Height) * progress);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleGroupPeer(this);

    sealed class ToggleGroupPeer(ToggleGroup owner) : ControlAutomationPeer(owner)
    {
        protected override string? GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(ToggleGroup);

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;
    }
}

/// <summary>
/// 切换按钮组里的一项（TS 的 ToggleItem）：同按钮的外观——没选中是中性色的 ghost；选中时品牌色，单选时底色是组的滑块，多选时是 surface。
/// 只有图标（没有 Content）时是方的，要写读屏名称。点击先发 Click（标记 Handled 可以拦下切换）。读屏里是切换按钮（选中是 On）。
/// </summary>
public class ToggleItem : Button
{
    /// <summary>这一项的值。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<ToggleItem, string?>(nameof(Value));

    /// <summary>选中着（按下）。</summary>
    public static readonly DirectProperty<ToggleItem, bool> IsSelectedProperty = AvaloniaProperty.RegisterDirect<ToggleItem, bool>(nameof(IsSelected), item => item.IsSelected);

    ToggleGroup? owner;

    /// <summary>这一项的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选中着。</summary>
    public bool IsSelected { get; private set => SetAndRaise(IsSelectedProperty, ref field, value); }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ToggleItem);

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        (Parent as ToggleGroup)?.Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) owner?.Sync();
        else if (change.Property == ContentProperty) PseudoClasses.Set(":square", Content is null);
    }

    /// <summary>跟着组：选中状态、外观（样式、色调、字号、波纹）。外观写在模板优先级上：主题的默认值之上、使用方的样式之下。</summary>
    internal void Apply(ToggleGroup group, bool selected)
    {
        owner = group;
        IsSelected = selected;
        PseudoClasses.Set(":selected", selected);
        PseudoClasses.Set(":square", Content is null);
        // 只读、没选中的项：不像能点（悬停也不出底色）
        PseudoClasses.Set(":readonly", group.ReadOnly);
        SetValue(VariantProperty, selected && !group.Sliding ? ButtonVariant.Surface : ButtonVariant.Ghost, BindingPriority.Template);
        SetValue(ToneProperty, selected ? StatusTone.Accent : StatusTone.Neutral, BindingPriority.Template);
        if (group.Size.Value is not null) SetValue(SizeProperty, group.Size, BindingPriority.Template);
        else ClearValue(SizeProperty);
        if (group.ReadOnly) SetValue(RippleProperty, false, BindingPriority.Template);
        else if (group.Ripple is { } ripple) SetValue(RippleProperty, ripple, BindingPriority.Template);
        else ClearValue(RippleProperty);
    }

    /// <summary>点击：先发 Click（使用方的处理先执行，标记 Handled 可以拦下），再交给组切换。</summary>
    protected override void OnClick()
    {
        if (IsDisabled || IsLoading) return;
        var click = new RoutedEventArgs(ClickEvent);
        RaiseEvent(click);
        if (click.Handled) return;
        if (Command is { } command && command.CanExecute(CommandParameter)) command.Execute(CommandParameter);
        if (Value is { } value) owner?.Toggle(value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleItemPeer(this);

    sealed class ToggleItemPeer(ToggleItem owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string GetClassNameCore() => nameof(ToggleItem);

        public ToggleState ToggleState => owner.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle()
        {
            if (!owner.IsDisabled && owner.Value is { } value) owner.owner?.Toggle(value);
        }
    }
}
