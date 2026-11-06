using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Reflection;

namespace Rune.WinForms;

/// <summary>
/// 设计器里选图标：属性窗口按名字列出 Lucide 的图标（下拉，也能手写），生成的代码是 <c>Lucide.Search</c>；
/// 自定义的图标生成 <c>new IconData(name, path, fillPath)</c>。改名留下的旧名（Obsolete）不列出。
/// </summary>
public sealed class IconDataConverter : TypeConverter
{
    static Dictionary<string, PropertyInfo> Icons => field ??= typeof(Lucide).GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(property => !property.IsDefined(typeof(ObsoleteAttribute)))
        .ToDictionary(property => property.Name);

    static Dictionary<IconData, string> Names => field ??= Icons.ToDictionary(pair => (IconData)pair.Value.GetValue(null)!, pair => pair.Key);

    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text) return base.ConvertFrom(context, culture, value);
        text = text.Trim();
        if (text.Length == 0) return null;
        return Icons.TryGetValue(text, out var property)
            ? property.GetValue(null)
            : throw new ArgumentException($"Lucide 里没有叫“{text}”的图标；名字同 Lucide 的 JS 导出名（PascalCase），如 Search、CircleX。", nameof(value));
    }

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is not IconData data) return base.ConvertTo(context, culture, value, destinationType);
        var lucideName = Names.GetValueOrDefault(data);
        if (destinationType == typeof(string)) return lucideName ?? data.Name;
        if (destinationType == typeof(InstanceDescriptor))
        {
            return lucideName is not null
                ? new InstanceDescriptor(Icons[lucideName], null)
                : new InstanceDescriptor(typeof(IconData).GetConstructor([typeof(string), typeof(string), typeof(string)]), new object?[] { data.Name, data.Path, data.FillPath });
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }

    /// <inheritdoc />
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    /// <inheritdoc />
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    /// <inheritdoc />
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) =>
        new(Icons.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value.GetValue(null)).ToArray());
}
