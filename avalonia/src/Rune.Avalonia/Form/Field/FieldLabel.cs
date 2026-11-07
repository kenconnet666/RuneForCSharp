using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 表单标签（vue-tsx-pro 的 Label；Avalonia 自带 Label，这里叫 FieldLabel）：md 字号、中等字重、行高 tight、正文色；
/// 必填时文字后有一个星号（0.7em、dangerText，对读屏隐藏，必填由控件告诉读屏）；不可用时变淡。
/// 点它聚焦 <see cref="Target"/>（是一组时聚焦组里能聚焦的第一项，同点原生输入框的标签）。<see cref="Field"/> 里自动关联。
/// 部件：文字 <see cref="FieldLabelParts.Content"/>、星号 <see cref="FieldLabelParts.Required"/>，默认外观在 FieldParts.axaml。
/// </summary>
[PseudoClasses(":required")]
public class FieldLabel : Control
{
    /// <summary>文字。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<FieldLabel, object?>(nameof(Content));

    /// <summary>必填：文字后加星号。</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AvaloniaProperty.Register<FieldLabel, bool>(nameof(IsRequired));

    /// <summary>点标签时聚焦的控件。</summary>
    public static readonly StyledProperty<Control?> TargetProperty = AvaloniaProperty.Register<FieldLabel, Control?>(nameof(Target));

    /// <summary>字号（继承），星号与间距按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<FieldLabel>();

    readonly ContentPresenter content = new() { Name = FieldLabelParts.Content };
    readonly Icon required = new() { Name = FieldLabelParts.Required, Data = Lucide.Asterisk, IsVisible = false };

    static FieldLabel() => AffectsMeasure<FieldLabel>(ContentProperty, IsRequiredProperty, FontSizeProperty);

    /// <summary>新建：星号对读屏隐藏。</summary>
    public FieldLabel()
    {
        AutomationProperties.SetAccessibilityView(required, AccessibilityView.Raw);
        LogicalChildren.AddRange([content, required]);
        VisualChildren.AddRange([content, required]);
        UpdateLineHeight();
    }

    /// <summary>文字。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>点标签时聚焦的控件。</summary>
    public Control? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>文字的第一行基线（相对标签的顶边）；<see cref="Field"/> 标签在起始边时按它与控件对齐。</summary>
    internal double? Baseline => Field.FirstBaseline(content) is { } baseline ? content.Bounds.Y + baseline : null;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty) content.Content = Content;
        else if (change.Property == IsRequiredProperty)
        {
            required.IsVisible = IsRequired;
            PseudoClasses.Set(":required", IsRequired);
        }
        else if (change.Property == FontSizeProperty || change.Property == Leading.FactorProperty) UpdateLineHeight();
    }

    /// <summary>文字是普通字符串时 TextBlock 的行高是像素数：按字号乘行高系数（同 Text）。新建时也算一次（字号没变时不会有通知）。</summary>
    void UpdateLineHeight() => content.SetValue(ContentPresenter.LineHeightProperty, Leading.GetFactor(this) * FontSize, BindingPriority.Template);

    /// <summary>画一层透明底：整块可点（点标签聚焦控件）。</summary>
    public override void Render(DrawingContext context) => context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

    /// <summary>点标签：聚焦目标。</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Target is not { } target || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Field.FocusControl(target);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var side = 0.7 * FontSize;
        var gap = IsRequired ? Gap : 0;
        content.Measure(availableSize.Deflate(new Thickness(IsRequired ? side + gap : 0, 0, 0, 0)));
        required.Size = side;
        required.Measure(LayoutSize.Infinity);
        var width = content.DesiredSize.Width + (IsRequired ? gap + side : 0);
        return new LayoutSize(width, Math.Max(content.DesiredSize.Height, IsRequired ? side : 0));
    }

    /// <summary>星号紧跟文字、贴着第一行的顶边（TS 的 align-self: flex-start）。</summary>
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var side = 0.7 * FontSize;
        var width = Math.Min(content.DesiredSize.Width, finalSize.Width - (IsRequired ? side + Gap : 0));
        content.Arrange(new Rect(0, 0, Math.Max(0, width), content.DesiredSize.Height));
        if (IsRequired) required.Arrange(new Rect(width + Gap, 0, side, side));
        return finalSize;
    }

    /// <summary>文字与星号的间距：space.xs。</summary>
    double Gap => (this.Resource<double>("Rune.Space.Xs", 0.25)) * FontSize;

    /// <summary>读屏：标签（名称是文字），指向目标。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new FieldLabelAutomationPeer(this);

    sealed class FieldLabelAutomationPeer(FieldLabel owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Field.PlainText(owner.Content);
    }
}

/// <summary>FieldLabel 的部件名：<c>rune|FieldLabel &gt; rune|Icon#PART_Required</c>。</summary>
public static class FieldLabelParts
{
    /// <summary>文字：ContentPresenter。</summary>
    public const string Content = "PART_Content";

    /// <summary>必填的星号（TS 的 propsRequired）：Icon。</summary>
    public const string Required = "PART_Required";
}
