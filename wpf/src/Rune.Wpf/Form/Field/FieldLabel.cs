using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 表单标签（vue-tsx-pro 的 Label；WPF 自带 Label，这里叫 FieldLabel）：md 字号、中等字重、行高 tight、正文色；
/// 必填时文字后有一个星号（0.7em、dangerText，对读屏隐藏，必填由控件告诉读屏）；不可用时变淡。
/// 点它聚焦 <see cref="Target"/>（是一组时聚焦组里的 Tab 停靠项，同点原生输入框的标签）。<see cref="Field"/> 里自动关联。
/// 部件样式键见 <see cref="FieldLabelParts"/>，默认外观在 Field.xaml。
/// </summary>
[ContentProperty(nameof(Content))]
public class FieldLabel : Control
{
    /// <summary>文字。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(FieldLabel), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, e) => ((FieldLabel)d).content.Content = e.NewValue));

    /// <summary>必填：文字后加星号。</summary>
    public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(
        nameof(IsRequired), typeof(bool), typeof(FieldLabel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure,
            (d, e) => ((FieldLabel)d).required.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    /// <summary>点标签时聚焦的控件。</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(UIElement), typeof(FieldLabel), new PropertyMetadata(null));

    static readonly DependencyProperty GapFactorProperty = DependencyProperty.Register(
        "GapFactor", typeof(double), typeof(FieldLabel), new FrameworkPropertyMetadata(0.25, FrameworkPropertyMetadataOptions.AffectsMeasure));

    readonly ContentPresenter content = new() { Name = "PART_Content" };
    readonly Icon required = new() { Name = "PART_Required", Data = Lucide.Asterisk, Visibility = Visibility.Collapsed };

    static FieldLabel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FieldLabel), new FrameworkPropertyMetadata(typeof(FieldLabel)));
        FontSizeProperty.OverrideMetadata(typeof(FieldLabel), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((FieldLabel)d).UpdateMetrics()));
        Leading.FactorProperty.OverrideMetadata(typeof(FieldLabel), new FrameworkPropertyMetadata(Leading.Normal, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((FieldLabel)d).UpdateMetrics()));
    }

    /// <summary>新建：部件引用主题的部件样式；星号对读屏隐藏。</summary>
    public FieldLabel()
    {
        content.SetResourceReference(StyleProperty, FieldLabelParts.Content);
        required.SetResourceReference(StyleProperty, FieldLabelParts.Required);
        SetResourceReference(GapFactorProperty, "Rune.Space.Xs");
        foreach (var child in new UIElement[] { content, required })
        {
            AddVisualChild(child);
            AddLogicalChild(child);
        }
        UpdateMetrics();
    }

    /// <summary>文字。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>点标签时聚焦的控件。</summary>
    public UIElement? Target { get => (UIElement?)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <summary>文字的第一行基线（相对标签的顶边）；<see cref="Field"/> 标签在起始边时按它与控件对齐。</summary>
    internal double? Baseline => Field.FirstBaseline(this);

    /// <inheritdoc />
    protected override int VisualChildrenCount => 2;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index == 0 ? content : required;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new UIElement[] { content, required }.GetEnumerator();

    double Gap => (double)GetValue(GapFactorProperty) * FontSize;

    /// <summary>文字的行高（TextBlock 的行高是像素数，从 ContentPresenter 继承）、星号 0.7em。新建时也算一次（字号没变时不会有通知）。</summary>
    void UpdateMetrics()
    {
        content.SetValue(TextBlock.LineHeightProperty, Leading.GetFactor(this) * FontSize);
        required.Size = 0.7 * FontSize;
    }

    /// <summary>点标签：聚焦目标。</summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Target is not { } target) return;
        Field.FocusControl(target);
        e.Handled = true;
    }

    /// <summary>画一层透明底：整块可点。</summary>
    protected override void OnRender(DrawingContext drawingContext) => drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var side = 0.7 * FontSize;
        var extra = IsRequired ? side + Gap : 0;
        content.Measure(new Size(Math.Max(0, constraint.Width - extra), constraint.Height));
        required.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new Size(content.DesiredSize.Width + extra, Math.Max(content.DesiredSize.Height, IsRequired ? side : 0));
    }

    /// <summary>星号紧跟文字、贴着第一行的顶边（TS 的 align-self: flex-start）。</summary>
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var side = 0.7 * FontSize;
        var width = Math.Max(0, Math.Min(content.DesiredSize.Width, arrangeBounds.Width - (IsRequired ? side + Gap : 0)));
        content.Arrange(new Rect(0, 0, width, content.DesiredSize.Height));
        if (IsRequired) required.Arrange(new Rect(width + Gap, 0, side, side));
        return arrangeBounds;
    }

    /// <summary>读屏：文字（名称是文字），星号不进子节点。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new FieldLabelAutomationPeer(this);

    sealed class FieldLabelAutomationPeer(FieldLabel owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(FieldLabel);

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Field.PlainText(owner.Content) ?? "";

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>FieldLabel 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:FieldLabelParts.Required}" TargetType="rune:Icon"&gt;</c>。</summary>
public static class FieldLabelParts
{
    /// <summary>文字：ContentPresenter。</summary>
    public static readonly PartKey Content = new(typeof(FieldLabel), nameof(Content), typeof(ContentPresenter));

    /// <summary>必填的星号（TS 的 propsRequired）：Icon，dangerText。</summary>
    public static readonly PartKey Required = new(typeof(FieldLabel), nameof(Required), typeof(Icon));
}
