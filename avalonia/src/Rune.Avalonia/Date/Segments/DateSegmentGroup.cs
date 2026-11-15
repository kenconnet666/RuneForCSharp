using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Rune.Avalonia;

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
/// 部件：段是 <see cref="DateSegmentBox"/>，分隔符是 Text#PART_Literal；默认外观在 DateSegmentGroup.axaml。
/// </summary>
[PseudoClasses(":invalid", ":readonly")]
public class DateSegmentGroup : Control
{
    /// <summary>分段状态。</summary>
    public static readonly StyledProperty<DateSegments?> SegmentsProperty = AvaloniaProperty.Register<DateSegmentGroup, DateSegments?>(nameof(Segments));

    /// <summary>只读：能聚焦、能换段，不能改。</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<DateSegmentGroup, bool>(nameof(IsReadOnly));

    /// <summary>必填（读屏）。</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AvaloniaProperty.Register<DateSegmentGroup, bool>(nameof(IsRequired));

    /// <summary>校验未通过。</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AvaloniaProperty.Register<DateSegmentGroup, bool>(nameof(IsInvalid));

    readonly StackPanel row = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly Dictionary<DateSegmentType, DateSegmentBox> boxes = [];

    /// <summary>新建：没有段。</summary>
    public DateSegmentGroup()
    {
        LogicalChildren.Add(row);
        VisualChildren.Add(row);
        AddHandler(KeyDownEvent, OnSegmentKeyDown);
        AddHandler(TextInputEvent, OnSegmentTextInput);
    }

    /// <summary>分段状态。</summary>
    public DateSegments? Segments { get => GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }

    /// <summary>只读。</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>可以输入的各段（测试用）。</summary>
    internal IReadOnlyList<DateSegmentBox> Boxes => [.. row.Children.OfType<DateSegmentBox>()];

    /// <summary>显示的文字（测试用）：各段与分隔符连起来。</summary>
    internal string Text => string.Concat(row.Children.Select(child => child is DateSegmentBox box ? box.Text : ((Text)child).Text));

    /// <summary>值或状态变了：重画各段；输入满了跳段时焦点跟过去。外面改了值（调过 DateSegments.Sync）后调它。</summary>
    public void Refresh()
    {
        PseudoClasses.Set(":invalid", IsInvalid);
        PseudoClasses.Set(":readonly", IsReadOnly);
        if (Segments is not { } state)
        {
            row.Children.Clear();
            boxes.Clear();
            return;
        }
        var segments = state.Segments;
        var shape = segments.Select(segment => segment.Type).ToList();
        if (!row.Children.Select(child => (child as DateSegmentBox)?.Type).SequenceEqual(shape))
        {
            row.Children.Clear();
            boxes.Clear();
            foreach (var segment in segments)
            {
                if (segment.Type is not { } type)
                {
                    row.Children.Add(new Text { Name = DateSegmentGroupParts.Literal, Tone = TextTone.Muted, VerticalAlignment = VerticalAlignment.Center });
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
        if (IsKeyboardFocusWithin && state.FocusedSegment is { } focused && boxes.TryGetValue(focused, out var target) && !target.IsFocused)
            target.Focus(NavigationMethod.Tab);
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

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SegmentsProperty || change.Property == IsReadOnlyProperty || change.Property == IsInvalidProperty || change.Property == IsRequiredProperty)
            Refresh();
        else if (change.Property == IsKeyboardFocusWithinProperty && change.GetNewValue<bool>() is false && Segments is { } state)
        {
            state.SetFocused(null);
            Refresh();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Refresh();
    }

    void OnSegmentTextInput(object? sender, TextInputEventArgs e)
    {
        if (Segments is not { } state || string.IsNullOrEmpty(e.Text) || !IsEffectivelyEnabled) return;
        e.Handled = true;
        if (IsReadOnly) return;
        state.Type(e.Text);
        Refresh();
    }

    void OnSegmentKeyDown(object? sender, KeyEventArgs e)
    {
        if (Segments is not { } state || !IsEffectivelyEnabled || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift)) return;
        if (e.Key == Key.Enter)
        {
            // 同输入框：写回打了一半的年份，再提交所在的表单（查询栏、对话框里的表单）
            e.Handled = true;
            state.Commit();
            Refresh();
            if (this.FindLogicalAncestorOfType<Form>() is { } form) _ = form.SubmitAsync();
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

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
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

    sealed class GroupPeer(DateSegmentGroup owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>
/// 日期分段里的一段（<see cref="DateSegmentGroup"/> 建的）：能聚焦的数字调节框，读屏是 spinbutton（取值、范围、"未填写"）。
/// 伪类 <c>:placeholder</c> 是还没填。
/// </summary>
[PseudoClasses(":placeholder")]
public sealed class DateSegmentBox : Border
{
    readonly DateSegmentGroup owner;
    readonly TextBlock text = new() { VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };

    internal DateSegmentBox(DateSegmentGroup owner, DateSegmentType type)
    {
        this.owner = owner;
        Type = type;
        Focusable = true;
        Child = text;
        Cursor = new Cursor(StandardCursorType.Ibeam);
    }

    /// <summary>哪一段。</summary>
    public DateSegmentType Type { get; }

    /// <summary>显示的这一段（值、范围、占位）。</summary>
    internal DateSegment Segment { get; private set; } = new(null, "", true, null, 0, 0, "");

    /// <summary>显示的文字。</summary>
    internal string Text => text.Text ?? "";

    internal void Show(DateSegment segment)
    {
        Segment = segment;
        text.Text = segment.Text;
        PseudoClasses.Set(":placeholder", segment.IsPlaceholder);
        IsTabStop = owner.IsEffectivelyEnabled;
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        owner.OnBoxFocused(Type);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEffectivelyEnabled) return;
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SegmentPeer(this);

    sealed class SegmentPeer(DateSegmentBox box) : ControlAutomationPeer(box), IRangeValueProvider, IValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;

        protected override string GetNameCore() => box.owner.NameOf(box.Segment);

        public bool IsReadOnly => box.owner.IsReadOnly || !box.owner.IsEffectivelyEnabled;

        public double Minimum => box.Segment.Min;

        public double Maximum => box.Segment.Max;

        double IRangeValueProvider.Value => box.Segment.Value ?? double.NaN;

        public double SmallChange => 1;

        public double LargeChange => 1;

        public void SetValue(double value) => throw new InvalidOperationException("分段用键盘输入");

        string? IValueProvider.Value => box.Segment.IsPlaceholder ? box.owner.EmptyText : box.Segment.Text;

        public void SetValue(string? value) => throw new InvalidOperationException("分段用键盘输入");
    }
}

/// <summary>DateSegmentGroup 的部件名。外层（TS 的 root）就是 DateSegmentGroup 自己，段是 <see cref="DateSegmentBox"/>。</summary>
public static class DateSegmentGroupParts
{
    /// <summary>分隔符（TS 的 propsLiteral）：Text，textMuted。</summary>
    public const string Literal = "PART_Literal";
}
