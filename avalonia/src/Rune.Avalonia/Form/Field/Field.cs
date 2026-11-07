using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 表单字段（vue-tsx-pro 的 Field）：标签 + 控件 + 说明 + 错误信息，并把字段的状态交给里面的控件。
/// <code>
/// &lt;rune:Field Label="通知方式" IsRequired="True" Error="{Binding ChannelsError}"&gt;
///   &lt;rune:CheckboxGroup Value="{Binding Channels}" Orientation="Horizontal"&gt;…&lt;/rune:CheckboxGroup&gt;
/// &lt;/rune:Field&gt;
/// </code>
/// 里面的控件（勾选类、复选组、单选组，以后的输入框）取字段的出错、必填、只读（控件上自己写了的优先），读屏的帮助文字是说明与错误；
/// 第一个控件认领标签：没有自己的名称时以标签为名，点标签聚焦它。有错误（且 IsInvalid 不是 false）时显示错误、控件标红；只写 IsInvalid 时只标红。
/// 不可用写 IsEnabled（里面一起不可用，标签变淡）。
/// 标签默认在上方，竖排间距 space.xs；<see cref="LabelPlacement"/> 为 start 时标签一列（<see cref="LabelWidth"/>，默认 8em；
/// 文字默认贴着控件右对齐）、控件一列，列距 space.md，标签与控件第一行文字的基线对齐。<see cref="IsLabelHidden"/> 时标签只给读屏、不占位置。
/// 说明与错误是 sm 字号、行高 normal（muted；错误是 dangerText，前面有警示图标）。说明、错误是文字（上游可以是任意内容）。
/// 部件：<see cref="FieldParts"/>，默认外观在 FieldParts.axaml。表单（Form）、字段布局的统一设置以后做。
/// </summary>
[PseudoClasses(":invalid", ":start", ":label-hidden")]
public class Field : Control
{
    /// <summary>所在的字段（继承给里面的元素，控件从它读字段的状态）。</summary>
    internal static readonly AttachedProperty<Field?> OwnerProperty = AvaloniaProperty.RegisterAttached<Field, Control, Field?>("Owner", inherits: true);

    /// <summary>标签。</summary>
    public static readonly StyledProperty<object?> LabelProperty = AvaloniaProperty.Register<Field, object?>(nameof(Label));

    /// <summary>控件下方的说明。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<Field, string?>(nameof(Description));

    /// <summary>错误信息：给了（且 IsInvalid 不是 false）就显示，并把控件标为出错。</summary>
    public static readonly StyledProperty<string?> ErrorProperty = AvaloniaProperty.Register<Field, string?>(nameof(Error));

    /// <summary>校验未通过；不写时有错误就算出错，写 false 时不显示错误。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<Field, bool?>(nameof(IsInvalid));

    /// <summary>必填：标签后加星号，控件读屏念"必填"。</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AvaloniaProperty.Register<Field, bool>(nameof(IsRequired));

    /// <summary>只读：交给里面的控件。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<Field, bool>(nameof(IsReadOnly));

    /// <summary>标签放在哪；默认上方。</summary>
    public static readonly StyledProperty<FieldLabelPlacement> LabelPlacementProperty = AvaloniaProperty.Register<Field, FieldLabelPlacement>(nameof(LabelPlacement));

    /// <summary>标签在起始边时标签列的宽度（设备无关像素）；不写（NaN）时 8em。</summary>
    public static readonly StyledProperty<double> LabelWidthProperty = AvaloniaProperty.Register<Field, double>(nameof(LabelWidth), double.NaN);

    /// <summary>标签在起始边时文字的对齐；默认 end（贴着控件）。</summary>
    public static readonly StyledProperty<FieldLabelAlign> LabelAlignProperty = AvaloniaProperty.Register<Field, FieldLabelAlign>(nameof(LabelAlign));

    /// <summary>标签只给读屏：视觉上隐藏，仍是控件的名称；不占标签列。</summary>
    public static readonly StyledProperty<bool> IsLabelHiddenProperty = AvaloniaProperty.Register<Field, bool>(nameof(IsLabelHidden));

