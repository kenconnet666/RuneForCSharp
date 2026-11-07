using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;

namespace Rune.Avalonia;

/// <summary>
/// 多行文本框（vue-tsx-pro 的 Textarea）：外壳与 TextField 相同（<see cref="TextInputBase"/>），高度随内容增长，在 <see cref="Rows"/> 与 <see cref="MaxRows"/> 行之间，
/// 超过 MaxRows 后出现浮层滚动条。
/// <code>
/// &lt;rune:Field Label="备注"&gt;&lt;rune:Textarea Text="{Binding Note}" Rows="3" MaxRows="8" ShowCount="True" MaxLength="200" /&gt;&lt;/rune:Field&gt;
/// </code>
/// 高度按行高（lineHeights.normal）算：至少 Rows 行加上文字区上下各 space.xs 的内边距（TS 的 rows 加自动增高），至多 MaxRows 行（含内边距，TS 的 <c>max-height: 8lh</c>）。<see cref="Fixed"/> 时固定为 Rows 行、不随内容增高
/// （TS 还能拖动右下角调整高度，桌面上没做）。清空按钮贴在第一行（右上角），字数贴在右下角。值是平台 TextBox 的 Text（TS 的 _value）。
/// </summary>
public class Textarea : TextInputBase
{
    /// <summary>最少显示几行，默认 3。</summary>
    public static readonly StyledProperty<int> RowsProperty = AvaloniaProperty.Register<Textarea, int>(nameof(Rows), 3, validate: rows => rows > 0);

    /// <summary>最多增高到几行，之后出现滚动条；不写时不限。</summary>
    public static readonly StyledProperty<int?> MaxRowsProperty = AvaloniaProperty.Register<Textarea, int?>(nameof(MaxRows), validate: rows => rows is null or > 0);

    /// <summary>不随内容增高，固定为 Rows 行。</summary>
    public static readonly StyledProperty<bool> FixedProperty = AvaloniaProperty.Register<Textarea, bool>(nameof(Fixed));

    Panel? area;

    static Textarea() => AffectsMeasure<Textarea>(RowsProperty, MaxRowsProperty, FixedProperty);

    /// <summary>最少行数。</summary>
    public int Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    /// <summary>最多行数。</summary>
    public int? MaxRows { get => GetValue(MaxRowsProperty); set => SetValue(MaxRowsProperty, value); }

    /// <summary>固定高度。</summary>
    public bool Fixed { get => GetValue(FixedProperty); set => SetValue(FixedProperty, value); }

    /// <inheritdoc />
    protected override void OnTemplateParts(INameScope scope) => area = scope.Find<Panel>(TextareaParts.Rows);

    /// <inheritdoc />
    protected override bool SyncsOn(AvaloniaProperty property) =>
        property == RowsProperty || property == MaxRowsProperty || property == FixedProperty || property == LineHeightProperty || property == TextElement.FontSizeProperty;

    /// <summary>文字区的高度：至少 Rows 行加上下各 space.xs，至多 MaxRows 行（含内边距），固定时正好是最少的高度。</summary>
    protected override void Sync()
    {
        base.Sync();
        if (area is null) return;
        var line = double.IsNaN(LineHeight) || LineHeight <= 0 ? FontSize * this.Resource("Rune.LineHeight.Normal", 1.5) : LineHeight;
        var min = Rows * line + 2 * this.Resource("Rune.Space.Xs", 0.25) * FontSize;
        area.MinHeight = min;
        area.MaxHeight = Fixed ? min : MaxRows is { } max ? Math.Max(min, max * line) : double.PositiveInfinity;
    }
}

/// <summary>Textarea 模板里的部件名（外壳、清空、字数同 <see cref="TextInputParts"/>）。</summary>
public static class TextareaParts
{
    /// <summary>外壳（Border：边框、底色、圆角）。</summary>
    public const string Root = TextInputParts.Root;

    /// <summary>文字区（行数的高度设在这层上）。</summary>
    public const string Rows = "PART_Rows";

    /// <summary>清空按钮（rune:ClearButton，贴在第一行）。</summary>
    public const string Clear = TextInputParts.Clear;

    /// <summary>字数计数（rune:CharacterCount，贴在右下角）。</summary>
    public const string Count = TextInputParts.Count;
}
