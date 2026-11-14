using System.Collections;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>
/// 原生下拉框（vue-tsx-pro 的 NativeSelect）：平台自己的 ComboBox（键盘、读屏、选中行为都是平台的），外壳同输入框（外观、尺寸、出错、只读）。
/// <code>
/// &lt;rune:Field Label="收货城市" IsRequired="True"&gt;
///   &lt;rune:NativeSelect Options="{Binding Cities}" Value="{Binding City}" PlaceholderText="选择城市" Icon="{x:Static rune:Lucide.MapPin}" /&gt;
/// &lt;/rune:Field&gt;
/// </code>
/// 和 <see cref="Select"/> 怎么选：选项要图标、说明、搜索、多选、远程加载用 Select；选项只是一串文字、想要平台下拉框的行为用它。
/// <list type="bullet">
/// <item><see cref="Options"/> 是 ListboxOption 或字符串（值与文字相同）；带 Group 的按组排，组名是一行不能选的标题（没有组的在前）。选项的说明、图标不显示。</item>
/// <item>值是选项的 Value（<see cref="Value"/> 默认双向绑定），没选时是 null，这时显示 <see cref="PlaceholderText"/>。不要直接设 ItemsSource：选项由 Options 生成。</item>
/// <item>只读用平台的 IsReadOnly（或所在 Field 的只读）：别的选项不能选、不打开列表（同上游，原生的下拉框没有只读）。</item>
/// </list>
/// </summary>
public class NativeSelect : ComboBox, IFieldControl
{
    /// <summary>选项：ListboxOption 或字符串。</summary>
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(nameof(Options), typeof(IEnumerable), typeof(NativeSelect),
        new PropertyMetadata(null, (d, _) => ((NativeSelect)d).Rebuild()));

    /// <summary>选中的值（默认双向绑定）；没选时是 null。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(NativeSelect),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((NativeSelect)d).SelectValue()));

