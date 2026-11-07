using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 勾选类控件（Checkbox、Radio、Switch）共用的外层（vue-tsx-pro 的 choice-styles）：控件部件（方框、圆、轨道）在起始边，
/// 文字与说明在后面一列，顶端对齐；控件部件居中在第一行文字的行高（1.5em）里。整块可点，高度不低于 24px（WCAG 2.5.8）；
/// 在上面按下、没离开过就松开，或空格松开时切换。部件样式键：各组件自己的叠放层与 <see cref="ChoiceBaseParts"/>（含结束边的内容）。
/// 卡片外观（Checkbox、Radio 的 Card，或跟随组）：整块是一张卡片（内边距 space.md、xs 描边、md 圆角、surface 底，悬停时描边 borderStrong），
/// 勾选时描边换成色调的实底、底色是色调 8% 混进 surface；卡片占满宽度。
/// 只读、必填、出错、色调、字号、说明由派生类按上游的 props 公开（Radio 的这些来自单选组）。
/// </summary>
[ContentProperty(nameof(Content))]
public abstract class ChoiceBase : Control, IFieldControl
{
    /// <summary>是否勾选（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked), typeof(bool), typeof(ChoiceBase), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsCheckedChanged));

    /// <summary>文字（读屏名称）。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(ChoiceBase), new FrameworkPropertyMetadata(null, OnStateChanged));

