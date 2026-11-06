using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Rune.Wpf;

/// <summary>
/// 档位类型（DESIGN §3"档位类型"）：字符串包装，XAML 里写法同 vue-tsx-pro（<c>Tone="danger"</c>），使用方能扩展新取值。
/// WPF 的 XAML（属性、Setter、Trigger 的 Value）经 <see cref="VariantConverter{T}"/> 转换，触发器按值相等匹配；<see cref="Known"/> 是库给出的常用取值。
/// </summary>
/// <typeparam name="TSelf">档位类型自己。</typeparam>
public interface IVariant<TSelf> where TSelf : struct, IVariant<TSelf>
{
    /// <summary>取值（vue-tsx-pro 里的写法，如 danger）；default 时为 null，按组件的默认值处理。</summary>
    string Value { get; }

    /// <summary>从字符串建一个取值（XAML 用它）。</summary>
    /// <param name="value">取值。</param>
    static abstract TSelf Parse(string value);

    /// <summary>库给出的常用取值（设计器、补全列表里列出）。</summary>
    static abstract IReadOnlyList<TSelf> Known { get; }
}

/// <summary>档位类型的 TypeConverter：字符串互转，常用取值作为下拉选项（WinForms 设计器、VS 的 XAML 补全会读）。</summary>
/// <typeparam name="T">档位类型。</typeparam>
public sealed class VariantConverter<T> : TypeConverter where T : struct, IVariant<T>
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? T.Parse(text) : base.ConvertFrom(context, culture, value);

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is T variant ? variant.Value : base.ConvertTo(context, culture, value, destinationType);

    /// <inheritdoc />
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    /// <inheritdoc />
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    /// <inheritdoc />
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(T.Known.ToArray());
}

/// <summary>档位类型共用的小工具。</summary>
static class Variants
{
    /// <summary>写错的取值不会报错，只是没有样式：Debug 构建里遇到不在常用取值里的，记一条输出方便查。</summary>
    [Conditional("DEBUG")]
    public static void WarnIfUnknown<T>(T value) where T : struct, IVariant<T>
    {
        if (value.Value is not null && !T.Known.Contains(value))
            Debug.WriteLine($"Rune.Wpf：{typeof(T).Name} 的取值 \"{value.Value}\" 不在常用取值里；是使用方扩展的就忽略，写错了的话不会有样式");
    }
}
