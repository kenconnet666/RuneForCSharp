using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>值的类型（没有 <see cref="DataImport.ParseRow"/> 时）。</summary>
public enum ImportFieldType
{
    /// <summary>文字（去掉首尾空白）。</summary>
    Text,

    /// <summary>数字：文字转成 decimal（千分位的逗号去掉），转不了算错。</summary>
    Number,
}

/// <summary>要导入的一个字段（vue-tsx-pro 的 ImportField）。</summary>
/// <param name="Key">字段名：导入的行里的键。</param>
/// <param name="Label">名称：对应列时显示，也用来自动对上文件里的同名列。</param>
public sealed record ImportField(string Key, string Label)
{
    /// <summary>必填：没对应上不能往下走；没有 ParseRow 时空值算错。</summary>
    public bool Required { get; init; }

    /// <summary>文件里这一列可能的其他叫法（"料号""Part No."）：自动对应时也认。</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>值的类型（没有 ParseRow 时），默认文字。</summary>
    public ImportFieldType Type { get; init; }

    /// <summary>预览表里这一列的宽度（像素）。</summary>
    public double? Width { get; init; }
}

/// <summary>一行的解析结果（<see cref="DataImport.ParseRow"/> 返回，上游是 zod 的 safeParse）：通过时是导入的值，没通过时是 字段名 → 原因。</summary>
/// <param name="Value">导入的值（没通过时为 null）。</param>
/// <param name="Errors">字段名 → 原因；空的表示通过。</param>
public sealed record ImportParseResult(object? Value, IReadOnlyDictionary<string, string> Errors)
{
    /// <summary>通过。</summary>
    public static ImportParseResult Ok(object value) => new(value, new Dictionary<string, string>());

    /// <summary>没通过。</summary>
    public static ImportParseResult Fail(IReadOnlyDictionary<string, string> errors) => new(null, errors);
}

/// <summary>
/// 数据导入（vue-tsx-pro 的 DataImport）：上传文件 → 对应列 → 逐行校验与预览（有错的格子就地改）→ 确认导入。批量导入零件、报价、人员这类数据用它。
/// <code>
/// new DataImport
/// {
///     Fields = [new("part", "零件号") { Required = true, Aliases = ["料号"] }, new("quantity", "数量") { Type = ImportFieldType.Number }],
///     Import = rows =&gt; api.ImportParts(rows),
/// };
/// </code>
/// <list type="bullet">
/// <item>上传：选文件或拖进来，<see cref="Parse"/> 读成表（默认读 CSV）；几张工作表时先选一张。</item>
/// <item>对应列：按字段的名称、字段名与别名自动对上文件里的列（去掉空白、括号与大小写比较："数量（件）"也认作"数量"），可以改；必填的字段没对上时不能往下走。</item>
/// <item>校验：空的格子是 null。有 <see cref="ParseRow"/> 时逐行解析（上游的 zod schema），错误标在对应的格子上；没有时按 Required、Type 检查，再加 <see cref="Validate"/>。
/// 有错的格子可以直接改（表格就地编辑），改完重新校验这一行；"只看有错的行"筛出要处理的。</item>
/// <item>导入：<see cref="Import"/> 拿到通过校验的行（ParseRow 给的值，没有时是 字段名 → 值 的字典）；任务完成前按钮加载中，失败时留在预览并说出原因。</item>
/// </list>
/// 换了一步时焦点跟到这一步的内容上（读屏念这一步的名称），不掉回窗口开头。部件：<see cref="DataImportParts"/>。
/// </summary>
public partial class DataImport : Control
{
    /// <summary>要导入的字段，按显示的顺序。</summary>
    public static readonly StyledProperty<IReadOnlyList<ImportField>> FieldsProperty = AvaloniaProperty.Register<DataImport, IReadOnlyList<ImportField>>(nameof(Fields), []);

