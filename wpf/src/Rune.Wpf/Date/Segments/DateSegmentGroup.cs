using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 一组日期分段（vue-tsx-pro 的 DateSegmentGroup）：年 / 月 / 日、时 : 分，每段是能聚焦的数字调节框（读屏的 spinbutton），分隔符对读屏隐藏。
/// 状态由调用方的 <see cref="DateSegments"/> 给；DateField、TimeField 放在输入框外壳里用，日期区间在一个外壳里放两组。
/// <list type="bullet">
/// <item>每段在 Tab 顺序里（同 React Aria），←→ 也能换段；输入满了自动跳到下一段，焦点跟过去；</item>
/// <item>打的字（含输入法给的全角数字）交给 DateSegments.Type，↑↓、Page Up / Down、Home / End、Backspace 交给 HandleKey；</item>
/// <item>Enter 同输入框：写回打了一半的年份，再提交所在的 <see cref="Form"/>；</item>
/// <item>读屏名称是"这一组的名称 + 段名"，没填的段取值念"未填写"（语言包 DateFieldEmpty）。</item>
/// </list>
/// 外观：整组上下 xs、数字等宽；每段左右 0.1em、xs 圆角，聚焦时 accent 底、accentContrast 字；没填的段与分隔符是 textMuted。
/// 部件样式键见 <see cref="DateSegmentGroupParts"/>，默认外观在 DateSegmentGroup.xaml。
/// </summary>
public class DateSegmentGroup : FrameworkElement
{
    /// <summary>分段状态。</summary>
    public static readonly DependencyProperty SegmentsProperty = Register(nameof(Segments), typeof(DateSegments), null);

    /// <summary>只读：能聚焦、能换段，不能改。</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = Register(nameof(IsReadOnly), typeof(bool), false);

    /// <summary>必填（读屏）。</summary>
    public static readonly DependencyProperty IsRequiredProperty = Register(nameof(IsRequired), typeof(bool), false);

    /// <summary>校验未通过。</summary>
    public static readonly DependencyProperty IsInvalidProperty = Register(nameof(IsInvalid), typeof(bool), false);

    readonly StackPanel row = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly Dictionary<DateSegmentType, DateSegmentBox> boxes = [];

    static DependencyProperty Register(string name, Type type, object? fallback) => DependencyProperty.Register(
        name, type, typeof(DateSegmentGroup), new PropertyMetadata(fallback, (d, _) => ((DateSegmentGroup)d).Refresh()));

