using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 勾选类控件（Checkbox、Radio、Switch）共用的外层（vue-tsx-pro 的 choice-styles）：控件部件（方框、圆、轨道）在起始边，
/// 文字与说明在后面一列，顶端对齐；控件部件居中在第一行文字的行高（1.5em）里。整块可点，高度不低于 24px（WCAG 2.5.8）；
/// 空格松开时切换。部件：控件部件（各组件自己的叠放层）、<see cref="ChoiceBaseParts.Label"/>、<see cref="ChoiceBaseParts.Description"/>、结束边的 <see cref="ChoiceBaseParts.End"/>。
/// 卡片外观（Checkbox、Radio 的 Card，或跟随组）：整块是一张卡片（内边距 space.md、xs 描边、md 圆角、surface 底，悬停时描边 borderStrong），
/// 勾选时描边换成色调的实底、底色是色调 8% 混进 surface；卡片占满宽度。
/// 只读、必填、出错、色调、字号、说明由派生类按上游的 props 公开（Radio 的这些来自单选组）。
/// </summary>
[PseudoClasses(":checked", ":readonly", ":invalid", ":card", ":end")]
public abstract class ChoiceBase : Control, IFieldControl
{
    /// <summary>是否勾选（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty = AvaloniaProperty.Register<ChoiceBase, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>文字（读屏名称）。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<ChoiceBase, object?>(nameof(Content));

    /// <summary>色调的实底（TS 的 --tone-fill），按色调取主题资源；部件样式绑它。</summary>
    public static readonly StyledProperty<IBrush?> ToneFillProperty = AvaloniaProperty.Register<ChoiceBase, IBrush?>(nameof(ToneFill));

    /// <summary>实底上的标记、文字色（TS 的 --tone-on-fill）。</summary>
    public static readonly StyledProperty<IBrush?> ToneOnFillProperty = AvaloniaProperty.Register<ChoiceBase, IBrush?>(nameof(ToneOnFill));

    /// <summary>字号（继承），em 尺寸按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ChoiceBase>();

    // 下面几项由派生类 AddOwner 公开：Switch、Checkbox 自己有，Radio 的来自单选组（上游 RadioProps 没有）
    internal static readonly StyledProperty<bool> IsReadOnlyCoreProperty = AvaloniaProperty.Register<ChoiceBase, bool>("IsReadOnly");
    internal static readonly StyledProperty<bool> IsRequiredCoreProperty = AvaloniaProperty.Register<ChoiceBase, bool>("IsRequired");
    internal static readonly StyledProperty<bool> IsInvalidCoreProperty = AvaloniaProperty.Register<ChoiceBase, bool>("IsInvalid");
    internal static readonly StyledProperty<StatusTone> ToneCoreProperty = AvaloniaProperty.Register<ChoiceBase, StatusTone>("Tone");
    internal static readonly StyledProperty<TextSize> SizeCoreProperty = AvaloniaProperty.Register<ChoiceBase, TextSize>("Size");
    internal static readonly StyledProperty<object?> DescriptionCoreProperty = AvaloniaProperty.Register<ChoiceBase, object?>("Description");
    internal static readonly StyledProperty<bool?> CardCoreProperty = AvaloniaProperty.Register<ChoiceBase, bool?>("Card");
    internal static readonly StyledProperty<object?> EndCoreProperty = AvaloniaProperty.Register<ChoiceBase, object?>("End");

    static readonly StyledProperty<double> GapFactorProperty = AvaloniaProperty.Register<ChoiceBase, double>("GapFactor", 0.5);
    static readonly StyledProperty<double> MinTargetProperty = AvaloniaProperty.Register<ChoiceBase, double>("MinTarget", 24);
    static readonly StyledProperty<double> CardPaddingFactorProperty = AvaloniaProperty.Register<ChoiceBase, double>("CardPaddingFactor", 0.75);
    static readonly StyledProperty<double> CardRadiusFactorProperty = AvaloniaProperty.Register<ChoiceBase, double>("CardRadiusFactor", 0.375);
    static readonly StyledProperty<IBrush?> CardSurfaceProperty = AvaloniaProperty.Register<ChoiceBase, IBrush?>("CardSurface");
    static readonly StyledProperty<IBrush?> CardBorderProperty = AvaloniaProperty.Register<ChoiceBase, IBrush?>("CardBorder");
    static readonly StyledProperty<IBrush?> CardBorderStrongProperty = AvaloniaProperty.Register<ChoiceBase, IBrush?>("CardBorderStrong");