    /// <summary>逐行解析（上游的 schema）：拿到 字段名 → 原值，给出导入的值或各字段的错误。</summary>
    public static readonly StyledProperty<Func<IReadOnlyDictionary<string, object?>, ImportParseResult>?> ParseRowProperty =
        AvaloniaProperty.Register<DataImport, Func<IReadOnlyDictionary<string, object?>, ImportParseResult>?>(nameof(ParseRow));

    /// <summary>没有 ParseRow 时的逐行校验：拿到转好的值，返回 字段名 → 原因（在 Required、Type 的检查之后）。</summary>
    public static readonly StyledProperty<Func<IReadOnlyDictionary<string, object?>, IReadOnlyDictionary<string, string>?>?> ValidateProperty =
        AvaloniaProperty.Register<DataImport, Func<IReadOnlyDictionary<string, object?>, IReadOnlyDictionary<string, string>?>?>(nameof(Validate));

    /// <summary>读文件：返回各工作表；默认读 CSV（<see cref="Sheet.ReadCsv"/>）。</summary>
    public static readonly StyledProperty<Func<IStorageFile, Task<IReadOnlyList<SheetTable>>>?> ParseProperty =
        AvaloniaProperty.Register<DataImport, Func<IStorageFile, Task<IReadOnlyList<SheetTable>>>?>(nameof(Parse));

    /// <summary>能选的文件类型，默认 .csv。</summary>
    public static readonly StyledProperty<string> AcceptProperty = AvaloniaProperty.Register<DataImport, string>(nameof(Accept), ".csv");

    /// <summary>导入通过校验的行：任务完成前按钮加载中；出错时留在预览，提示原因。</summary>
    public static readonly StyledProperty<Func<IReadOnlyList<object?>, Task?>?> ImportProperty = AvaloniaProperty.Register<DataImport, Func<IReadOnlyList<object?>, Task?>?>(nameof(Import));

    /// <summary>点了"下载模板"（TS 的 onTemplate）：订阅了才有这个按钮。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> TemplateRequestedEvent = RoutedEvent.Register<DataImport, RoutedEventArgs>(nameof(TemplateRequested), RoutingStrategies.Bubble);

    /// <summary>预览里的一行：第几行数据（从 1 起）、对应后的原值（就地改的是它）、通过时的值、字段名 → 原因。</summary>
    internal sealed record ImportRow(int Line, IReadOnlyDictionary<string, object?> Raw, object? Value, IReadOnlyDictionary<string, string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch };
    readonly Steps steps = new() { Name = DataImportParts.Steps };
    readonly Border panel = new() { Name = DataImportParts.Panel, Focusable = true, FocusAdorner = null };
    readonly Alert importFailure = new() { Name = DataImportParts.ImportError, Tone = StatusTone.Danger, Live = AlertLive.Assertive };
    readonly Flex actions = new() { Name = DataImportParts.Actions, Justify = FlexJustify.End, Gap = Space.Sm };
    readonly Button previous = new() { Name = DataImportParts.Previous, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral };
    readonly Button next = new() { Name = DataImportParts.Next };
    readonly Button submit = new() { Name = DataImportParts.Submit };
    int step;
    IReadOnlyList<SheetTable> sheets = [];
    string? sheetName;
    Dictionary<string, string?> mapping = [];
    string parseError = "";
    bool onlyErrors;
    int imported;
    IReadOnlyList<ImportRow> rows = [];
    DataTable? preview;
    Small? summary;
    Switch? onlyErrorsSwitch;
    Small? editHint;
    // 订阅了 TemplateRequested 才有"下载模板"（上游写了 onTemplate 才有）
    int templateHandlers;

