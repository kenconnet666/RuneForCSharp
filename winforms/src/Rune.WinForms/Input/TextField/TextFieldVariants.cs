using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>文本框的输入类型（TS 的 input type）：桌面上只有 password 不同（圆点、显示密码的按钮）。</summary>
/// <param name="Value">取值（text、password、search、email、url、tel）。</param>
[TypeConverter(typeof(VariantConverter<TextFieldType>))]
public readonly record struct TextFieldType(string Value) : IVariant<TextFieldType>
{
    /// <summary>文字（默认）。</summary>
    public static readonly TextFieldType Text = new("text");

    /// <summary>密码：字符显示成圆点，带显示密码的按钮。</summary>
    public static readonly TextFieldType Password = new("password");

    /// <summary>搜索。</summary>
    public static readonly TextFieldType Search = new("search");

    /// <summary>邮箱。</summary>
    public static readonly TextFieldType Email = new("email");

    /// <summary>网址。</summary>
    public static readonly TextFieldType Url = new("url");

    /// <summary>电话。</summary>
    public static readonly TextFieldType Tel = new("tel");

    /// <inheritdoc />
    public static IReadOnlyList<TextFieldType> Known { get; } = [Text, Password, Search, Email, Url, Tel];

    /// <inheritdoc />
    public static TextFieldType Parse(string value)
    {
        var type = new TextFieldType(value);
        Variants.WarnIfUnknown(type);
        return type;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