    /// <summary>控件。</summary>
    public static readonly StyledProperty<Control?> ContentProperty = AvaloniaProperty.Register<Field, Control?>(nameof(Content));

    /// <summary>字号（继承），间距与标签列宽按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Field>();

    readonly FieldLabel label = new() { Name = FieldParts.Label };
    readonly Text description = new() { Name = FieldParts.Description, Size = TextSize.Sm, Tone = TextTone.Muted };
    readonly Text errorText = new() { Tone = TextTone.Danger };
    readonly Flex error = new() { Name = FieldParts.Error, Gap = Space.Xs, Align = FlexAlign.Start };
    readonly Icon errorIcon = new() { Data = Lucide.CircleExclamationPoint, Tone = TextTone.Danger };

    static Field() => AffectsMeasure<Field>(LabelProperty, DescriptionProperty, ErrorProperty, IsInvalidProperty, LabelPlacementProperty, LabelWidthProperty, IsLabelHiddenProperty, ContentProperty, FontSizeProperty);

    /// <summary>新建：标签在上方。</summary>
    public Field()
    {
        SetValue(OwnerProperty, this);
        error.Children.AddRange([errorIcon, errorText]);
        LogicalChildren.AddRange([label, description, error]);
        VisualChildren.AddRange([label, description, error]);
        Update();
    }

    /// <summary>标签。</summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>错误信息。</summary>
    public string? Error { get => GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>标签放在哪。</summary>
    public FieldLabelPlacement LabelPlacement { get => GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <summary>标签列的宽度。</summary>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>标签文字的对齐。</summary>
    public FieldLabelAlign LabelAlign { get => GetValue(LabelAlignProperty); set => SetValue(LabelAlignProperty, value); }

    /// <summary>标签只给读屏。</summary>
    public bool IsLabelHidden { get => GetValue(IsLabelHiddenProperty); set => SetValue(IsLabelHiddenProperty, value); }

    /// <summary>控件。</summary>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>出错：写了 IsInvalid 按它，否则有错误就算。</summary>
    internal bool Invalid => IsInvalid ?? HasText(Error);

    bool ShowsError => HasText(Error) && IsInvalid != false;

    bool HasLabel => Label is not null and not "";

    bool Start => LabelPlacement == FieldLabelPlacement.Start && !IsLabelHidden;

    /// <summary>标签的文字（读屏名称）。</summary>
    internal string? LabelText => PlainText(Label);

    /// <summary>读屏的帮助文字：说明与（显示着的）错误。</summary>
    internal string? HelpText => string.Join(" ", new[] { Description, ShowsError ? Error : null }.Where(HasText)) is { Length: > 0 } text ? text : null;

    /// <summary>里面接入字段的控件（深度优先、不进到控件里面）：第一个认领标签。</summary>
    List<IFieldControl> Controls()
    {
        var found = new List<IFieldControl>();
        if (Content is { } content) Walk(content);
        return found;

        void Walk(ILogical node)
        {
            if (node is IFieldControl control)
            {
                found.Add(control);
                return;
            }
            if (node is Field) return;
            foreach (var child in node.LogicalChildren) Walk(child);
        }
    }

    /// <summary>控件是不是认领了标签的那个（第一个接入的）。</summary>
    internal bool Owns(IFieldControl control) => Controls().FirstOrDefault() == control;

    /// <summary>元素所在的字段；不在字段里时为 null。</summary>
    internal static Field? Of(AvaloniaObject element) => element.GetValue(OwnerProperty) is { } field && field != element ? field : null;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
        {
            if (change.OldValue is Control old)
            {
                LogicalChildren.Remove(old);
                VisualChildren.Remove(old);
            }
            if (change.NewValue is Control added)
            {
                LogicalChildren.Insert(1, added);
                VisualChildren.Insert(1, added);
            }
        }
        if (change.Property == ContentProperty || change.Property == LabelProperty || change.Property == DescriptionProperty || change.Property == ErrorProperty
            || change.Property == IsInvalidProperty || change.Property == IsRequiredProperty || change.Property == IsReadOnlyProperty
            || change.Property == LabelPlacementProperty || change.Property == IsLabelHiddenProperty)
            Update();
    }

    /// <summary>各部分的内容与显示，再让里面的控件跟上（出错、必填、只读、名称、帮助文字）。</summary>
    void Update()
    {
        label.Content = Label;
        label.IsRequired = IsRequired;
        label.IsVisible = HasLabel && !IsLabelHidden;
        description.Text = Description;
        description.IsVisible = HasText(Description);
        errorText.Text = Error;
        error.IsVisible = ShowsError;
        PseudoClasses.Set(":invalid", Invalid);
        PseudoClasses.Set(":start", Start);
        PseudoClasses.Set(":label-hidden", IsLabelHidden);
        var controls = Controls();
        label.Target = controls.FirstOrDefault() as Control;
        foreach (var control in controls) control.FieldChanged();
    }

    /// <summary>子元素加进逻辑树时（项、内容换了）：让控件跟上。</summary>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Update();
    }

