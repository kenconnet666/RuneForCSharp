using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 文字输入的外壳（vue-tsx-pro 的 control：inputRecipe、controlParts）：<see cref="TextField"/>、<see cref="Textarea"/> 共用。
/// 从平台的 TextBox 派生（Text、PlaceholderText、MaxLength、IsReadOnly、输入法、撤销都现成），外壳是边框、悬停、聚焦时压住边框的焦点环、出错、只读、不可用；
/// 外观 <see cref="Variant"/>（surface / soft）、<see cref="Size"/>、<see cref="Radius"/>（默认 md）。可以一键清空、显示字数。
/// 放进 Field 时以标签为名称，沿用字段的出错、必填、只读。共用主题 <c>Rune.TextInput</c>，各组件 BasedOn 它、只写自己的模板。
/// 模板里的清空按钮叫 <c>PART_Clear</c>、字数叫 <c>PART_Count</c>（<see cref="TextInputParts"/>）。伪类：<c>:soft</c>、<c>:invalid</c>、<c>:readonly</c>。
/// </summary>
public abstract class TextInputBase : TextBox, IFieldControl
{
    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<ControlVariant> VariantProperty = AvaloniaProperty.Register<TextInputBase, ControlVariant>(nameof(Variant));

    /// <summary>字号档位，内部尺寸随之缩放；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<TextInputBase, TextSize>(nameof(Size));

    /// <summary>圆角，默认 md；搜索框常用 full（胶囊形）。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<TextInputBase, Radius>(nameof(Radius));

    /// <summary>有内容、可编辑时显示清空按钮。</summary>
    public static readonly StyledProperty<bool> ClearableProperty = AvaloniaProperty.Register<TextInputBase, bool>(nameof(Clearable));

    /// <summary>显示字数计数；上限取 MaxLength，超出时用危险色。</summary>
    public static readonly StyledProperty<bool> ShowCountProperty = AvaloniaProperty.Register<TextInputBase, bool>(nameof(ShowCount));

    /// <summary>校验未通过：红色描边与焦点环；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<TextInputBase, bool?>(nameof(IsInvalid));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<TextInputBase, bool?>(nameof(IsRequired));

    readonly TemplateResources resources;
    ButtonBase? clear;
    CharacterCount? count;

    // 放进 ControlGroup 时相邻一侧不要圆角
    static TextInputBase() => Attached.SquareCornersOf<TextInputBase>();

    /// <summary>新建：字号、圆角按档位解析成主题资源。</summary>
    protected TextInputBase()
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

    /// <summary>显示清空按钮。</summary>
    public bool Clearable { get => GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>显示字数计数。</summary>
    public bool ShowCount { get => GetValue(ShowCountProperty); set => SetValue(ShowCountProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只要输入、不要外壳（没有边框、底色、内边距与焦点环；命令面板的搜索行）：伪类 <c>:bare</c>。</summary>
    internal bool Bare { get; set { field = value; PseudoClasses.Set(":bare", value); } }

    /// <summary>出错：自己写了的优先，其次所在字段的。</summary>
    internal bool Invalid => IsInvalid ?? Field.Of(this)?.Invalid ?? false;

    /// <summary>只读：自己写了的优先，其次所在字段的。</summary>
    internal bool ReadOnly => IsSet(IsReadOnlyProperty) ? IsReadOnly : Field.Of(this)?.IsReadOnly ?? false;

    /// <summary>可以编辑（可用、不只读）：清空按钮只在这时出现。</summary>
    protected bool Editable => IsEffectivelyEnabled && !ReadOnly;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => GetType();

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (clear is not null) clear.Click -= OnClear;
        clear = e.NameScope.Find<ButtonBase>(TextInputParts.Clear);
        count = e.NameScope.Find<CharacterCount>(TextInputParts.Count);
        if (clear is not null) clear.Click += OnClear;
        OnTemplateParts(e.NameScope);
        Sync();
    }

    /// <summary>派生类取自己模板里的部件（OnApplyTemplate 里、同步之前）。</summary>
    /// <param name="scope">模板的名字范围。</param>
    protected virtual void OnTemplateParts(INameScope scope)
    {
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
        else if (change.Property == TextProperty || change.Property == ClearableProperty || change.Property == ShowCountProperty
                 || change.Property == IsInvalidProperty || change.Property == IsReadOnlyProperty || change.Property == MaxLengthProperty
                 || change.Property == Field.OwnerProperty || change.Property == IsEnabledProperty || SyncsOn(change.Property))
            Sync();
    }

    /// <summary>派生类的哪些属性变了也要同步。</summary>
    /// <param name="property">变了的属性。</param>
    protected virtual bool SyncsOn(AvaloniaProperty property) => false;

    /// <summary>状态伪类、清空按钮、字数；派生类接着同步自己的部件。</summary>
    protected virtual void Sync()
    {
        PseudoClasses.Set(":invalid", Invalid);
        PseudoClasses.Set(":readonly", ReadOnly);
        if (clear is not null) clear.IsVisible = Clearable && Editable && !string.IsNullOrEmpty(Text);
        if (count is not null)
        {
            count.IsVisible = ShowCount;
            count.Count = Text?.Length ?? 0;
            count.Max = MaxLength > 0 ? MaxLength : null;
        }
    }

    void OnClear(object? sender, RoutedEventArgs e) => ClearText();

    /// <summary>清空按钮：清空文字，焦点回到输入框。派生类先清自己的值（NumberField 的值置空）再调它。</summary>
    protected virtual void ClearText()
    {
        SetCurrentValue(TextProperty, "");
        Focus();
    }

    /// <summary>点在外壳的空白处（内边距、图标、前后缀）：焦点交给输入框；框里的按钮自己处理。</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<ButtonBase>(true) is not null) return;
        base.OnPointerPressed(e);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TextInputPeer(this);

    /// <summary>读屏：类名是组件名；没写名称时取所在字段的标签；帮助文字拼上字段的说明与错误。派生类在它上面加自己的读屏接口（NumberField 的数值范围）。</summary>
    /// <param name="owner">输入框。</param>
    protected class TextInputPeer(TextInputBase owner) : TextBoxAutomationPeer(owner)
    {
        /// <inheritdoc />
        protected override string GetClassNameCore() => owner.GetType().Name;

        /// <inheritdoc />
        protected override string? GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        /// <inheritdoc />
        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;
    }
}

/// <summary>文字输入共用的部件名（TextField、Textarea 的模板里都有）。</summary>
public static class TextInputParts
{
    /// <summary>外壳（Border：边框、底色、圆角）。</summary>
    public const string Root = "PART_Root";

    /// <summary>清空按钮（rune:ClearButton）。</summary>
    public const string Clear = "PART_Clear";

    /// <summary>字数计数（rune:CharacterCount）。</summary>
    public const string Count = "PART_Count";
}
