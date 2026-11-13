using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 单选组（vue-tsx-pro 的 RadioGroup）：<see cref="Value"/> 是选中项的值（双向绑定，没有选中时为 null），组内每个 Radio 按自己的 Value 选中。
/// <code>
/// &lt;rune:RadioGroup Value="{Binding Shipping}" AutomationProperties.Name="配送方式"&gt;
///   &lt;rune:Radio Value="express"&gt;快递&lt;/rune:Radio&gt;
///   &lt;rune:Radio Value="pickup"&gt;自提&lt;/rune:Radio&gt;
/// &lt;/rune:RadioGroup&gt;
/// </code>
/// 同原生的 radio：整组一个 Tab 位置，停在选中项（没有选中时是第一项）；方向键在组内移动并选中（下、右下一个，上、左上一个，
/// 到头从另一端继续，跳过不可用的；从右到左时左右反过来）；只读时方向键不动。色调、字号给组内各项。
/// 排列、只读、出错、数据生成的项见 <see cref="ChoiceGroup"/>。
/// </summary>
public class RadioGroup : ChoiceGroup
{
    /// <summary>选中项的值（默认双向绑定）。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<RadioGroup, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>必填。</summary>
    public static readonly StyledProperty<bool> IsRequiredProperty = AvaloniaProperty.Register<RadioGroup, bool>(nameof(IsRequired));

    /// <summary>选中时的色调；主题默认 accent。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<RadioGroup, StatusTone>(nameof(Tone));

    /// <summary>字号档位；不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<RadioGroup, TextSize>(nameof(Size));

    /// <summary>选中项的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>必填。</summary>
    public bool IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>必填：自己写了的优先，其次所在字段的。</summary>
    internal bool Required => IsSet(IsRequiredProperty) ? IsRequired : Field.Of(this)?.IsRequired ?? false;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) Sync();
    }

    /// <summary>选中一个值（组内 Radio 选中时调用）。</summary>
    /// <param name="value">值。</param>
    public void Select(string value) => SetCurrentValue(ValueProperty, value);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new Radio();

    /// <inheritdoc />
    protected override void Prepare(ChoiceBase item, ListboxOption option)
    {
        ((Radio)item).Value = option.Value;
    }

    /// <summary>各项按组的值选中；Tab 停在选中项，没有选中（或选中项不可用）时停在第一个可用项。</summary>
    internal override void Sync()
    {
        var radios = Choices<Radio>().ToList();
        foreach (var radio in radios) radio.Refresh(radio.Value is not null && radio.Value == Value);
        var stop = radios.FirstOrDefault(radio => radio.IsChecked && radio.IsEffectivelyEnabled) ?? radios.FirstOrDefault(radio => radio.IsEffectivelyEnabled);
        foreach (var radio in radios) KeyboardNavigation.SetIsTabStop(radio, radio == stop);
    }

    /// <summary>方向键：在组内移动并选中。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not Radio { Group: { } group } current || group != this) return;
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
        if (ReadOnly) return;
        var radios = Choices<Radio>().Where(radio => radio.IsEffectivelyVisible && radio.IsEffectivelyEnabled).ToList();
        var index = radios.IndexOf(current);
        if (radios.Count == 0 || index < 0) return;
        var next = radios[(index + delta + radios.Count) % radios.Count];
        next.Focus(NavigationMethod.Directional);
        next.Toggle();
    }
}