    /// <summary>没选时显示的文字。</summary>
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(NativeSelect));

    /// <summary>样式，默认 surface。</summary>
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(nameof(Variant), typeof(ControlVariant), typeof(NativeSelect));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(TextSize), typeof(NativeSelect));

    /// <summary>圆角，默认 md。</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(Radius), typeof(NativeSelect));

    /// <summary>外壳的圆角（像素，由主题按 Radius 与字号算）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(NativeSelect),
        new FrameworkPropertyMetadata(default(CornerRadius), null, EmSize.CoerceCornerRadius));

    /// <summary>前置图标。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(NativeSelect));

    /// <summary>校验未通过；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(nameof(IsInvalid), typeof(bool?), typeof(NativeSelect),
        new PropertyMetadata(null, (d, _) => ((NativeSelect)d).Sync()));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(nameof(IsRequired), typeof(bool?), typeof(NativeSelect));

    static readonly DependencyPropertyKey ShowsInvalidKey = DependencyProperty.RegisterReadOnly(nameof(ShowsInvalid), typeof(bool), typeof(NativeSelect), new PropertyMetadata(false));

    /// <summary>显示成出错（自己的 IsInvalid 或所在 Field 出错；样式触发器用）。</summary>
    public static readonly DependencyProperty ShowsInvalidProperty = ShowsInvalidKey.DependencyProperty;

    static readonly DependencyPropertyKey ShowsReadOnlyKey = DependencyProperty.RegisterReadOnly(nameof(ShowsReadOnly), typeof(bool), typeof(NativeSelect), new PropertyMetadata(false));

    /// <summary>显示成只读（IsReadOnly 或所在 Field 只读；样式触发器用）。</summary>
    public static readonly DependencyProperty ShowsReadOnlyProperty = ShowsReadOnlyKey.DependencyProperty;

    static readonly DependencyPropertyKey ShowsPlaceholderKey = DependencyProperty.RegisterReadOnly(nameof(ShowsPlaceholder), typeof(bool), typeof(NativeSelect), new PropertyMetadata(true));

    /// <summary>显示占位文字（没选；样式触发器用）。</summary>
    public static readonly DependencyProperty ShowsPlaceholderProperty = ShowsPlaceholderKey.DependencyProperty;

    readonly List<(NativeSelectItem Item, ListboxOption Option)> choices = [];
    // 改选中项时不回写 Value（是 Value 变了引起的）
    bool syncing;

    static NativeSelect()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NativeSelect), new FrameworkPropertyMetadata(typeof(NativeSelect)));
        EmSize.Register(typeof(NativeSelect), CornerRadiusProperty);
        IsReadOnlyProperty.OverrideMetadata(typeof(NativeSelect), new FrameworkPropertyMetadata(false, (d, _) => ((NativeSelect)d).Sync()));
        IsDropDownOpenProperty.OverrideMetadata(typeof(NativeSelect), new FrameworkPropertyMetadata(false, null,
            (d, value) => (bool)value && ((NativeSelect)d).ReadOnly ? false : value));
    }

    /// <summary>选项。</summary>
    public IEnumerable? Options { get => (IEnumerable?)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }

    /// <summary>选中的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>没选时显示的文字。</summary>
    public string? PlaceholderText { get => (string?)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    /// <summary>样式。</summary>
    public ControlVariant Variant { get => (ControlVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => (Radius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>外壳的圆角（像素）。</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => (bool?)GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => (bool?)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>显示成出错。</summary>
    public bool ShowsInvalid => (bool)GetValue(ShowsInvalidProperty);

    /// <summary>显示成只读。</summary>
    public bool ShowsReadOnly => (bool)GetValue(ShowsReadOnlyProperty);

    /// <summary>显示占位文字。</summary>
    public bool ShowsPlaceholder => (bool)GetValue(ShowsPlaceholderProperty);

    bool ReadOnly => IsReadOnly || (Field.Of(this)?.IsReadOnly ?? false);

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) UpdateMetrics();
    }

    /// <summary>弹出列表按 em 的尺寸：面板 md 圆角、里面留 space.xs。</summary>
    void UpdateMetrics()
    {
        var em = FontSize;
        var radius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Md) * em);
        if (GetTemplateChild("PART_Popover") is Border panel) panel.CornerRadius = radius;
        if (GetTemplateChild("PART_PopoverShadow") is Border shadow) shadow.CornerRadius = radius;
        if (GetTemplateChild("PART_Items") is ItemsPresenter items) items.Margin = new Thickness(this.Resource<double>(ThemeKeys.Space.Xs) * em);
    }

    /// <summary>按组生成选项行：组名是一行不能选的标题，组里的选项缩进一档。</summary>
    void Rebuild()
    {
        var items = new List<NativeSelectItem>();
        choices.Clear();
        foreach (var (group, options) in NativeSelectGroups.Grouped(NativeSelectGroups.Options(Options?.Cast<object?>())))
        {
            if (group is not null) items.Add(new NativeSelectItem { Content = group, IsEnabled = false, Focusable = false, Row = NativeSelectRow.Group });
            foreach (var option in options)
            {
                var item = new NativeSelectItem { Content = option.Label, Tag = option.Value, Row = group is null ? NativeSelectRow.Option : NativeSelectRow.Grouped };
                items.Add(item);
                choices.Add((item, option));
            }
        }
        ItemsSource = items;
        SelectValue();
    }

    /// <summary>按 Value 选中对应的行；没有这个值时什么都不选（显示占位）。</summary>
    void SelectValue()
    {
        syncing = true;
        SelectedItem = choices.FirstOrDefault(choice => choice.Option.Value == Value).Item;
        syncing = false;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (!syncing && choices.FirstOrDefault(choice => choice.Item == SelectedItem).Option is { } option) SetCurrentValue(ValueProperty, option.Value);
        SetValue(ShowsPlaceholderKey, SelectedItem is null);
    }

    /// <summary>出错、只读、占位的显示状态；只读时别的选项不能选（平台的下拉框没有只读）。</summary>
    void Sync()
    {
        var readOnly = ReadOnly;
        SetValue(ShowsInvalidKey, IsInvalid ?? Field.Of(this)?.Invalid ?? false);
        SetValue(ShowsReadOnlyKey, readOnly);
        SetValue(ShowsPlaceholderKey, SelectedItem is null);
        foreach (var (item, option) in choices) item.IsEnabled = !option.IsDisabled && (!readOnly || option.Value == Value);
        if (readOnly && IsDropDownOpen) SetCurrentValue(IsDropDownOpenProperty, false);
    }

    /// <summary>只读时方向键、滚轮也换不了（平台的 ComboBox 关着时用它们换选中项）。</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (ReadOnly && e.Key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (ReadOnly && !IsDropDownOpen)
        {
            e.Handled = true;
            return;
        }
        base.OnPreviewMouseWheel(e);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NativeSelectPeer(this);

    /// <summary>读屏：组合框；没写名称时取所在字段的标签，说明与错误是帮助文字。</summary>
    sealed class NativeSelectPeer(NativeSelect owner) : ComboBoxAutomationPeer(owner)
    {
        protected override string GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        protected override string GetClassNameCore() => nameof(NativeSelect);

        protected override string GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText ?? "" : "";
    }
}

/// <summary>NativeSelect 列表里的一行（由 Options 生成；单独一个类型是为了内边距、圆角按字号算）。Tag 是选项的值。</summary>
public class NativeSelectItem : ComboBoxItem
{
    /// <summary>这一行的种类（样式触发器按它区分）。</summary>
    public static readonly DependencyProperty RowProperty = DependencyProperty.Register(nameof(Row), typeof(NativeSelectRow), typeof(NativeSelectItem));

    /// <summary>圆角（像素，由主题按字号算）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(NativeSelectItem),
        new FrameworkPropertyMetadata(default(CornerRadius), null, EmSize.CoerceCornerRadius));

    static NativeSelectItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NativeSelectItem), new FrameworkPropertyMetadata(typeof(NativeSelectItem)));
        EmSize.Register(typeof(NativeSelectItem), CornerRadiusProperty);
    }

    /// <summary>这一行的种类。</summary>
    public NativeSelectRow Row { get => (NativeSelectRow)GetValue(RowProperty); set => SetValue(RowProperty, value); }

    /// <summary>圆角。</summary>
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
}

/// <summary>NativeSelect 列表里一行的种类。</summary>
public enum NativeSelectRow
{
    /// <summary>没有组的选项。</summary>
    Option,

    /// <summary>组名：不能选的标题。</summary>
    Group,

    /// <summary>组里的选项：缩进一档。</summary>
    Grouped,
}
