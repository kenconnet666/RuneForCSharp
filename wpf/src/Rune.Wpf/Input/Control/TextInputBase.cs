using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 文字输入的外壳（vue-tsx-pro 的 control：inputRecipe、controlParts）：<see cref="TextField"/>、<see cref="Textarea"/> 共用。
/// 从平台的 TextBox 派生（Text、MaxLength、IsReadOnly、输入法、撤销都现成），外壳是边框、悬停、聚焦时压住边框的焦点环、出错、只读、不可用；
/// 外观 <see cref="Variant"/>（surface / soft）、<see cref="Size"/>、<see cref="Radius"/>（默认 md），占位文字 <see cref="PlaceholderText"/>。可以一键清空、显示字数。
/// 放进 Field 时以标签为名称，沿用字段的出错、必填、只读（<see cref="ShowsInvalid"/>、<see cref="ShowsReadOnly"/> 给主题样式用）。
/// 模板里的清空按钮叫 <c>PART_Clear</c>、字数叫 <c>PART_Count</c>（<see cref="TextInputParts"/>）。
/// </summary>
[TemplatePart(Name = "PART_ContentHost", Type = typeof(ScrollViewer))]
public abstract class TextInputBase : TextBox, IFieldControl
{
    /// <summary>样式，默认 surface。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ControlVariant), typeof(TextInputBase),
        new FrameworkPropertyMetadata(ControlVariant.Surface));

    /// <summary>字号档位，内部尺寸随之缩放；不写时跟着外面。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(TextInputBase));

    /// <summary>圆角，默认 md；搜索框常用 full（胶囊形）。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(Radius), typeof(TextInputBase),
        new FrameworkPropertyMetadata(Radius.Md));

    /// <summary>外壳的圆角（按 <see cref="Radius"/> 与字号算出，主题样式写系数）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(TextInputBase),
        new FrameworkPropertyMetadata(default(CornerRadius), null, EmSize.CoerceCornerRadius));

    /// <summary>占位文字：没有内容时显示（textPlaceholder）。</summary>
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(TextInputBase));

    /// <summary>有内容、可编辑时显示清空按钮。</summary>
    public static readonly DependencyProperty ClearableProperty = DependencyProperty.Register(nameof(Clearable), typeof(bool), typeof(TextInputBase),
        new FrameworkPropertyMetadata(false, (d, _) => ((TextInputBase)d).Sync()));

    /// <summary>显示字数计数；上限取 MaxLength，超出时用危险色。</summary>
    public static readonly DependencyProperty ShowCountProperty = DependencyProperty.Register(nameof(ShowCount), typeof(bool), typeof(TextInputBase),
        new FrameworkPropertyMetadata(false, (d, _) => ((TextInputBase)d).Sync()));

    /// <summary>校验未通过：红色描边与焦点环；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(nameof(IsInvalid), typeof(bool?), typeof(TextInputBase),
        new FrameworkPropertyMetadata(null, (d, _) => ((TextInputBase)d).Sync()));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(nameof(IsRequired), typeof(bool?), typeof(TextInputBase));

    static readonly DependencyPropertyKey ShowsInvalidKey = DependencyProperty.RegisterReadOnly(nameof(ShowsInvalid), typeof(bool), typeof(TextInputBase), new PropertyMetadata(false));

    /// <summary>出错（自己的或所在字段的）：主题样式按它标红。</summary>
    public static readonly DependencyProperty ShowsInvalidProperty = ShowsInvalidKey.DependencyProperty;

    static readonly DependencyPropertyKey ShowsReadOnlyKey = DependencyProperty.RegisterReadOnly(nameof(ShowsReadOnly), typeof(bool), typeof(TextInputBase), new PropertyMetadata(false));

    /// <summary>只读（自己的或所在字段的）：主题样式按它换浅底。</summary>
    public static readonly DependencyProperty ShowsReadOnlyProperty = ShowsReadOnlyKey.DependencyProperty;

    ButtonBase? clear;
    CharacterCount? count;

    static TextInputBase()
    {
        EmSize.Register(typeof(TextInputBase), CornerRadiusProperty);
        IsReadOnlyProperty.OverrideMetadata(typeof(TextInputBase), new FrameworkPropertyMetadata(false, (d, _) => ((TextInputBase)d).Sync()));
        MaxLengthProperty.OverrideMetadata(typeof(TextInputBase), new FrameworkPropertyMetadata(0, (d, _) => ((TextInputBase)d).Sync()));
    }

    /// <summary>新建：内容、启用状态变了跟着更新。</summary>
    protected TextInputBase()
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

    /// <summary>占位文字。</summary>
    public string? PlaceholderText { get => (string?)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    /// <summary>显示清空按钮。</summary>
    public bool Clearable { get => (bool)GetValue(ClearableProperty); set => SetValue(ClearableProperty, value); }

    /// <summary>显示字数计数。</summary>
    public bool ShowCount { get => (bool)GetValue(ShowCountProperty); set => SetValue(ShowCountProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => (bool?)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => (bool?)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只要输入、不要外壳（没有边框、底色、内边距与焦点环；命令面板的搜索行）：本地值压过主题样式与模板触发器。</summary>
    internal bool Bare
    {
        get;
        set
        {
            field = value;
            if (value)
            {
                Background = BorderBrush = Brushes.Transparent;
                Padding = default;
            }
            else
            {
                ClearValue(BackgroundProperty);
                ClearValue(BorderBrushProperty);
                ClearValue(PaddingProperty);
            }
            HideFocusRing();
        }
    }

    /// <summary>出错（自己的或所在字段的）。</summary>
    public bool ShowsInvalid => (bool)GetValue(ShowsInvalidProperty);

    /// <summary>只读（自己的或所在字段的）。</summary>
    public bool ShowsReadOnly => (bool)GetValue(ShowsReadOnlyProperty);

    bool ReadOnly => Field.IsSet(this, IsReadOnlyProperty) ? IsReadOnly : Field.Of(this)?.IsReadOnly ?? false;

    /// <summary>可以编辑（可用、不只读）：清空按钮只在这时出现。</summary>
    protected bool Editable => IsEnabled && !ReadOnly;

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        if (clear is not null) clear.Click -= OnClear;
        base.OnApplyTemplate();
        clear = GetTemplateChild(TextInputParts.Clear) as ButtonBase;
        count = GetTemplateChild(TextInputParts.Count) as CharacterCount;
        if (clear is not null) clear.Click += OnClear;
        OnTemplateParts();
        HideFocusRing();
        FitInnerButtons();
        Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) FitInnerButtons();
    }

    /// <summary>框里的小按钮按字号收紧（派生类加上自己的，如显示密码按钮）。</summary>
    protected virtual void FitInnerButtons() => FitInnerButton(clear, FontSize);

    /// <summary>框里的小按钮（清空、显示密码）：比独立的图标按钮紧凑，1.5em（不小于 24），左右各收进 space.xs；Select 的清空按钮同样。</summary>
    /// <param name="button">按钮；null 时什么也不做。</param>
    /// <param name="em">外壳的字号。</param>
    internal static void FitInnerButton(FrameworkElement? button, double em)
    {
        if (button is null) return;
        button.Width = button.MinHeight = Math.Max(1.5 * em, ControlHeight.MinTarget);
        button.Margin = new Thickness(-0.25 * em, 0, -0.25 * em, 0);
    }

    void HideFocusRing()
    {
        if (GetTemplateChild("PART_FocusRing") is not UIElement ring) return;
        if (Bare) ring.Visibility = Visibility.Collapsed;
        else ring.ClearValue(VisibilityProperty);
    }

    /// <summary>派生类取自己模板里的部件（OnApplyTemplate 里、同步之前）。</summary>
    protected virtual void OnTemplateParts()
    {
    }

    /// <summary>出错、只读、清空按钮、字数；派生类接着同步自己的部件。</summary>
    protected virtual void Sync()
    {
        SetValue(ShowsInvalidKey, IsInvalid ?? Field.Of(this)?.Invalid ?? false);
        SetValue(ShowsReadOnlyKey, ReadOnly);
        if (clear is not null) clear.Visibility = Clearable && Editable && !string.IsNullOrEmpty(Text) ? Visibility.Visible : Visibility.Collapsed;
        if (count is not null)
        {
            count.Visibility = ShowCount ? Visibility.Visible : Visibility.Collapsed;
            count.Count = Text?.Length ?? 0;
            count.Max = MaxLength > 0 ? MaxLength : null;
        }
    }

    void OnClear(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(TextProperty, "");
        Focus();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TextInputPeer(this);

    /// <summary>读屏：类名是组件名；没写名称时取所在字段的标签。</summary>
    sealed class TextInputPeer(TextInputBase owner) : TextBoxAutomationPeer(owner)
    {
        protected override string GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        protected override string GetClassNameCore() => owner.GetType().Name;

        protected override string GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText ?? "" : "";
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
