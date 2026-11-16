using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 颜色输入（vue-tsx-pro 的 ColorField）：前面是色块（点开取色器 <see cref="ColorPicker"/>），后面可以直接输入颜色值。主题编辑器、品牌色设置这类地方用。
/// <code>
/// &lt;rune:Field Label="品牌色"&gt;&lt;rune:ColorField Value="{Binding Brand}" Presets="{Binding BrandColors}" /&gt;&lt;/rune:Field&gt;
/// </code>
/// <list type="bullet">
/// <item>值是 #rrggbb（有透明度时 #rrggbbaa）：输入 rgb()、颜色名也行，失焦或回车时规范成 #hex 写回；写不成颜色时外壳标为无效，值不变，失焦后恢复成原来的值。</item>
/// <item>色块是按钮：点开的弹出层里是取色器（色板、色相、不透明度、预设与最近使用），改动马上写回；Esc 或点外面关上，焦点回到色块。只读、不可用时不能打开。</item>
/// <item>外壳同 TextField（主题 Rune.TextInput），接入 Field；文字是等宽字体（#hex 各位对齐，改一位时不跳动）。</item>
/// </list>
/// 部件：<see cref="ColorFieldParts"/>。
/// </summary>
public class ColorField : TextInputBase
{
    /// <summary>颜色值（#rrggbb 或 #rrggbbaa，双向绑定），默认 #000000。</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<ColorField, string>(nameof(Value), "#000000", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>取色器里的预设颜色。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PresetsProperty = ColorPicker.PresetsProperty.AddOwner<ColorField>();

    /// <summary>取色器里有不透明度滑块，默认 true。</summary>
    public static readonly StyledProperty<bool> AlphaProperty = ColorPicker.AlphaProperty.AddOwner<ColorField>();

    readonly SwatchButton swatch = new() { Name = ColorFieldParts.Swatch };
    readonly ColorPicker picker = new();
    readonly Popover popover = new() { Name = ColorFieldParts.Popover, Placement = FloatingPlacement.BottomStart };
    bool syncing;

    /// <summary>新建：色块（弹出层的触发元素）与取色器。</summary>
    public ColorField()
    {
        popover.Trigger = swatch;
        popover.Content = picker;
        picker.PropertyChanged += (_, e) =>
        {
            if (!syncing && e.Property == ColorPicker.ValueProperty) SetCurrentValue(ValueProperty, picker.Value);
        };
        Text = Value;
    }

    /// <summary>颜色值。</summary>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>取色器里的预设颜色。</summary>
    public IReadOnlyList<string>? Presets { get => GetValue(PresetsProperty); set => SetValue(PresetsProperty, value); }

    /// <summary>取色器里有不透明度滑块。</summary>
    public bool Alpha { get => GetValue(AlphaProperty); set => SetValue(AlphaProperty, value); }

    /// <summary>色块（测试用）。</summary>
    internal Control SwatchPart => swatch;

    /// <summary>弹出层（测试用）。</summary>
    internal Popover PopoverPart => popover;

    /// <summary>正在输入的文字写不成颜色（外壳标为无效）。</summary>
    internal bool DraftInvalid => ColorMath.ParseColor(Text ?? "") is null;

    /// <summary>色块（弹出层的触发元素）放进模板。</summary>
    protected override void OnTemplateParts(INameScope scope)
    {
        if (scope.Find<ContentPresenter>(ColorFieldParts.SwatchHost) is { } host) host.Content = popover;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ValueProperty) Text = Value;
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty) swatch.Width = swatch.Height = 1.25 * FontSize;
    }

    /// <inheritdoc />
    protected override bool SyncsOn(AvaloniaProperty property) =>
        property == ValueProperty || property == PresetsProperty || property == AlphaProperty || property == Provider.LangProperty;

    /// <summary>值 → 色块、取色器；写不成颜色的输入也标为无效。</summary>
    protected override void Sync()
    {
        base.Sync();
        syncing = true;
        swatch.Fill = ColorMath.ToHex(Value) is { } hex ? new SolidColorBrush(Color.Parse(hex)) : null;
        swatch.IsEnabled = IsEffectivelyEnabled && !ReadOnly;
        AutomationProperties.SetName(swatch, Provider.GetLang(this).ColorPick);
        picker.Value = Value;
        picker.Presets = Presets;
        picker.Alpha = Alpha;
        syncing = false;
        PseudoClasses.Set(":invalid", Invalid || DraftInvalid);
        swatch.Width = swatch.Height = 1.25 * FontSize;
    }

    /// <summary>写不成颜色时不写回，文字恢复成原来的值。</summary>
    void Commit()
    {
        if (ColorMath.ToHex(Text ?? "") is { } hex) SetCurrentValue(ValueProperty, hex);
        Text = Value;
        Sync();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Commit();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Commit();
        base.OnKeyDown(e);
    }
}

/// <summary>ColorField 的部件名。</summary>
public static class ColorFieldParts
{
    /// <summary>色块（打开取色器的按钮，TS 的 propsSwatch）：1.25em、sm 圆角，棋盘格垫底。</summary>
    public const string Swatch = "PART_Swatch";

    /// <summary>模板里放色块（弹出层的触发元素）的位置。</summary>
    public const string SwatchHost = "PART_SwatchHost";

    /// <summary>弹出层（TS 的 propsPopover），里面是 ColorPicker。</summary>
    public const string Popover = "PART_Popover";
}
