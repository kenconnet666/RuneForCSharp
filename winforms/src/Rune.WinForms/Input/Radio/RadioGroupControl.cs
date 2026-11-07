using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 单选组（vue-tsx-pro 的 RadioGroup）：<see cref="Value"/> 是选中项的值（没有选中时为 null），组内每个 <see cref="RadioControl"/> 按自己的 Value 选中。
/// <code>
/// var shipping = new RadioGroupControl { Value = "express", AccessibleName = "配送方式" };
/// shipping.Controls.AddRange([new RadioControl { Value = "express", Text = "快递" }, new RadioControl { Value = "pickup", Text = "自提" }]);
/// var period = new RadioGroupControl { ItemsSource = new[] { "按日", "按周", "按月" }, Orientation = Orientation.Horizontal };
/// </code>
/// 同原生的 radio：整组一个 Tab 位置，停在选中项（没有选中时是第一项）；方向键在组内移动并选中（下、右下一个，上、左上一个，
/// 到头从另一端继续，跳过不可用的；从右到左时左右反过来）；只读时方向键不动。色调、字号给组内各项。
/// 排列、只读、出错、数据生成的项见 <see cref="ChoiceGroupControl"/>。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
[Description("单选组：Value 是选中项的值。")]
public class RadioGroupControl : ChoiceGroupControl
{
    /// <summary>选中项的值变了；数据绑定认它。</summary>
    [Category("Property Changed")]
    [Description("Value 变了。")]
    public event EventHandler? ValueChanged;

    /// <summary>选中项的值；没有选中时为 null。</summary>
    [Bindable(true)]
    [Category("Data")]
    [Description("选中项的值；没有选中时为空。")]
    [DefaultValue(null)]
    public string? Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Sync();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>必填。</summary>
    [Category("Behavior")]
    [Description("必填（外观不变，供表单用）。")]
    [DefaultValue(false)]
    public bool IsRequired { get => required ?? false; set => required = value; }

    bool? required;

    /// <summary>必填：自己写了的优先，其次所在字段的。</summary>
    internal bool Required => required ?? FieldControl.Of(this)?.IsRequired ?? false;

    /// <summary>选中时的色调；不写时取作用域的默认值（<see cref="ChoiceDefaults"/>），再没有时 accent。</summary>
    [Category("Appearance")]
    [Description("选中时的色调：neutral、accent（默认）、info、success、warning、danger。")]
    public StatusTone Tone { get => tone.Or(ProviderControl.Of(this)?.ResolveDefaults<ChoiceDefaults>()?.Tone, StatusTone.Accent); set { tone = value; Sync(); } }

    /// <summary>字号档位；不写时取作用域的默认值，再没有时跟随字体。</summary>
    [Category("Appearance")]
    [Description("字号：xs–xl 或像素数；各项随之缩放，空着时跟随字体。")]
    public TextSize TextSize { get => size.Or(ProviderControl.Of(this)?.ResolveDefaults<ChoiceDefaults>()?.Size, default); set { size = value; Sync(); } }

    StatusTone tone;
    TextSize size;

    bool ShouldSerializeTone() => tone.Value is not null;

    void ResetTone() => Tone = default;

    bool ShouldSerializeTextSize() => size.Value is not null;

    void ResetTextSize() => TextSize = default;

    /// <inheritdoc />
    private protected override Control CreateItem(ListboxOption option) => new RadioControl { Value = option.Value, Description = option.Description };

    /// <summary>各项按组的值选中；Tab 停在选中项，没有选中（或选中项不可用）时停在第一个可用项。</summary>
    internal override void Sync()
    {
        var radios = Choices<RadioControl>();
        foreach (var radio in radios)
        {
            radio.Element.Group = this;
            radio.Element.IsChecked = radio.Value is not null && radio.Value == Value;
            radio.Element.Relayout();
            radio.PerformLayout();
            radio.Invalidate();
        }
        var stop = radios.FirstOrDefault(radio => radio.IsChecked && radio.Enabled) ?? radios.FirstOrDefault(radio => radio.Enabled);
        foreach (var radio in radios) radio.TabStop = radio == stop;
    }

    /// <summary>从一项往前或往后移到下一个可用项并选中（方向键）；只读时不动。</summary>
    /// <param name="from">当前项。</param>
    /// <param name="delta">1 往后，-1 往前。</param>
    internal void Step(RadioControl from, int delta)
    {
        if (ReadOnly) return;
        var radios = Choices<RadioControl>().Where(radio => radio.Visible && radio.Enabled).ToList();
        var index = radios.IndexOf(from);
        if (radios.Count == 0 || index < 0) return;
        var next = radios[(index + delta + radios.Count) % radios.Count];
        next.Focus();
        next.Toggle();
    }
}