    readonly ContentPresenter label = new() { Name = ChoiceBaseParts.Label };
    readonly ContentPresenter description = new() { Name = ChoiceBaseParts.Description };
    readonly ContentPresenter end = new() { Name = ChoiceBaseParts.End };
    readonly TemplateResources resources;
    Control? box;
    bool pressed;

    static ChoiceBase()
    {
        FocusableProperty.OverrideDefaultValue<ChoiceBase>(true);
        AffectsMeasure<ChoiceBase>(FontSizeProperty, GapFactorProperty, MinTargetProperty, ContentProperty, DescriptionCoreProperty, CardCoreProperty, EndCoreProperty,
            CardPaddingFactorProperty);
        AffectsRender<ChoiceBase>(CardRadiusFactorProperty, CardSurfaceProperty, CardBorderProperty, CardBorderStrongProperty, ToneFillProperty, IsPointerOverProperty);
    }

    /// <summary>新建：间距、色调绑到主题。</summary>
    protected ChoiceBase()
    {
        resources = new TemplateResources(this);
        resources.Bind(GapFactorProperty, "Rune.Space.Sm");
        resources.Bind(MinTargetProperty, "Rune.Size.MinTarget");
        resources.Bind(CardPaddingFactorProperty, "Rune.Space.Md");
        resources.Bind(CardRadiusFactorProperty, "Rune.Radius.Md");
        resources.Bind(CardSurfaceProperty, "Rune.Brush.Surface");
        resources.Bind(CardBorderProperty, "Rune.Brush.Border");
        resources.Bind(CardBorderStrongProperty, "Rune.Brush.BorderStrong");
        BindTone();
        UpdateMetrics();
    }

    /// <summary>
    /// 要切换时（点击、空格、读屏）先发：设 Cancel 不改值——切换要等接口、要先确认时拦下，之后再写 IsChecked（TS 的受控 onUpdate:_checked）。
    /// </summary>
    public event EventHandler<ChoiceTogglingEventArgs>? Toggling;

    /// <summary>是否勾选。</summary>
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>文字。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>色调的实底。</summary>
    public IBrush? ToneFill { get => GetValue(ToneFillProperty); set => SetValue(ToneFillProperty, value); }

