using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;

namespace Rune.WinForms;

/// <summary>
/// 开关轨道里的内容（TS 的 checkedContent、uncheckedContent）：文字或图标。字符串与 <see cref="IconData"/> 都能直接赋值：
/// <code>
/// new SwitchControl { CheckedContent = "开", UncheckedContent = "关" };
/// new SwitchControl { CheckedContent = Lucide.Moon, UncheckedContent = Lucide.Sun };
/// </code>
/// </summary>
[TypeConverter(typeof(SwitchContentConverter))]
public sealed record SwitchContent
{
    /// <summary>文字内容。</summary>
    /// <param name="text">文字。</param>
    public SwitchContent(string text) => Text = text;

    /// <summary>图标内容。</summary>
    /// <param name="icon">图标。</param>
    public SwitchContent(IconData icon) => Icon = icon;

    /// <summary>文字；是图标时为 null。</summary>
    public string? Text { get; }

    /// <summary>图标；是文字时为 null。</summary>
    public IconData? Icon { get; }

    /// <summary>文字。</summary>
    /// <param name="text">文字；null 时没有内容。</param>
    public static implicit operator SwitchContent?(string? text) => text is null ? null : new(text);

    /// <summary>图标。</summary>
    /// <param name="icon">图标；null 时没有内容。</param>
    public static implicit operator SwitchContent?(IconData? icon) => icon is null ? null : new(icon);
}

/// <summary>设计器里：属性窗口写文字，生成的代码是 <c>new SwitchContent("开")</c>；图标显示它的名字。</summary>
public sealed class SwitchContentConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? (text.Length == 0 ? null : new SwitchContent(text)) : base.ConvertFrom(context, culture, value);

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is not SwitchContent content) return base.ConvertTo(context, culture, value, destinationType);
        if (destinationType == typeof(string)) return content.Text ?? content.Icon!.Name;
        if (destinationType != typeof(InstanceDescriptor)) return base.ConvertTo(context, culture, value, destinationType);
        return content.Icon is { } icon
            ? new InstanceDescriptor(typeof(SwitchContent).GetConstructor([typeof(IconData)]), new object[] { icon })
            : new InstanceDescriptor(typeof(SwitchContent).GetConstructor([typeof(string)]), new object[] { content.Text! });
    }
}
