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
/// 空格松开时切换。部件：控件部件（各组件自己的叠放层）、<see cref="ChoiceBaseParts.Label"/>、<see cref="ChoiceBaseParts.Description"/>。
/// 只读、必填、出错、色调、字号、说明由派生类按上游的 props 公开（Radio 的这些来自单选组）。
/// </summary>
[PseudoClasses(":checked", ":readonly", ":invalid")]
public abstract class ChoiceBase : Control
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

    static readonly StyledProperty<double> GapFactorProperty = AvaloniaProperty.Register<ChoiceBase, double>("GapFactor", 0.5);
    static readonly StyledProperty<double> MinTargetProperty = AvaloniaProperty.Register<ChoiceBase, double>("MinTarget", 24);

    readonly ContentPresenter label = new() { Name = ChoiceBaseParts.Label };
    readonly ContentPresenter description = new() { Name = ChoiceBaseParts.Description };
    readonly TemplateResources resources;
    Control? box;
    bool pressed;

    static ChoiceBase()
    {
        FocusableProperty.OverrideDefaultValue<ChoiceBase>(true);
        AffectsMeasure<ChoiceBase>(FontSizeProperty, GapFactorProperty, MinTargetProperty, ContentProperty, DescriptionCoreProperty);
    }

    /// <summary>新建：间距、色调绑到主题。</summary>
    protected ChoiceBase()
    {
        resources = new TemplateResources(this);
        resources.Bind(GapFactorProperty, "Rune.Space.Sm");
        resources.Bind(MinTargetProperty, "Rune.Size.MinTarget");
        BindTone();
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

    /// <summary>只读：能聚焦，不能切换；外观照常。</summary>
    protected virtual bool ReadOnly => GetValue(IsReadOnlyCoreProperty);

    /// <summary>校验未通过。</summary>
    protected virtual bool Invalid => GetValue(IsInvalidCoreProperty);

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
        LogicalChildren.AddRange([control, label, description]);
        VisualChildren.AddRange([control, label, description]);
        Update();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty || change.Property == Leading.FactorProperty)
        {
            box?.InvalidateMeasure();
            // 说明小一号（0.875em）；文字是普通字符串时 TextBlock 的行高是像素数，按各自的字号乘行高系数（同 Text）。主题样式与使用方的值盖得过
            var leading = Leading.GetFactor(this);
            description.SetValue(ContentPresenter.FontSizeProperty, 0.875 * Em, BindingPriority.Template);
            description.SetValue(ContentPresenter.LineHeightProperty, leading * 0.875 * Em, BindingPriority.Template);
            label.SetValue(ContentPresenter.LineHeightProperty, leading * Em, BindingPriority.Template);
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
                 || change.Property == DescriptionCoreProperty) Update();
    }

    void BindTone()
    {
        var (fill, onFill) = Rune.Avalonia.ToneFill.Keys(GetValue(ToneCoreProperty));
        resources.Bind(ToneFillProperty, fill);
        resources.Bind(ToneOnFillProperty, onFill);
    }

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
        box!.Measure(LayoutSize.Infinity);
        var boxSize = box.DesiredSize;
        var text = availableSize.Deflate(new Thickness(boxSize.Width + Gap, 0, 0, 0));
        var textSize = default(LayoutSize);
        foreach (var part in new[] { label, description })
        {
            if (!part.IsVisible) continue;
            part.Measure(text);
            textSize = new LayoutSize(Math.Max(textSize.Width, part.DesiredSize.Width), textSize.Height + part.DesiredSize.Height);
        }
        var width = boxSize.Width + (textSize.Width > 0 ? Gap + textSize.Width : 0);
        return new LayoutSize(width, Math.Max(GetValue(MinTargetProperty), Math.Max(boxSize.Height, textSize.Height)));
    }

    /// <summary>控件部件与文字一行、顶端对齐；说明在文字下面。</summary>
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        box!.Arrange(new Rect(box.DesiredSize));
        var x = box.DesiredSize.Width + Gap;
        var width = Math.Max(0, finalSize.Width - x);
        var y = 0d;
        foreach (var part in new[] { label, description })
        {
            if (!part.IsVisible) continue;
            part.Arrange(new Rect(x, y, width, part.DesiredSize.Height));
            y += part.DesiredSize.Height;
        }
        return finalSize;
    }

    /// <summary>画一层透明底：控件与文字之间的空隙也能点（TS 的 label 整块可点）。</summary>
    public override void Render(DrawingContext context) => context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

    /// <summary>读屏的共同部分：名称取文字，说明是帮助文字（TS 的 aria-describedby）。</summary>
    protected abstract class ChoiceAutomationPeer(ChoiceBase owner) : ControlAutomationPeer(owner)
    {
        /// <inheritdoc />
        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Content as string;

        /// <inheritdoc />
        protected override string? GetHelpTextCore() => base.GetHelpTextCore() is { Length: > 0 } help ? help : owner.GetValue(DescriptionCoreProperty) as string;
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

    /// <summary>文字下面的说明（TS 的 propsDescription）：ContentPresenter，muted、0.875em。</summary>
    public const string Description = "PART_Description";
}
