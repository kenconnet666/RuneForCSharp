using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 单行文本框（vue-tsx-pro 的 TextField）：输入框外壳（边框、悬停、聚焦时压住边框的焦点环、出错、只读、不可用）里放平台的文字输入，
/// 前后可以带图标与固定文字，可以一键清空；<see cref="Type"/> 为 password 时带显示 / 隐藏密码的按钮。
/// <code>
/// &lt;rune:TextField Text="{Binding Keyword}" Type="search" Icon="{x:Static rune:Lucide.Search}" Clearable="True" AutomationProperties.Name="搜索" /&gt;
/// &lt;rune:Field Label="密码" Error="{Binding PasswordError}"&gt;&lt;rune:TextField Text="{Binding Password}" Type="password" /&gt;&lt;/rune:Field&gt;
/// </code>
/// 从平台的 TextBox 派生：Text（TS 的 _value）、PlaceholderText、MaxLength、IsReadOnly、输入法、撤销都现成。外观：<see cref="Variant"/>（surface / soft）、
/// <see cref="Size"/>、<see cref="Radius"/>（默认 md）。放进 Field 时以标签为名称，沿用字段的出错、必填、只读。部件名在 <see cref="TextFieldParts"/>。
/// 没做：<c>format</c>（手机号、身份证号的分组显示）。
/// </summary>
public class TextField : TextBox, IFieldControl
{
    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<ControlVariant> VariantProperty = AvaloniaProperty.Register<TextField, ControlVariant>(nameof(Variant));

    /// <summary>字号档位，内部尺寸随之缩放；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<TextField, TextSize>(nameof(Size));

    /// <summary>圆角，默认 md；搜索框常用 full（胶囊形）。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<TextField, Radius>(nameof(Radius));

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

    /// <summary>有内容、可编辑时显示清空按钮。</summary>
    public static readonly StyledProperty<bool> ClearableProperty = AvaloniaProperty.Register<TextField, bool>(nameof(Clearable));

    /// <summary>显示字数计数；上限取 MaxLength，超出时用危险色。</summary>
    public static readonly StyledProperty<bool> ShowCountProperty = AvaloniaProperty.Register<TextField, bool>(nameof(ShowCount));

    /// <summary>校验未通过：红色描边与焦点环；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<TextField, bool?>(nameof(IsInvalid));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<TextField, bool?>(nameof(IsRequired));

    readonly TemplateResources resources;
    ButtonBase? clear;
    IconButton? reveal;
    CharacterCount? count;

    /// <summary>新建：字号、圆角按档位解析成主题资源。</summary>
    public TextField()
    {
        resources = new TemplateResources(this);
        Resolved.Bind(this, Resolved.RadiusProperty, Radius.Md.ResourceKey);
    }

    /// <summary>样式。</summary>
    public ControlVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

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

    /// <summary>显示清空按钮。</summary>
    public bool Clearable { get => GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>显示字数计数。</summary>
    public bool ShowCount { get => GetValue(ShowCountProperty); set => SetValue(ShowCountProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>出错：自己写了的优先，其次所在字段的。</summary>
    internal bool Invalid => IsInvalid ?? Field.Of(this)?.Invalid ?? false;

    /// <summary>只读：自己写了的优先，其次所在字段的。</summary>
    internal bool ReadOnly => IsSet(IsReadOnlyProperty) ? IsReadOnly : Field.Of(this)?.IsReadOnly ?? false;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TextField);

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (clear is not null) clear.Click -= OnClear;
        if (reveal is not null) reveal.Click -= OnReveal;
        clear = e.NameScope.Find<ButtonBase>(TextFieldParts.Clear);
        reveal = e.NameScope.Find<IconButton>(TextFieldParts.Reveal);
        count = e.NameScope.Find<CharacterCount>(TextFieldParts.Count);
        if (clear is not null) clear.Click += OnClear;
        if (reveal is not null) reveal.Click += OnReveal;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            ControlHeight.Apply(this, Size);
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == RadiusProperty) Resolved.Bind(this, Resolved.RadiusProperty, (Radius.Value is null ? Radius.Md : Radius).ResourceKey);
        else if (change.Property == VariantProperty) PseudoClasses.Set(":soft", Variant == ControlVariant.Soft);
        else if (change.Property == TextProperty || change.Property == ClearableProperty || change.Property == ShowCountProperty || change.Property == TypeProperty
                 || change.Property == IsInvalidProperty || change.Property == IsReadOnlyProperty || change.Property == MaxLengthProperty
                 || change.Property == Field.OwnerProperty || change.Property == IsEnabledProperty || change.Property == RevealPasswordProperty)
            Sync();
    }

    /// <summary>状态伪类、清空按钮、密码、字数。</summary>
    void Sync()
    {
        var editable = IsEffectivelyEnabled && !ReadOnly;
        PseudoClasses.Set(":invalid", Invalid);
        PseudoClasses.Set(":readonly", ReadOnly);
        var password = Type == TextFieldType.Password;
        SetCurrentValue(PasswordCharProperty, password ? '•' : default);
        if (clear is not null) clear.IsVisible = Clearable && editable && !string.IsNullOrEmpty(Text);
        if (reveal is not null)
        {
            reveal.IsVisible = password;
            reveal.Icon = RevealPassword ? Lucide.EyeOff : Lucide.Eye;
            global::Avalonia.Automation.AutomationProperties.SetName(reveal, Provider.GetLang(this).PasswordShow);
        }
        if (count is not null)
        {
            count.IsVisible = ShowCount;
            count.Count = Text?.Length ?? 0;
            count.Max = MaxLength > 0 ? MaxLength : null;
        }
    }

    void OnClear(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(TextProperty, "");
        Focus();
    }

    void OnReveal(object? sender, RoutedEventArgs e) => SetCurrentValue(RevealPasswordProperty, !RevealPassword);

    /// <summary>点在外壳的空白处（内边距、图标、前后缀）：焦点交给输入框。</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<ButtonBase>(true) is not null) return;
        base.OnPointerPressed(e);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TextFieldPeer(this);

    sealed class TextFieldPeer(TextField owner) : TextBoxAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(TextField);

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;
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
}
