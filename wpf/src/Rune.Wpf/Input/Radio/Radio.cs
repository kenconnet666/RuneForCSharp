using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 单选项（vue-tsx-pro 的 Radio）：放在 <see cref="RadioGroup"/> 里，选中由组的值决定（<see cref="Value"/> 是这一项的值）。
/// <code>
/// &lt;rune:RadioGroup Value="{Binding Shipping}"&gt;
///   &lt;rune:Radio Value="express"&gt;快递&lt;/rune:Radio&gt;
///   &lt;rune:Radio Value="pickup"&gt;自提&lt;/rune:Radio&gt;
/// &lt;/rune:RadioGroup&gt;
/// </code>
/// 圆 1.125em、2px borderStrong 描边；选中时是色调的实底，中间的圆点（0.45em、实底上的颜色）在 durations.sm 内从 0 放大。
/// 色调、只读、出错随组，字号从组继承；不可用写 IsEnabled。外层、文字与说明见 <see cref="ChoiceBase"/>；部件样式键见 <see cref="RadioParts"/>，默认外观在 Radio.xaml。
/// </summary>
public class Radio : ChoiceBase
{
    /// <summary>文字下方的说明（读屏的帮助文字）。</summary>
    public static readonly DependencyProperty DescriptionProperty = DescriptionCoreProperty.AddOwner(typeof(Radio));

    /// <summary>卡片外观：整块是一张带边框的卡片，勾选时边框与底色换成色调色；不写时跟随所在组的 Card。</summary>
    public static readonly DependencyProperty CardProperty = CardCoreProperty.AddOwner(typeof(Radio));

    /// <summary>结束边的内容（价格、标记），卡片外观时常用。</summary>
    public static readonly DependencyProperty EndProperty = EndCoreProperty.AddOwner(typeof(Radio));

    /// <summary>这一项的值。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(Radio), new PropertyMetadata(null, (d, _) => ((Radio)d).group?.Sync()));

