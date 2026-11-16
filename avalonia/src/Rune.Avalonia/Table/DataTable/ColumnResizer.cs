using Avalonia;
using Avalonia.Automation;

namespace Rune.Avalonia;

/// <summary>
/// 列宽调整手柄（vue-tsx-pro 的 ColumnResizer）：表头单元格结束边的 <see cref="ResizeHandle"/>，拖动或用 ← / → 调整所在列的宽度。
/// 读屏名称取语言包（调整"列名"的列宽），最小宽度默认 <see cref="TableColumns.ColumnMinWidth"/>。
/// DataTable 的可调整列（列的 Resizable，或表格的 Resizable）都用它：宽度读写的是表格的列宽（不是父元素的 Width），调整后写回 DataTable.ColumnWidths。
/// </summary>
public class ColumnResizer : ResizeHandle
{
    /// <summary>列名：读屏名称取语言包（调整"列名"的列宽）。</summary>
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<ColumnResizer, string>(nameof(Label), "");

    /// <summary>新建：最小宽度默认 64px。</summary>
    public ColumnResizer() => SetCurrentValue(MinLengthProperty, TableColumns.ColumnMinWidth);

    /// <summary>列名。</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>读写这一列的宽度（DataTable 给）。</summary>
    internal void Bind(Func<double> read, Action<double> write) => Override = (read, write);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == Provider.LangProperty)
            AutomationProperties.SetName(this, Provider.GetLang(this).TableResizeColumn(Label));
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AutomationProperties.SetName(this, Provider.GetLang(this).TableResizeColumn(Label));
    }
}