    /// <summary>新建：没有段。</summary>
    public DateSegmentGroup()
    {
        row.SetResourceReference(StyleProperty, DateSegmentGroupParts.Row);
        AddVisualChild(row);
        AddLogicalChild(row);
        AddHandler(KeyDownEvent, new KeyEventHandler(OnSegmentKeyDown));
        AddHandler(TextInputEvent, new TextCompositionEventHandler(OnSegmentTextInput));
        IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (e.NewValue is not false || Segments is not { } state) return;
            state.SetFocused(null);
            Refresh();
        };
    }

    /// <summary>分段状态。</summary>
    public DateSegments? Segments { get => (DateSegments?)GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool IsInvalid { get => (bool)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>可以输入的各段（测试用）。</summary>
    internal IReadOnlyList<DateSegmentBox> Boxes => [.. row.Children.OfType<DateSegmentBox>()];

    /// <summary>显示的文字（测试用）：各段与分隔符连起来。</summary>
    internal string Text => string.Concat(row.Children.Cast<UIElement>().Select(child => child is DateSegmentBox box ? box.Text : ((Text)child).Text));

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => row;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new[] { row }.GetEnumerator();

    /// <summary>值或状态变了：重画各段；输入满了跳段时焦点跟过去。外面改了值（调过 DateSegments.Sync）后调它。</summary>
    public void Refresh()
    {
        if (Segments is not { } state)
        {
            row.Children.Clear();
            boxes.Clear();
            return;
        }
        var segments = state.Segments;
        var shape = segments.Select(segment => segment.Type).ToList();
        if (!row.Children.Cast<UIElement>().Select(child => (child as DateSegmentBox)?.Type).SequenceEqual(shape))
        {
            row.Children.Clear();
            boxes.Clear();
            foreach (var segment in segments)
            {
                if (segment.Type is not { } type)
                {
                    var literal = new Text { Tone = TextTone.Muted, VerticalAlignment = VerticalAlignment.Center };
                    literal.SetResourceReference(StyleProperty, DateSegmentGroupParts.Literal);
                    row.Children.Add(literal);
                    continue;
                }
                var box = new DateSegmentBox(this, type);
                boxes[type] = box;
                row.Children.Add(box);
            }
        }
        for (var i = 0; i < segments.Count; i++)
        {
            if (row.Children[i] is DateSegmentBox box) box.Show(segments[i]);
            else ((Text)row.Children[i]).Text = segments[i].Text;
        }
        // 输入满了跳段、Backspace 删空退回上一段：焦点跟着走（只在焦点本来就在这组里时）
        if (IsKeyboardFocusWithin && state.FocusedSegment is { } focused && boxes.TryGetValue(focused, out var target) && !target.IsKeyboardFocused)
            target.Focus();
    }

    /// <summary>读屏用的一段名称：这一组的名称加段名（出生日期 年）。</summary>
    internal string NameOf(DateSegment segment) =>
        AutomationProperties.GetName(this) is { Length: > 0 } name ? $"{name} {segment.Label}" : segment.Label;

    /// <summary>没填的段给读屏的取值。</summary>
    internal string EmptyText => Provider.GetLang(this).DateFieldEmpty;

    internal void OnBoxFocused(DateSegmentType type)
    {
        Segments?.SetFocused(type);
        Refresh();
    }

    void OnSegmentTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Segments is not { } state || string.IsNullOrEmpty(e.Text) || !IsEnabled) return;
        e.Handled = true;
        if (IsReadOnly) return;
        state.Type(e.Text);
        Refresh();
    }

    void OnSegmentKeyDown(object sender, KeyEventArgs e)
    {
        if (Segments is not { } state || !IsEnabled || Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift)) return;
        if (e.Key == Key.Enter)
        {
            // 同输入框：写回打了一半的年份，再提交所在的表单（查询栏、对话框里的表单）
            e.Handled = true;
            state.Commit();
            Refresh();
            if (FormOf(this) is { } form) _ = form.SubmitAsync();
            return;
        }
        DateSegmentKey? key = e.Key switch
        {
            Key.Up => DateSegmentKey.Up,
            Key.Down => DateSegmentKey.Down,
            Key.PageUp => DateSegmentKey.PageUp,
            Key.PageDown => DateSegmentKey.PageDown,
            Key.Home => DateSegmentKey.Home,
            Key.End => DateSegmentKey.End,
            Key.Back or Key.Delete => DateSegmentKey.Backspace,
            Key.Right => DateSegmentKey.Right,
            Key.Left => DateSegmentKey.Left,
            _ => null,
        };
        // 只读时只能换段
        if (key is not { } pressed || IsReadOnly && pressed is not (DateSegmentKey.Left or DateSegmentKey.Right)) return;
        if (!state.HandleKey(pressed, FlowDirection == FlowDirection.RightToLeft)) return;
        e.Handled = true;
        Refresh();
    }

    static Form? FormOf(DependencyObject node)
    {
        for (var current = LogicalTreeHelper.GetParent(node) ?? VisualTreeHelper.GetParent(node); current is not null; current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current))
            if (current is Form form) return form;
        return null;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 上下 xs：StackPanel 没有内边距，用外边距
        row.Margin = new Thickness(0, 0.25 * TextElement.GetFontSize(this), 0, 0.25 * TextElement.GetFontSize(this));
        row.Measure(availableSize);
        return row.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        row.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    sealed class GroupPeer(DateSegmentGroup owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(DateSegmentGroup);
    }
}

/// <summary>
/// 日期分段里的一段（<see cref="DateSegmentGroup"/> 建的）：能聚焦的数字调节框，读屏是 spinbutton（取值、范围、"未填写"）。
/// <see cref="IsPlaceholder"/> 是还没填（主题样式的触发器用）。
/// </summary>
public sealed class DateSegmentBox : Border
{
    static readonly DependencyPropertyKey IsPlaceholderKey = DependencyProperty.RegisterReadOnly(
        nameof(IsPlaceholder), typeof(bool), typeof(DateSegmentBox), new PropertyMetadata(true));

