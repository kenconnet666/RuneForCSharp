using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.LogicalTree;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 复选框（vue-tsx-pro 的 Checkbox）：要提交才生效的勾选；立即生效的设置用 Switch。
/// <code>
/// &lt;rune:Checkbox IsChecked="{Binding Agreed}"&gt;我已阅读并同意&lt;/rune:Checkbox&gt;
/// &lt;rune:Checkbox IsChecked="{Binding AllChecked}" IsIndeterminate="{Binding SomeChecked}" Toggling="OnToggleAll"&gt;全选&lt;/rune:Checkbox&gt;
/// </code>
/// 方框 1.125em、圆角 radii.sm、2px borderStrong 描边；勾上或部分勾选时是色调的实底，里面是对勾或横线（0.875em、描线 3）。
/// 部分勾选（IsIndeterminate）时读屏念"部分选中"。放在 <see cref="CheckboxGroup"/> 里时勾选由组的值决定（<see cref="Value"/> 是这一项的值），
/// 只读、出错随组。外层、文字与说明见 <see cref="ChoiceBase"/>；部件见 <see cref="CheckboxParts"/>，默认外观在 CheckboxParts.axaml。
/// </summary>
[PseudoClasses(":indeterminate")]
public class Checkbox : ChoiceBase
{
    /// <summary>只读：能聚焦、读屏念"只读"，勾选状态不能改；外观照常。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = IsReadOnlyCoreProperty.AddOwner<Checkbox>();

    /// <summary>必填（如"同意条款"）。</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = IsRequiredCoreProperty.AddOwner<Checkbox>();

    /// <summary>校验未通过：方框的描边是危险色。</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = IsInvalidCoreProperty.AddOwner<Checkbox>();

    /// <summary>选中时的色调；主题默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = ToneCoreProperty.AddOwner<Checkbox>();

    /// <summary>字号档位：方框随之缩放；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = SizeCoreProperty.AddOwner<Checkbox>();

    /// <summary>文字下方的说明（读屏的帮助文字）。</summary>
    public static readonly StyledProperty<object?> DescriptionProperty = DescriptionCoreProperty.AddOwner<Checkbox>();

    /// <summary>卡片外观：整块是一张带边框的卡片，勾选时边框与底色换成色调色；不写时跟随所在组的 Card。</summary>
    public static readonly StyledProperty<bool?> CardProperty = CardCoreProperty.AddOwner<Checkbox>();

    /// <summary>结束边的内容（价格、标记），卡片外观时常用。</summary>
    public static readonly StyledProperty<object?> EndProperty = EndCoreProperty.AddOwner<Checkbox>();

    /// <summary>部分勾选（"全选"下面只选了一部分）：显示横线。</summary>
    public static readonly StyledProperty<bool> IsIndeterminateProperty = AvaloniaProperty.Register<Checkbox, bool>(nameof(IsIndeterminate));

    /// <summary>在复选组里的值。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<Checkbox, string?>(nameof(Value));

    static readonly StyledProperty<double> RadiusFactorProperty = AvaloniaProperty.Register<Checkbox, double>("RadiusFactor", 0.25);

    readonly CheckboxStack control;
    readonly Border box = new() { Name = CheckboxParts.Box };
    readonly Icon indicator = new() { Name = CheckboxParts.Indicator, StrokeWidth = 3, IsHitTestVisible = false };
    readonly Border focusRing = new() { Name = "PART_FocusRing", IsHitTestVisible = false };
    readonly TemplateResources resources;
    CheckboxGroup? group;

    static Checkbox() => AffectsArrange<Checkbox>(RadiusFactorProperty);

    /// <summary>新建：没勾选。</summary>
    public Checkbox()
    {
        resources = new TemplateResources(this);
        resources.Bind(RadiusFactorProperty, "Rune.Radius.Sm");
        control = new CheckboxStack(this) { Name = CheckboxParts.Control };
        control.Children.AddRange([box, indicator, focusRing]);
        Compose(control);
    }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>说明。</summary>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>卡片外观。</summary>
    public bool? Card { get => GetValue(CardProperty); set => SetValue(CardProperty, value); }

    /// <summary>结束边的内容。</summary>
    public object? End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <summary>部分勾选。</summary>
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

