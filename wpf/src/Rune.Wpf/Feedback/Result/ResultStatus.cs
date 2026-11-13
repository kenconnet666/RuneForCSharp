using System.ComponentModel;

namespace Rune.Wpf;

/// <summary>结果页的常见出错状态（vue-tsx-pro 的 ResultStatuses）：图标、色调、状态码与默认的标题、说明（取语言包的 ResultStatus）。</summary>
/// <param name="Value">取值（403、404、500）。</param>
[TypeConverter(typeof(VariantConverter<ResultStatus>))]
public readonly record struct ResultStatus(string Value) : IVariant<ResultStatus>
{
    /// <summary>没有权限。</summary>
    public static readonly ResultStatus Forbidden = new("403");

    /// <summary>找不到页面。</summary>
    public static readonly ResultStatus NotFound = new("404");

    /// <summary>服务出错。</summary>
    public static readonly ResultStatus ServerError = new("500");

    /// <inheritdoc />
    public static IReadOnlyList<ResultStatus> Known { get; } = [Forbidden, NotFound, ServerError];

    /// <summary>状态的图标与色调（TS 的 STATUS_LOOKS）；不认识的状态都是 null。</summary>
    internal (IconData? Icon, StatusTone? Tone) Look => Value switch
    {
        "403" => (Lucide.ShieldX, StatusTone.Warning),
        "404" => (Lucide.FileQuestionMark, StatusTone.Info),
        "500" => (Lucide.ServerCrash, StatusTone.Danger),
        _ => (null, null),
    };

    /// <inheritdoc />
    public static ResultStatus Parse(string value)
    {
        var status = new ResultStatus(value);
        Variants.WarnIfUnknown(status);
        return status;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
