using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 复选框（vue-tsx-pro 的 Checkbox）：要提交才生效的勾选；立即生效的设置用 Switch。
/// <code>
/// &lt;rune:Checkbox IsChecked="{Binding Agreed}"&gt;我已阅读并同意&lt;/rune:Checkbox&gt;
/// &lt;rune:Checkbox IsChecked="{Binding AllChecked, Mode=OneWay}" IsIndeterminate="{Binding SomeChecked}" Toggling="OnToggleAll"&gt;全选&lt;/rune:Checkbox&gt;
/// </code>
/// 方框 1.125em、圆角 radii.sm、2px borderStrong 描边；勾上或部分勾选时是色调的实底，里面是对勾或横线（0.875em、描线 3）。
/// 部分勾选（IsIndeterminate）时读屏念"部分选中"。放在 <see cref="CheckboxGroup"/> 里时勾选由组的值决定（<see cref="Value"/> 是这一项的值），
/// 只读、出错随组。外层、文字与说明见 <see cref="ChoiceBase"/>；部件样式键见 <see cref="CheckboxParts"/>，默认外观在 Checkbox.xaml。
/// </summary>
public class Checkbox : ChoiceBase
{
    /// <summary>只读：能聚焦、读屏念"只读"，勾选状态不能改；外观照常。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = IsReadOnlyCoreProperty.AddOwner(typeof(Checkbox));

    /// <summary>必填（如"同意条款"）。</summary>
    public static readonly DependencyProperty IsRequiredProperty = IsRequiredCoreProperty.AddOwner(typeof(Checkbox));

    /// <summary>校验未通过：方框的描边是危险色。</summary>
    public static readonly DependencyProperty IsInvalidProperty = IsInvalidCoreProperty.AddOwner(typeof(Checkbox));

    /// <summary>选中时的色调；主题默认 accent。</summary>
    public static readonly DependencyProperty ToneProperty = ToneCoreProperty.AddOwner(typeof(Checkbox));

    /// <summary>字号档位：方框随之缩放；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = SizeCoreProperty.AddOwner(typeof(Checkbox));

    /// <summary>文字下方的说明（读屏的帮助文字）。</summary>
    public static readonly DependencyProperty DescriptionProperty = DescriptionCoreProperty.AddOwner(typeof(Checkbox));