    /// <summary>在复选组里的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    private protected override bool InGroup => group is not null;

    /// <inheritdoc />
    private protected override ChoiceGroup? OwnerGroup => group;

    /// <inheritdoc />
    protected override bool ReadOnly => base.ReadOnly || group?.ReadOnly == true;

    /// <inheritdoc />
    protected override bool Invalid => base.Invalid || group?.Invalid == true;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIndeterminateProperty) Update();
        else if (change.Property == ValueProperty) group?.Sync(this);
    }

    /// <summary>加上部分勾选的伪类；对勾或横线只在勾上、部分勾选时显示（不显示时仍占位）。</summary>
    protected override void Update()
    {
        base.Update();
        PseudoClasses.Set(":indeterminate", IsIndeterminate);
        indicator.Data = IsIndeterminate ? Lucide.Minus : Lucide.Check;
        indicator.Opacity = IsChecked || IsIndeterminate ? 1 : 0;
    }

    /// <summary>在复选组里时勾选交给组。</summary>
    protected override void ApplyChecked(bool value)
    {
        if (group is not null && Value is not null) group.Toggle(Value);
        else base.ApplyChecked(value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        group = this.FindLogicalAncestorOfType<CheckboxGroup>();
        group?.Sync(this);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        group = null;
        Update();
    }

    /// <summary>组的只读、出错变了。</summary>
    internal void Refresh() => Update();

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CheckboxAutomationPeer(this);

    /// <summary>叠放层（TS 的 control）：方框、标记、焦点环叠在一起，高度不低于 1.5em（与第一行文字居中）。</summary>
    sealed class CheckboxStack(Checkbox owner) : Panel
    {
        protected override LayoutSize MeasureOverride(LayoutSize availableSize)
        {
            foreach (var child in Children) child.Measure(LayoutSize.Infinity);
            var em = owner.Em;
            return new LayoutSize(1.125 * em, Math.Max(1.5 * em, 1.125 * em));
        }

        protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
        {
            var em = owner.Em;
            var side = 1.125 * em;
            var rect = new Rect(0, (finalSize.Height - side) / 2, side, side);
            var radius = owner.GetValue(RadiusFactorProperty) * em;
            owner.box.CornerRadius = new CornerRadius(radius);
            owner.box.Arrange(rect);
            const double Ring = 4;
            owner.focusRing.CornerRadius = new CornerRadius(radius + Ring);
            owner.focusRing.Arrange(rect.Inflate(Ring));
            var mark = 0.875 * em;
            owner.indicator.Size = mark;
            owner.indicator.Arrange(new Rect(rect.X + (side - mark) / 2, rect.Y + (side - mark) / 2, mark, mark));
            return finalSize;
        }
    }

    /// <summary>读屏：复选框，Toggle 模式（勾上 / 没勾 / 部分勾选）。</summary>
    sealed class CheckboxAutomationPeer : ChoiceAutomationPeer, IToggleProvider
    {
        readonly Checkbox owner;

        public CheckboxAutomationPeer(Checkbox owner) : base(owner)
        {
            this.owner = owner;
            owner.PropertyChanged += (_, e) =>
            {
                if (e.Property == IsCheckedProperty || e.Property == IsIndeterminateProperty)
                    RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, null, ToggleState);
            };
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;

        public ToggleState ToggleState => owner.IsIndeterminate ? ToggleState.Indeterminate : owner.IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => owner.Toggle();
    }
}

/// <summary>Checkbox 的部件名（代码组合的子元素）：<c>rune|Checkbox #PART_Box</c>。外层（TS 的 propsRoot）就是 Checkbox 自己，文字见 <see cref="ChoiceBaseParts"/>。</summary>
public static class CheckboxParts
{
    /// <summary>叠放层（TS 的 propsControl）：方框、标记叠在一起。</summary>
    public const string Control = "PART_Control";

    /// <summary>方框（TS 的 input）：Border，平时 borderStrong 描边、表面底色，勾上时色调实底，出错时描边是危险色。</summary>
    public const string Box = "PART_Box";

    /// <summary>对勾 / 横线（TS 的 propsIndicator）：Icon，实底上的颜色。</summary>
    public const string Indicator = "PART_Indicator";
}
