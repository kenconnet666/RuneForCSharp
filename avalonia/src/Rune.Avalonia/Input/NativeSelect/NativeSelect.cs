using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;

namespace Rune.Avalonia;

/// <summary>
/// 原生下拉框（vue-tsx-pro 的 NativeSelect）：平台自己的 ComboBox（键盘、读屏、滚轮切换都是平台的行为），外壳同输入框（外观、尺寸、出错、只读、相连）。
/// <code>
/// &lt;rune:Field Label="收货城市" IsRequired="True"&gt;
///   &lt;rune:NativeSelect Options="{Binding Cities}" Value="{Binding City}" PlaceholderText="选择城市" Icon="{x:Static rune:Lucide.MapPin}" /&gt;
/// &lt;/rune:Field&gt;
/// </code>
/// 和 <see cref="Select"/> 怎么选：选项要图标、说明、搜索、多选、远程加载用 Select；选项只是一串文字、想要平台下拉框的行为用它。
/// <list type="bullet">
/// <item><see cref="Options"/> 是 ListboxOption 或字符串（值与文字相同）；带 Group 的按组排，组名是一行不能选的标题（没有组的在前）。选项的说明、图标不显示。</item>
/// <item>值是选项的 Value（<see cref="Value"/> 默认双向绑定），没选时是 null；占位文字是平台的 PlaceholderText。不要直接设 ItemsSource：选项由 Options 生成。</item>
/// <item>只读：别的选项不能选（同上游，平台的下拉框没有只读），读屏念只读；值照常。</item>
/// </list>
/// </summary>
[PseudoClasses(":soft", ":invalid", ":readonly")]
public class NativeSelect : ComboBox, IFieldControl
{
    /// <summary>选项：ListboxOption 或字符串。</summary>
    public static readonly StyledProperty<IEnumerable?> OptionsProperty = AvaloniaProperty.Register<NativeSelect, IEnumerable?>(nameof(Options));

    /// <summary>选中的值（默认双向绑定）；没选时是 null。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<NativeSelect, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>样式，默认 surface。</summary>
    public static readonly StyledProperty<ControlVariant> VariantProperty = AvaloniaProperty.Register<NativeSelect, ControlVariant>(nameof(Variant));

    /// <summary>字号档位；不写时跟着外面。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<NativeSelect, TextSize>(nameof(Size));

    /// <summary>圆角，默认 md。</summary>
    public static readonly StyledProperty<Radius> RadiusProperty = AvaloniaProperty.Register<NativeSelect, Radius>(nameof(Radius));

    /// <summary>前置图标。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<NativeSelect, IconData?>(nameof(Icon));

    /// <summary>校验未通过；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<NativeSelect, bool?>(nameof(IsInvalid));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<NativeSelect, bool?>(nameof(IsRequired));

    /// <summary>只读：能聚焦、能看，换不了；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<NativeSelect, bool?>(nameof(IsReadOnly));

    readonly TemplateResources resources;
    readonly List<(ComboBoxItem Item, ListboxOption Option)> choices = [];
    // 改选中项时不回写 Value（是 Value 变了引起的）
    bool syncing;

    // 放进 ControlGroup 时相邻一侧不要圆角
    static NativeSelect() => Attached.SquareCornersOf<NativeSelect>();

    /// <summary>新建：外壳的字号、圆角按档位解析成主题资源。</summary>
    public NativeSelect()
    {
        resources = new TemplateResources(this);
        Resolved.Bind(this, Resolved.RadiusProperty, Radius.Md.ResourceKey);
    }

    /// <summary>平台 ComboBox 的样式键：主题按 NativeSelect 找（不然套上的是平台的 ComboBox 模板）。</summary>
    protected override Type StyleKeyOverride => typeof(NativeSelect);

    /// <summary>选项。</summary>
    public IEnumerable? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }

    /// <summary>选中的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>样式。</summary>
    public ControlVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>圆角。</summary>
    public Radius Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <summary>前置图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    bool Invalid => IsInvalid ?? Field.Of(this)?.Invalid ?? false;

    bool ReadOnly => IsReadOnly ?? Field.Of(this)?.IsReadOnly ?? false;

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OptionsProperty) Rebuild();
        else if (change.Property == ValueProperty) Select();
        else if (change.Property == SelectedItemProperty && !syncing && SelectedItem is ComboBoxItem { Tag: string value })
            SetCurrentValue(ValueProperty, value);
        else if (change.Property == SizeProperty)
        {
            ControlHeight.Apply(this, Size);
            if (Size.Pixels is { } pixels) resources.Set(FontSizeProperty, pixels);
            else resources.Bind(FontSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == RadiusProperty) Resolved.Bind(this, Resolved.RadiusProperty, (Radius.Value is null ? Radius.Md : Radius).ResourceKey);
        else if (change.Property == VariantProperty) PseudoClasses.Set(":soft", Variant == ControlVariant.Soft);
        else if (change.Property == IsInvalidProperty || change.Property == IsReadOnlyProperty || change.Property == IsRequiredProperty || change.Property == Field.OwnerProperty)
            Sync();
        else if (change.Property == IsDropDownOpenProperty && IsDropDownOpen && ReadOnly) SetCurrentValue(IsDropDownOpenProperty, false);
    }

    /// <summary>按组生成选项行：组名是一行不能选的标题，选项缩进在它下面。</summary>
    void Rebuild()
    {
        var items = new List<ComboBoxItem>();
        choices.Clear();
        foreach (var (group, options) in NativeSelectGroups.Grouped(NativeSelectGroups.Options(Options?.Cast<object?>())))
        {
            if (group is not null)
            {
                var header = new ComboBoxItem { Content = group, IsEnabled = false, Focusable = false };
                header.Classes.Add("group");
                AutomationProperties.SetAccessibilityView(header, AccessibilityView.Raw);
                items.Add(header);
            }
            foreach (var option in options)
            {
                var item = new ComboBoxItem { Content = option.Label, Tag = option.Value };
                if (group is not null) item.Classes.Add("grouped");
                items.Add(item);
                choices.Add((item, option));
            }
        }
        ItemsSource = items;
        Select();
        Sync();
    }

    /// <summary>按 Value 选中对应的行；没有这个值时什么都不选（显示占位）。</summary>
    void Select()
    {
        syncing = true;
        SelectedItem = choices.FirstOrDefault(choice => choice.Option.Value == Value).Item;
        syncing = false;
        Sync();
    }

    /// <summary>出错、只读的伪类；只读时别的选项不能选（平台的下拉框没有只读）。</summary>
    void Sync()
    {
        PseudoClasses.Set(":invalid", Invalid);
        PseudoClasses.Set(":readonly", ReadOnly);
        foreach (var (item, option) in choices)
            item.IsEnabled = !option.IsDisabled && (!ReadOnly || option.Value == Value);
    }

    /// <summary>只读时方向键、滚轮也换不了（平台的 ComboBox 关着时用它们换选中项）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (ReadOnly && e.Key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (ReadOnly)
        {
            e.Handled = true;
            return;
        }
        base.OnPointerWheelChanged(e);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NativeSelectPeer(this);

    /// <summary>读屏：组合框；没写名称时取所在字段的标签，说明与错误是帮助文字。</summary>
    sealed class NativeSelectPeer(NativeSelect owner) : ComboBoxAutomationPeer(owner)
    {
        protected override string? GetHelpTextCore() => Field.DescribedBy(owner, base.GetHelpTextCore());

        protected override string GetClassNameCore() => nameof(NativeSelect);

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : null;
    }
}
