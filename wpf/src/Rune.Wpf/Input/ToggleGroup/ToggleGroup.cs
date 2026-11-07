using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

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
/// <see cref="IsRequired"/> 时至少保留一项。外观：fill 底、md 圆角、四周与项之间 space.xs。整组一个 Tab 停靠点（选中项），方向键在组内移动；
/// 在工具栏里时焦点交给工具栏。放进 Field 时以标签为名称，沿用字段的必填、只读、不可用。读屏里每项是切换按钮（TS 的 aria-pressed）。
/// </summary>
public class ToggleGroup : Flex, IFieldControl
{
    /// <summary>选择方式，默认 single。</summary>
    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(nameof(SelectionMode), typeof(SelectionMode), typeof(ToggleGroup),
        new FrameworkPropertyMetadata(SelectionMode.Single, (d, _) => ((ToggleGroup)d).Sync()));

    /// <summary>单选：选中项的值（默认双向绑定）；没有选中时为空。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(ToggleGroup),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ToggleGroup)d).Sync()));

    /// <summary>多选：选中项的值（默认双向绑定）。</summary>
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(IReadOnlyList<string>), typeof(ToggleGroup),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ToggleGroup)d).Sync()));

    /// <summary>必须有选中项：最后一项不能取消（分段控件通常要求）；在必填的 Field 里默认开启。</summary>
    public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(ToggleGroup));

    /// <summary>整组只读：选中项照常显示、按钮能聚焦，点了不切换。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(ToggleGroup),
        new FrameworkPropertyMetadata(false, (d, _) => ((ToggleGroup)d).Sync()));

    /// <summary>方向，默认 horizontal。</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(ToggleGroupOrientation), typeof(ToggleGroup),
        new FrameworkPropertyMetadata(ToggleGroupOrientation.Horizontal, (d, _) => ((ToggleGroup)d).ApplyOrientation()));

    /// <summary>字号档位，按钮随之缩放；不写时跟着外面。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(ToggleGroup),
        new FrameworkPropertyMetadata(default(TextSize), (d, _) => ((ToggleGroup)d).Sync()));

    /// <summary>按下时的波纹；不写时沿用全局配置。</summary>
    public static readonly DependencyProperty RippleProperty = DependencyProperty.Register(nameof(Ripple), typeof(bool?), typeof(ToggleGroup),
        new FrameworkPropertyMetadata(null, (d, _) => ((ToggleGroup)d).Sync()));

    /// <summary>滑块的位置与大小（相对组）。</summary>
    static readonly DependencyProperty IndicatorProperty = DependencyProperty.Register("Indicator", typeof(Rect), typeof(ToggleGroup),
        new FrameworkPropertyMetadata(Rect.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    RovingFocus? focus;
    bool placed;

    /// <summary>新建：横排、间距 space.xs、宽度按内容；挂上来时决定焦点归谁管。</summary>
    public ToggleGroup()
    {
        Gap = Space.Xs;
        HorizontalAlignment = HorizontalAlignment.Left;
        Loaded += (_, _) =>
        {
            if (focus is null && !InToolbar())
                focus = new RovingFocus(this, element => element is ToggleItem)
                {
                    Loop = true,
                    Orientation = Orientation == ToggleGroupOrientation.Vertical ? "vertical" : "horizontal",
                    // Tab 进来时落在选中项上
                    Preferred = element => element is ToggleItem { IsSelected: true },
                };
            Sync();
        };
        LayoutUpdated += (_, _) => PlaceIndicator();
    }

    /// <summary>选择方式。</summary>
    public SelectionMode SelectionMode { get => (SelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>单选的选中值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>多选的选中值。</summary>
    public IReadOnlyList<string>? Values { get => (IReadOnlyList<string>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <summary>必须有选中项。</summary>
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>整组只读。</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>方向。</summary>
    public ToggleGroupOrientation Orientation { get => (ToggleGroupOrientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>按下时的波纹。</summary>
    public bool? Ripple { get => (bool?)GetValue(RippleProperty); set => SetValue(RippleProperty, value); }

    bool Multiple => SelectionMode == SelectionMode.Multiple;

    /// <summary>单选：选中底色由滑块画，项本身不画。</summary>
    internal bool Sliding => !Multiple;

    /// <summary>必选：自己写了的优先，其次所在字段的。</summary>
    internal bool Required => Field.IsSet(this, IsRequiredProperty) ? IsRequired : Field.Of(this)?.IsRequired ?? false;

    /// <summary>只读：自己写了的优先，其次所在字段的。</summary>
    internal bool ReadOnly => Field.IsSet(this, IsReadOnlyProperty) ? IsReadOnly : Field.Of(this)?.IsReadOnly ?? false;

    /// <summary>选中的值（单选时最多一个；单选的空字符串算没有选中）。</summary>
    public IReadOnlyList<string> SelectedValues => Multiple ? Values ?? [] : string.IsNullOrEmpty(Value) ? [] : [Value];

    IEnumerable<ToggleItem> Items => Children.OfType<ToggleItem>();

    bool InToolbar()
    {
        for (var parent = LogicalTreeHelper.GetParent(this) ?? VisualTreeHelper.GetParent(this); parent is not null; parent = LogicalTreeHelper.GetParent(parent) ?? VisualTreeHelper.GetParent(parent))
            if (parent is Toolbar) return true;
        return false;
    }

    void ApplyOrientation()
    {
        var vertical = Orientation == ToggleGroupOrientation.Vertical;
        Direction = vertical ? FlexDirection.Column : FlexDirection.Row;
        if (focus is not null) focus.Orientation = vertical ? "vertical" : "horizontal";
        focus?.Sync();
    }

    void IFieldControl.FieldChanged() => Sync();

    /// <summary>项加进来时就同步：选中项、外观在加载前定好。</summary>
    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        Sync();
    }

    /// <summary>选中状态、外观交给各项；滑块对准选中项。</summary>
    internal void Sync()
    {
        var selected = SelectedValues;
        foreach (var item in Items) item.Apply(this, item.Value is { } value && selected.Contains(value));
        focus?.Sync();
        InvalidateVisual();
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
        if (!Sliding || Items.FirstOrDefault(item => item.IsSelected) is not { IsArrangeValid: true, ActualWidth: > 0 } selected) return;
        var bounds = new Rect(selected.TranslatePoint(default, this), selected.RenderSize);
        if (bounds == (Rect)GetValue(IndicatorProperty) && placed) return;
        var duration = placed ? Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200)) : TimeSpan.Zero;
        placed = true;
        if (duration <= TimeSpan.Zero)
        {
            BeginAnimation(IndicatorProperty, null);
            SetValue(IndicatorProperty, bounds);
            return;
        }
        BeginAnimation(IndicatorProperty, new RectAnimation(bounds, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction });
    }

    /// <summary>四周 space.xs 的内边距（Flex 没有 Padding：量之前收进去、排完再挪过去）。</summary>
    Thickness Inset => new(0.25 * TextElement.GetFontSize(this));

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Inset;
        var inner = base.MeasureOverride(new Size(Math.Max(0, availableSize.Width - inset.Left - inset.Right), Math.Max(0, availableSize.Height - inset.Top - inset.Bottom)));
        return new Size(inner.Width + inset.Left + inset.Right, inner.Height + inset.Top + inset.Bottom);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = Inset;
        base.ArrangeOverride(new Size(Math.Max(0, finalSize.Width - inset.Left - inset.Right), Math.Max(0, finalSize.Height - inset.Top - inset.Bottom)));
        foreach (UIElement child in InternalChildren)
        {
            var slot = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot((FrameworkElement)child);
            child.Arrange(new Rect(slot.X + inset.Left, slot.Y + inset.Top, slot.Width, slot.Height));
        }
        return finalSize;
    }

    /// <summary>fill 底、md 圆角；单选时选中项下面 accentSoft 底、xs 宽的 accentBorder 描边、圆角同项的滑块。</summary>
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var em = TextElement.GetFontSize(this);
        var radius = (TryFindResource("Rune.Radius.Md") as double? ?? 0.375) * em;
        if (TryFindResource("Rune.Brush.Fill") is Brush fill) context.DrawRoundedRectangle(fill, null, new Rect(RenderSize), radius, radius);
        var indicator = (Rect)GetValue(IndicatorProperty);
        if (!Sliding || SelectedValues.Count == 0 || indicator.IsEmpty || indicator.Width <= 0) return;
        var pen = TryFindResource("Rune.Brush.AccentBorder") is Brush border ? new Pen(border, 1) : null;
        var corner = Math.Max(0, (Items.FirstOrDefault(item => item.IsSelected)?.CornerRadius.TopLeft ?? 0.25 * em) - 0.5);
        var inner = new Rect(indicator.X + 0.5, indicator.Y + 0.5, Math.Max(0, indicator.Width - 1), Math.Max(0, indicator.Height - 1));
        // 滑块的四边吸附到设备像素（同浏览器画盒子的边）：组的内边距 0.25em 带小数，不吸附时 1 像素的描边压在两行之间发虚
        context.PushGuidelineSet(new GuidelineSet([indicator.Left, indicator.Right], [indicator.Top, indicator.Bottom]));
        context.DrawRoundedRectangle(TryFindResource("Rune.Brush.AccentSoft") as Brush, pen, inner, corner, corner);
        context.Pop();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleGroupPeer(this);

    sealed class ToggleGroupPeer(ToggleGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(ToggleGroup);

        protected override string GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText ?? "" : "";
    }
}

/// <summary>
/// 切换按钮组里的一项（TS 的 ToggleItem）：同按钮的外观——没选中是中性色的 ghost；选中时品牌色，单选时底色是组的滑块，多选时是 surface。
/// 只有图标（没有 Content）时是方的，要写读屏名称。点击先发 Click（标记 Handled 可以拦下切换）。读屏里是切换按钮（选中是 On）。
/// 外观在主题样式里按只读的 IsSelected、IsSliding、IsGroupReadOnly 触发。
/// </summary>
public class ToggleItem : Button
{
    /// <summary>这一项的值。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(ToggleItem),
        new FrameworkPropertyMetadata(null, (d, _) => ((ToggleItem)d).owner?.Sync()));

    static readonly DependencyPropertyKey IsSelectedKey = DependencyProperty.RegisterReadOnly(nameof(IsSelected), typeof(bool), typeof(ToggleItem), new PropertyMetadata(false));

    /// <summary>选中着（按下）。</summary>
    public static readonly DependencyProperty IsSelectedProperty = IsSelectedKey.DependencyProperty;

    static readonly DependencyPropertyKey IsSlidingKey = DependencyProperty.RegisterReadOnly(nameof(IsSliding), typeof(bool), typeof(ToggleItem), new PropertyMetadata(true));

    /// <summary>所在的组是单选（选中底色由组的滑块画）。</summary>
    public static readonly DependencyProperty IsSlidingProperty = IsSlidingKey.DependencyProperty;

    static readonly DependencyPropertyKey IsGroupReadOnlyKey = DependencyProperty.RegisterReadOnly(nameof(IsGroupReadOnly), typeof(bool), typeof(ToggleItem), new PropertyMetadata(false));

    /// <summary>所在的组只读。</summary>
    public static readonly DependencyProperty IsGroupReadOnlyProperty = IsGroupReadOnlyKey.DependencyProperty;

    ToggleGroup? owner;

    static ToggleItem() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleItem), new FrameworkPropertyMetadata(typeof(ToggleItem)));

    /// <summary>这一项的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>选中着。</summary>
    public bool IsSelected => (bool)GetValue(IsSelectedProperty);

    /// <summary>所在的组是单选。</summary>
    public bool IsSliding => (bool)GetValue(IsSlidingProperty);

    /// <summary>所在的组只读。</summary>
    public bool IsGroupReadOnly => (bool)GetValue(IsGroupReadOnlyProperty);

    /// <summary>跟着组：选中状态、单选与否、只读；字号、波纹跟着组。</summary>
    internal void Apply(ToggleGroup group, bool selected)
    {
        owner = group;
        SetValue(IsSelectedKey, selected);
        SetValue(IsSlidingKey, group.Sliding);
        SetValue(IsGroupReadOnlyKey, group.ReadOnly);
        if (group.Size.Value is not null) Size = group.Size;
        else ClearValue(SizeProperty);
        if (group.ReadOnly) Ripple = false;
        else if (group.Ripple is { } ripple) Ripple = ripple;
        else ClearValue(RippleProperty);
    }

    /// <summary>点击：先发 Click（使用方的处理先执行，标记 Handled 可以拦下），再交给组切换。</summary>
    protected override void OnClick()
    {
        if (IsLoading || IsDisabled) return;
        var click = new RoutedEventArgs(ClickEvent, this);
        RaiseEvent(click);
        if (click.Handled) return;
        if (Command is { } command && command.CanExecute(CommandParameter)) command.Execute(CommandParameter);
        if (Value is { } value) owner?.Toggle(value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleItemPeer(this);

    sealed class ToggleItemPeer(ToggleItem owner) : ButtonBaseAutomationPeer(owner), IToggleProvider
    {
        protected override string GetClassNameCore() => nameof(ToggleItem);

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);

        public ToggleState ToggleState => owner.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle()
        {
            if (!owner.IsDisabled && owner.Value is { } value) owner.owner?.Toggle(value);
        }
    }
}
