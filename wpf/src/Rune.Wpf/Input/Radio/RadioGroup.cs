using System.Windows;
using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>
/// 单选组（vue-tsx-pro 的 RadioGroup）：<see cref="Value"/> 是选中项的值（双向绑定，没有选中时为 null），组内每个 Radio 按自己的 Value 选中。
/// <code>
/// &lt;rune:RadioGroup Value="{Binding Shipping}" AutomationProperties.Name="配送方式"&gt;
///   &lt;rune:Radio Value="express"&gt;快递&lt;/rune:Radio&gt;
///   &lt;rune:Radio Value="pickup"&gt;自提&lt;/rune:Radio&gt;
/// &lt;/rune:RadioGroup&gt;
/// </code>
/// 同原生的 radio：整组一个 Tab 位置，停在选中项（没有选中时是第一项）；方向键在组内移动并选中（下、右下一个，上、左上一个，
/// 到头从另一端继续，跳过不可用的；从右到左时左右反过来）；只读时方向键不动。色调给组内各项，字号档位是组的字号、各项继承。
/// 排列、只读、出错、数据生成的项见 <see cref="ChoiceGroup"/>。
/// </summary>
public class RadioGroup : ChoiceGroup
{
    /// <summary>选中项的值（默认双向绑定）。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(RadioGroup), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((RadioGroup)d).Sync()));

    /// <summary>必填。</summary>
    public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(
        nameof(IsRequired), typeof(bool), typeof(RadioGroup), new PropertyMetadata(false));

    /// <summary>选中时的色调；主题默认 accent。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(RadioGroup), new PropertyMetadata(default(StatusTone)));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(RadioGroup), new FrameworkPropertyMetadata(default(TextSize), OnSizeChanged));

    static RadioGroup() => DefaultStyleKeyProperty.OverrideMetadata(typeof(RadioGroup), new FrameworkPropertyMetadata(typeof(RadioGroup)));

    /// <summary>选中项的值。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>档位的字号在主题样式的触发器里；写成像素数时是本地值。</summary>
    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (((TextSize)e.NewValue).Pixels is { } pixels) d.SetValue(FontSizeProperty, pixels);
        else if (((TextSize)e.OldValue).Pixels is not null) d.ClearValue(FontSizeProperty);
    }

    /// <summary>选中一个值（组内 Radio 选中时调用）。</summary>
    /// <param name="value">值。</param>
    public void Select(string value) => SetCurrentValue(ValueProperty, value);

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride() => new Radio();

    /// <inheritdoc />
    protected override void Prepare(ChoiceBase item, ListboxOption option)
    {
        var radio = (Radio)item;
        radio.Value = option.Value;
        radio.Description = option.Description;
    }

    /// <summary>各项按组的值选中；Tab 停在选中项，没有选中（或选中项不可用）时停在第一个可用项。</summary>
    internal override void Sync()
    {
        var radios = Choices<Radio>();
        foreach (var radio in radios)
        {
            radio.SetCurrentValue(ChoiceBase.IsCheckedProperty, radio.Value is not null && radio.Value == Value);
            radio.Update();
        }
        var stop = radios.FirstOrDefault(radio => radio.IsChecked && radio.IsEnabled) ?? radios.FirstOrDefault(radio => radio.IsEnabled);
        foreach (var radio in radios) KeyboardNavigation.SetIsTabStop(radio, radio == stop);
    }

    /// <summary>方向键：在组内移动并选中（拦下 WPF 默认的方向键导航）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.OriginalSource is not Radio { Group: { } group } current || group != this) return;
        var rtl = FlowDirection == FlowDirection.RightToLeft;
        int? step = e.Key switch
        {
            Key.Down => 1,
            Key.Up => -1,
            Key.Right => rtl ? -1 : 1,
            Key.Left => rtl ? 1 : -1,
            _ => null,
        };
        if (step is not { } delta) return;
        e.Handled = true;
        if (IsReadOnly) return;
        var radios = Choices<Radio>().Where(radio => radio.IsVisible && radio.IsEnabled).ToList();
        var index = radios.IndexOf(current);
        if (radios.Count == 0 || index < 0) return;
        var next = radios[(index + delta + radios.Count) % radios.Count];
        next.Focus();
        next.Toggle();
    }
}
