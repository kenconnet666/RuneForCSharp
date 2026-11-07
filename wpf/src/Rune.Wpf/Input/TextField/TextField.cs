using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>
/// 单行文本框（vue-tsx-pro 的 TextField）：输入框外壳（边框、悬停、聚焦时压住边框的焦点环、出错、只读、不可用）里放平台的文字输入，
/// 前后可以带图标与固定文字，可以一键清空；<see cref="Type"/> 为 password 时字符显示成圆点（模板里换成 PasswordBox，与 Text 同步），带显示密码的按钮。
/// <code>
/// &lt;rune:TextField Text="{Binding Keyword}" Type="search" Icon="{x:Static rune:Lucide.Search}" Clearable="True" AutomationProperties.Name="搜索" /&gt;
/// </code>
/// 从平台的 TextBox 派生：Text（TS 的 _value）、MaxLength、IsReadOnly、输入法、撤销都现成；占位文字是 <see cref="PlaceholderText"/>。
/// 外观：<see cref="Variant"/>（surface / soft）、<see cref="Size"/>、<see cref="Radius"/>（默认 md）。放进 Field 时以标签为名称，沿用字段的出错、只读。
/// 没做：<c>format</c>（手机号、身份证号的分组显示）。
/// </summary>
[TemplatePart(Name = "PART_ContentHost", Type = typeof(ScrollViewer))]
public class TextField : TextBox, IFieldControl
{
    /// <summary>样式，默认 surface。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ControlVariant), typeof(TextField),
        new FrameworkPropertyMetadata(ControlVariant.Surface));

    /// <summary>字号档位，内部尺寸随之缩放；不写时跟着外面。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(TextField));

    /// <summary>圆角，默认 md；搜索框常用 full（胶囊形）。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(Radius), typeof(TextField),
        new FrameworkPropertyMetadata(Radius.Md));

    /// <summary>外壳的圆角（按 <see cref="Radius"/> 与字号算出，主题样式写系数）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(TextField),
        new FrameworkPropertyMetadata(default(CornerRadius), null, EmSize.CoerceCornerRadius));

    /// <summary>输入的类型（TS 的 type）：password 时字符显示成圆点，带显示密码的按钮；其余在桌面上同 text。</summary>
    public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(nameof(Type), typeof(TextFieldType), typeof(TextField),
        new FrameworkPropertyMetadata(TextFieldType.Text, (d, _) => ((TextField)d).Sync()));

    /// <summary>占位文字：没有内容时显示（textPlaceholder）。</summary>
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(TextField));

    /// <summary>前置图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(TextField));

    /// <summary>后置图标。</summary>
    public static readonly DependencyProperty IconEndProperty = DependencyProperty.Register(nameof(IconEnd), typeof(IconData), typeof(TextField));

    /// <summary>输入框前的固定文字（如 <c>https://</c>），不算在值里。</summary>
    public static readonly DependencyProperty PrefixProperty = DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(TextField));

    /// <summary>输入框后的固定文字（如 <c>元</c>），不算在值里。</summary>
    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(TextField));

    /// <summary>有内容、可编辑时显示清空按钮。</summary>
    public static readonly DependencyProperty ClearableProperty = DependencyProperty.Register(nameof(Clearable), typeof(bool), typeof(TextField),
        new FrameworkPropertyMetadata(false, (d, _) => ((TextField)d).Sync()));

    /// <summary>显示字数计数；上限取 MaxLength，超出时用危险色。</summary>
    public static readonly DependencyProperty ShowCountProperty = DependencyProperty.Register(nameof(ShowCount), typeof(bool), typeof(TextField),
        new FrameworkPropertyMetadata(false, (d, _) => ((TextField)d).Sync()));

    /// <summary>校验未通过：红色描边与焦点环；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(nameof(IsInvalid), typeof(bool?), typeof(TextField),
        new FrameworkPropertyMetadata(null, (d, _) => ((TextField)d).Sync()));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(nameof(IsRequired), typeof(bool?), typeof(TextField));

    /// <summary>显示着密码的原文。</summary>
    public static readonly DependencyProperty RevealPasswordProperty = DependencyProperty.Register(nameof(RevealPassword), typeof(bool), typeof(TextField),
        new FrameworkPropertyMetadata(false, (d, _) => ((TextField)d).Sync()));

    static readonly DependencyPropertyKey ShowsInvalidKey = DependencyProperty.RegisterReadOnly(nameof(ShowsInvalid), typeof(bool), typeof(TextField), new PropertyMetadata(false));

    /// <summary>出错（自己的或所在字段的）：主题样式按它标红。</summary>
    public static readonly DependencyProperty ShowsInvalidProperty = ShowsInvalidKey.DependencyProperty;

    static readonly DependencyPropertyKey ShowsReadOnlyKey = DependencyProperty.RegisterReadOnly(nameof(ShowsReadOnly), typeof(bool), typeof(TextField), new PropertyMetadata(false));

    /// <summary>只读（自己的或所在字段的）：主题样式按它换浅底。</summary>
    public static readonly DependencyProperty ShowsReadOnlyProperty = ShowsReadOnlyKey.DependencyProperty;

    ButtonBase? clear;
    IconButton? reveal;
    CharacterCount? count;
    PasswordBox? password;
    bool syncingPassword;

    static TextField()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TextField), new FrameworkPropertyMetadata(typeof(TextField)));
        EmSize.Register(typeof(TextField), CornerRadiusProperty);
        IsReadOnlyProperty.OverrideMetadata(typeof(TextField), new FrameworkPropertyMetadata(false, (d, _) => ((TextField)d).Sync()));
        MaxLengthProperty.OverrideMetadata(typeof(TextField), new FrameworkPropertyMetadata(0, (d, _) => ((TextField)d).Sync()));
    }

    /// <summary>新建：内容、启用状态变了跟着更新。</summary>
    public TextField()
    {
        TextChanged += (_, _) => Sync();
        IsEnabledChanged += (_, _) => Sync();
        Loaded += (_, _) => Sync();
    }

    /// <summary>样式。</summary>
    public ControlVariant Variant { get => (ControlVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>外壳的圆角。</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>输入的类型。</summary>
    public TextFieldType Type { get => (TextFieldType)GetValue(TypeProperty); set => SetValue(TypeProperty, value); }

    /// <summary>占位文字。</summary>
    public string? PlaceholderText { get => (string?)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>后置图标。</summary>
    public IconData? IconEnd { get => (IconData?)GetValue(IconEndProperty); set => SetValue(IconEndProperty, value); }

    /// <summary>前缀文字。</summary>
    public string? Prefix { get => (string?)GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>后缀文字。</summary>
    public string? Suffix { get => (string?)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>显示清空按钮。</summary>
    public bool Clearable { get => (bool)GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>显示字数计数。</summary>
    public bool ShowCount { get => (bool)GetValue(ShowCountProperty); set => SetValue(ShowCountProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => (bool?)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => (bool?)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>显示着密码的原文。</summary>
    public bool RevealPassword { get => (bool)GetValue(RevealPasswordProperty); set => SetValue(RevealPasswordProperty, value); }

    /// <summary>出错（自己的或所在字段的）。</summary>
    public bool ShowsInvalid => (bool)GetValue(ShowsInvalidProperty);

    /// <summary>只读（自己的或所在字段的）。</summary>
    public bool ShowsReadOnly => (bool)GetValue(ShowsReadOnlyProperty);

    bool ReadOnly => Field.IsSet(this, IsReadOnlyProperty) ? IsReadOnly : Field.Of(this)?.IsReadOnly ?? false;

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        if (clear is not null) clear.Click -= OnClear;
        if (reveal is not null) reveal.Click -= OnReveal;
        if (password is not null) password.PasswordChanged -= OnPasswordChanged;
        base.OnApplyTemplate();
        clear = GetTemplateChild(TextFieldParts.Clear) as ButtonBase;
        reveal = GetTemplateChild(TextFieldParts.Reveal) as IconButton;
        count = GetTemplateChild(TextFieldParts.Count) as CharacterCount;
        password = GetTemplateChild(TextFieldParts.Password) as PasswordBox;
        if (clear is not null) clear.Click += OnClear;
        if (reveal is not null) reveal.Click += OnReveal;
        if (password is not null) password.PasswordChanged += OnPasswordChanged;
        Sync();
    }

    /// <summary>出错、只读、清空按钮、密码框、字数。</summary>
    void Sync()
    {
        SetValue(ShowsInvalidKey, IsInvalid ?? Field.Of(this)?.Invalid ?? false);
        SetValue(ShowsReadOnlyKey, ReadOnly);
        var editable = IsEnabled && !ReadOnly;
        var isPassword = Type == TextFieldType.Password;
        if (clear is not null) clear.Visibility = Clearable && editable && !string.IsNullOrEmpty(Text) ? Visibility.Visible : Visibility.Collapsed;
        if (reveal is not null)
        {
            reveal.Visibility = isPassword ? Visibility.Visible : Visibility.Collapsed;
            reveal.Icon = RevealPassword ? Lucide.EyeOff : Lucide.Eye;
            System.Windows.Automation.AutomationProperties.SetName(reveal, Provider.GetLang(this).PasswordShow);
        }
        if (password is not null)
        {
            password.Visibility = isPassword && !RevealPassword ? Visibility.Visible : Visibility.Collapsed;
            if (password.Password != Text && !syncingPassword)
            {
                syncingPassword = true;
                password.Password = Text;
                syncingPassword = false;
            }
        }
        if (count is not null)
        {
            count.Visibility = ShowCount ? Visibility.Visible : Visibility.Collapsed;
            count.Count = Text?.Length ?? 0;
            count.Max = MaxLength > 0 ? MaxLength : null;
        }
    }

    void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (syncingPassword || password is null) return;
        syncingPassword = true;
        SetCurrentValue(TextProperty, password.Password);
        syncingPassword = false;
    }

    void OnClear(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(TextProperty, "");
        Focus();
    }

    void OnReveal(object sender, RoutedEventArgs e) => SetCurrentValue(RevealPasswordProperty, !RevealPassword);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TextFieldPeer(this);

    sealed class TextFieldPeer(TextField owner) : TextBoxAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(TextField);

        protected override string GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText ?? "" : "";
    }
}

/// <summary>TextField 模板里的部件名。</summary>
public static class TextFieldParts
{
    /// <summary>外壳（Border：边框、底色、圆角）。</summary>
    public const string Root = "PART_Root";

    /// <summary>前置图标（rune:Icon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>后置图标（rune:Icon）。</summary>
    public const string IconEnd = "PART_IconEnd";

    /// <summary>前缀文字（TextBlock）。</summary>
    public const string Prefix = "PART_Prefix";

    /// <summary>后缀文字（TextBlock）。</summary>
    public const string Suffix = "PART_Suffix";

    /// <summary>清空按钮（rune:ClearButton）。</summary>
    public const string Clear = "PART_Clear";

    /// <summary>显示密码的按钮（rune:IconButton）。</summary>
    public const string Reveal = "PART_Reveal";

    /// <summary>字数计数（rune:CharacterCount）。</summary>
    public const string Count = "PART_Count";

    /// <summary>密码框（PasswordBox：type 为 password、没显示原文时代替文字输入）。</summary>
    public const string Password = "PART_Password";
}
