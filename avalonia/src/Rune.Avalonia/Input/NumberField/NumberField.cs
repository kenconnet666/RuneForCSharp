using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 数字输入框（vue-tsx-pro 的 NumberField，读屏里是 spinbutton）：外壳同 <see cref="TextField"/>，右侧带减少 / 增加按钮。
/// <code>
/// &lt;rune:Field Label="数量"&gt;&lt;rune:NumberField Value="{Binding Count}" Min="1" Max="99" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:NumberField Value="{Binding Yuan}" Suffix="万元"&gt;&lt;rune:NumberField.Format&gt;&lt;rune:NumericFormat Scale="10000" MaximumFractionDigits="2" /&gt;&lt;/rune:NumberField.Format&gt;&lt;/rune:NumberField&gt;
/// </code>
/// 值是 decimal（十进制精确，金额、大整数不经过浮点数），空着时为 null，默认双向绑定。输入时是普通文字；失焦或回车时解析、夹到 Min–Max、
/// 对齐到 Step 后写回，解析不了时恢复原值，清空后写回 null。没在编辑时按 <see cref="Format"/> 与当前语言显示，编辑时去掉千分位；
/// 写了 Scale（或是百分比）时显示与编辑都按缩放后的单位，写回时换回值本身的单位。认当前语言的小数点与千分位，也认全角数字与全角句点。
/// ↑ / ↓ 加减 Step，Page Up / Page Down 加减 BigStep（默认 Step × 10），Home / End 到 Min / Max；按钮不在 Tab 顺序里，
/// 按住连续加减（先停 400ms，之后每 60ms 一步）。部件：<see cref="NumberFieldParts"/>。
/// </summary>
public class NumberField : TextInputBase
{
    /// <summary>当前值；空着时为 null。默认双向绑定。</summary>
    public static readonly StyledProperty<decimal?> ValueProperty =
        AvaloniaProperty.Register<NumberField, decimal?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>最小值。</summary>
    public static readonly StyledProperty<decimal?> MinProperty = AvaloniaProperty.Register<NumberField, decimal?>(nameof(Min));

    /// <summary>最大值。</summary>
    public static readonly StyledProperty<decimal?> MaxProperty = AvaloniaProperty.Register<NumberField, decimal?>(nameof(Max));

    /// <summary>步长，默认 1；写回时对齐到 Min + n × Step。</summary>
    public static readonly StyledProperty<decimal> StepProperty = AvaloniaProperty.Register<NumberField, decimal>(nameof(Step), 1);

    /// <summary>Page Up / Page Down 的步长；不写时是 Step × 10。</summary>
    public static readonly StyledProperty<decimal?> BigStepProperty = AvaloniaProperty.Register<NumberField, decimal?>(nameof(BigStep));

    /// <summary>显示格式（币种、百分比、缩放与单位）。</summary>
    public static readonly StyledProperty<NumericFormat?> FormatProperty = AvaloniaProperty.Register<NumberField, NumericFormat?>(nameof(Format));

    /// <summary>前置图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<NumberField, IconData?>(nameof(Icon));

    /// <summary>输入框里文字前的内容（币种符号）。</summary>
    public static readonly StyledProperty<string?> PrefixProperty = AvaloniaProperty.Register<NumberField, string?>(nameof(Prefix));

    /// <summary>输入框里文字后的内容：单位，如"万元""元/件"。</summary>
    public static readonly StyledProperty<string?> SuffixProperty = AvaloniaProperty.Register<NumberField, string?>(nameof(Suffix));

    /// <summary>显示减少 / 增加按钮，默认 true；只读时不显示。</summary>
    public static readonly StyledProperty<bool> StepperProperty = AvaloniaProperty.Register<NumberField, bool>(nameof(Stepper), true);

    // 按住步进按钮：先走一步，停 400ms 后每 60ms 走一步（同 React Aria）；松开（在哪里松开都算）时停
    readonly DispatcherTimer repeat = new() { Interval = TimeSpan.FromMilliseconds(400) };
    IconButton? decrement;
    IconButton? increment;
    int direction;
    // 指针按下已经走过一步，随后的 Click 不再走（读屏、键盘触发的 Click 照常走一步）
    bool stepped;
    // 编辑中（有焦点）：文字是草稿；不在编辑时显示格式化后的值
    bool editing;
    bool showing;