    double Em => FontSize;

    double Spacing(string key, double fallback) => (this.TryFindResource(key, ActualThemeVariant, out var value) && value is double factor ? factor : fallback) * Em;

    double LabelColumn => double.IsNaN(LabelWidth) ? 8 * Em : LabelWidth;

    IEnumerable<Control> Below => new Control[] { description, error }.Where(part => part.IsVisible);

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        var gap = Spacing("Rune.Space.Xs", 0.25);
        var column = Start ? LabelColumn + Spacing("Rune.Space.Md", 1) : 0;
        var inner = availableSize.Deflate(new Thickness(column, 0, 0, 0));
        var height = 0d;
        var width = 0d;
        var rows = 0;
        void Add(Control part, LayoutSize available)
        {
            part.Measure(available);
            height += part.DesiredSize.Height + (rows++ > 0 ? gap : 0);
            width = Math.Max(width, part.DesiredSize.Width);
        }
        if (label.IsVisible)
        {
            if (Start) label.Measure(new LayoutSize(LabelColumn, availableSize.Height));
            else Add(label, availableSize);
        }
        if (Content is { } content) Add(content, inner);
        foreach (var part in Below) Add(part, inner);
        if (Start)
        {
            height = Math.Max(height, label.IsVisible ? LabelTop(0) + label.DesiredSize.Height : 0);
            width += column;
        }
        return new LayoutSize(width, height);
    }

    /// <summary>上方：标签、控件、说明、错误竖排；起始边：标签一列，控件、说明、错误一列，标签与控件第一行文字的基线对齐。</summary>
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var gap = Spacing("Rune.Space.Xs", 0.25);
        var x = Start ? LabelColumn + Spacing("Rune.Space.Md", 1) : 0;
        var width = Math.Max(0, finalSize.Width - x);
        var y = 0d;
        if (label.IsVisible && !Start)
        {
            label.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, label.DesiredSize.Width), label.DesiredSize.Height));
            y = label.DesiredSize.Height + gap;
        }
        var top = y;
        var first = true;
        foreach (var part in new[] { Content }.OfType<Control>().Concat(Below))
        {
            if (!first) y += gap;
            part.Arrange(new Rect(x, y, width, part.DesiredSize.Height));
            y += part.DesiredSize.Height;
            first = false;
        }
        if (label.IsVisible && Start)
        {
            var labelWidth = Math.Min(LabelColumn, label.DesiredSize.Width);
            var labelX = LabelAlign == FieldLabelAlign.Start ? 0 : LabelColumn - labelWidth;
            label.Arrange(new Rect(labelX, LabelTop(top), labelWidth, label.DesiredSize.Height));
        }
        return finalSize;
    }

    /// <summary>标签在起始边时的上边：标签的基线落在控件第一行文字的基线上（找不到文字时与控件顶边对齐）。</summary>
    double LabelTop(double controlTop)
    {
        var target = Content is { } content && FirstBaseline(content) is { } baseline ? controlTop + baseline : (double?)null;
        var own = FirstBaseline(label);
        return target is { } t && own is { } o ? Math.Max(0, t - o) : controlTop;
    }

    /// <summary>元素里第一行文字的基线（相对元素的顶边）：可视树里第一个显示着的 TextBlock 的第一行。</summary>
    internal static double? FirstBaseline(Control element)
    {
        foreach (var node in element.GetSelfAndVisualDescendants())
            if (node is TextBlock { IsEffectivelyVisible: true, TextLayout.TextLines: [var line, ..] } text && text.TranslatePoint(default, element) is { } at)
                return at.Y + text.Padding.Top + line.Baseline;
        return null;
    }

    /// <summary>聚焦字段的控件：能聚焦就聚焦它，否则聚焦它里面的 Tab 停靠项（单选组的选中项）或第一个能聚焦的。</summary>
    internal static void FocusControl(Control control)
    {
        var candidates = control.GetSelfAndVisualDescendants().OfType<InputElement>().Where(element => element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled).ToList();
        (candidates.FirstOrDefault(KeyboardNavigation.GetIsTabStop) ?? candidates.FirstOrDefault())?.Focus(NavigationMethod.Pointer);
    }

    /// <summary>读屏名称用的纯文字：字符串原样，文字控件取文字；别的为 null。</summary>
    internal static string? PlainText(object? content) => content switch
    {
        string text => text,
        TextBlock text => text.Text,
        Control control => AutomationProperties.GetName(control),
        _ => null,
    };

    static bool HasText(string? text) => !string.IsNullOrEmpty(text);
}