    /// <summary>色调的实底（TS 的 --tone-fill），按色调取主题资源；部件样式绑它。</summary>
    public static readonly DependencyProperty ToneFillProperty = DependencyProperty.Register(
        nameof(ToneFill), typeof(Brush), typeof(ChoiceBase), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>实底上的标记、文字色（TS 的 --tone-on-fill）。</summary>
    public static readonly DependencyProperty ToneOnFillProperty = DependencyProperty.Register(
        nameof(ToneOnFill), typeof(Brush), typeof(ChoiceBase), new PropertyMetadata(null));

    static readonly DependencyPropertyKey IsFocusVisiblePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsFocusVisible), typeof(bool), typeof(ChoiceBase), new PropertyMetadata(false));

    /// <summary>焦点环是否显示：只在键盘聚焦时（指针点击得到的焦点不显示，同 :focus-visible）。</summary>
    public static readonly DependencyProperty IsFocusVisibleProperty = IsFocusVisiblePropertyKey.DependencyProperty;

    static readonly DependencyPropertyKey IsEffectivelyInvalidPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsEffectivelyInvalid), typeof(bool), typeof(ChoiceBase), new PropertyMetadata(false));

    /// <summary>实际出错：自己写的，其次所在的组、字段的（部件样式的触发器读它）。</summary>
    public static readonly DependencyProperty IsEffectivelyInvalidProperty = IsEffectivelyInvalidPropertyKey.DependencyProperty;

    static readonly DependencyPropertyKey IsEffectivelyReadOnlyPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsEffectivelyReadOnly), typeof(bool), typeof(ChoiceBase), new PropertyMetadata(false));

    /// <summary>实际只读：自己写的，其次所在的组、字段的（主题样式的触发器读它）。</summary>
    public static readonly DependencyProperty IsEffectivelyReadOnlyProperty = IsEffectivelyReadOnlyPropertyKey.DependencyProperty;

    static readonly DependencyPropertyKey IsEffectivelyCardPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsEffectivelyCard), typeof(bool), typeof(ChoiceBase), new PropertyMetadata(false));

    /// <summary>实际是卡片外观：自己写的，其次所在组的（主题样式的触发器读它：卡片占满宽度）。</summary>
    public static readonly DependencyProperty IsEffectivelyCardProperty = IsEffectivelyCardPropertyKey.DependencyProperty;

    static readonly DependencyPropertyKey HasEndPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasEnd), typeof(bool), typeof(ChoiceBase), new PropertyMetadata(false));

    /// <summary>有结束边的内容（主题样式的触发器读它：撑开宽度，结束边的内容才贴得到结束边）。</summary>
    public static readonly DependencyProperty HasEndProperty = HasEndPropertyKey.DependencyProperty;

    // 下面几项由派生类 AddOwner 公开：Switch、Checkbox 自己有，Radio 的来自单选组（上游 RadioProps 没有）
    internal static readonly DependencyProperty IsReadOnlyCoreProperty = DependencyProperty.Register(
        "IsReadOnly", typeof(bool), typeof(ChoiceBase), new PropertyMetadata(false, OnStateChanged));

    internal static readonly DependencyProperty IsRequiredCoreProperty = DependencyProperty.Register(
        "IsRequired", typeof(bool), typeof(ChoiceBase), new PropertyMetadata(false));

    internal static readonly DependencyProperty IsInvalidCoreProperty = DependencyProperty.Register(
        "IsInvalid", typeof(bool), typeof(ChoiceBase), new PropertyMetadata(false, OnStateChanged));

    internal static readonly DependencyProperty ToneCoreProperty = DependencyProperty.Register(
        "Tone", typeof(StatusTone), typeof(ChoiceBase), new FrameworkPropertyMetadata(default(StatusTone), (d, _) => ((ChoiceBase)d).BindTone()));

    internal static readonly DependencyProperty SizeCoreProperty = DependencyProperty.Register(
        "Size", typeof(TextSize), typeof(ChoiceBase), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    internal static readonly DependencyProperty DescriptionCoreProperty = DependencyProperty.Register(
        "Description", typeof(object), typeof(ChoiceBase), new FrameworkPropertyMetadata(null, OnStateChanged));

    internal static readonly DependencyProperty CardCoreProperty = DependencyProperty.Register(
        "Card", typeof(bool?), typeof(ChoiceBase), new FrameworkPropertyMetadata(null, OnStateChanged));

    internal static readonly DependencyProperty EndCoreProperty = DependencyProperty.Register(
        "End", typeof(object), typeof(ChoiceBase), new FrameworkPropertyMetadata(null, OnStateChanged));

    static readonly DependencyProperty GapFactorProperty = DependencyProperty.Register(
        "GapFactor", typeof(double), typeof(ChoiceBase), new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static readonly DependencyProperty MinTargetProperty = DependencyProperty.Register(
        "MinTarget", typeof(double), typeof(ChoiceBase), new FrameworkPropertyMetadata(24d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static readonly DependencyProperty CardPaddingFactorProperty = DependencyProperty.Register(
        "CardPaddingFactor", typeof(double), typeof(ChoiceBase), new FrameworkPropertyMetadata(0.75, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static readonly DependencyProperty CardRadiusFactorProperty = DependencyProperty.Register(
        "CardRadiusFactor", typeof(double), typeof(ChoiceBase), new FrameworkPropertyMetadata(0.375, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty CardSurfaceProperty = DependencyProperty.Register(
        "CardSurface", typeof(Brush), typeof(ChoiceBase), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty CardBorderProperty = DependencyProperty.Register(
        "CardBorder", typeof(Brush), typeof(ChoiceBase), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty CardBorderStrongProperty = DependencyProperty.Register(
        "CardBorderStrong", typeof(Brush), typeof(ChoiceBase), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    readonly ContentPresenter label = new() { Name = "PART_Label" };
    readonly ContentPresenter description = new() { Name = "PART_Description" };
    readonly ContentPresenter end = new() { Name = "PART_End" };
    Panel? box;
    bool pressed;
    bool spacePressed;

    static ChoiceBase()
    {
        FocusableProperty.OverrideMetadata(typeof(ChoiceBase), new FrameworkPropertyMetadata(true));
        FontSizeProperty.OverrideMetadata(typeof(ChoiceBase), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((ChoiceBase)d).UpdateMetrics()));
        Leading.FactorProperty.OverrideMetadata(typeof(ChoiceBase), new FrameworkPropertyMetadata(Leading.Normal, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((ChoiceBase)d).UpdateMetrics()));
    }

    /// <summary>新建：文字与说明引用主题的部件样式，间距、色调绑到主题。</summary>
    protected ChoiceBase()
    {
        label.SetResourceReference(StyleProperty, ChoiceBaseParts.Label);
        description.SetResourceReference(StyleProperty, ChoiceBaseParts.Description);
        end.SetResourceReference(StyleProperty, ChoiceBaseParts.End);
        SetResourceReference(GapFactorProperty, "Rune.Space.Sm");
        SetResourceReference(MinTargetProperty, "Rune.Size.MinTarget");
        SetResourceReference(CardPaddingFactorProperty, "Rune.Space.Md");
        SetResourceReference(CardRadiusFactorProperty, "Rune.Radius.Md");
        SetResourceReference(CardSurfaceProperty, "Rune.Brush.Surface");
        SetResourceReference(CardBorderProperty, "Rune.Brush.Border");
        SetResourceReference(CardBorderStrongProperty, "Rune.Brush.BorderStrong");
        BindTone();
    }

    /// <summary>
    /// 要切换时（点击、空格、读屏）先发：设 Cancel 不改值——切换要等接口、要先确认时拦下，之后再写 IsChecked（TS 的受控 onUpdate:_checked）。
    /// </summary>
    public event EventHandler<ChoiceTogglingEventArgs>? Toggling;

    /// <summary>是否勾选。</summary>
    public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>文字。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>色调的实底。</summary>
    public Brush? ToneFill { get => (Brush?)GetValue(ToneFillProperty); set => SetValue(ToneFillProperty, value); }

    /// <summary>实底上的颜色。</summary>
    public Brush? ToneOnFill { get => (Brush?)GetValue(ToneOnFillProperty); set => SetValue(ToneOnFillProperty, value); }

    /// <summary>焦点环是否显示。</summary>
    public bool IsFocusVisible => (bool)GetValue(IsFocusVisibleProperty);

    /// <summary>实际出错。</summary>
    public bool IsEffectivelyInvalid => (bool)GetValue(IsEffectivelyInvalidProperty);

    /// <summary>实际只读。</summary>
    public bool IsEffectivelyReadOnly => (bool)GetValue(IsEffectivelyReadOnlyProperty);

    /// <summary>实际是卡片外观。</summary>
    public bool IsEffectivelyCard => (bool)GetValue(IsEffectivelyCardProperty);

    /// <summary>有结束边的内容。</summary>
    public bool HasEnd => (bool)GetValue(HasEndProperty);

    /// <summary>所在的字段：在复选组、单选组里时字段归组（项不接字段）。</summary>
    private protected Field? FieldOwner => InGroup ? null : Field.Of(this);

    /// <summary>在复选组、单选组里。</summary>
    private protected virtual bool InGroup => false;

    /// <summary>只读：能聚焦，不能切换；外观照常。自己写了的优先，其次所在字段的。</summary>
    protected internal virtual bool ReadOnly => Field.IsSet(this, IsReadOnlyCoreProperty) ? (bool)GetValue(IsReadOnlyCoreProperty) : FieldOwner?.IsReadOnly ?? false;

    /// <summary>校验未通过：自己写了的优先，其次所在字段的。</summary>
    protected internal virtual bool Invalid => Field.IsSet(this, IsInvalidCoreProperty) ? (bool)GetValue(IsInvalidCoreProperty) : FieldOwner?.Invalid ?? false;

    /// <summary>必填：自己写了的优先，其次所在字段的。</summary>
    protected internal virtual bool Required => Field.IsSet(this, IsRequiredCoreProperty) ? (bool)GetValue(IsRequiredCoreProperty) : FieldOwner?.IsRequired ?? false;

    /// <summary>所在的复选组、单选组（卡片外观跟随它）。</summary>
    private protected virtual ChoiceGroup? OwnerGroup => null;

    /// <summary>卡片外观：自己写了的优先，其次所在组的。</summary>
    internal bool AsCard => (bool?)GetValue(CardCoreProperty) ?? OwnerGroup?.Card ?? false;

    /// <summary>内容离外沿的距离：卡片的内边距（space.md）加描边（xs，1px）；不是卡片时为 0。</summary>
    double Inset => AsCard ? (double)GetValue(CardPaddingFactorProperty) * Em + 1 : 0;

    /// <summary>这次能不能切换（只读、不可用之外的理由，如 Switch 切换中）。</summary>
    protected virtual bool CanToggle => true;

    /// <summary>切换后的值：复选框、开关取反，单选项总是选中。</summary>
    protected virtual bool NextChecked => !IsChecked;

    /// <summary>em：字号。</summary>
    protected double Em => FontSize;

    double Gap => (double)GetValue(GapFactorProperty) * Em;

    /// <summary>派生类在构造函数里交出控件部件（叠放层），与文字、说明一起成为子元素。</summary>
    /// <param name="control">控件部件。</param>
    protected void Compose(Panel control)
    {
        box = control;
        foreach (var child in new UIElement[] { control, label, description, end })
        {
            AddVisualChild(child);
            AddLogicalChild(child);
        }
        UpdateMetrics();
        Update();
    }

    /// <inheritdoc />
    protected override int VisualChildrenCount => box is null ? 0 : 4;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index switch
    {
        0 => box!,
        1 => label,
        2 => description,
        _ => end,
    };

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => (box is null ? Array.Empty<UIElement>() : new UIElement[] { box, label, description, end }).GetEnumerator();

    static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var choice = (ChoiceBase)d;
        choice.Update();
        choice.OnCheckedChanged((bool)e.OldValue, (bool)e.NewValue);
    }

    static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ChoiceBase)d).Update();

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    void BindTone()
    {
        var (fill, onFill) = Rune.Wpf.ToneFill.Keys((StatusTone)GetValue(ToneCoreProperty));
        SetResourceReference(ToneFillProperty, fill);
        SetResourceReference(ToneOnFillProperty, onFill);
    }

    /// <summary>勾选变了（Switch 让滑块滑过去、读屏的状态通知）。</summary>
    /// <param name="oldValue">原来的值。</param>
    /// <param name="newValue">新的值。</param>
    protected virtual void OnCheckedChanged(bool oldValue, bool newValue) { }

    void IFieldControl.FieldChanged() => Update();

    /// <summary>文字与说明、实际的出错与只读；派生类加上自己随状态变的部分。</summary>
    protected internal virtual void Update()
    {
        SetValue(IsEffectivelyInvalidPropertyKey, Invalid);
        SetValue(IsEffectivelyReadOnlyPropertyKey, ReadOnly);
        label.Content = Content;
        label.Visibility = Content is null ? Visibility.Collapsed : Visibility.Visible;
        description.Content = GetValue(DescriptionCoreProperty);
        description.Visibility = description.Content is null ? Visibility.Collapsed : Visibility.Visible;
        end.Content = GetValue(EndCoreProperty);
        end.Visibility = end.Content is null ? Visibility.Collapsed : Visibility.Visible;
        SetValue(IsEffectivelyCardPropertyKey, AsCard);
        SetValue(HasEndPropertyKey, end.Content is not null);
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// em 尺寸：文字与说明的行高（TextBlock 的行高是像素数，文字是 ContentPresenter 里生成的 TextBlock，从它继承）、说明小一号（0.875em）；
    /// 派生类加上控件部件的圆角等。
    /// </summary>
    protected virtual void UpdateMetrics()
    {
        var leading = Leading.GetFactor(this);
        label.SetValue(TextBlock.LineHeightProperty, leading * Em);
        description.SetValue(TextElement.FontSizeProperty, 0.875 * Em);
        description.SetValue(TextBlock.LineHeightProperty, leading * 0.875 * Em);
        box?.InvalidateMeasure();
    }

    /// <summary>把勾选写回：复选组、单选组里时交给组。</summary>
    /// <param name="value">新的值。</param>
    protected virtual void ApplyChecked(bool value) => SetCurrentValue(IsCheckedProperty, value);

    /// <summary>切换（点击、空格、读屏）：只读、不可用、值不变时什么都不做。</summary>
    internal void Toggle()
    {
        if (ReadOnly || !CanToggle || !IsEnabled) return;
        var next = NextChecked;
        if (next == IsChecked) return;
        var args = new ChoiceTogglingEventArgs(next);
        Toggling?.Invoke(this, args);
        if (!args.Cancel) ApplyChecked(next);
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        pressed = true;
        Focus();
        e.Handled = true;
    }

    /// <summary>在上面按下、没离开过就松开才切换（离开再回来不算）。</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!pressed) return;
        pressed = false;
        e.Handled = true;
        Toggle();
    }

    /// <summary>卡片悬停时描边换成 borderStrong。</summary>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (AsCard) InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        pressed = false;
        if (AsCard) InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Space) return;
        spacePressed = true;
        e.Handled = true;
    }

    /// <summary>空格松开时切换（同原生 checkbox）。</summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key != Key.Space) return;
        e.Handled = true;
        if (!spacePressed) return;
        spacePressed = false;
        Toggle();
    }

    /// <summary>焦点环只在键盘聚焦时显示。</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        SetValue(IsFocusVisiblePropertyKey, InputManager.Current.MostRecentInputDevice is KeyboardDevice);
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        SetValue(IsFocusVisiblePropertyKey, false);
        spacePressed = false;
    }

    /// <summary>
    /// 画一层透明底：控件与文字之间的空隙也能点（TS 的 label 整块可点）。卡片外观时画卡片：勾选时描边是色调的实底、底色是色调 8% 混进 surface，
    /// 没勾选时 surface 底、border 描边（悬停 borderStrong）。
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        var bounds = new Rect(RenderSize);
        if (!AsCard)
        {
            drawingContext.DrawRectangle(Brushes.Transparent, null, bounds);
            return;
        }
        var surface = GetValue(CardSurfaceProperty) as Brush ?? Brushes.White;
        var (fill, stroke) = IsChecked && ToneFill is SolidColorBrush tone && surface is SolidColorBrush surfaceColor
            ? (new SolidColorBrush(Mix(tone.Color, surfaceColor.Color, 0.08)), (Brush)tone)
            : (surface, (Brush?)GetValue(IsMouseOver ? CardBorderStrongProperty : CardBorderProperty));
        var radius = (double)GetValue(CardRadiusFactorProperty) * Em;
        bounds.Inflate(-0.5, -0.5);
        drawingContext.DrawRoundedRectangle(fill, new Pen(stroke, 1), bounds, radius, radius);
    }

    /// <summary>两种颜色按比例混合（CSS 的 color-mix，前一种占 <paramref name="amount"/>；在 sRGB 里插值，8% 时与 oklab 差不到一个色阶）。</summary>
    static Color Mix(Color a, Color b, double amount)
    {
        byte Channel(byte x, byte y) => (byte)Math.Round(x * amount + y * (1 - amount));
        return Color.FromArgb(Channel(a.A, b.A), Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var inset = Inset;
        constraint = new Size(Math.Max(0, constraint.Width - 2 * inset), Math.Max(0, constraint.Height - 2 * inset));
        box!.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var boxSize = box.DesiredSize;
        var endSize = default(Size);
        if (end.Visibility != Visibility.Collapsed)
        {
            end.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            endSize = end.DesiredSize;
        }
        var endWidth = endSize.Width > 0 ? endSize.Width + Gap : 0;
        var text = new Size(Math.Max(0, constraint.Width - boxSize.Width - Gap - endWidth), constraint.Height);
        var textSize = default(Size);
        foreach (var part in new[] { label, description })
        {
            if (part.Visibility == Visibility.Collapsed) continue;
            part.Measure(text);
            textSize = new Size(Math.Max(textSize.Width, part.DesiredSize.Width), textSize.Height + part.DesiredSize.Height);
        }
        var width = boxSize.Width + (textSize.Width > 0 ? Gap + textSize.Width : 0) + endWidth;
        var height = Math.Max(Math.Max(boxSize.Height, endSize.Height), textSize.Height);
        return new Size(width + 2 * inset, Math.Max((double)GetValue(MinTargetProperty), height + 2 * inset));
    }

    /// <summary>控件部件与文字一行、顶端对齐；说明在文字下面；结束边的内容贴着结束边（margin-inline-start: auto）。</summary>
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var inset = Inset;
        box!.Arrange(new Rect(new Point(inset, inset), box.DesiredSize));
        var x = inset + box.DesiredSize.Width + Gap;
        var right = arrangeBounds.Width - inset;
        if (end.Visibility != Visibility.Collapsed)
        {
            end.Arrange(new Rect(right - end.DesiredSize.Width, inset, end.DesiredSize.Width, end.DesiredSize.Height));
            right -= end.DesiredSize.Width + Gap;
        }
        var width = Math.Max(0, right - x);
        var y = inset;
        foreach (var part in new[] { label, description })
        {
            if (part.Visibility == Visibility.Collapsed) continue;
            part.Arrange(new Rect(x, y, width, part.DesiredSize.Height));
            y += part.DesiredSize.Height;
        }
        return arrangeBounds;
    }

    /// <summary>
    /// 读屏的共同部分：名称取文字，没有文字时是所在字段的标签（第一个控件认领）；帮助文字是说明，其次字段的说明与错误（TS 的 aria-describedby）；
    /// 必填；控件部件不进子节点。
    /// </summary>
    protected abstract class ChoiceAutomationPeer(ChoiceBase owner) : FrameworkElementAutomationPeer(owner)
    {
        /// <inheritdoc />
        protected override string GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name
            : owner.Content as string ?? (owner.FieldOwner is { } field && field.Owns(owner) ? field.LabelText : null) ?? "";

        /// <inheritdoc />
        protected override string GetHelpTextCore() =>
            base.GetHelpTextCore() is { Length: > 0 } help ? help : owner.GetValue(DescriptionCoreProperty) as string ?? owner.FieldOwner?.HelpText ?? "";

        /// <inheritdoc />
        protected override bool IsRequiredForFormCore() => owner.Required;

        /// <inheritdoc />
        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>勾选类控件要切换时的参数：<see cref="IsChecked"/> 是要切到的值，设 Cancel 不改。</summary>
/// <param name="isChecked">要切到的值。</param>
public sealed class ChoiceTogglingEventArgs(bool isChecked) : CancelEventArgs
{
    /// <summary>要切到的值。</summary>
    public bool IsChecked { get; } = isChecked;
}

/// <summary>
/// 勾选类控件共用的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:ChoiceBaseParts.Description}" TargetType="ContentPresenter"&gt;</c>。
/// 外层（TS 的 propsRoot）就是控件自己。
/// </summary>
public static class ChoiceBaseParts
{
    /// <summary>文字（TS 的 propsLabel）：ContentPresenter。</summary>
    public static readonly PartKey Label = new(typeof(ChoiceBase), nameof(Label), typeof(ContentPresenter));

    /// <summary>文字下面的说明（TS 的 propsDescription）：ContentPresenter，muted、0.875em。</summary>
    public static readonly PartKey Description = new(typeof(ChoiceBase), nameof(Description), typeof(ContentPresenter));

    /// <summary>结束边的内容（TS 的 propsEnd：价格、标记）：ContentPresenter，贴着结束边、与文字顶端对齐。</summary>
    public static readonly PartKey End = new(typeof(ChoiceBase), nameof(End), typeof(ContentPresenter));
}
