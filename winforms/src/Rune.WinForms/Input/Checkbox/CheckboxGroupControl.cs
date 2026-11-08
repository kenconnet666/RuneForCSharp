using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 一组复选框（vue-tsx-pro 的 CheckboxGroup）：<see cref="Value"/> 是勾选项的值，组内每个 <see cref="CheckboxControl"/> 按自己的 Value 勾选。
/// <code>
/// var channels = new CheckboxGroupControl { Value = ["email"], Orientation = Orientation.Horizontal, AccessibleName = "通知方式" };
/// channels.Controls.AddRange([new CheckboxControl { Value = "email", Text = "邮件" }, new CheckboxControl { Value = "sms", Text = "短信" }]);
/// var notify = new CheckboxGroupControl { ItemsSource = new[] { new ListboxOption("email", "邮件") { Description = "每天汇总一封" } } };
/// </code>
/// 勾选、取消时换一个新的列表（追加在末尾或去掉）并发 <see cref="ValueChanged"/>。排列、只读、出错、数据生成的项见 <see cref="ChoiceGroupControl"/>。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[Description("一组复选框：Value 是勾选项的值。")]
public class CheckboxGroupControl : ChoiceGroupControl
{
    /// <summary>勾选项的值变了；数据绑定认它。</summary>
    [Category("Property Changed")]
    [Description("Value 变了。")]
    public event EventHandler? ValueChanged;

    /// <summary>勾选项的值。</summary>
    [Bindable(true)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Value
    {
        get;
        set
        {
            if (field.SequenceEqual(value)) return;
            field = value;
            Sync();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    } = [];

    /// <summary>勾选或取消一个值（组内 Checkbox 切换时调用）。</summary>
    /// <param name="value">值。</param>
    public void Toggle(string value) => Value = Value.Contains(value) ? [.. Value.Where(item => item != value)] : [.. Value, value];

    /// <inheritdoc />
    private protected override Control CreateItem(ListboxOption option) => new CheckboxControl { Value = option.Value, Description = option.Description };

    /// <inheritdoc />
    private protected override void AssignValue(Control control, string value) => ((CheckboxControl)control).Value = value;

    /// <summary>各项按组的值勾选，只读、出错随组。</summary>
    internal override void Sync()
    {
        foreach (var checkbox in Choices<CheckboxControl>())
        {
            checkbox.Element.Group = this;
            if (checkbox.Value is { } value) checkbox.IsChecked = Value.Contains(value);
            checkbox.Element.Relayout();
            checkbox.Invalidate();
        }
    }
}
