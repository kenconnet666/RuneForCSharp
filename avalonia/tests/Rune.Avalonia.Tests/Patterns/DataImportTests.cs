using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>DataImport：CSV 上传 → 按名称与别名自动对应列 → 逐行校验（错误标在格子上、只看有错的行）→ 就地改正后重新校验 → 导入通过的行；导入失败留在预览。</summary>
public class DataImportTests
{
    const string Csv = "料号,名称,数量（件）\r\nP-001,支架,40\r\nP-002,,abc\r\nP-003,盖板,12\r\n";

    /// <summary>同上游的 zod schema：名称必填、数量是正整数。</summary>
    static ImportParseResult Parse(IReadOnlyDictionary<string, object?> raw)
    {
        var errors = new Dictionary<string, string>();
        if (raw["part"] is not string { Length: > 0 } part) errors["part"] = "请填写零件号";
        if (raw["name"] is not string { Length: > 0 } name) errors["name"] = "请填写名称";
        var quantity = TableColumns.Numeric(raw["quantity"]);
        if (quantity is null) errors["quantity"] = "数量要是数字";
        else if (quantity <= 0 || quantity != decimal.Truncate(quantity.Value)) errors["quantity"] = "数量要大于 0";
        return errors.Count > 0 ? ImportParseResult.Fail(errors) : ImportParseResult.Ok(new Part((string)raw["part"]!, (string)raw["name"]!, (int)quantity!.Value));
    }

    record Part(string Code, string Name, int Quantity);

    static (DataImport Import, Window Window, List<IReadOnlyList<object?>> Calls) Setup(Func<IReadOnlyList<object?>, Task?>? import = null)
    {
        var calls = new List<IReadOnlyList<object?>>();
        var control = new DataImport
        {
            Fields =
            [
                new("part", "零件号") { Required = true, Aliases = ["料号"] },
                new("name", "名称") { Required = true },
                new("quantity", "数量") { Type = ImportFieldType.Number },
            ],
            ParseRow = Parse,
            Import = rows =>
            {
                calls.Add(rows);
                return import?.Invoke(rows);
            },
            Width = 900,
        };
        var window = HeadlessUi.Show(control);
        return (control, window, calls);
    }