/// <summary>接入 <see cref="Field"/> 的控件：字段的状态变了时跟上。</summary>
interface IFieldControl
{
    /// <summary>字段的出错、必填、只读、标签、说明变了。</summary>
    void FieldChanged();
}

/// <summary>标签放在哪（vue-tsx-pro 的 FieldLabelPlacements）。</summary>
/// <param name="Value">取值（top、start）。</param>
[TypeConverter(typeof(VariantConverter<FieldLabelPlacement>))]
public readonly record struct FieldLabelPlacement(string Value) : IVariant<FieldLabelPlacement>
{
    /// <summary>控件上方（默认）：窄容器、标签很长时。</summary>
    public static readonly FieldLabelPlacement Top = new("top");

    /// <summary>控件的起始边：宽屏的长表单，标签一列对齐、省纵向空间。</summary>
    public static readonly FieldLabelPlacement Start = new("start");

    /// <inheritdoc />
    public static IReadOnlyList<FieldLabelPlacement> Known { get; } = [Top, Start];

    /// <inheritdoc />
    public static FieldLabelPlacement Parse(string value)
    {
        var placement = new FieldLabelPlacement(value);
        Variants.WarnIfUnknown(placement);
        return placement;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>标签在起始边时文字的对齐（vue-tsx-pro 的 FieldLabelAligns）。</summary>
/// <param name="Value">取值（start、end）。</param>
[TypeConverter(typeof(VariantConverter<FieldLabelAlign>))]
public readonly record struct FieldLabelAlign(string Value) : IVariant<FieldLabelAlign>
{
    /// <summary>靠起始边。</summary>
    public static readonly FieldLabelAlign Start = new("start");

    /// <summary>靠结束边、贴着控件（默认）：标签长短不一时，标签与控件之间的距离一致。</summary>
    public static readonly FieldLabelAlign End = new("end");

    /// <inheritdoc />
    public static IReadOnlyList<FieldLabelAlign> Known { get; } = [Start, End];

    /// <inheritdoc />
    public static FieldLabelAlign Parse(string value)
    {
        var align = new FieldLabelAlign(value);
        Variants.WarnIfUnknown(align);
        return align;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Field 的部件名（代码组合的子元素）：<c>rune|Field &gt; rune|Text#PART_Description</c>。外层（TS 的 root）就是 Field 自己，控件是 Content。</summary>
public static class FieldParts
{
    /// <summary>标签（TS 的 propsLabel）：FieldLabel。</summary>
    public const string Label = "PART_Label";

    /// <summary>说明（TS 的 propsDescription）：Text，sm、muted。</summary>
    public const string Description = "PART_Description";

    /// <summary>错误（TS 的 propsError）：Flex，里面是警示图标与 Text（sm、dangerText）。</summary>
    public const string Error = "PART_Error";
}
