using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rune.Wpf;

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
/// 部件样式键见 <see cref="FieldParts"/>，默认外观在 Field.xaml。表单（Form）、字段布局的统一设置以后做。
/// </summary>
[ContentProperty(nameof(Content))]
public class Field : Control
{
    /// <summary>所在的字段（继承给里面的元素，控件从它读字段的状态；变了时通知接入的控件）。</summary>
    internal static readonly DependencyProperty OwnerProperty = DependencyProperty.RegisterAttached(
        "Owner", typeof(Field), typeof(Field), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => (d as IFieldControl)?.FieldChanged()));

    /// <summary>标签。</summary>
    public static readonly DependencyProperty LabelProperty = Register(nameof(Label), typeof(object), null);

    /// <summary>控件下方的说明。</summary>
    public static readonly DependencyProperty DescriptionProperty = Register(nameof(Description), typeof(string), null);

    /// <summary>错误信息：给了（且 IsInvalid 不是 false）就显示，并把控件标为出错。</summary>
    public static readonly DependencyProperty ErrorProperty = Register(nameof(Error), typeof(string), null);

    /// <summary>校验未通过；不写时有错误就算出错，写 false 时不显示错误。</summary>
    public static readonly DependencyProperty IsInvalidProperty = Register(nameof(IsInvalid), typeof(bool?), null);

    /// <summary>必填：标签后加星号，控件读屏念"必填"。</summary>
    public static readonly DependencyProperty IsRequiredProperty = Register(nameof(IsRequired), typeof(bool), false);

    /// <summary>只读：交给里面的控件。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = Register(nameof(IsReadOnly), typeof(bool), false);

    /// <summary>标签放在哪；默认上方。</summary>
    public static readonly DependencyProperty LabelPlacementProperty = Register(nameof(LabelPlacement), typeof(FieldLabelPlacement), default(FieldLabelPlacement));

    /// <summary>标签在起始边时标签列的宽度（设备无关像素）；不写（NaN）时 8em。</summary>
    public static readonly DependencyProperty LabelWidthProperty = Register(nameof(LabelWidth), typeof(double), double.NaN);

    /// <summary>标签在起始边时文字的对齐；默认 end（贴着控件）。</summary>
    public static readonly DependencyProperty LabelAlignProperty = Register(nameof(LabelAlign), typeof(FieldLabelAlign), default(FieldLabelAlign));

    /// <summary>标签只给读屏：视觉上隐藏，仍是控件的名称；不占标签列。</summary>
    public static readonly DependencyProperty IsLabelHiddenProperty = Register(nameof(IsLabelHidden), typeof(bool), false);

    /// <summary>控件。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(UIElement), typeof(Field), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnContentChanged));

    static readonly DependencyProperty GapFactorProperty = DependencyProperty.Register(
        "GapFactor", typeof(double), typeof(Field), new FrameworkPropertyMetadata(0.25, FrameworkPropertyMetadataOptions.AffectsMeasure));

    static readonly DependencyProperty ColumnGapFactorProperty = DependencyProperty.Register(
        "ColumnGapFactor", typeof(double), typeof(Field), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    readonly FieldLabel label = new() { Name = "PART_Label" };
    readonly Text description = new() { Name = "PART_Description" };
    readonly Text errorText = new();
    readonly Flex error = new() { Name = "PART_Error" };
    readonly Icon errorIcon = new() { Data = Lucide.CircleExclamationPoint };

    static DependencyProperty Register(string name, Type type, object? fallback) => DependencyProperty.Register(
        name, type, typeof(Field), new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((Field)d).Update()));

    static Field()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Field), new FrameworkPropertyMetadata(typeof(Field)));
        FocusableProperty.OverrideMetadata(typeof(Field), new FrameworkPropertyMetadata(false));
    }

    /// <summary>新建：标签在上方；部件引用主题的部件样式。</summary>
    public Field()
    {
        SetValue(OwnerProperty, this);
        label.SetResourceReference(StyleProperty, FieldParts.Label);
        description.SetResourceReference(StyleProperty, FieldParts.Description);
        error.SetResourceReference(StyleProperty, FieldParts.Error);
        errorText.SetResourceReference(StyleProperty, FieldParts.ErrorText);
        errorIcon.SetResourceReference(StyleProperty, FieldParts.ErrorIcon);
        SetResourceReference(GapFactorProperty, "Rune.Space.Xs");
        SetResourceReference(ColumnGapFactorProperty, "Rune.Space.Md");
        error.Children.Add(errorIcon);
        error.Children.Add(errorText);
        foreach (var child in new UIElement[] { label, description, error })
        {
            AddVisualChild(child);
            AddLogicalChild(child);
        }
        Loaded += (_, _) => Update();
        Update();
    }

    /// <summary>标签。</summary>
    public object? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>错误信息。</summary>
    public string? Error { get => (string?)GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => (bool?)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>标签放在哪。</summary>
    public FieldLabelPlacement LabelPlacement { get => (FieldLabelPlacement)GetValue(LabelPlacementProperty); set => SetValue(LabelPlacementProperty, value); }

    /// <summary>标签列的宽度。</summary>
    public double LabelWidth { get => (double)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>标签文字的对齐。</summary>
    public FieldLabelAlign LabelAlign { get => (FieldLabelAlign)GetValue(LabelAlignProperty); set => SetValue(LabelAlignProperty, value); }

    /// <summary>标签只给读屏。</summary>
    public bool IsLabelHidden { get => (bool)GetValue(IsLabelHiddenProperty); set => SetValue(IsLabelHiddenProperty, value); }

    /// <summary>控件。</summary>
    public UIElement? Content { get => (UIElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>出错：写了 IsInvalid 按它，否则有错误就算。</summary>
    internal bool Invalid => IsInvalid ?? HasText(Error);

    bool ShowsError => HasText(Error) && IsInvalid != false;

    bool HasLabel => Label is not null and not "";

    bool Start => LabelPlacement == FieldLabelPlacement.Start && !IsLabelHidden;

    /// <summary>标签的文字（读屏名称）。</summary>
    internal string? LabelText => PlainText(Label);

    /// <summary>读屏的帮助文字：说明与（显示着的）错误。</summary>
    internal string? HelpText => string.Join(" ", new[] { Description, ShowsError ? Error : null }.Where(HasText)) is { Length: > 0 } text ? text : null;

    /// <inheritdoc />
    protected override int VisualChildrenCount => Content is null ? 3 : 4;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => Parts()[index];

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => Parts().GetEnumerator();

    UIElement[] Parts() => Content is { } content ? [label, content, description, error] : [label, description, error];

    static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (Field)d;
        if (e.OldValue is UIElement old)
        {
            field.RemoveVisualChild(old);
            field.RemoveLogicalChild(old);
        }
        if (e.NewValue is UIElement added)
        {
            field.AddVisualChild(added);
            field.AddLogicalChild(added);
        }
        field.Update();
    }

    /// <summary>里面接入字段的控件（逻辑树深度优先、不进到控件里面）：第一个认领标签。</summary>
    List<IFieldControl> Controls()
    {
        var found = new List<IFieldControl>();
        if (Content is { } content) Walk(content);
        return found;

        void Walk(object node)
        {
            if (node is IFieldControl control)
            {
                found.Add(control);
                return;
            }
            if (node is Field or not DependencyObject) return;
            foreach (var child in LogicalTreeHelper.GetChildren((DependencyObject)node)) Walk(child);
        }
    }

    /// <summary>控件是不是认领了标签的那个（第一个接入的）。</summary>
    internal bool Owns(IFieldControl control) => Controls().FirstOrDefault() == control;

    /// <summary>
    /// 控件读屏的帮助文字（上游的 aria-describedby，各层拼在一起、重复的只留一份）：所在字段的说明与错误（认领标签的控件才有），
    /// 再接控件自己的（勾选项的说明、使用方写的 HelpText）。不在字段里时只有自己的；什么都没有时是空字符串（UIA 的约定）。
    /// </summary>
    /// <param name="control">接入字段的控件。</param>
    /// <param name="own">控件自己的帮助文字。</param>
    internal static string DescribedBy(IFieldControl control, params string?[] own)
    {
        var field = control is DependencyObject element && Of(element) is { } owner && owner.Owns(control) ? owner.HelpText : null;
        return string.Join(" ", own.Prepend(field).Where(HasText).Distinct());
    }

    /// <summary>元素所在的字段；不在字段里时为 null。</summary>
    internal static Field? Of(DependencyObject element) => element.GetValue(OwnerProperty) is Field field && field != element ? field : null;

    /// <summary>值来自使用方（本地值、样式、绑定），不是属性的默认值：控件上写了的状态优先于字段。</summary>
    internal static bool IsSet(DependencyObject element, DependencyProperty property) =>
        DependencyPropertyHelper.GetValueSource(element, property).BaseValueSource != BaseValueSource.Default;

    /// <summary>各部分的内容与显示，再让里面的控件跟上（出错、必填、只读、名称、帮助文字）。</summary>
    void Update()
    {
        label.Content = Label;
        label.IsRequired = IsRequired;
        label.Visibility = HasLabel && !IsLabelHidden ? Visibility.Visible : Visibility.Collapsed;
        description.Text = Description ?? "";
        description.Visibility = HasText(Description) ? Visibility.Visible : Visibility.Collapsed;
        errorText.Text = Error ?? "";
        error.Visibility = ShowsError ? Visibility.Visible : Visibility.Collapsed;
        var controls = Controls();
        label.Target = controls.FirstOrDefault() as UIElement;
        foreach (var control in controls) control.FieldChanged();
    }

    double Em => FontSize;

    double LabelColumn => double.IsNaN(LabelWidth) ? 8 * Em : LabelWidth;

    double Gap => (double)GetValue(GapFactorProperty) * Em;

    double ColumnGap => (double)GetValue(ColumnGapFactorProperty) * Em;

    IEnumerable<UIElement> Below => new UIElement[] { description, error }.Where(part => part.Visibility != Visibility.Collapsed);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        var column = Start ? LabelColumn + ColumnGap : 0;
        var inner = new Size(Math.Max(0, constraint.Width - column), constraint.Height);
        var (width, height, rows) = (0d, 0d, 0);
        void Add(UIElement part, Size available)
        {
            part.Measure(available);
            height += part.DesiredSize.Height + (rows++ > 0 ? Gap : 0);
            width = Math.Max(width, part.DesiredSize.Width);
        }
        if (label.Visibility != Visibility.Collapsed)
        {
            if (Start) label.Measure(new Size(LabelColumn, constraint.Height));
            else Add(label, constraint);
        }
        if (Content is { } content) Add(content, inner);
        // 警示图标上下各让 0.25em（错误文字的字号）：与第一行文字居中
        errorIcon.Margin = new Thickness(0, 0.25 * errorText.FontSize, 0, 0.25 * errorText.FontSize);
        foreach (var part in Below) Add(part, inner);
        if (Start)
        {
            height = Math.Max(height, label.Visibility != Visibility.Collapsed ? label.DesiredSize.Height : 0);
            width += column;
        }
        return new Size(width, height);
    }

    /// <summary>上方：标签、控件、说明、错误竖排；起始边：标签一列，控件、说明、错误一列，标签与控件第一行文字的基线对齐。</summary>
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var x = Start ? LabelColumn + ColumnGap : 0;
        var width = Math.Max(0, arrangeBounds.Width - x);
        var y = 0d;
        var shown = label.Visibility != Visibility.Collapsed;
        if (shown && !Start)
        {
            label.Arrange(new Rect(0, 0, Math.Min(arrangeBounds.Width, label.DesiredSize.Width), label.DesiredSize.Height));
            y = label.DesiredSize.Height + Gap;
        }
        var first = true;
        foreach (var part in new[] { Content }.OfType<UIElement>().Concat(Below))
        {
            if (!first) y += Gap;
            part.Arrange(new Rect(x, y, width, part.DesiredSize.Height));
            y += part.DesiredSize.Height;
            first = false;
        }
        if (shown && Start)
        {
            var labelWidth = Math.Min(LabelColumn, label.DesiredSize.Width);
            var labelX = LabelAlign == FieldLabelAlign.Start ? 0 : LabelColumn - labelWidth;
            label.Arrange(new Rect(labelX, LabelTop(), labelWidth, label.DesiredSize.Height));
        }
        return arrangeBounds;
    }

    /// <summary>标签在起始边时的上边：标签的基线落在控件第一行文字的基线上（找不到文字时与控件顶边对齐）。</summary>
    double LabelTop() =>
        Content is FrameworkElement content && FirstBaseline(content) is { } target && FirstBaseline(label) is { } own ? Math.Max(0, target - own) : 0;

    /// <summary>元素里第一行文字的基线（相对元素的顶边）：可视树里第一个显示着的 TextBlock 的第一行（行高按 TextBlock 的行高或字体）。</summary>
    internal static double? FirstBaseline(FrameworkElement element)
    {
        var stack = new Stack<DependencyObject>([element]);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is UIElement { IsVisible: false } && node != element) continue;
            if (node is TextBlock text)
            {
                var family = text.FontFamily;
                var line = double.IsNaN(text.LineHeight) ? family.LineSpacing * text.FontSize : text.LineHeight;
                // 行高大于字体的行距时，多出来的上下平分（同 CSS 的 half-leading）
                var baseline = (line - family.LineSpacing * text.FontSize) / 2 + family.Baseline * text.FontSize;
                return text.TranslatePoint(default, element).Y + text.Padding.Top + baseline;
            }
            for (var i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--) stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        return null;
    }

    /// <summary>聚焦字段的控件：能聚焦就聚焦它，否则聚焦它里面的 Tab 停靠项（单选组的选中项）或第一个能聚焦的。</summary>
    internal static void FocusControl(UIElement control)
    {
        var candidates = Descendants(control).OfType<UIElement>().Where(element => element.Focusable && element.IsVisible && element.IsEnabled).ToList();
        (candidates.FirstOrDefault(element => KeyboardNavigation.GetIsTabStop(element)) ?? candidates.FirstOrDefault())?.Focus();

        static IEnumerable<DependencyObject> Descendants(DependencyObject node)
        {
            yield return node;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                foreach (var child in Descendants(VisualTreeHelper.GetChild(node, i))) yield return child;
        }
    }

    /// <summary>读屏名称用的纯文字：字符串原样，文字控件取文字；别的为 null。</summary>
    internal static string? PlainText(object? content) => content switch
    {
        string text => text,
        TextBlock text => text.Text,
        UIElement element => AutomationProperties.GetName(element),
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

/// <summary>Field 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:FieldParts.Description}" TargetType="rune:Text"&gt;</c>。外层就是 Field 自己，控件是 Content。</summary>
public static class FieldParts
{
    /// <summary>标签（TS 的 propsLabel）：FieldLabel。</summary>
    public static readonly PartKey Label = new(typeof(Field), nameof(Label), typeof(FieldLabel));

    /// <summary>说明（TS 的 propsDescription）：Text，sm、muted。</summary>
    public static readonly PartKey Description = new(typeof(Field), nameof(Description), typeof(Text));

    /// <summary>错误（TS 的 propsError）：Flex，里面是警示图标与文字。</summary>
    public static readonly PartKey Error = new(typeof(Field), nameof(Error), typeof(Flex));

    /// <summary>错误的文字：Text，sm、dangerText。</summary>
    public static readonly PartKey ErrorText = new(typeof(Field), nameof(ErrorText), typeof(Text));

    /// <summary>错误的警示图标：Icon，上下各让 0.25em（与第一行文字居中）。</summary>
    public static readonly PartKey ErrorIcon = new(typeof(Field), nameof(ErrorIcon), typeof(Icon));
}