    /// <summary>新建：空值，步长 1。</summary>
    public NumberField()
    {
        repeat.Tick += (_, _) =>
        {
            repeat.Interval = TimeSpan.FromMilliseconds(60);
            StepBy(Step, direction);
        };
    }

    /// <summary>当前值。</summary>
    public decimal? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>最小值。</summary>
    public decimal? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>最大值。</summary>
    public decimal? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>步长。</summary>
    public decimal Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>Page Up / Page Down 的步长。</summary>
    public decimal? BigStep { get => GetValue(BigStepProperty); set => SetValue(BigStepProperty, value); }

    /// <summary>显示格式。</summary>
    public NumericFormat? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>前缀文字。</summary>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>后缀文字（单位）。</summary>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>显示加减按钮。</summary>
    public bool Stepper { get => GetValue(StepperProperty); set => SetValue(StepperProperty, value); }

    /// <summary>减少、增加按钮（测试用）。</summary>
    internal (IconButton? Decrement, IconButton? Increment) Buttons => (decrement, increment);

    string Code => Provider.GetLang(this).Code;

    /// <summary>没在编辑时显示的文字：按格式与当前语言。</summary>
    string Display => Value is { } value ? (Format ?? NumericFormat.Default).Format(value, Code, Provider.GetLang(this).Currency) : "";

    /// <inheritdoc />
    protected override void OnTemplateParts(INameScope scope)
    {
        foreach (var button in new[] { decrement, increment })
        {
            if (button is null) continue;
            button.Click -= OnStepClick;
            button.RemoveHandler(PointerPressedEvent, OnStepPressed);
        }
        decrement = scope.Find<IconButton>(NumberFieldParts.Decrement);
        increment = scope.Find<IconButton>(NumberFieldParts.Increment);
        foreach (var button in new[] { decrement, increment })
        {
            if (button is null) continue;
            button.Click += OnStepClick;
            // 按下时就走一步（Click 要等松开）：隧道路由，赶在按钮自己处理之前
            button.AddHandler(PointerPressedEvent, OnStepPressed, RoutingStrategies.Tunnel);
        }
    }