    /// <summary>实底上的颜色。</summary>
    public IBrush? ToneOnFill { get => GetValue(ToneOnFillProperty); set => SetValue(ToneOnFillProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>所在的字段：在复选组、单选组里时字段归组（项不接字段）。</summary>
    private protected Field? FieldOwner => InGroup ? null : Field.Of(this);

    /// <summary>在复选组、单选组里。</summary>
    private protected virtual bool InGroup => false;

    /// <summary>只读：能聚焦，不能切换；外观照常。自己写了的优先，其次所在字段的。</summary>
    protected virtual bool ReadOnly => IsSet(IsReadOnlyCoreProperty) ? GetValue(IsReadOnlyCoreProperty) : FieldOwner?.IsReadOnly ?? false;

    /// <summary>校验未通过：自己写了的优先，其次所在字段的。</summary>
    protected virtual bool Invalid => IsSet(IsInvalidCoreProperty) ? GetValue(IsInvalidCoreProperty) : FieldOwner?.Invalid ?? false;

    /// <summary>必填：自己写了的优先，其次所在字段的。</summary>
    protected virtual bool Required => IsSet(IsRequiredCoreProperty) ? GetValue(IsRequiredCoreProperty) : FieldOwner?.IsRequired ?? false;

    /// <summary>所在的复选组、单选组（卡片外观跟随它）。</summary>
    private protected virtual ChoiceGroup? OwnerGroup => null;

    /// <summary>卡片外观：自己写了的优先，其次所在组的。</summary>
    internal bool AsCard => GetValue(CardCoreProperty) ?? OwnerGroup?.Card ?? false;

    /// <summary>内容离外沿的距离：卡片的内边距（space.md）加描边（xs，1px）；不是卡片时为 0。</summary>
    double Inset => AsCard ? GetValue(CardPaddingFactorProperty) * Em + 1 : 0;

    /// <summary>这次能不能切换（只读、不可用之外的理由，如 Switch 切换中）。</summary>
    protected virtual bool CanToggle => true;

    /// <summary>切换后的值：复选框、开关取反，单选项总是选中。</summary>
    protected virtual bool NextChecked => !IsChecked;

    /// <summary>em：字号。</summary>
    protected double Em => FontSize;

    double Gap => GetValue(GapFactorProperty) * Em;

    /// <summary>派生类在构造函数里交出控件部件（叠放层），与文字、说明一起成为子元素。</summary>
    /// <param name="control">控件部件。</param>
    protected void Compose(Control control)
    {
        box = control;
        LogicalChildren.AddRange([control, label, description, end]);
        VisualChildren.AddRange([control, label, description, end]);
        Update();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty || change.Property == Leading.FactorProperty)
        {
            box?.InvalidateMeasure();
            UpdateMetrics();
        }
        if (change.Property == IsCheckedProperty)
        {
            Update();
            OnCheckedChanged();
        }
        else if (change.Property == ToneCoreProperty) BindTone();
        else if (change.Property == SizeCoreProperty)
        {
            var size = GetValue(SizeCoreProperty);
            if (size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, size.ResourceKey);
        }
        else if (change.Property == IsReadOnlyCoreProperty || change.Property == IsInvalidCoreProperty || change.Property == ContentProperty
                 || change.Property == DescriptionCoreProperty || change.Property == Field.OwnerProperty || change.Property == CardCoreProperty
                 || change.Property == EndCoreProperty) Update();
    }

    /// <summary>
    /// 说明小一号（0.86em，相对字号的 xs 档）；文字是普通字符串时 TextBlock 的行高是像素数，按各自的字号乘行高系数（同 Text）。主题样式与使用方的值盖得过。
    /// 新建时也算一次：字号与继承来的一样时不会有变化通知
    /// </summary>
    void UpdateMetrics()
    {
        var leading = Leading.GetFactor(this);
        description.SetValue(ContentPresenter.FontSizeProperty, 0.86 * Em, BindingPriority.Template);
        description.SetValue(ContentPresenter.LineHeightProperty, leading * 0.86 * Em, BindingPriority.Template);
        label.SetValue(ContentPresenter.LineHeightProperty, leading * Em, BindingPriority.Template);
    }

    void BindTone()
    {
        var (fill, onFill) = Rune.Avalonia.ToneFill.Keys(GetValue(ToneCoreProperty));
        resources.Bind(ToneFillProperty, fill);
        resources.Bind(ToneOnFillProperty, onFill);
    }

    void IFieldControl.FieldChanged() => Update();

    /// <summary>勾选变了（Switch 在这里让滑块滑过去）。</summary>
    protected virtual void OnCheckedChanged() { }

    /// <summary>状态伪类、文字与说明；派生类加上自己的部分。</summary>
    protected virtual void Update()
    {
        PseudoClasses.Set(":checked", IsChecked);
        PseudoClasses.Set(":readonly", ReadOnly);
        PseudoClasses.Set(":invalid", Invalid);
        label.Content = Content;
        label.IsVisible = Content is not null;
        description.Content = GetValue(DescriptionCoreProperty);
        description.IsVisible = description.Content is not null;
        end.Content = GetValue(EndCoreProperty);
        end.IsVisible = end.Content is not null;
        PseudoClasses.Set(":card", AsCard);
        PseudoClasses.Set(":end", end.IsVisible);
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>把勾选写回：复选组、单选组里时交给组。</summary>
    /// <param name="value">新的值。</param>
    protected virtual void ApplyChecked(bool value) => SetCurrentValue(IsCheckedProperty, value);

    /// <summary>切换（点击、空格、读屏）：只读、不可用、值不变时什么都不做。</summary>
    internal void Toggle()
    {
        if (ReadOnly || !CanToggle || !IsEffectivelyEnabled) return;
        var next = NextChecked;
        if (next == IsChecked) return;
        var args = new ChoiceTogglingEventArgs(next);
        Toggling?.Invoke(this, args);
        if (!args.Cancel) ApplyChecked(next);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        pressed = true;
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!pressed) return;
        pressed = false;
        e.Handled = true;
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) Toggle();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space) e.Handled = true;
    }

    /// <summary>空格松开时切换（同原生 checkbox）。</summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key != Key.Space) return;
        e.Handled = true;
        Toggle();
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var inset = Inset;
        availableSize = availableSize.Deflate(new Thickness(inset));
        box!.Measure(LayoutSize.Infinity);
        var boxSize = box.DesiredSize;
        var endSize = default(LayoutSize);
        if (end.IsVisible)
        {
            end.Measure(LayoutSize.Infinity);
            endSize = end.DesiredSize;
        }
        var endWidth = endSize.Width > 0 ? endSize.Width + Gap : 0;
        var text = availableSize.Deflate(new Thickness(boxSize.Width + Gap, 0, endWidth, 0));
        var textSize = default(LayoutSize);
        foreach (var part in new[] { label, description })
        {
            if (!part.IsVisible) continue;
            part.Measure(text);
            textSize = new LayoutSize(Math.Max(textSize.Width, part.DesiredSize.Width), textSize.Height + part.DesiredSize.Height);
        }
        var width = boxSize.Width + (textSize.Width > 0 ? Gap + textSize.Width : 0) + endWidth;
        var height = Math.Max(Math.Max(boxSize.Height, endSize.Height), textSize.Height);
        return new LayoutSize(width + 2 * inset, Math.Max(GetValue(MinTargetProperty), height + 2 * inset));
    }

    /// <summary>控件部件与文字一行、顶端对齐；说明在文字下面；结束边的内容贴着结束边（margin-inline-start: auto）。</summary>
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var inset = Inset;
        box!.Arrange(new Rect(new Point(inset, inset), box.DesiredSize));
        var x = inset + box.DesiredSize.Width + Gap;
        var right = finalSize.Width - inset;
        if (end.IsVisible)
        {
            end.Arrange(new Rect(right - end.DesiredSize.Width, inset, end.DesiredSize.Width, end.DesiredSize.Height));
            right -= end.DesiredSize.Width + Gap;
        }
        var width = Math.Max(0, right - x);
        var y = inset;
        foreach (var part in new[] { label, description })
        {
            if (!part.IsVisible) continue;
            part.Arrange(new Rect(x, y, width, part.DesiredSize.Height));
            y += part.DesiredSize.Height;
        }
        return finalSize;
    }

    /// <summary>
    /// 画一层透明底：控件与文字之间的空隙也能点（TS 的 label 整块可点）。卡片外观时画卡片：勾选时描边是色调的实底、底色是色调 8% 混进 surface，
    /// 没勾选时 surface 底、border 描边（悬停 borderStrong）。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (!AsCard)
        {
            context.FillRectangle(Brushes.Transparent, bounds);
            return;
        }
        var surface = GetValue(CardSurfaceProperty) ?? Brushes.White;
        var tone = GetValue(ToneFillProperty);
        var (fill, stroke) = IsChecked && tone is ISolidColorBrush toneColor && surface is ISolidColorBrush surfaceColor
            ? (new SolidColorBrush(Mix(toneColor.Color, surfaceColor.Color, 0.08)), tone)
            : (surface, IsPointerOver ? GetValue(CardBorderStrongProperty) : GetValue(CardBorderProperty));
        var radius = GetValue(CardRadiusFactorProperty) * Em;
        context.DrawRectangle(fill, new Pen(stroke, 1), bounds.Deflate(0.5), radius, radius);
    }

    /// <summary>两种颜色按比例混合（CSS 的 color-mix，前一种占 <paramref name="amount"/>；在 sRGB 里插值，8% 时与 oklab 差不到一个色阶）。</summary>
    internal static Color Mix(Color a, Color b, double amount)
    {
        byte Channel(byte x, byte y) => (byte)Math.Round(x * amount + y * (1 - amount));
        return Color.FromArgb(Channel(a.A, b.A), Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B));
    }

    /// <summary>
    /// 读屏的共同部分：名称取文字，没有文字时是所在字段的标签（第一个控件认领）；帮助文字是说明，其次字段的说明与错误（TS 的 aria-describedby）。Avalonia 的读屏接口没有必填。
    /// </summary>
    protected abstract class ChoiceAutomationPeer(ChoiceBase owner) : ControlAutomationPeer(owner)
    {
        /// <inheritdoc />
        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name
            : owner.Content as string ?? (owner.FieldOwner is { } field && field.Owns(owner) ? field.LabelText : null);

        /// <inheritdoc />
        protected override string? GetHelpTextCore() =>
            base.GetHelpTextCore() is { Length: > 0 } help ? help : owner.GetValue(DescriptionCoreProperty) as string ?? owner.FieldOwner?.HelpText;
    }
}

/// <summary>勾选类控件要切换时的参数：<see cref="IsChecked"/> 是要切到的值，设 Cancel 不改。</summary>
/// <param name="isChecked">要切到的值。</param>
public sealed class ChoiceTogglingEventArgs(bool isChecked) : System.ComponentModel.CancelEventArgs
{
    /// <summary>要切到的值。</summary>
    public bool IsChecked { get; } = isChecked;
}

/// <summary>勾选类控件共用的部件名：<c>rune|Checkbox &gt; ContentPresenter#PART_Description</c>。外层（TS 的 propsRoot）就是控件自己。</summary>
public static class ChoiceBaseParts
{
    /// <summary>文字（TS 的 propsLabel）：ContentPresenter。</summary>
    public const string Label = "PART_Label";

    /// <summary>文字下面的说明（TS 的 propsDescription）：ContentPresenter，muted、0.86em。</summary>
    public const string Description = "PART_Description";

    /// <summary>结束边的内容（TS 的 propsEnd：价格、标记）：ContentPresenter，贴着结束边、与文字顶端对齐。</summary>
    public const string End = "PART_End";
}