    static async Task Upload(DataImport control, Window window)
    {
        // FileDrop 交出的 IStorageFile 不能自己实现：读好的表直接交给 DataImport（读 CSV 由 Sheet 的测试覆盖）
        await control.Load(await Sheet.ReadCsv(new MemoryStream(Encoding.UTF8.GetBytes(Csv)), "零件.csv"));
        HeadlessUi.Flush(window);
    }

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static string Texts(Control root) => string.Concat(root.GetLogicalDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text));

    static TableCell Cell(DataImport control, int line, string column) =>
        control.PreviewTable!.Surface.Realized.Values.Single(view => ((DataImport.ImportRow)view.Row!).Line == line).CellOf(column)!;

    [Fact]
    [Upstream("DataImport", "上传后按名称、别名自动对应列（“数量（件）”也认作“数量”），示例是文件里的头两个值")]
    public Task Upload_maps_columns_by_name_and_alias() => HeadlessUi.Run(async () =>
    {
        var (control, window, _) = Setup();
        await Upload(control, window);
        Assert.Equal(1, control.Step);
        var selects = control.Part<ColumnTable>(DataImportParts.Mapping).GetLogicalDescendants().OfType<Select>().ToList();
        Assert.Equal(["料号", "名称", "数量（件）"], selects.Select(select => select.Value));
        Assert.Contains("P-001、P-002", Texts(control));
        Assert.True(control.Part<Button>(DataImportParts.Next).IsEnabled);
    });

    [Fact]
    [Upstream("DataImport", "校验：错误标在格子上、只看有错的行；改正后重新校验，导入通过的行（schema 解析后的值）")]
    public Task Validation_marks_cells_and_edits_revalidate() => HeadlessUi.Run(async () =>
    {
        var (control, window, calls) = Setup();
        await Upload(control, window);
        Click(control.Part<Button>(DataImportParts.Next));
        HeadlessUi.Flush(window);
        Assert.Contains("共 3 行，可以导入 2 行，1 行有错", Texts(control));
        var name = Cell(control, 2, "name");
        Assert.Contains("invalid", name.Classes);
        Assert.Equal("请填写名称", ToolTip.GetTip(name));

        // 只看有错的行
        control.Part<Switch>(DataImportParts.OnlyErrors).IsChecked = true;
        HeadlessUi.Flush(window);
        Assert.Single(control.PreviewTable!.View);
        control.Part<Switch>(DataImportParts.OnlyErrors).IsChecked = false;
        HeadlessUi.Flush(window);

        // 就地改正名称：数量仍是错的
        name = Cell(control, 2, "name");
        name.Focus();
        name.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = name });
        HeadlessUi.Flush(window);
        var editor = Assert.IsType<TextField>(((CellEditorHost)Cell(control, 2, "name").Content!).Editor);
        editor.Text = "底座";
        editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = editor });
        HeadlessUi.Flush(window);
        Assert.Equal("底座", control.Rows[1].Raw["name"]);
        Assert.Contains("可以导入 2 行，1 行有错", Texts(control));
        Assert.Equal("数量要是数字", ToolTip.GetTip(Cell(control, 2, "quantity")));

        var submit = control.Part<Button>(DataImportParts.Submit);
        Assert.Equal("导入 2 行", submit.Content);
        Click(submit);
        HeadlessUi.Flush(window);
        Assert.Equal([new Part("P-001", "支架", 40), new Part("P-003", "盖板", 12)], Assert.Single(calls).Cast<Part>());
        Assert.Equal(3, control.Step);
        Assert.Contains("已导入 2 行", Texts(control));
    });

    [Fact]
    [Upstream("DataImport", "onImport 失败：留在预览，说出原因；换步后焦点在这一步的内容上")]
    public Task Failed_import_stays_in_preview_and_focus_follows_steps() => HeadlessUi.Run(async () =>
    {
        var fail = true;
        var (control, window, _) = Setup(_ => fail ? Task.FromException(new InvalidOperationException("库存系统没有响应")) : Task.CompletedTask);
        await Upload(control, window);
        var panel = control.Part<Border>(DataImportParts.Panel);
        Assert.True(panel.IsFocused);
        Assert.Equal("对应列", AutomationProperties.GetName(panel));
        Click(control.Part<Button>(DataImportParts.Next));
        HeadlessUi.Flush(window);
        Click(control.Part<Button>(DataImportParts.Submit));
        HeadlessUi.Flush(window);
        var alert = control.Part<Alert>(DataImportParts.ImportError);
        Assert.True(alert.IsVisible);
        Assert.Equal(("导入失败", "库存系统没有响应"), (alert.Title, alert.Content));
        Assert.Equal(2, control.Step);

        // 再试一次成功：提示收掉
        fail = false;
        Click(control.Part<Button>(DataImportParts.Submit));
        HeadlessUi.Flush(window);
        Assert.False(alert.IsVisible);
        Assert.Contains("已导入 2 行", Texts(control));
    });

    [Fact]
    public Task Without_parse_row_required_and_number_checks_apply() => HeadlessUi.Run(async () =>
    {
        IReadOnlyList<object?>? imported = null;
        var control = new DataImport
        {
            Fields = [new("part", "零件号") { Required = true, Aliases = ["料号"] }, new("name", "名称") { Required = true }, new("quantity", "数量") { Type = ImportFieldType.Number }],
            Validate = row => row["part"] as string == "P-003" ? new Dictionary<string, string> { ["part"] = "停用的零件" } : null,
            Import = rows =>
            {
                imported = rows;
                return null;
            },
        };
        var window = HeadlessUi.Show(control);
        await Upload(control, window);
        Click(control.Part<Button>(DataImportParts.Next));
        HeadlessUi.Flush(window);
        Assert.Equal(["必填", "不是数字"], control.Rows[1].Errors.Values.Select(text => text.Contains("必填") ? "必填" : text));
        Assert.Equal("停用的零件", control.Rows[2].Errors["part"]);
        Click(control.Part<Button>(DataImportParts.Submit));
        var row = (IReadOnlyDictionary<string, object?>)Assert.Single(imported!)!;
        Assert.Equal(("P-001", "支架", (object?)40m), ((string)row["part"]!, (string)row["name"]!, row["quantity"]));
    });

    [Fact]
    public void Auto_map_ignores_brackets_case_and_takes_each_column_once()
    {
        var map = DataImport.AutoMap([new("part", "零件号") { Aliases = ["Part No."] }, new("code", "零件号")], ["PART NO.", "零件号 (必填)"]);
        Assert.Equal(("PART NO.", "零件号 (必填)"), (map["part"], map["code"]));
    }
}
