using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 数字输入框（vue-tsx-pro 的 NumberField，读屏里是 SpinButton）：外壳同 <see cref="TextFieldControl"/>，右侧带减少 / 增加按钮。
/// <code>
/// var count = new NumberFieldControl { Value = 2, Min = 1, Max = 99 };
/// var budget = new NumberFieldControl { Value = 155353.2m, Format = new NumericFormat { Scale = 10_000, MaximumFractionDigits = 2 }, Suffix = "万元" };
/// </code>
/// 值是 decimal（十进制精确，金额、大整数不经过浮点数），空着时为 null，变了发 <see cref="ValueChanged"/>。输入时是普通文字；失焦或回车时解析、
/// 夹到 Min–Max、对齐到 Step 后写回，解析不了时恢复原值，清空后写回 null。没在编辑时按 <see cref="Format"/> 与当前语言显示，编辑时去掉千分位；
/// 写了 Scale（或是百分比）时显示与编辑都按缩放后的单位，写回时换回值本身的单位。认当前语言的小数点与千分位，也认全角数字与全角句点。
/// ↑ / ↓ 加减 Step，Page Up / Page Down 加减 BigStep（默认 Step × 10），Home / End 到 Min / Max；按钮不在 Tab 顺序里，
/// 按住连续加减（先停 400ms，之后每 60ms 一步）。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[Description("数字输入框：输入、方向键与按钮加减，提交时夹到范围并对齐步长。")]
public class NumberFieldControl : TextInputControl
{
    readonly IconControl icon = new() { Tone = TextTone.Muted, Visible = false };
    readonly TextControl prefix = new() { Tone = TextTone.Muted, Visible = false };
    readonly TextControl suffix = new() { Tone = TextTone.Muted, Visible = false };
    readonly IconButtonControl decrement = new() { Icon = Lucide.Minus, Radius = Radius.Full, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, TabStop = false };
    readonly IconButtonControl increment = new() { Icon = Lucide.Plus, Radius = Radius.Full, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, TabStop = false };
    // 按住步进按钮：先走一步，停 400ms 后每 60ms 走一步（同 React Aria）；松开时停
    readonly System.Windows.Forms.Timer repeat = new() { Interval = 400 };
    int direction;
    // 指针按下已经走过一步，随后的 Click 不再走（读屏、键盘触发的 Click 照常走一步）
    bool stepped;
    // 编辑中（有焦点）：文字是草稿；不在编辑时显示格式化后的值
    bool editing;
    bool showing;

    /// <summary>新建：空值，步长 1。</summary>
    public NumberFieldControl() : base(new TextBox())
    {
        Controls.AddRange([icon, prefix, Input, suffix, ClearButton, decrement, increment]);
        SetGrow(Input, true);
        repeat.Tick += (_, _) =>
        {
            repeat.Interval = 60;
            StepBy(Step, direction);
        };
        Input.GotFocus += (_, _) =>
        {
            if (editing) return;
            editing = true;
            ShowText();
        };
        Input.LostFocus += (_, _) =>
        {
            Commit();
            editing = false;
            ShowText();
        };
        Input.KeyDown += OnInputKeyDown;
        foreach (var button in new[] { decrement, increment })
        {
            button.MouseDown += OnStepPressed;
            button.MouseUp += (_, _) => repeat.Stop();
            button.MouseCaptureChanged += (_, _) => repeat.Stop();
            button.Click += OnStepClick;
        }
    }

    /// <summary>当前值；空着时为 null。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public decimal? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (!showing) ShowText();
            Sync();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>最小值。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public decimal? Min { get; set { field = value; Sync(); } }

    /// <summary>最大值。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public decimal? Max { get; set { field = value; Sync(); } }

    /// <summary>步长，默认 1；写回时对齐到 Min + n × Step。</summary>
    [Category("Behavior")]
    [DefaultValue(typeof(decimal), "1")]
    public decimal Step { get; set; } = 1;

    /// <summary>Page Up / Page Down 的步长；不写时是 Step × 10。</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    public decimal? BigStep { get; set; }

    /// <summary>显示格式（币种、百分比、缩放与单位）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NumericFormat? Format { get; set { field = value; ShowText(); } }

    /// <summary>前置图标。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public IconData? Icon
    {
        get => icon.Data;
        set
        {
            icon.Data = value;
            icon.Visible = value is not null;
        }
    }