    /// <summary>部分勾选（"全选"下面只选了一部分）：显示横线。</summary>
    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
        nameof(IsIndeterminate), typeof(bool), typeof(Checkbox), new PropertyMetadata(false, (d, _) => ((Checkbox)d).OnIndeterminateChanged()));

    /// <summary>在复选组里的值。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(Checkbox), new PropertyMetadata(null, (d, _) => ((Checkbox)d).group?.Sync()));

    static readonly DependencyProperty RadiusFactorProperty = DependencyProperty.Register(
        "RadiusFactor", typeof(double), typeof(Checkbox), new FrameworkPropertyMetadata(0.25, (d, _) => ((Checkbox)d).UpdateMetrics()));

    readonly CheckboxStack control;
    readonly Border box = new() { Name = "PART_Box" };
    readonly Icon indicator = new() { Name = "PART_Indicator", StrokeWidth = 3, IsHitTestVisible = false };
    readonly Border focusRing = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    CheckboxGroup? group;

    static Checkbox() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Checkbox), new FrameworkPropertyMetadata(typeof(Checkbox)));

    /// <summary>新建：没勾选；部件的外观引用主题的部件样式。</summary>
    public Checkbox()
    {
        control = new CheckboxStack(this) { Name = "PART_Control" };
        control.SetResourceReference(StyleProperty, CheckboxParts.Control);
        box.SetResourceReference(StyleProperty, CheckboxParts.Box);
        indicator.SetResourceReference(StyleProperty, CheckboxParts.Indicator);
        focusRing.SetResourceReference(StyleProperty, CheckboxParts.FocusRing);
        SetResourceReference(RadiusFactorProperty, "Rune.Radius.Sm");
        foreach (var part in new UIElement[] { box, indicator, focusRing }) control.Children.Add(part);
        Compose(control);
        Loaded += (_, _) =>
        {
            group = ChoiceGroup.Of<CheckboxGroup>(this);
            group?.Join(this);
        };
        Unloaded += (_, _) =>
        {
            group?.Leave(this);
            group = null;
            Update();
        };
    }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool IsInvalid { get => (bool)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>部分勾选。</summary>
    public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

    /// <summary>在复选组里的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    protected internal override bool ReadOnly => base.ReadOnly || group?.IsReadOnly == true;

    /// <inheritdoc />
    protected internal override bool Invalid => base.Invalid || group?.IsInvalid == true;

    void OnIndeterminateChanged()
    {
        Update();
        (UIElementAutomationPeer.FromElement(this) as CheckboxAutomationPeer)?.RaiseToggleStateChanged();
    }

    /// <inheritdoc />
    protected override void OnCheckedChanged(bool oldValue, bool newValue) =>
        (UIElementAutomationPeer.FromElement(this) as CheckboxAutomationPeer)?.RaiseToggleStateChanged();

    /// <summary>对勾或横线只在勾上、部分勾选时显示（不显示时仍占位）；方框的颜色随勾选、出错。</summary>
    protected internal override void Update()
    {
        base.Update();
        indicator.Data = IsIndeterminate ? Lucide.Minus : Lucide.Check;
        indicator.Opacity = IsChecked || IsIndeterminate ? 1 : 0;
    }

    /// <summary>em 尺寸：方框的圆角、标记的大小。</summary>
    protected override void UpdateMetrics()
    {
        var radius = (double)GetValue(RadiusFactorProperty) * Em;
        box.CornerRadius = new CornerRadius(radius);
        focusRing.CornerRadius = new CornerRadius(radius + CheckboxStack.Ring);
        indicator.Size = 0.875 * Em;
        base.UpdateMetrics();
    }

    /// <summary>在复选组里时勾选交给组。</summary>
    protected override void ApplyChecked(bool value)
    {
        if (group is not null && Value is not null) group.Toggle(Value);
        else base.ApplyChecked(value);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CheckboxAutomationPeer(this);

    /// <summary>叠放层（TS 的 control）：方框、标记、焦点环叠在一起，高度不低于 1.5em（与第一行文字居中）。</summary>
    sealed class CheckboxStack(Checkbox owner) : Panel
    {
        public const double Ring = 4;

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (UIElement child in Children) child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var em = owner.Em;
            return new Size(1.125 * em, 1.5 * em);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var em = owner.Em;
            var side = 1.125 * em;
            var rect = new Rect(0, (finalSize.Height - side) / 2, side, side);
            owner.box.Arrange(rect);
            owner.focusRing.Arrange(new Rect(rect.X - Ring, rect.Y - Ring, rect.Width + 2 * Ring, rect.Height + 2 * Ring));
            var mark = 0.875 * em;
            owner.indicator.Arrange(new Rect(rect.X + (side - mark) / 2, rect.Y + (side - mark) / 2, mark, mark));
            return finalSize;
        }
    }

    /// <summary>读屏：复选框，Toggle 模式（勾上 / 没勾 / 部分勾选）。</summary>
    sealed class CheckboxAutomationPeer(Checkbox owner) : ChoiceAutomationPeer(owner), IToggleProvider
    {
        ToggleState last = State(owner);

        static ToggleState State(Checkbox checkbox) => checkbox.IsIndeterminate ? ToggleState.Indeterminate : checkbox.IsChecked ? ToggleState.On : ToggleState.Off;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;

        protected override string GetClassNameCore() => nameof(Checkbox);

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Toggle ? this : base.GetPattern(patternInterface);

        public ToggleState ToggleState => State(owner);

        public void Toggle()
        {
            if (!owner.IsEnabled) throw new ElementNotEnabledException();
            owner.Toggle();
        }

        public void RaiseToggleStateChanged()
        {
            var now = State(owner);
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, last, now);
            last = now;
        }
    }
}

/// <summary>
/// Checkbox 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:CheckboxParts.Box}" TargetType="Border"&gt;</c>。
/// 外层（TS 的 propsRoot）就是 Checkbox 自己，文字见 <see cref="ChoiceBaseParts"/>。Box、FocusRing 是 TS 里的 input（方框本身与它的焦点环）。
/// </summary>
public static class CheckboxParts
{
    /// <summary>叠放层（TS 的 propsControl）：方框、标记叠在一起。</summary>
    public static readonly PartKey Control = new(typeof(Checkbox), nameof(Control), typeof(Panel));

    /// <summary>方框（TS 的 input）：Border，平时 borderStrong 描边、表面底色，勾上或部分勾选时色调实底，出错（自己的或组的）时描边是危险色。</summary>
    public static readonly PartKey Box = new(typeof(Checkbox), nameof(Box), typeof(Border));

    /// <summary>焦点环：Border，键盘聚焦时贴着方框显示。</summary>
    public static readonly PartKey FocusRing = new(typeof(Checkbox), nameof(FocusRing), typeof(Border));

    /// <summary>对勾 / 横线（TS 的 propsIndicator）：Icon，实底上的颜色。</summary>
    public static readonly PartKey Indicator = new(typeof(Checkbox), nameof(Indicator), typeof(Icon));
}