    /// <inheritdoc />
    protected override bool SyncsOn(AvaloniaProperty property) =>
        property == ValueProperty || property == MinProperty || property == MaxProperty || property == StepperProperty;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (showing) return;
        if (change.Property == ValueProperty || change.Property == FormatProperty) Show();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (editing) return;
        editing = true;
        Show();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Commit();
        editing = false;
        Show();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!Editable)
        {
            base.OnKeyDown(e);
            return;
        }
        Action? action = e.Key switch
        {
            Key.Up => () => StepBy(Step, 1),
            Key.Down => () => StepBy(Step, -1),
            Key.PageUp => () => StepBy(BigStep ?? Step * 10, 1),
            Key.PageDown => () => StepBy(BigStep ?? Step * 10, -1),
            Key.Home when Min is { } min => () => SetValue(min),
            Key.End when Max is { } max => () => SetValue(max),
            Key.Enter => () =>
            {
                Commit();
                Show();
            },
            _ => null,
        };
        if (action is null)
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        action();
    }

    /// <inheritdoc />
    protected override void ClearText()
    {
        SetCurrentValue(ValueProperty, null);
        base.ClearText();
    }

    /// <summary>值 → 文字：编辑中是编辑单位下的纯数字（不带千分位），否则是格式化后的。</summary>
    void Show()
    {
        showing = true;
        try
        {
            SetCurrentValue(TextProperty, editing ? NumberRules.EditText(Value, Format, Code) : Display);
        }
        finally
        {
            showing = false;
        }
        Sync();
    }

    /// <summary>
    /// 解析并写回（解析不了时恢复原值）。文字没改过、或只读与不可用时不写：只是聚焦再离开，不该把值按步长取整、夹到范围里。
    /// </summary>
    void Commit()
    {
        if (!editing || !Editable) return;
        var text = Text ?? "";
        if (text == NumberRules.EditText(Value, Format, Code)) return;
        if (!NumberRules.TryParse(text, Code, out var parsed)) return;
        SetCurrentValue(ValueProperty, parsed is { } typed ? NumberRules.Snap(NumberRules.FromEdit(typed, Format), Step, Min, Max) : null);
    }

    /// <summary>从当前值（编辑中则先解析）加减 delta 的 times 倍；没有值时从 Min（或 0）开始。</summary>
    void StepBy(decimal delta, int times)
    {
        if (!Editable) return;
        var current = Value;
        if (editing && NumberRules.TryParse(Text ?? "", Code, out var typed)) current = typed is { } number ? NumberRules.FromEdit(number, Format) : null;
        SetValue(NumberRules.Snap(current is { } start ? start + delta * times : Min ?? 0, Step, Min, Max));
    }

    void SetValue(decimal value)
    {
        SetCurrentValue(ValueProperty, value);
        Show();
    }

    void OnStepPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        // 按住按钮不让输入框失焦：焦点留在输入框里，接着可以用方向键
        e.Handled = true;
        stepped = true;
        direction = sender == increment ? 1 : -1;
        StepBy(Step, direction);
        repeat.Interval = TimeSpan.FromMilliseconds(400);
        repeat.Start();
        if (sender is IconButton button)
        {
            e.Pointer.Capture(button);
            button.PointerReleased += StopRepeat;
            button.PointerCaptureLost += StopRepeat;
        }
    }

    void StopRepeat(object? sender, EventArgs e)
    {
        repeat.Stop();
        if (sender is not IconButton button) return;
        button.PointerReleased -= StopRepeat;
        button.PointerCaptureLost -= StopRepeat;
    }

    void OnStepClick(object? sender, RoutedEventArgs e)
    {
        if (stepped)
        {
            stepped = false;
            return;
        }
        StepBy(Step, sender == increment ? 1 : -1);
    }

    /// <summary>加减按钮：只读时不显示，到头时不可用；名称取语言包。</summary>
    protected override void Sync()
    {
        base.Sync();
        var lang = Provider.GetLang(this);
        var shown = Stepper && !ReadOnly;
        if (decrement is not null)
        {
            decrement.IsVisible = shown;
            decrement.IsEnabled = Editable && !(Value is { } value && Min is { } min && value <= min);
            AutomationProperties.SetName(decrement, lang.NumberDecrement);
        }
        if (increment is not null)
        {
            increment.IsVisible = shown;
            increment.IsEnabled = Editable && !(Value is { } value && Max is { } max && value >= max);
            AutomationProperties.SetName(increment, lang.NumberIncrement);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NumberFieldPeer(this);

    /// <summary>读屏：spinbutton（数值、最小、最大、步长），值的文字是格式化后的。</summary>
    sealed class NumberFieldPeer(NumberField owner) : TextInputPeer(owner), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;

        public new bool IsReadOnly => !owner.Editable;

        public double Minimum => (double)(owner.Min ?? decimal.MinValue);

        public double Maximum => (double)(owner.Max ?? decimal.MaxValue);

        public new double Value => (double)(owner.Value ?? 0);

        public double SmallChange => (double)owner.Step;

        public double LargeChange => (double)(owner.BigStep ?? owner.Step * 10);

        public void SetValue(double value) => owner.SetValue(NumberRules.Snap((decimal)value, owner.Step, owner.Min, owner.Max));
    }
}

/// <summary>NumberField 模板里的部件名。</summary>
public static class NumberFieldParts
{
    /// <summary>外壳（同 <see cref="TextInputParts.Root"/>）。</summary>
    public const string Root = TextInputParts.Root;

    /// <summary>清空按钮（同 <see cref="TextInputParts.Clear"/>）。</summary>
    public const string Clear = TextInputParts.Clear;

    /// <summary>前置图标（rune:Icon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>前缀（TextBlock）。</summary>
    public const string Prefix = "PART_Prefix";

    /// <summary>后缀（TextBlock）。</summary>
    public const string Suffix = "PART_Suffix";

    /// <summary>减少按钮（rune:IconButton）。</summary>
    public const string Decrement = "PART_Decrement";

    /// <summary>增加按钮（rune:IconButton）。</summary>
    public const string Increment = "PART_Increment";
}
