using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf;

/// <summary>小表单对话框里一个字段的控件（上游 FormDialogField 的 type）。</summary>
public enum FormPromptFieldType
{
    /// <summary>单行文字（默认）。</summary>
    Text,

    /// <summary>多行文字（3 行）。</summary>
    Textarea,

    /// <summary>下拉选择（Options；Multiple 时多选）。</summary>
    Select,

    /// <summary>日期（日期选择器；值是 ISO 日期 2026-10-20，范围是 Min、Max）。</summary>
    Date,
}

/// <summary>小表单对话框里的一个字段（vue-tsx-pro 的 FormDialogField）。</summary>
/// <param name="Name">字段名：结果里的 key。</param>
/// <param name="Label">标签。</param>
public sealed record FormPromptField(string Name, string Label)
{
    /// <summary>控件，默认单行文字。</summary>
    public FormPromptFieldType Type { get; init; }

    /// <summary>必填。</summary>
    public bool Required { get; init; }

    /// <summary>下拉的选项（Type 为 Select）；Description 是选项的第二行，Group 相同的归在一组标题下。</summary>
    public IReadOnlyList<ListboxOption> Options { get; init; } = [];

    /// <summary>多选（Type 为 Select）：值是选中的值的列表，不经过 Validate。</summary>
    public bool Multiple { get; init; }

    /// <summary>初始值：单值字段是字符串，多选是字符串列表。</summary>
    public object? DefaultValue { get; init; }

    /// <summary>占位文字。</summary>
    public string? Placeholder { get; init; }

    /// <summary>标签下面的说明。</summary>
    public string? Description { get; init; }

    /// <summary>最多几个字（单行、多行文字）。</summary>
    public int? MaxLength { get; init; }

    /// <summary>最早可选的日期（Type 为 Date，ISO 日期）。</summary>
    public string? Min { get; init; }

    /// <summary>最晚可选的日期（Type 为 Date，ISO 日期）。</summary>
    public string? Max { get; init; }

    /// <summary>校验：返回错误说明时不提交；第二个参数是全部单值字段的值。</summary>
    public Func<string, IReadOnlyDictionary<string, string>, string?>? Validate { get; init; }
}

/// <summary>小表单对话框的选项（vue-tsx-pro 的 FormDialogOptions）。</summary>
/// <param name="Title">标题。</param>
/// <param name="Fields">字段。</param>
public sealed record FormPromptOptions(string Title, IReadOnlyList<FormPromptField> Fields)
{
    /// <summary>标题下面的说明。</summary>
    public string? Description { get; init; }

    /// <summary>确认按钮的文字；不写时取语言包（确定）。</summary>
    public string? ConfirmText { get; init; }

    /// <summary>确认按钮的色调，默认 accent；驳回、终止用 danger。</summary>
    public StatusTone Tone { get; init; } = StatusTone.Accent;

    /// <summary>对话框的宽度档位，默认 sm。</summary>
    public DialogSize Size { get; init; } = DialogSize.Sm;

    /// <summary>提交：等它完成再关；抛异常时对话框留着（调用方提示原因）。值同 PromptAsync 的结果。</summary>
    public Func<IReadOnlyDictionary<string, object>, Task>? OnSubmit { get; init; }
}

/// <summary>
/// 命令式的小表单对话框（vue-tsx-pro 的 useFormDialog）：一个操作前要填一两个字段（退回的原因、改派给谁），不值得为它单独写一个对话框。
/// 必填的没填、Validate 返回说明时错误落在字段下面、不提交；值是去掉首尾空白的字符串，多选的是字符串列表。在单行输入框里按回车也是确认。
/// </summary>
static class FormPrompt
{
    public static async Task<IReadOnlyDictionary<string, object>?> ShowAsync(FrameworkElement owner, FormPromptOptions options)
    {
        var lang = Provider.GetLang(owner);
        IReadOnlyDictionary<string, object>? result = null;
        var rows = options.Fields.Select(field => (Spec: field, Field: new Field { Label = field.Label, Description = field.Description, IsRequired = field.Required, Content = Control(field) })).ToList();
        var column = new Flex { Direction = FlexDirection.Column, Gap = Space.Md };
        foreach (var row in rows) column.Children.Add(row.Field);
        var dialog = new Dialog
        {
            Title = options.Title,
            Description = options.Description,
            ConfirmText = options.ConfirmText,
            Tone = options.Tone,
            Size = options.Size,
            Content = column,
        };
        dialog.Confirm = async () =>
        {
            var values = rows.ToDictionary(row => row.Spec.Name, row => Read(row.Field.Content!));
            var strings = values.Where(entry => entry.Value is string).ToDictionary(entry => entry.Key, entry => (string)entry.Value);
            var valid = true;
            foreach (var (spec, field) in rows)
            {
                var value = values[spec.Name];
                field.Error = spec.Required && (value is string text ? text.Length == 0 : ((IReadOnlyList<string>)value).Count == 0)
                    ? lang.FormRequired
                    : value is string single ? spec.Validate?.Invoke(single, strings) : null;
                valid &= field.Error is null;
            }
            if (!valid) return false;
            if (options.OnSubmit is { } submit) await submit(values);
            result = values;
            return true;
        };
        // 同上游的隐式提交：单行输入框里按回车就是点确认
        column.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Handled || e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || e.OriginalSource is not TextBox { AcceptsReturn: false }) return;
            e.Handled = true;
            _ = dialog.RunConfirm();
        }));
        await dialog.ShowAsync(owner);
        return result;
    }

    static UIElement Control(FormPromptField field) => field.Type switch
    {
        FormPromptFieldType.Date => new DatePicker { Value = field.DefaultValue as string, Min = field.Min, Max = field.Max },
        FormPromptFieldType.Textarea => new Textarea { Rows = 3, Text = field.DefaultValue as string ?? "", PlaceholderText = field.Placeholder, MaxLength = field.MaxLength ?? 0 },
        FormPromptFieldType.Select when field.Multiple => new Select
        {
            ItemsSource = field.Options,
            SelectionMode = SelectionMode.Multiple,
            Values = field.DefaultValue as IReadOnlyList<string> ?? [],
            Placeholder = field.Placeholder,
        },
        FormPromptFieldType.Select => new Select { ItemsSource = field.Options, Value = field.DefaultValue as string, Placeholder = field.Placeholder },
        _ => new TextField { Value = field.DefaultValue as string ?? "", PlaceholderText = field.Placeholder, MaxLength = field.MaxLength ?? 0 },
    };

    /// <summary>控件的值：文字去掉首尾空白，单选没选是空字符串，多选是选中的值的列表。</summary>
    static object Read(object control) => control switch
    {
        Select select when select.SelectionMode == SelectionMode.Multiple => select.Values ?? [],
        Select select => select.Value ?? "",
        DatePicker date => date.Value ?? "",
        TextField text => text.Value.Trim(),
        TextBox box => (box.Text ?? "").Trim(),
        _ => "",
    };
}
