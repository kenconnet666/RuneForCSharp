using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 单选项（vue-tsx-pro 的 Radio）：放在 <see cref="RadioGroup"/> 里，选中由组的值决定（<see cref="Value"/> 是这一项的值）。
/// <code>
/// &lt;rune:RadioGroup Value="{Binding Shipping}"&gt;
///   &lt;rune:Radio Value="express"&gt;快递&lt;/rune:Radio&gt;
///   &lt;rune:Radio Value="pickup"&gt;自提&lt;/rune:Radio&gt;
/// &lt;/rune:RadioGroup&gt;
/// </code>
/// 圆 1.125em、2px borderStrong 描边；选中时是色调的实底，中间的圆点（0.45em、实底上的颜色）在 durations.sm 内从 0 放大。
/// 色调、字号、只读、出错随组；不可用写 IsEnabled。外层、文字与说明见 <see cref="ChoiceBase"/>；部件见 <see cref="RadioParts"/>，默认外观在 RadioParts.axaml。
/// </summary>
public class Radio : ChoiceBase
{
    /// <summary>文字下方的说明（读屏的帮助文字）。</summary>
    public static readonly StyledProperty<object?> DescriptionProperty = DescriptionCoreProperty.AddOwner<Radio>();

    /// <summary>卡片外观：整块是一张带边框的卡片，勾选时边框与底色换成色调色；不写时跟随所在组的 Card。</summary>
    public static readonly StyledProperty<bool?> CardProperty = CardCoreProperty.AddOwner<Radio>();

    /// <summary>结束边的内容（价格、标记），卡片外观时常用。</summary>
    public static readonly StyledProperty<object?> EndProperty = EndCoreProperty.AddOwner<Radio>();

    /// <summary>这一项的值。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Radio, string?>(nameof(Value));

    static readonly TransformOperations Hidden = TransformOperations.Parse("scale(0)");

    readonly RadioStack control;
    readonly Border circle = new() { Name = RadioParts.Circle };
    readonly Border dot = new() { Name = RadioParts.Indicator, IsHitTestVisible = false, RenderTransform = Hidden };
    readonly Border focusRing = new() { Name = "PART_FocusRing", IsHitTestVisible = false };
    RadioGroup? group;

    /// <summary>新建：没选中。</summary>
    public Radio()
    {
        control = new RadioStack(this) { Name = RadioParts.Control };
        control.Children.AddRange([circle, dot, focusRing]);
        Compose(control);
    }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>卡片外观。</summary>
    public bool? Card { get => GetValue(CardProperty); set => SetValue(CardProperty, value); }

    /// <summary>结束边的内容。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>这一项的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>所在的单选组。</summary>
    internal RadioGroup? Group => group;

    /// <inheritdoc />
    private protected override bool InGroup => group is not null;

    /// <inheritdoc />
    private protected override ChoiceGroup? OwnerGroup => group;

    /// <inheritdoc />
    protected override bool ReadOnly => group?.ReadOnly == true;

    /// <inheritdoc />
    protected override bool Invalid => group?.Invalid == true;

    /// <inheritdoc />
    protected override bool Required => group?.Required == true;

    /// <summary>单选项点了只会选中。</summary>
    protected override bool NextChecked => true;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) group?.Sync();
    }

    /// <summary>圆点：选中时放大到原大，没选中时缩成 0。</summary>
    protected override void Update()
    {
        base.Update();
        dot.RenderTransform = IsChecked ? TransformOperations.Identity : Hidden;
    }

    /// <summary>选中交给组。</summary>
    protected override void ApplyChecked(bool value)
    {
        if (group is not null && Value is not null) group.Select(Value);
        else base.ApplyChecked(value);
    }

    /// <summary>挂上时：随组的色调、字号；圆点的缩放过渡用 durations.sm（系统关了动画时没有）。</summary>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        group = this.FindLogicalAncestorOfType<RadioGroup>();
        if (group is not null)
        {
            Bind(ToneCoreProperty, group.GetObservable(RadioGroup.ToneProperty));
            Bind(SizeCoreProperty, group.GetObservable(RadioGroup.SizeProperty));
            group.Sync();
        }
        var duration = Motion.Duration(this, ThemeKeys.Duration.Sm);
        dot.Transitions = duration > TimeSpan.Zero ? [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = duration }] : null;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        var old = group;
        group = null;
        ClearValue(ToneCoreProperty);
        ClearValue(SizeCoreProperty);
        old?.Sync();
    }

    /// <summary>组的值、只读、出错变了。</summary>
    internal void Refresh(bool isChecked)
    {
        SetCurrentValue(IsCheckedProperty, isChecked);
        Update();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new RadioAutomationPeer(this);

    /// <summary>叠放层（TS 的 control）：圆、圆点、焦点环叠在一起，高度不低于 1.5em（与第一行文字居中）。</summary>
    sealed class RadioStack(Radio owner) : Panel
    {
        protected override LayoutSize MeasureOverride(LayoutSize availableSize)
        {
            foreach (var child in Children) child.Measure(LayoutSize.Infinity);
            var em = owner.Em;
            return new LayoutSize(1.125 * em, 1.5 * em);
        }

        protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
        {
            var em = owner.Em;
            var side = 1.125 * em;
            var rect = new Rect(0, (finalSize.Height - side) / 2, side, side);
            owner.circle.CornerRadius = new CornerRadius(side / 2);
            owner.circle.Arrange(rect);
            const double Ring = 4;
            owner.focusRing.CornerRadius = new CornerRadius(side / 2 + Ring);
            owner.focusRing.Arrange(rect.Inflate(Ring));
            var mark = 0.45 * em;
            owner.dot.CornerRadius = new CornerRadius(mark / 2);
            owner.dot.Arrange(new Rect(rect.X + (side - mark) / 2, rect.Y + (side - mark) / 2, mark, mark));
            return finalSize;
        }
    }

    /// <summary>读屏：单选按钮，SelectionItem 模式（选中、所在的组）。</summary>
    sealed class RadioAutomationPeer : ChoiceAutomationPeer, ISelectionItemProvider
    {
        readonly Radio owner;

        public RadioAutomationPeer(Radio owner) : base(owner)
        {
            this.owner = owner;
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsCheckedProperty)
                    RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, e.OldValue, e.NewValue);
            };
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        public bool IsSelected => owner.IsChecked;

        // 单选组没有实现 Selection 模式（读屏从组的名称与各项的选中状态得知）
        public ISelectionProvider? SelectionContainer => null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection() { }

        public void Select() => owner.Toggle();
    }
}

/// <summary>Radio 的部件名（代码组合的子元素）：<c>rune|Radio #PART_Indicator</c>。外层（TS 的 propsRoot）就是 Radio 自己，文字见 <see cref="ChoiceBaseParts"/>。</summary>
public static class RadioParts
{
    /// <summary>叠放层（TS 的 propsControl）：圆、圆点叠在一起。</summary>
    public const string Control = "PART_Control";

    /// <summary>圆（TS 的 input）：Border，平时 borderStrong 描边、表面底色，选中时色调实底，出错时描边是危险色。</summary>
    public const string Circle = "PART_Circle";

    /// <summary>圆点（TS 的 propsIndicator）：Border，实底上的颜色。</summary>
    public const string Indicator = "PART_Indicator";
}