    /// <summary>新建：从"上传文件"这一步开始。</summary>
    public DataImport()
    {
        previous.Click += (_, _) =>
        {
            if (step == 1) Restart();
            else GoTo(1);
        };
        next.Click += (_, _) => Preview();
        submit.Click += async (_, _) => await Submit();
        importFailure.IsVisible = false;
        root.Children.AddRange([steps, panel, importFailure, actions]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Render();
    }

    /// <summary>要导入的字段。</summary>
    public IReadOnlyList<ImportField> Fields { get => GetValue(FieldsProperty); set => SetValue(FieldsProperty, value); }

    /// <summary>逐行解析。</summary>
    public Func<IReadOnlyDictionary<string, object?>, ImportParseResult>? ParseRow { get => GetValue(ParseRowProperty); set => SetValue(ParseRowProperty, value); }

    /// <summary>逐行校验。</summary>
    public Func<IReadOnlyDictionary<string, object?>, IReadOnlyDictionary<string, string>?>? Validate { get => GetValue(ValidateProperty); set => SetValue(ValidateProperty, value); }

    /// <summary>读文件。</summary>
    public Func<IStorageFile, Task<IReadOnlyList<SheetTable>>>? Parse { get => GetValue(ParseProperty); set => SetValue(ParseProperty, value); }

    /// <summary>能选的文件类型。</summary>
    public string Accept { get => GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }

    /// <summary>导入通过校验的行。</summary>
    public Func<IReadOnlyList<object?>, Task?>? Import { get => GetValue(ImportProperty); set => SetValue(ImportProperty, value); }

    /// <summary>点了"下载模板"。</summary>
    public event EventHandler<RoutedEventArgs>? TemplateRequested
    {
        add
        {
            AddHandler(TemplateRequestedEvent, value);
            templateHandlers++;
            if (step == 0) Render();
        }
        remove
        {
            RemoveHandler(TemplateRequestedEvent, value);
            templateHandlers--;
            if (step == 0) Render();
        }
    }

    /// <summary>当前在第几步（0 上传、1 对应列、2 预览、3 完成；测试用）。</summary>
    internal int Step => step;

    /// <summary>预览的行（测试用）。</summary>
    internal IReadOnlyList<ImportRow> Rows => rows;

    /// <summary>预览表（测试用）。</summary>
    internal DataTable? PreviewTable => preview;

    /// <summary>各字段对上的列（测试用）。</summary>
    internal IReadOnlyDictionary<string, string?> Mapping => mapping;

    PatternsMessages Messages => PatternsMessages.For(Provider.GetLang(this));

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == Provider.LangProperty || change.Property == AcceptProperty || change.Property == FieldsProperty) Render();
    }

    // ---- 上传 ----

    /// <summary>选了文件：读成表，有数据行时自动对应列、进入下一步。</summary>
    internal async Task Load(IStorageFile file)
    {
        parseError = "";
        try
        {
            var read = await (Parse ?? ReadCsv)(file);
            await Load(read);
        }
        catch (Exception error)
        {
            parseError = Messages.DataImportParseFailed(error.Message);
            Render();
        }
    }

    /// <summary>读好了的表：去掉没有数据行的，有剩下的就进入"对应列"。</summary>
    internal Task Load(IReadOnlyList<SheetTable> read)
    {
        var kept = read.Where(sheet => sheet.Rows.Count > 0).ToList();
        if (kept.Count == 0)
        {
            parseError = Messages.DataImportEmpty;
            Render();
            return Task.CompletedTask;
        }
        sheets = kept;
        sheetName = kept[0].Name;
        mapping = AutoMap(Fields, kept[0].Headers);
        GoTo(1);
        return Task.CompletedTask;
    }

    static async Task<IReadOnlyList<SheetTable>> ReadCsv(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        return await Sheet.ReadCsv(stream, file.Name);
    }

    /// <summary>文字规范化后比较：去掉空白、括号里的说明与大小写（"零件号 (必填)"也认作"零件号"）。</summary>
    static string Normalize(string text) => Spaces().Replace(Brackets().Replace(text.ToLowerInvariant(), ""), "");

    [GeneratedRegex("[（(][^）)]*[）)]")]
    private static partial Regex Brackets();

    [GeneratedRegex("[\\s_*]")]
    private static partial Regex Spaces();

    /// <summary>按名称、字段名与别名对上文件里的列；一列只对给一个字段。</summary>
    internal static Dictionary<string, string?> AutoMap(IReadOnlyList<ImportField> fields, IReadOnlyList<string> headers)
    {
        var taken = new HashSet<string>();
        var result = new Dictionary<string, string?>();
        foreach (var field in fields)
        {
            var names = new[] { field.Label, field.Key }.Concat(field.Aliases).Select(Normalize).ToHashSet();
            var header = headers.FirstOrDefault(item => !taken.Contains(item) && names.Contains(Normalize(item)));
            if (header is not null) taken.Add(header);
            result[field.Key] = header;
        }
        return result;
    }

    SheetTable? CurrentSheet => sheets.FirstOrDefault(sheet => sheet.Name == sheetName);

    List<string> Unmapped => [.. Fields.Where(item => item.Required && mapping.GetValueOrDefault(item.Key) is null).Select(item => item.Label)];

    // ---- 校验 ----

    /// <summary>校验一行：ParseRow 优先；否则 Required、Type 与 Validate。</summary>
    ImportRow Check(int line, IReadOnlyDictionary<string, object?> raw)
    {
        if (ParseRow is { } parse)
        {
            var result = parse(raw);
            return new ImportRow(line, raw, result.Errors.Count == 0 ? result.Value : null, result.Errors);
        }
        var lang = Provider.GetLang(this);
        var errors = new Dictionary<string, string>();
        var value = new Dictionary<string, object?>();
        foreach (var field in Fields)
        {
            var cell = raw.GetValueOrDefault(field.Key);
            if (Sheet.IsBlank(cell))
            {
                if (field.Required) errors[field.Key] = lang.FormRequired;
                value[field.Key] = null;
            }
            else if (field.Type == ImportFieldType.Number)
            {
                var number = TableColumns.Numeric(cell is string text ? text.Replace(",", "").Trim() : cell);
                if (number is null) errors[field.Key] = Messages.DataImportNotNumber;
                value[field.Key] = number;
            }
            else value[field.Key] = cell is string text ? text.Trim() : cell;
        }
        // 必填、类型的错误优先，Validate 补上其余字段的
        foreach (var (key, reason) in Validate?.Invoke(value) ?? new Dictionary<string, string>()) errors.TryAdd(key, reason);
        return new ImportRow(line, raw, errors.Count == 0 ? value : null, errors);
    }

    /// <summary>对应完：按对应关系取出各行并校验，进入预览。</summary>
    void Preview()
    {
        if (CurrentSheet is not { } sheet) return;
        rows = [.. sheet.Rows.Select((row, index) =>
        {
            var raw = Fields.ToDictionary(field => field.Key, field =>
            {
                var value = mapping.GetValueOrDefault(field.Key) is { } header ? row.GetValueOrDefault(header) : null;
                return Sheet.IsBlank(value) ? null : value;
            });
            return Check(index + 1, raw);
        })];
        onlyErrors = false;
        GoTo(2);
    }

    /// <summary>就地改了一格：改原值、重新校验这一行。</summary>
    Task<string?> ChangeCell(object row, string key, object? value)
    {
        var target = (ImportRow)row;
        rows = [.. rows.Select(item =>
        {
            if (item != target) return item;
            var raw = new Dictionary<string, object?>(item.Raw) { [key] = Sheet.IsBlank(value) ? null : value };
            return Check(item.Line, raw);
        })];
        SyncPreview();
        return Task.FromResult<string?>(null);
    }

    // ---- 导入 ----

    async Task Submit()
    {
        var valid = rows.Where(row => row.IsValid).ToList();
        submit.IsLoading = true;
        importFailure.IsVisible = false;
        try
        {
            if (Import?.Invoke([.. valid.Select(row => row.Value)]) is { } pending) await pending;
            imported = valid.Count;
            GoTo(3);
        }
        catch (Exception error)
        {
            // 留在预览，把原因说出来：不然按钮转完圈什么都没发生，用户不知道是没导进去
            importFailure.Title = Messages.DataImportFailed;
            importFailure.Content = error.Message is { Length: > 0 } message ? message : Provider.GetLang(this).RequestFailed;
            importFailure.IsVisible = true;
            System.Diagnostics.Trace.TraceError($"DataImport 的 Import 失败，留在预览：{error}");
        }
        finally
        {
            submit.IsLoading = false;
        }
    }

    void Restart()
    {
        sheets = [];
        sheetName = null;
        rows = [];
        GoTo(0);
    }

    // ---- 画面 ----

    void GoTo(int target)
    {
        step = target;
        importFailure.IsVisible = false;
        Render();
        // 换了一步：原来聚焦的东西（拖放区、"导入"按钮）没有了，焦点跟到这一步的内容上，不掉回窗口开头
        if (TopLevel.GetTopLevel(this) is not null) Dispatcher.UIThread.Post(() => panel.Focus(), DispatcherPriority.Loaded);
    }

    /// <summary>按当前这一步重建内容与按钮。</summary>
    void Render()
    {
        var messages = Messages;
        steps.Items = [.. messages.DataImportSteps.Select(title => new StepItem(title))];
        steps.Current = step;
        AutomationProperties.SetName(panel, messages.DataImportSteps[step]);
        previous.Content = messages.StepsFormPrevious;
        next.Content = messages.StepsFormNext;
        preview = null;
        panel.Child = step switch
        {
            0 => RenderUpload(messages),
            1 => RenderMapping(messages),
            2 => RenderPreview(messages),
            _ => RenderDone(messages),
        };
        actions.Children.Clear();
        actions.IsVisible = step is 1 or 2;
        if (step == 1)
        {
            next.IsEnabled = Unmapped.Count == 0;
            actions.Children.AddRange([previous, next]);
        }
        else if (step == 2)
        {
            SyncSubmit();
            actions.Children.AddRange([previous, submit]);
        }
    }

    Control RenderUpload(PatternsMessages messages)
    {
        var drop = new FileDrop { Name = DataImportParts.Drop, Accept = Accept, Description = messages.DataImportAccept(string.Join(", ", Accept.Split(','))) };
        drop.FilesPicked += async (_, e) =>
        {
            if (e.Files.Count > 0) await Load(e.Files[0].File);
        };
        var content = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Children = { drop } };
        if (parseError.Length > 0) content.Children.Add(new Alert { Tone = StatusTone.Danger, Content = parseError });
        if (templateHandlers > 0)
        {
            var template = new Button { Name = DataImportParts.Template, Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Icon = Lucide.Download, Content = messages.DataImportTemplate };
            template.Click += (_, _) => RaiseEvent(new RoutedEventArgs(TemplateRequestedEvent));
            content.Children.Add(new Flex { Justify = FlexJustify.End, Children = { template } });
        }
        return content;
    }

    Control RenderMapping(PatternsMessages messages)
    {
        var sheet = CurrentSheet;
        var headers = sheet?.Headers ?? [];
        string SampleOf(string? header) => header is null ? "" : string.Join("、", (sheet?.Rows ?? []).Take(2).Select(row => row.GetValueOrDefault(header))
            .Where(value => !Sheet.IsBlank(value)).Select(value => Convert.ToString(value, CultureInfo.CurrentCulture)));
        var content = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch };
        if (sheets.Count > 1)
        {
            var pick = new Select { ItemsSource = sheets.Select(item => item.Name).ToList(), Value = sheetName, Size = TextSize.Sm };
            AutomationProperties.SetName(pick, messages.DataImportSheet);
            pick.PropertyChanged += (_, e) =>
            {
                if (e.Property != SelectBase.ValueProperty) return;
                sheetName = pick.Value;
                if (CurrentSheet is { } current) mapping = AutoMap(Fields, current.Headers);
                Render();
            };
            content.Children.Add(new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Small { Tone = TextTone.Muted, Text = messages.DataImportSheet }, pick } });
        }
        var samples = new Dictionary<string, Small>();
        var table = new ColumnTable
        {
            Name = DataImportParts.Mapping, ItemsSource = Fields,
            Columns =
            [
                new("Label", messages.DataImportField)
                {
                    Cell = (row, _) =>
                    {
                        var field = (ImportField)row;
                        var label = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new TextBlock { Text = field.Label } } };
                        if (field.Required) label.Children.Add(new Badge { Name = DataImportParts.Required, Tone = StatusTone.Danger, Content = "*" });
                        return label;
                    },
                },
                new("Column", messages.DataImportColumn)
                {
                    Width = 240,
                    Cell = (row, _) =>
                    {
                        var field = (ImportField)row;
                        var pick = new Select { ItemsSource = headers, Value = mapping.GetValueOrDefault(field.Key), Placeholder = messages.DataImportSkip, Clearable = true, Size = TextSize.Sm };
                        AutomationProperties.SetName(pick, $"{field.Label}：{messages.DataImportColumn}");
                        pick.PropertyChanged += (_, e) =>
                        {
                            if (e.Property != SelectBase.ValueProperty) return;
                            mapping = new Dictionary<string, string?>(mapping) { [field.Key] = pick.Value };
                            if (samples.TryGetValue(field.Key, out var sample)) sample.Text = SampleOf(pick.Value);
                            SyncUnmapped(content, messages);
                        };
                        return pick;
                    },
                },
                new("Sample", messages.DataImportSample)
                {
                    Cell = (row, _) =>
                    {
                        var field = (ImportField)row;
                        return samples[field.Key] = new Small { Tone = TextTone.Muted, Text = SampleOf(mapping.GetValueOrDefault(field.Key)) };
                    },
                },
            ],
        };
        AutomationProperties.SetName(table, messages.DataImportSteps[1]);
        content.Children.Add(table);
        SyncUnmapped(content, messages);
        return content;
    }

    /// <summary>必填的字段没对上：提示条与"下一步"不可用。</summary>
    void SyncUnmapped(Flex content, PatternsMessages messages)
    {
        var missing = Unmapped;
        if (content.Children.OfType<Alert>().FirstOrDefault() is { } old) content.Children.Remove(old);
        if (missing.Count > 0) content.Children.Add(new Alert { Name = DataImportParts.Unmapped, Tone = StatusTone.Warning, Content = messages.DataImportUnmapped(missing) });
        next.IsEnabled = missing.Count == 0;
    }

    Control RenderPreview(PatternsMessages messages)
    {
        summary = new Small();
        onlyErrorsSwitch = new Switch { Name = DataImportParts.OnlyErrors, Content = messages.DataImportOnlyErrors, IsChecked = onlyErrors };
        onlyErrorsSwitch.PropertyChanged += (_, e) =>
        {
            if (e.Property != ChoiceBase.IsCheckedProperty) return;
            onlyErrors = onlyErrorsSwitch.IsChecked;
            SyncPreview();
        };
        editHint = new Small { Tone = TextTone.Muted, Text = messages.DataImportEditHint };
        preview = new DataTable
        {
            Name = DataImportParts.Preview, Density = TableDensity.Compact, MaxHeight = 24 * 16,
            GetKey = row => ((ImportRow)row).Line.ToString(CultureInfo.InvariantCulture),
            Columns = PreviewColumns(messages),
            RowTone = row => ((ImportRow)row).IsValid ? null : StatusTone.Danger,
            ValidateRow = row => ((ImportRow)row).Errors,
            CellChange = ChangeCell,
        };
        AutomationProperties.SetName(preview, messages.DataImportPreview);
        SyncPreview();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children =
            {
                new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Wrap = true, Children = { summary, onlyErrorsSwitch } },
                preview,
                editHint,
            },
        };
    }

    /// <summary>预览的行、统计、开关与"导入 n 行"跟上当前的行。</summary>
    void SyncPreview()
    {
        if (preview is null || summary is null || onlyErrorsSwitch is null || editHint is null) return;
        var (total, valid, invalid) = Counts();
        summary.Text = Messages.DataImportSummary(total, valid, invalid);
        onlyErrorsSwitch.IsVisible = invalid > 0;
        editHint.IsVisible = invalid > 0;
        preview.ItemsSource = onlyErrors ? rows.Where(row => !row.IsValid).ToList() : rows;
        SyncSubmit();
    }

    void SyncSubmit()
    {
        var (_, valid, _) = Counts();
        submit.Content = Messages.DataImportSubmit(valid);
        submit.IsEnabled = valid > 0;
    }

    (int Total, int Valid, int Invalid) Counts()
    {
        var invalid = rows.Count(row => !row.IsValid);
        return (rows.Count, rows.Count - invalid, invalid);
    }

    IReadOnlyList<DataTableColumn> PreviewColumns(PatternsMessages messages) =>
    [
        new("Line", messages.DataImportRow) { Width = 72, Value = row => ((ImportRow)row).Line },
        new("Status", messages.DataImportStatus)
        {
            Width = 200,
            Cell = (row, _) =>
            {
                var item = (ImportRow)row;
                return item.Errors.Values.FirstOrDefault() is { } first
                    ? new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = Lucide.CircleExclamationPoint, Tone = TextTone.Danger }, new Small { Tone = TextTone.Danger, Text = first } } }
                    : new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new Icon { Data = Lucide.CircleCheck, Tone = TextTone.Success }, new Small { Tone = TextTone.Muted, Text = messages.DataImportValid } } };
            },
        },
        .. Fields.Select(field => new DataTableColumn(field.Key, field.Label)
        {
            Width = field.Width, MinWidth = 120, Type = field.Type == ImportFieldType.Number ? ColumnType.Number : ColumnType.Text,
            Value = row => ((ImportRow)row).Raw.GetValueOrDefault(field.Key), Editable = true,
        }),
    ];

    Control RenderDone(PatternsMessages messages)
    {
        var again = new Button { Name = DataImportParts.Again, Size = TextSize.Sm, Variant = ButtonVariant.Soft, Content = messages.DataImportAgain };
        again.Click += (_, _) => Restart();
        return new Alert { Tone = StatusTone.Success, Title = messages.DataImportDone(imported), Content = again };
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>DataImport 的部件名（TS 的 propsSteps、propsDrop 等）。</summary>
public static class DataImportParts
{
    /// <summary>步骤条（Steps）。</summary>
    public const string Steps = "PART_Steps";

    /// <summary>这一步的内容（Border，换步后焦点在它上面）。</summary>
    public const string Panel = "PART_Panel";

    /// <summary>选文件区域（FileDrop）。</summary>
    public const string Drop = "PART_Drop";

    /// <summary>"下载模板"（Button）。</summary>
    public const string Template = "PART_Template";

    /// <summary>对应列的表（ColumnTable）。</summary>
    public const string Mapping = "PART_Mapping";

    /// <summary>必填字段的星号（Badge）。</summary>
    public const string Required = "PART_Required";

    /// <summary>必填字段没对上的提示（Alert）。</summary>
    public const string Unmapped = "PART_Unmapped";

    /// <summary>预览表（DataTable）。</summary>
    public const string Preview = "PART_Preview";

    /// <summary>"只看有错的行"（Switch）。</summary>
    public const string OnlyErrors = "PART_OnlyErrors";

    /// <summary>按钮行（Flex）。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>"上一步"（Button）。</summary>
    public const string Previous = "PART_Previous";

    /// <summary>"下一步"（Button）。</summary>
    public const string Next = "PART_Next";

    /// <summary>"导入 n 行"（Button）。</summary>
    public const string Submit = "PART_Submit";

    /// <summary>导入失败的提示（Alert）。</summary>
    public const string ImportError = "PART_ImportError";

    /// <summary>导入完成后的"再导入一次"（Button）。</summary>
    public const string Again = "PART_Again";
}
