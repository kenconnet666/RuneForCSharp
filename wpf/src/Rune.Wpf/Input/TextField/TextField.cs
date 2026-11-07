using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 单行文本框（vue-tsx-pro 的 TextField）：输入框外壳里放平台的文字输入，前后可以带图标与固定文字，可以一键清空、显示字数；
/// <see cref="Type"/> 为 password 时字符显示成圆点（模板里换成 PasswordBox，与 Text 同步），带显示密码的按钮。
/// <code>
/// &lt;rune:TextField Text="{Binding Keyword}" Type="search" Icon="{x:Static rune:Lucide.Search}" Clearable="True" AutomationProperties.Name="搜索" /&gt;
/// &lt;rune:TextField Value="{Binding Phone}" Format="{x:Static rune:TextFormat.Phone}" /&gt;   &lt;!-- 显示 138 0000 1111，Value 是 13800001111 --&gt;
/// </code>
/// 外壳、外观档位、占位文字、清空、字数、Field 的状态在 <see cref="TextInputBase"/>（同 Textarea）。值是 <see cref="Value"/>（TS 的 _value，默认双向绑定）：
/// 没有 <see cref="Format"/> 时就是 Text；有 Format 时输入框里显示分好组的文字，Value 是去掉分隔的原文，过滤掉不接受的字符、截到最长，在中间插字光标也不跳。
/// </summary>
public class TextField : TextInputBase
{
    /// <summary>输入的类型（TS 的 type）：password 时字符显示成圆点，带显示密码的按钮；其余在桌面上同 text。</summary>
    public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(nameof(Type), typeof(TextFieldType), typeof(TextField),
        new FrameworkPropertyMetadata(TextFieldType.Text, (d, _) => ((TextField)d).Sync()));

    /// <summary>前置图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(TextField));

    /// <summary>后置图标。</summary>
    public static readonly DependencyProperty IconEndProperty = DependencyProperty.Register(nameof(IconEnd), typeof(IconData), typeof(TextField));

    /// <summary>输入框前的固定文字（如 <c>https://</c>），不算在值里。</summary>
    public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(TextField));

    /// <summary>输入框后的固定文字（如 <c>元</c>），不算在值里。</summary>
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(TextField));

    /// <summary>显示着密码的原文。</summary>
    public static readonly DependencyProperty RevealPasswordProperty = DependencyProperty.Register(nameof(RevealPassword), typeof(bool), typeof(TextField),
        new FrameworkPropertyMetadata(false, (d, _) => ((TextField)d).Sync()));

    /// <summary>输入时自动分组显示的格式（手机号、身份证号、银行卡号，<see cref="TextFormat"/>）。</summary>
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(TextFormat), typeof(TextField),
        new FrameworkPropertyMetadata(null, (d, _) => ((TextField)d).ShowValue()));

    /// <summary>值：有 Format 时是去掉分隔的原文，没有时同 Text。默认双向绑定。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(TextField),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((TextField)d).ShowValue()));

    IconButton? reveal;
    PasswordBox? password;
    bool syncingPassword;
    bool formatting;

    static TextField() => DefaultStyleKeyProperty.OverrideMetadata(typeof(TextField), new FrameworkPropertyMetadata(typeof(TextField)));

    /// <summary>新建：文字变了写回值（有格式时重新分组）。</summary>
    public TextField() => TextChanged += (_, _) => ReadText();

    /// <summary>输入的类型。</summary>
    public TextFieldType Type { get => (TextFieldType)GetValue(TypeProperty); set => SetValue(TypeProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>后置图标。</summary>
    public IconData? IconEnd { get => (IconData?)GetValue(IconEndProperty); set => SetValue(IconEndProperty, value); }

    /// <summary>前缀文字。</summary>
    public string? Prefix { get => (string?)GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>后缀文字。</summary>
    public string? Suffix { get => (string?)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>显示着密码的原文。</summary>
    public bool RevealPassword { get => (bool)GetValue(RevealPasswordProperty); set => SetValue(RevealPasswordProperty, value); }

    /// <summary>显示格式。</summary>
    public TextFormat? Format { get => (TextFormat?)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>值。</summary>
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    protected override void FitInnerButtons()
    {
        base.FitInnerButtons();
        FitInnerButton(reveal, FontSize);
    }

    /// <inheritdoc />
    protected override void OnTemplateParts()
    {
        if (reveal is not null) reveal.Click -= OnReveal;
        if (password is not null) password.PasswordChanged -= OnPasswordChanged;
        reveal = GetTemplateChild(TextFieldParts.Reveal) as IconButton;
        password = GetTemplateChild(TextFieldParts.Password) as PasswordBox;
        if (reveal is not null) reveal.Click += OnReveal;
        if (password is not null) password.PasswordChanged += OnPasswordChanged;
    }

    /// <summary>值 → 输入框里的文字（有格式时分好组）。</summary>
    void ShowValue()
    {
        if (formatting) return;
        var raw = Format?.Parse(Value ?? "") ?? Value ?? "";
        SetFormatted(() =>
        {
            SetCurrentValue(TextProperty, Format?.Display(raw) ?? raw);
            if (raw != Value) SetCurrentValue(ValueProperty, raw);
        });
    }

    /// <summary>输入框里的文字 → 值；有格式时重新分组（投递到这次输入处理完之后，光标已经到位）。</summary>
    void ReadText()
    {
        if (formatting) return;
        var text = Text ?? "";
        SetFormatted(() => SetCurrentValue(ValueProperty, Format?.Parse(text) ?? text));
        if (Format is { } format && format.Display(format.Parse(text)) != text) Dispatcher.BeginInvoke(Regroup, DispatcherPriority.Input);
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

    /// <summary>密码框、显示密码的按钮。</summary>
    protected override void Sync()
    {
        base.Sync();
        var isPassword = Type == TextFieldType.Password;
        if (reveal is not null)
        {
            reveal.Visibility = isPassword ? Visibility.Visible : Visibility.Collapsed;
            reveal.Icon = RevealPassword ? Lucide.EyeOff : Lucide.Eye;
            System.Windows.Automation.AutomationProperties.SetName(reveal, Provider.GetLang(this).PasswordShow);
        }
        if (password is null) return;
        password.Visibility = isPassword && !RevealPassword ? Visibility.Visible : Visibility.Collapsed;
        if (password.Password == Text || syncingPassword) return;
        syncingPassword = true;
        password.Password = Text;
        syncingPassword = false;
    }

    void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (syncingPassword || password is null) return;
        syncingPassword = true;
        SetCurrentValue(TextProperty, password.Password);
        syncingPassword = false;
    }

    void OnReveal(object sender, RoutedEventArgs e) => SetCurrentValue(RevealPasswordProperty, !RevealPassword);
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

    /// <summary>密码框（PasswordBox：type 为 password、没显示原文时代替文字输入）。</summary>
    public const string Password = "PART_Password";
}
