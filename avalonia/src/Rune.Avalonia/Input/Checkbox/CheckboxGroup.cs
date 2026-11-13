using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace Rune.Avalonia;

/// <summary>
/// 一组复选框（vue-tsx-pro 的 CheckboxGroup）：<see cref="Value"/> 是勾选项的值（双向绑定），组内每个 Checkbox 按自己的 Value 勾选。
/// <code>
/// &lt;rune:CheckboxGroup Value="{Binding Channels}" Orientation="Horizontal" AutomationProperties.Name="通知方式"&gt;
///   &lt;rune:Checkbox Value="email"&gt;邮件&lt;/rune:Checkbox&gt;
///   &lt;rune:Checkbox Value="sms"&gt;短信&lt;/rune:Checkbox&gt;
/// &lt;/rune:CheckboxGroup&gt;
/// &lt;rune:CheckboxGroup Value="{Binding Notify}" ItemsSource="{Binding ChannelOptions}" /&gt;
/// </code>
/// 勾选、取消时换一个新的列表（追加在末尾或去掉），绑定方收到的是新值。排列、只读、出错、数据生成的项见 <see cref="ChoiceGroup"/>。
/// </summary>
public class CheckboxGroup : ChoiceGroup
{
    /// <summary>勾选项的值（默认双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> ValueProperty =
        AvaloniaProperty.Register<CheckboxGroup, IReadOnlyList<string>>(nameof(Value), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>勾选项的值。</summary>
    public IReadOnlyList<string> Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) Sync();
    }

    /// <summary>勾选或取消一个值（组内 Checkbox 切换时调用）。</summary>
    /// <param name="value">值。</param>
    public void Toggle(string value) =>
        SetCurrentValue(ValueProperty, Value.Contains(value) ? [.. Value.Where(item => item != value)] : [.. Value, value]);

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new Checkbox();

    /// <inheritdoc />
    protected override void Prepare(ChoiceBase item, ListboxOption option)
    {
        ((Checkbox)item).Value = option.Value;
    }

    /// <inheritdoc />
    internal override void Sync()
    {
        foreach (var checkbox in Choices<Checkbox>()) Sync(checkbox);
    }

    /// <summary>一项按组的值勾选。</summary>
    internal void Sync(Checkbox checkbox)
    {
        if (checkbox.Value is { } value) checkbox.SetCurrentValue(ChoiceBase.IsCheckedProperty, Value.Contains(value));
        checkbox.Refresh();
    }
}
