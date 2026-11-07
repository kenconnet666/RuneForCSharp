using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 单行文本框（vue-tsx-pro 的 TextField）：输入框外壳里放无边框的平台 TextBox，前后可以带图标与固定文字，可以一键清空、显示字数；
/// <see cref="Type"/> 为 password 时字符显示成圆点，带显示密码的按钮。
/// <code>
/// var search = new TextFieldControl { Type = TextFieldType.Search, Icon = Lucide.Search, Clearable = true, AccessibleName = "搜索" };
/// var phone = new TextFieldControl { Format = TextFormat.Phone, Value = "13800001111" };   // 显示 138 0000 1111
/// </code>
/// 外壳、外观档位、占位文字、清空、字数、字段的状态在 <see cref="TextInputControl"/>（同 TextareaControl）。值是 <see cref="Value"/>（TS 的 _value）：
/// 没有 <see cref="Format"/> 时就是 Text；有 Format 时输入框里显示分好组的文字，Value 是去掉分隔的原文，过滤掉不接受的字符、截到最长，在中间插字光标也不跳。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[Description("单行文本框：外壳里放输入，前后可以带图标与固定文字。")]
public class TextFieldControl : TextInputControl
{
    readonly IconControl icon = new() { Tone = TextTone.Muted, Visible = false };
    readonly TextControl prefix = new() { Tone = TextTone.Muted, Visible = false };
    readonly TextControl suffix = new() { Tone = TextTone.Muted, Visible = false };
    readonly IconButtonControl reveal = new() { Radius = Radius.Full, Visible = false };
    readonly IconControl iconEnd = new() { Tone = TextTone.Muted, Visible = false };
    bool formatting;
    string value = "";

    /// <summary>新建：图标、前缀、输入框、后缀、清空、显示密码、字数、后置图标排成一行。</summary>
    public TextFieldControl() : base(new TextBox())
    {
        Controls.AddRange([icon, prefix, Input, suffix, ClearButton, reveal, Count, iconEnd]);
        SetGrow(Input, true);
        reveal.Click += (_, _) => RevealPassword = !RevealPassword;
        Input.TextChanged += (_, _) => ReadText();
    }

    /// <summary>输入的类型（TS 的 type）：password 时字符显示成圆点，带显示密码的按钮。</summary>
    [Category("Behavior")]
    public TextFieldType Type
    {
        get => field.Value is null ? TextFieldType.Text : field;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>显示着密码的原文。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool RevealPassword
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

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

    /// <summary>后置图标。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public IconData? IconEnd
    {
        get => iconEnd.Data;
        set
        {
            iconEnd.Data = value;
            iconEnd.Visible = value is not null;
        }
    }

    /// <summary>输入框前的固定文字（如 <c>https://</c>），不算在值里。</summary>
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

    /// <summary>输入框后的固定文字（如 <c>元</c>），不算在值里。</summary>
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

    /// <summary>输入时自动分组显示的格式（手机号、身份证号、银行卡号，<see cref="TextFormat"/>；代码里设）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextFormat? Format
    {
        get;
        set
        {
            field = value;
            ShowValue(Value);
        }
    }

    /// <summary>值：有 Format 时是去掉分隔的原文，没有时同 Text。</summary>
    [Category("Data")]
    [DefaultValue("")]
    public string Value { get => value; set => ShowValue(value); }

    /// <summary>值变了（输入、清空、代码里改）。</summary>
    public event EventHandler? ValueChanged;

    bool ShouldSerializeType() => Type != TextFieldType.Text;

    /// <summary>显示密码的按钮（测试用）。</summary>
    internal IconButtonControl RevealButton => reveal;

    /// <summary>值 → 输入框里的文字（有格式时分好组）。</summary>
    void ShowValue(string? value)
    {
        var raw = Format?.Parse(value ?? "") ?? value ?? "";
        SetFormatted(() => Text = Format?.Display(raw) ?? raw);
        SetValue(raw);
    }

    /// <summary>输入框里的文字 → 值；有格式时重新分组，光标放回刚才那个字符的后面（TS 的 displayPosition）。</summary>
    void ReadText()
    {
        if (formatting) return;
        var text = Text;
        if (Format is not { } format)
        {
            SetValue(text);
            return;
        }
        var before = format.Parse(text[..Math.Clamp(Input.SelectionStart, 0, text.Length)]).Length;
        var raw = format.Parse(text);
        var shown = format.Display(raw);
        if (shown != text)
        {
            SetFormatted(() => Text = shown);
            Input.SelectionStart = format.DisplayPosition(raw, before);
        }
        SetValue(raw);
    }

    void SetValue(string raw)
    {
        if (raw == value) return;
        value = raw;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    void SetFormatted(Action change)
    {
        formatting = true;
        try
        {
            change();
        }
        finally
        {
            formatting = false;
        }
    }

    /// <summary>密码、显示密码的按钮。</summary>
    protected override void Sync()
    {
        base.Sync();
        var password = Type == TextFieldType.Password;
        Input.UseSystemPasswordChar = password && !RevealPassword;
        reveal.Visible = password;
        reveal.Icon = RevealPassword ? Lucide.EyeOff : Lucide.Eye;
        reveal.AccessibleName = ProviderControl.LangOf(this).PasswordShow;
    }
}

/// <summary>
/// 字数计数（vue-tsx-pro 的 CharacterCount）：显示"12 / 100"，超出上限时用危险色；sm、textMuted。TextFieldControl 的 ShowCount 用它，
/// 也可以单独放在 FieldControl 里给自己的输入组件用。读屏念完整的句子（语言包的 characterCount）。
/// </summary>
[ToolboxItem(true)]
[Description("字数计数：超出上限时用危险色。")]
public class CharacterCountControl : TextControl
{
    /// <summary>新建：sm、textMuted，显示 0。</summary>
    public CharacterCountControl()
    {
        TextSize = TextSize.Sm;
        SyncText();
    }

    /// <summary>已输入的字符数。</summary>
    [Category("Data")]
    [DefaultValue(0)]
    public int Count
    {
        get;
        set
        {
            field = value;
            SyncText();
        }
    }

    /// <summary>上限；不给时只显示字数。</summary>
    [Category("Data")]
    [DefaultValue(null)]
    public int? Max
    {
        get;
        set
        {
            field = value;
            SyncText();
        }
    }

    /// <summary>超出上限。</summary>
    [Browsable(false)]
    public bool IsOver => Max is { } max && Count > max;

    void SyncText()
    {
        Text = Max is { } max ? $"{Count} / {max}" : Count.ToString();
        Tone = IsOver ? TextTone.Danger : TextTone.Muted;
        AccessibleName = ProviderControl.LangOf(this).CharacterCount(Count, Max);
    }
}