    /// <summary>还没填。</summary>
    public static readonly DependencyProperty IsPlaceholderProperty = IsPlaceholderKey.DependencyProperty;

    /// <summary>圆角的 em 系数（主题的 radius.xs）。</summary>
    static readonly DependencyProperty RadiusFactorProperty = DependencyProperty.Register(
        "RadiusFactor", typeof(double), typeof(DateSegmentBox), new FrameworkPropertyMetadata(0.125, FrameworkPropertyMetadataOptions.AffectsMeasure));

    readonly DateSegmentGroup owner;
    readonly TextBlock text = new() { VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };

    internal DateSegmentBox(DateSegmentGroup owner, DateSegmentType type)
    {
        this.owner = owner;
        Type = type;
        Focusable = true;
        FocusVisualStyle = null;
        Cursor = Cursors.IBeam;
        Child = text;
        SetResourceReference(StyleProperty, DateSegmentGroupParts.Segment);
        SetResourceReference(RadiusFactorProperty, ThemeKeys.Radius.Xs);
    }

    /// <summary>左右 0.1em、xs 圆角，随字号（Border 不是 Control，EmSize 管不到它）。</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        var em = TextElement.GetFontSize(this);
        Padding = new Thickness(0.1 * em, 0, 0.1 * em, 0);
        CornerRadius = new CornerRadius((double)GetValue(RadiusFactorProperty) * em);
        return base.MeasureOverride(constraint);
    }

    /// <summary>哪一段。</summary>
    public DateSegmentType Type { get; }

    /// <summary>还没填。</summary>
    public bool IsPlaceholder => (bool)GetValue(IsPlaceholderProperty);

    /// <summary>显示的这一段（值、范围、占位）。</summary>
    internal DateSegment Segment { get; private set; } = new(null, "", true, null, 0, 0, "");

    /// <summary>显示的文字。</summary>
    internal string Text => text.Text;

    internal void Show(DateSegment segment)
    {
        Segment = segment;
        text.Text = segment.Text;
        SetValue(IsPlaceholderKey, segment.IsPlaceholder);
    }

    /// <inheritdoc />
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        owner.OnBoxFocused(Type);
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsEnabled) return;
        Focus();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SegmentPeer(this);

    sealed class SegmentPeer(DateSegmentBox box) : FrameworkElementAutomationPeer(box), IRangeValueProvider, IValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;

        protected override string GetClassNameCore() => nameof(DateSegmentBox);

        protected override string GetNameCore() => box.owner.NameOf(box.Segment);

        protected override bool IsKeyboardFocusableCore() => true;

        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface is PatternInterface.RangeValue or PatternInterface.Value ? this : base.GetPattern(patternInterface);

        public bool IsReadOnly => box.owner.IsReadOnly || !box.owner.IsEnabled;

        public double Minimum => box.Segment.Min;

        public double Maximum => box.Segment.Max;

        double IRangeValueProvider.Value => box.Segment.Value ?? double.NaN;

        public double SmallChange => 1;

        public double LargeChange => 1;

        public void SetValue(double value) => throw new InvalidOperationException("分段用键盘输入");

        string IValueProvider.Value => box.Segment.IsPlaceholder ? box.owner.EmptyText : box.Segment.Text;

        public void SetValue(string value) => throw new InvalidOperationException("分段用键盘输入");
    }
}

/// <summary>DateSegmentGroup 的部件样式键。外层就是 DateSegmentGroup 自己。</summary>
public static class DateSegmentGroupParts
{
    /// <summary>放各段的一行：StackPanel，上下 xs、数字等宽。</summary>
    public static readonly PartKey Row = new(typeof(DateSegmentGroup), nameof(Row), typeof(StackPanel));

    /// <summary>每一段（TS 的 propsSegment）：DateSegmentBox，聚焦时 accent 底。</summary>
    public static readonly PartKey Segment = new(typeof(DateSegmentGroup), nameof(Segment), typeof(DateSegmentBox));

    /// <summary>分隔符（TS 的 propsLiteral）：Text，textMuted。</summary>
    public static readonly PartKey Literal = new(typeof(DateSegmentGroup), nameof(Literal), typeof(Text));
}