    /// <summary>输入框里文字前的内容（币种符号）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Prefix
    {
        get => prefix.Text is { Length: > 0 } text ? text : null;
        set
        {
            prefix.Text = value ?? "";
            prefix.Visible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>输入框里文字后的内容：单位，如"万元""元/件"。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Suffix
    {
        get => suffix.Text is { Length: > 0 } text ? text : null;
        set
        {
            suffix.Text = value ?? "";
            suffix.Visible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>显示减少 / 增加按钮，默认 true；只读时不显示。</summary>
    [Category("Appearance")]
    [DefaultValue(true)]
    public bool Stepper { get; set { field = value; Sync(); } } = true;

    /// <summary>值变了（失焦、回车提交，加减，清空，或代码里改了 Value）。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>减少、增加按钮（测试用）。</summary>
    internal (IconButtonControl Decrement, IconButtonControl Increment) Buttons => (decrement, increment);

    bool Editable => Enabled && !ReadOnly;

    string Code => ProviderControl.LangOf(this).Code;

    /// <summary>没在编辑时显示的文字：按格式与当前语言。</summary>
    string Display => Value is { } value ? (Format ?? NumericFormat.Default).Format(value, Code, ProviderControl.LangOf(this).Currency) : "";

    /// <inheritdoc />
    protected override void ClearText()
    {
        Value = null;
        base.ClearText();
    }

    /// <summary>值 → 文字：编辑中是编辑单位下的纯数字（不带千分位），否则是格式化后的。</summary>
    void ShowText()
    {
        showing = true;
        try
        {
            Text = editing ? NumberRules.EditText(Value, Format, Code) : Display;
        }
        finally
        {
            showing = false;
        }
    }

    /// <summary>
    /// 解析并写回（解析不了时恢复原值）。文字没改过、或只读与不可用时不写：只是聚焦再离开，不该把值按步长取整、夹到范围里。
    /// </summary>
    void Commit()
    {
        if (!editing || !Editable) return;
        if (Text == NumberRules.EditText(Value, Format, Code)) return;
        if (!NumberRules.TryParse(Text, Code, out var parsed)) return;
        showing = true;
        try
        {
            Value = parsed is { } typed ? NumberRules.Snap(NumberRules.FromEdit(typed, Format), Step, Min, Max) : null;
        }
        finally
        {
            showing = false;
        }
    }

    /// <summary>从当前值（编辑中则先解析）加减 delta 的 times 倍；没有值时从 Min（或 0）开始。</summary>
    void StepBy(decimal delta, int times)
    {
        if (!Editable) return;
        var current = Value;
        if (editing && NumberRules.TryParse(Text, Code, out var typed)) current = typed is { } number ? NumberRules.FromEdit(number, Format) : null;
        SetValue(NumberRules.Snap(current is { } start ? start + delta * times : Min ?? 0, Step, Min, Max));
    }

    void SetValue(decimal value)
    {
        Value = value;
        ShowText();
    }

    /// <summary>方向键、翻页键、Home / End 是加减（spinbutton 的约定），不挪光标；回车提交。</summary>
    void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (!Editable) return;
        Action? action = e.KeyCode switch
        {
            Keys.Up => () => StepBy(Step, 1),
            Keys.Down => () => StepBy(Step, -1),
            Keys.PageUp => () => StepBy(BigStep ?? Step * 10, 1),
            Keys.PageDown => () => StepBy(BigStep ?? Step * 10, -1),
            Keys.Home when Min is { } min => () => SetValue(min),
            Keys.End when Max is { } max => () => SetValue(max),
            Keys.Enter => () =>
            {
                Commit();
                ShowText();
            },
            _ => null,
        };
        if (action is null) return;
        e.Handled = e.SuppressKeyPress = true;
        action();
    }

    void OnStepPressed(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        stepped = true;
        direction = sender == increment ? 1 : -1;
        StepBy(Step, direction);
        repeat.Interval = 400;
        repeat.Start();
    }

    void OnStepClick(object? sender, EventArgs e)
    {
        if (stepped)
        {
            stepped = false;
            return;
        }
        StepBy(Step, sender == increment ? 1 : -1);
    }

    /// <summary>加减按钮：只读时不显示，到头时不可用、名称取语言包；按钮同清空按钮的紧凑尺寸。</summary>
    protected override void Sync()
    {
        base.Sync();
        if (decrement is null || increment is null) return;
        var lang = ProviderControl.LangOf(this);
        ControlShell.FitInnerButton(decrement, Em);
        ControlShell.FitInnerButton(increment, Em);
        decrement.Visible = increment.Visible = Stepper && !ReadOnly;
        decrement.Enabled = Editable && !(Value is { } low && Min is { } min && low <= min);
        increment.Enabled = Editable && !(Value is { } high && Max is { } max && high >= max);
        decrement.AccessibleName = lang.NumberDecrement;
        increment.AccessibleName = lang.NumberIncrement;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) repeat.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new NumberAccessibleObject(this);

    /// <summary>读屏：SpinButton，值是格式化后的文字。</summary>
    sealed class NumberAccessibleObject(NumberFieldControl owner) : FieldAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.SpinButton;
    }
}