    readonly RadioStack control;
    readonly Border circle = new() { Name = "PART_Circle" };
    readonly Border dot = new() { Name = "PART_Indicator", IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5) };
    readonly ScaleTransform scale = new(0, 0);
    readonly Border focusRing = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    RadioGroup? group;
    bool joined;

    static Radio() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Radio), new FrameworkPropertyMetadata(typeof(Radio)));

    /// <summary>新建：没选中；部件的外观引用主题的部件样式。</summary>
    public Radio()
    {
        control = new RadioStack(this) { Name = "PART_Control" };
        control.SetResourceReference(StyleProperty, RadioParts.Control);
        circle.SetResourceReference(StyleProperty, RadioParts.Circle);
        dot.SetResourceReference(StyleProperty, RadioParts.Indicator);
        focusRing.SetResourceReference(StyleProperty, RadioParts.FocusRing);
        dot.RenderTransform = scale;
        foreach (var part in new UIElement[] { circle, dot, focusRing }) control.Children.Add(part);
        Compose(control);
        Loaded += (_, _) =>
        {
            group = ChoiceGroup.Of<RadioGroup>(this);
            if (group is null) return;
            SetBinding(ToneCoreProperty, new Binding(nameof(RadioGroup.Tone)) { Source = group });
            group.Join(this);
            joined = true;
        };
        Unloaded += (_, _) =>
        {
            var old = group;
            group = null;
            joined = false;
            BindingOperations.ClearBinding(this, ToneCoreProperty);
            old?.Leave(this);
        };
    }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>卡片外观。</summary>
    public bool? Card { get => (bool?)GetValue(CardProperty); set => SetValue(CardProperty, value); }

    /// <summary>结束边的内容。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>这一项的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>所在的单选组。</summary>
    internal RadioGroup? Group => group;

    /// <inheritdoc />
    private protected override bool InGroup => group is not null;

    /// <inheritdoc />
    private protected override ChoiceGroup? OwnerGroup => group;

    /// <inheritdoc />
    protected internal override bool ReadOnly => group?.ReadOnly == true;

    /// <inheritdoc />
    protected internal override bool Invalid => group?.Invalid == true;

    /// <inheritdoc />
    protected internal override bool Required => group?.Required == true;

    /// <summary>单选项点了只会选中。</summary>
    protected override bool NextChecked => true;

    /// <summary>圆点在 durations.sm 内放大到原大或缩成 0；加入组时按组的值直接到位（初次显示不播）。读屏收到选中变化。</summary>
    protected override void OnCheckedChanged(bool oldValue, bool newValue)
    {
        var to = newValue ? 1d : 0d;
        var duration = joined ? Motion.Duration(this, "Rune.Duration.Sm", TimeSpan.FromMilliseconds(120)) : TimeSpan.Zero;
        foreach (var axis in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            scale.SetValue(axis, to);
            scale.BeginAnimation(axis, duration <= TimeSpan.Zero ? null
                : new DoubleAnimation(1 - to, to, duration) { EasingFunction = TryFindResource("Rune.Easing.Standard") as IEasingFunction, FillBehavior = FillBehavior.Stop });
        }
        if (UIElementAutomationPeer.FromElement(this) is RadioAutomationPeer peer)
            peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, oldValue, newValue);
    }

    /// <summary>em 尺寸：圆、圆点的圆角。</summary>
    protected override void UpdateMetrics()
    {
        circle.CornerRadius = new CornerRadius(0.5625 * Em);
        focusRing.CornerRadius = new CornerRadius(0.5625 * Em + RadioStack.Ring);
        dot.CornerRadius = new CornerRadius(0.225 * Em);
        base.UpdateMetrics();
    }

    /// <summary>选中交给组。</summary>
    protected override void ApplyChecked(bool value)
    {
        if (group is not null && Value is not null) group.Select(Value);
        else base.ApplyChecked(value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RadioAutomationPeer(this);

    /// <summary>叠放层（TS 的 control）：圆、圆点、焦点环叠在一起，高度不低于 1.5em（与第一行文字居中）。</summary>
    sealed class RadioStack(Radio owner) : Panel
    {
        public const double Ring = 4;

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (UIElement child in Children) child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new Size(1.125 * owner.Em, 1.5 * owner.Em);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var em = owner.Em;
            var side = 1.125 * em;
            var rect = new Rect(0, (finalSize.Height - side) / 2, side, side);
            owner.circle.Arrange(rect);
            owner.focusRing.Arrange(new Rect(rect.X - Ring, rect.Y - Ring, rect.Width + 2 * Ring, rect.Height + 2 * Ring));
            var mark = 0.45 * em;
            owner.dot.Arrange(new Rect(rect.X + (side - mark) / 2, rect.Y + (side - mark) / 2, mark, mark));
            return finalSize;
        }
    }

    /// <summary>读屏：单选按钮，SelectionItem 模式（是否选中）。</summary>
    sealed class RadioAutomationPeer(Radio owner) : ChoiceAutomationPeer(owner), ISelectionItemProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        protected override string GetClassNameCore() => nameof(Radio);

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);

        public bool IsSelected => owner.IsChecked;

        // 单选组没有实现 Selection 模式（读屏从组的名称与各项的选中状态得知）
        public IRawElementProviderSimple? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection() { }

        public void Select()
        {
            if (!owner.IsEnabled) throw new ElementNotEnabledException();
            owner.Toggle();
        }
    }
}

/// <summary>
/// Radio 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:RadioParts.Indicator}" TargetType="Border"&gt;</c>。
/// 外层（TS 的 propsRoot）就是 Radio 自己，文字见 <see cref="ChoiceBaseParts"/>。Circle、FocusRing 是 TS 里的 input（圆本身与它的焦点环）。
/// </summary>
public static class RadioParts
{
    /// <summary>叠放层（TS 的 propsControl）：圆、圆点叠在一起。</summary>
    public static readonly PartKey Control = new(typeof(Radio), nameof(Control), typeof(Panel));

    /// <summary>圆（TS 的 input）：Border，平时 borderStrong 描边、表面底色，选中时色调实底，出错（组的）时描边是危险色。</summary>
    public static readonly PartKey Circle = new(typeof(Radio), nameof(Circle), typeof(Border));

    /// <summary>焦点环：Border，键盘聚焦时贴着圆显示。</summary>
    public static readonly PartKey FocusRing = new(typeof(Radio), nameof(FocusRing), typeof(Border));

    /// <summary>圆点（TS 的 propsIndicator）：Border，实底上的颜色。</summary>
    public static readonly PartKey Indicator = new(typeof(Radio), nameof(Indicator), typeof(Border));
}
