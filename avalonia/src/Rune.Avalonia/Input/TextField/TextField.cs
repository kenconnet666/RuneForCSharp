using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 单行文本框（vue-tsx-pro 的 TextField）：输入框外壳里放平台的文字输入，前后可以带图标与固定文字，可以一键清空、显示字数；
/// <see cref="Type"/> 为 password 时带显示 / 隐藏密码的按钮。
/// <code>
/// &lt;rune:TextField Text="{Binding Keyword}" Type="search" Icon="{x:Static rune:Lucide.Search}" Clearable="True" AutomationProperties.Name="搜索" /&gt;
/// &lt;rune:Field Label="密码" Error="{Binding PasswordError}"&gt;&lt;rune:TextField Text="{Binding Password}" Type="password" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:TextField Value="{Binding Phone}" Format="{x:Static rune:TextFormat.Phone}" /&gt;   &lt;!-- 显示 138 0000 1111，Value 是 13800001111 --&gt;
/// </code>
/// 外壳、外观档位、清空、字数、Field 的状态在 <see cref="TextInputBase"/>（同 Textarea）。值是 <see cref="Value"/>（TS 的 _value，默认双向绑定）：
/// 没有 <see cref="Format"/> 时就是 Text；有 Format 时输入框里显示分好组的文字，Value 是去掉分隔的原文，过滤掉不接受的字符、截到最长，在中间插字光标也不跳。
/// 部件名在 <see cref="TextFieldParts"/>。
/// </summary>
public class TextField : TextInputBase
{
    /// <summary>输入的类型（TS 的 type）：password 时字符显示成圆点，带显示密码的按钮；其余在桌面上同 text。</summary>
    public static readonly StyledProperty<TextFieldType> TypeProperty = AvaloniaProperty.Register<TextField, TextFieldType>(nameof(Type));

    /// <summary>前置图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<TextField, IconData?>(nameof(Icon));

    /// <summary>后置图标。</summary>
    public static readonly StyledProperty<IconData?> IconEndProperty = AvaloniaProperty.Register<TextField, IconData?>(nameof(IconEnd));

    /// <summary>输入框前的固定文字（如 <c>https://</c>、<c>¥</c>），不算在值里。</summary>
    public static readonly StyledProperty<string?> PrefixProperty = AvaloniaProperty.Register<TextField, string?>(nameof(Prefix));

    /// <summary>输入框后的固定文字（如 <c>.com</c>、<c>元</c>），不算在值里。</summary>
    public static readonly StyledProperty<string?> SuffixProperty = AvaloniaProperty.Register<TextField, string?>(nameof(Suffix));

    /// <summary>输入时自动分组显示的格式（手机号、身份证号、银行卡号，<see cref="TextFormat"/>）。</summary>
    public static readonly StyledProperty<TextFormat?> FormatProperty = AvaloniaProperty.Register<TextField, TextFormat?>(nameof(Format));

    /// <summary>值：有 Format 时是去掉分隔的原文，没有时同 Text。默认双向绑定。</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<TextField, string>(nameof(Value), "", defaultBindingMode: BindingMode.TwoWay);

    IconButton? reveal;
    bool formatting;

    /// <summary>输入的类型。</summary>
    public TextFieldType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>后置图标。</summary>
    public IconData? IconEnd { get => GetValue(IconEndProperty); set => SetValue(IconEndProperty, value); }

    /// <summary>前缀文字。</summary>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>后缀文字。</summary>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>显示格式。</summary>
    public TextFormat? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>值。</summary>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    protected override void OnTemplateParts(INameScope scope)
    {
        if (reveal is not null) reveal.Click -= OnReveal;
        reveal = scope.Find<IconButton>(TextFieldParts.Reveal);
        if (reveal is not null) reveal.Click += OnReveal;
    }

    /// <inheritdoc />
    protected override bool SyncsOn(AvaloniaProperty property) => property == TypeProperty || property == RevealPasswordProperty;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (formatting) return;
        if (change.Property == ValueProperty || change.Property == FormatProperty) ShowValue();
        else if (change.Property == TextProperty) ReadText();
    }

    /// <summary>值 → 输入框里的文字（有格式时分好组）。</summary>
    void ShowValue()
    {
        var raw = Format?.Parse(Value ?? "") ?? Value ?? "";
        SetFormatted(() =>
        {
            SetCurrentValue(TextProperty, Format?.Display(raw) ?? raw);
            if (raw != Value) SetCurrentValue(ValueProperty, raw);
        });
    }

    /// <summary>输入框里的文字 → 值；有格式时重新分组（投递到 TextBox 处理完这次输入之后：它改完文字还会再挪光标）。</summary>
    void ReadText()
    {
        var text = Text ?? "";
        SetFormatted(() => SetCurrentValue(ValueProperty, Format?.Parse(text) ?? text));
        if (Format is { } format && format.Display(format.Parse(text)) != text) Dispatcher.UIThread.Post(Regroup);
    }

    /// <summary>重新分组，光标放回刚才那个字符的后面（TS 的 displayPosition）。</summary>
    void Regroup()
    {
        if (Format is not { } format) return;
        var text = Text ?? "";
        var before = format.Parse(text[..Math.Clamp(CaretIndex, 0, text.Length)]).Length;
        var raw = format.Parse(text);
        SetFormatted(() =>
        {
            SetCurrentValue(TextProperty, format.Display(raw));
            SetCurrentValue(ValueProperty, raw);
        });
        CaretIndex = format.DisplayPosition(raw, before);
    }

    /// <summary>自己改文字、值的时候不再触发同步。</summary>
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

    /// <summary>密码：字符显示成圆点，显示密码的按钮换图标。</summary>
    protected override void Sync()
    {
        base.Sync();
        var password = Type == TextFieldType.Password;
        SetCurrentValue(PasswordCharProperty, password ? '•' : default);
        if (reveal is null) return;
        reveal.IsVisible = password;
        reveal.Icon = RevealPassword ? Lucide.EyeOff : Lucide.Eye;
        global::Avalonia.Automation.AutomationProperties.SetName(reveal, Provider.GetLang(this).PasswordShow);
    }

    void OnReveal(object? sender, RoutedEventArgs e) => SetCurrentValue(RevealPasswordProperty, !RevealPassword);
}

/// <summary>TextField 模板里的部件名（外壳、清空、字数同 <see cref="TextInputParts"/>）。</summary>
public static class TextFieldParts
{
    /// <summary>外壳（Border：边框、底色、圆角）。</summary>
    public const string Root = TextInputParts.Root;

    /// <summary>前置图标（rune:Icon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>后置图标（rune:Icon）。</summary>
    public const string IconEnd = "PART_IconEnd";

    /// <summary>前缀文字（TextBlock）。</summary>
    public const string Prefix = "PART_Prefix";

    /// <summary>后缀文字（TextBlock）。</summary>
    public const string Suffix = "PART_Suffix";

    /// <summary>清空按钮（rune:ClearButton）。</summary>
    public const string Clear = TextInputParts.Clear;

    /// <summary>显示密码的按钮（rune:IconButton）。</summary>
    public const string Reveal = "PART_Reveal";

    /// <summary>字数计数（rune:CharacterCount）。</summary>
    public const string Count = TextInputParts.Count;
}
