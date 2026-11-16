using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Rune.Avalonia.Examples;

/// <summary>DataImport 页的示例（vue-tsx-pro 文档站 patterns/DataImport）。Excel 的读写（上游的 xlsx 子路径）还没做，模板与导入先用 CSV；导出 Excel 的示例随 xlsx 补上。</summary>
static class DataImportExamples
{
    public static Example[] All =>
    [
        new("DataImport", "parts", "零件批量导入", "先点“下载模板”存一份示例（故意留了两处错），再把它拖进来。Fields 是要导入的字段：Label 与 Aliases 用来自动对上文件里的列（“料号”“数量（件）”都认得），可以在“对应列”一步改；必填的字段没对上时不能往下走。ParseRow 逐行解析（上游的 zod schema），错误标在对应的格子上（悬停、读屏都能知道原因）；有错的格子可以直接改，改完重新校验，“只看有错的行”筛出要处理的。Import 拿到通过校验的行（ParseRow 给的值）。Parse 读文件：不写时读 CSV（Sheet.ReadCsv）；Sheet.FromMatrix 把行列矩阵转成表：多行表头连成“组 / 列”（HeaderRows）、单元格里有换行的可以拆成几行（SplitLines）。", Parts),
    ];

    record Part(string Code, string Name, string Category, int Quantity, decimal Price);

    static readonly string[] Categories = ["机械件", "塑料件", "钣金件", "标准件", "线缆"];

    /// <summary>同上游的 zod schema：零件号是 P- 加数字、名称必填、品类在清单里、数量是正整数、单价不为负。</summary>
    static ImportParseResult ParsePart(IReadOnlyDictionary<string, object?> raw)
    {
        var errors = new Dictionary<string, string>();
        var code = raw["part"] as string;
        if (string.IsNullOrEmpty(code)) errors["part"] = "请填写零件号";
        else if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^P-\d{3,}$")) errors["part"] = "零件号是 P- 加数字";
        if (raw["name"] is not string { Length: > 0 } name) errors["name"] = "请填写名称";
        if (raw["category"] is not string category || !Categories.Contains(category)) errors["category"] = "品类只能是：" + string.Join("、", Categories);
        var quantity = TableColumns.Numeric(raw["quantity"]);
        if (quantity is null) errors["quantity"] = "数量要是数字";
        else if (quantity != decimal.Truncate(quantity.Value)) errors["quantity"] = "数量是整数";
        else if (quantity <= 0) errors["quantity"] = "数量要大于 0";
        var price = TableColumns.Numeric(raw["price"]);
        if (price is null) errors["price"] = "单价要是数字";
        else if (price < 0) errors["price"] = "单价不能为负";
        return errors.Count > 0
            ? ImportParseResult.Fail(errors)
            : ImportParseResult.Ok(new Part(code!, (string)raw["name"]!, (string)raw["category"]!, (int)quantity!.Value, price!.Value));
    }

    record TemplateRow(string? Part, string Name, string Category, object Quantity, decimal Price);

    // 模板：表头与几行示例（故意留两处错，导入时能看到校验）
    static readonly TemplateRow[] Sample =
    [
        new("P-101", "显示器支架", "机械件", 40, 128),
        new("P-102", "铰链底座", "机械件", 80, 76.5m),
        new(null, "检修口盖板", "钣金件", 24, 212),
        new("P-104", "线束卡箍", "线缆", "三百", 3.2m),
        new("P-105", "加强筋", "塑料件", 60, 45),
    ];

    static readonly DataTableColumn[] TemplateColumns =
    [
        new("Part", "料号"),
        new("Name", "名称"),
        new("Category", "品类"),
        new("Quantity", "数量（件）") { Type = ColumnType.Text },
        new("Price", "单价（元）") { Type = ColumnType.Number, Format = new NumericFormat { MaximumFractionDigits = 2 } },
    ];

    static Control Parts()
    {
        var imported = new List<Part>();
        var list = new Flex { Gap = Space.Xs, Wrap = true, Align = FlexAlign.Center, IsVisible = false };
        var import = new DataImport
        {
            Fields =
            [
                new("part", "零件号") { Required = true, Aliases = ["料号", "Part No."], Width = 120 },
                new("name", "名称") { Required = true, Width = 160 },
                new("category", "品类") { Required = true, Width = 100 },
                new("quantity", "数量") { Type = ImportFieldType.Number, Width = 100 },
                new("price", "单价") { Type = ImportFieldType.Number, Width = 100 },
            ],
            ParseRow = ParsePart,
            Import = async rows =>
            {
                await Task.Delay(500);
                imported.AddRange(rows.Cast<Part>());
                list.Children.Clear();
                list.Children.Add(new Small { Tone = TextTone.Muted, Text = "已导入：" });
                foreach (var part in imported) list.Children.Add(new Tag { Content = $"{part.Code} {part.Name} × {part.Quantity}" });
                list.IsVisible = true;
            },
        };
        // 模板存成 CSV（Excel 打开中文不乱码：开头带 BOM）
        import.TemplateRequested += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(import)?.StorageProvider is not { } storage) return;
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "零件导入模板.csv", DefaultExtension = "csv" });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(TableCsv.ToCsv(Sample, TemplateColumns, new CsvOptions { Bom = true }));
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
            Children = { new Card { Content = import }, list },
        };
    }
}
