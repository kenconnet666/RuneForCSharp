using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 按模型生成字段（vue-tsx-pro 的 SchemaFields，放在 <see cref="Form"/> 里）：字段从表单控制器的模型类型推出（<see cref="SchemaModel"/>），
/// 每个字段是 Field（写了 Path，自动绑定值、显示错误）里放按类型选的控件；校验、必填标记、长度与范围照旧由 FormController 与模型的特性负责。
/// <code>
/// &lt;rune:Form Controller="{Binding Apply}"&gt;
///   &lt;rune:SchemaFields Pick="Company,Contact" Columns="2" /&gt;
///   &lt;rune:Field Path="Region" Label="地区"&gt;&lt;rune:Cascader … /&gt;&lt;/rune:Field&gt;
/// &lt;/rune:Form&gt;
/// </code>
/// <list type="bullet">
/// <item>布局：没分组的字段在最前面一组，分组相同的放进一个 FormSection（组名是标题）；<see cref="Columns"/> 是最多几栏（窄时自动减少），字段的 Span 跨栏。
/// 写了页签（Tab）的字段放进标签页（页签里照旧分组），没写的排在页签前面。</item>
/// <item>嵌套的对象是一个 FormSection，对象的列表是可增删的行（标题旁"添加"，行末删除），Table 控件排成明细表（写了 Sum 的列表底合计）；只读时不能增删行。</item>
/// <item>权限与条件（FormController.Access、SchemaField 特性的 VisibleWhen / RequiredWhen / ReadonlyWhen）随值更新：不显示的不渲染，只读、必填写到 Field 上。</item>
/// <item><see cref="Fields"/> 按字段路径换掉控件（返回的控件放进 Field 里，照样自动绑定值）；<see cref="Sources"/> 给字典、远程搜索、人员与上传。</item>
/// </list>
/// 部件：<see cref="SchemaFormParts"/>。
/// </summary>
public class SchemaFields : Control
{
    /// <summary>只生成这些顶层字段，按这里的顺序。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PickProperty = AvaloniaProperty.Register<SchemaFields, IReadOnlyList<string>?>(nameof(Pick));

    /// <summary>不生成这些顶层字段。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> OmitProperty = AvaloniaProperty.Register<SchemaFields, IReadOnlyList<string>?>(nameof(Omit));

    /// <summary>换掉某些字段的控件：字段路径（列表里的列不带行号：<c>Lines.Price</c>）→ 给出控件。</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, Func<SchemaField, Control>>?> FieldsProperty =
        AvaloniaProperty.Register<SchemaFields, IReadOnlyDictionary<string, Func<SchemaField, Control>>?>(nameof(Fields));

    /// <summary>最多几栏（每组各自），容器窄时自动减少；默认 1。</summary>
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<SchemaFields, int>(nameof(Columns), 1);

    /// <summary>数据来源（字典与远程选项、人员、上传）：盖在 Provider 的 FormSources 之上。</summary>
    public static readonly StyledProperty<FormSources?> SourcesProperty = AvaloniaProperty.Register<SchemaFields, FormSources?>(nameof(Sources));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch };
    // 建出来的各个字段：随值更新显示、只读与必填
    readonly List<(string Path, Control Host, Field? Field, SchemaField Schema)> entries = [];
    // 各个列表此刻的行数：变了重建（加一行、删一行）
    readonly Dictionary<string, int> counts = [];
    IFormController? controller;
    string? tab;
    // 加了一行之后：焦点到新行的第一个控件上
    string? focusRowOf;

    /// <summary>新建。</summary>
    public SchemaFields()
    {
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>只生成这些顶层字段。</summary>
    public IReadOnlyList<string>? Pick { get => GetValue(PickProperty); set => SetValue(PickProperty, value); }

    /// <summary>不生成这些顶层字段。</summary>
    public IReadOnlyList<string>? Omit { get => GetValue(OmitProperty); set => SetValue(OmitProperty, value); }

    /// <summary>换掉某些字段的控件。</summary>
    public IReadOnlyDictionary<string, Func<SchemaField, Control>>? Fields { get => GetValue(FieldsProperty); set => SetValue(FieldsProperty, value); }

    /// <summary>最多几栏。</summary>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <summary>数据来源。</summary>
    public FormSources? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == Form.ControllerProperty) Attach(Form.GetController(this));
        else if (change.Property == PickProperty || change.Property == OmitProperty || change.Property == FieldsProperty || change.Property == ColumnsProperty
                 || change.Property == SourcesProperty || change.Property == Provider.LangProperty || change.Property == Provider.FormSourcesProperty) Build();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(global::Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Attach(Form.GetController(this));
    }

    void Attach(IFormController? next)
    {
        if (ReferenceEquals(next, controller) && root.Children.Count > 0) return;
        if (controller is not null) controller.Changed -= OnChanged;
        controller = next;
        if (controller is not null) controller.Changed += OnChanged;
        Build();
    }

    void OnChanged(object? sender, EventArgs e)
    {
        // 行数变了（加一行、删一行、换了整份值）：重建；否则就地改显示、只读、必填
        if (controller is { } form && counts.Any(pair => Count(form, pair.Key) != pair.Value)) Build();
        else Sync();
    }

    static int Count(IFormController form, string path) => (form.GetValue(path) as ICollection)?.Count ?? 0;

    FormSources Merged => FormSources.Merge(Provider.GetFormSources(this), Sources);

    // 所在的 Form、FormSection 整组只读时，列表与明细表也不能增删行（里面的控件已经跟着只读）
    bool Locked => GetValue(Field.IsReadOnlyProperty);

    /// <summary>按当前的字段、权限与值重建。</summary>
    void Build()
    {
        root.Children.Clear();
        entries.Clear();
        counts.Clear();
        // 挂上去之前不建：数据来源（Provider.FormSources）要从外层继承下来
        if (controller is not { } form || !((global::Avalonia.LogicalTree.ILogical)this).IsAttachedToLogicalTree) return;
        var list = SchemaModel.Pick(SchemaModel.Of(form.ModelType), Pick, Omit);
        var tabs = list.Where(field => field.Tab is not null).Select(field => field.Tab!).Distinct().ToList();
        foreach (var section in Grouped([.. list.Where(field => field.Tab is null)])) root.Children.Add(section);
        if (tabs.Count > 0)
        {
            if (tab is null || !tabs.Contains(tab)) tab = tabs[0];
            var tabList = new TabList();
            AutomationProperties.SetName(tabList, Provider.GetLang(this).FormTabs);
            var host = new Tabs { Value = tab, Children = { tabList } };
            host.PropertyChanged += (_, e) =>
            {
                if (e.Property == Tabs.ValueProperty) tab = host.Value;
            };
            foreach (var name in tabs)
            {
                tabList.Children.Add(new Tab { Value = name, Content = name });
                var page = new Flex { Name = SchemaFormParts.TabPanel, Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Margin = new Thickness(0, 12, 0, 0) };
                foreach (var section in Grouped([.. list.Where(field => field.Tab == name)])) page.Children.Add(section);
                host.Children.Add(new TabPanel { Value = name, Content = page });
            }
            root.Children.Add(host);
        }
        Sync();
        if (focusRowOf is { } added)
        {
            focusRowOf = null;
            Dispatcher.UIThread.Post(() => FocusLastRow(added), DispatcherPriority.Loaded);
        }
    }

    /// <summary>字段按分组排成几组：没分组的在最前面，组按第一次出现的先后。</summary>
    IEnumerable<FormSection> Grouped(IReadOnlyList<SchemaField> list) =>
        list.GroupBy(field => field.Group).OrderBy(group => group.Key is null ? 0 : 1).Select(group => Section(group.ToList(), "", group.Key));

    /// <summary>一组字段：标题是组名或对象的标签。</summary>
    FormSection Section(IReadOnlyList<SchemaField> list, string prefix, string? title, string? description = null)
    {
        var section = new FormSection { Name = SchemaFormParts.Section, Columns = Columns, Title = title, Description = description };
        foreach (var field in list)
            if (Item(field, prefix) is { } item) section.Children.Add(item);
        return section;
    }

    /// <summary>一个字段：嵌套对象是一组，列表是可增删的行，明细表是表格，其余是 Field 里放控件。</summary>
    Control? Item(SchemaField field, string prefix)
    {
        var path = prefix + field.Key;
        var form = controller!;
        var readOnly = form.FieldState(path).Readonly || Locked;
        Control host = field.Control switch
        {
            SchemaControl.List => new GridItem { Span = GridSpan.Full, Child = Rows(field, path, readOnly) },
            SchemaControl.Table => new GridItem { Span = GridSpan.Full, Child = TableOf(field, path, readOnly) },
            SchemaControl.Object => new GridItem { Span = GridSpan.Full, Child = Section(field.Fields ?? [], path + ".", field.Label, field.Description) },
            _ => FieldOf(field, path, field.Label, labelHidden: field.Control == SchemaControl.Checkbox),
        };
        if (field.Control is not (SchemaControl.List or SchemaControl.Table or SchemaControl.Object) && field.Span is { } span)
            host = new GridItem { Span = span == SchemaFieldAttribute.Full ? GridSpan.Full : span, Child = host };
        entries.Add((path, host, (host as GridItem)?.Child as Field ?? host as Field, field));
        return host;
    }

    Field FieldOf(SchemaField field, string path, string label, bool labelHidden)
    {
        var custom = Fields?.GetValueOrDefault(WithoutIndexes(path));
        var control = custom?.Invoke(field) ?? ControlOf(field, Merged);
        var item = new Field { Path = path, Label = label, IsLabelHidden = labelHidden, Description = field.Description, Content = control };
        // 一个控件有几个双向的属性（下拉的单选与多选、人员、附件）：说清楚绑哪一个
        if (custom is null && BoundPropertyOf(field.Control) is { } bound) item.BoundProperty = bound;
        return item;
    }

    static string? BoundPropertyOf(SchemaControl control) => control switch
    {
        SchemaControl.Multiselect => nameof(SelectBase.Values),
        SchemaControl.People => nameof(PeoplePicker.Entries),
        SchemaControl.File => nameof(Upload.Files),
        _ => null,
    };

    static string WithoutIndexes(string path) =>
        string.Join('.', path.Split('.').Where(part => !int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _)));

    /// <summary>
    /// 按模型给出查询栏的一组条件（vue-tsx-pro 的 schemaFilters）：Field 加控件，值直接读写 values（QueryTable 的草稿、自己的对象），不经表单控制器；
    /// 文字与选择可以清空。嵌套的对象、列表、人员、附件不生成。放进 FilterBar 的 Fields、QueryTable 的 Filters：
    /// <code>Filters = draft =&gt; SchemaFields.Filters(typeof(OrderFilters), draft, pick: ["Keyword", "Status"])</code>
    /// </summary>
    /// <param name="type">条件的类型（字段从它推）。</param>
    /// <param name="values">读写的对象。</param>
    /// <param name="pick">只要这些字段，按这里的顺序（常用的放 Filters，其余的放 MoreFilters）。</param>
    /// <param name="omit">不要这些字段。</param>
    /// <param name="fields">换掉某些字段的控件：自己读写值。</param>
    /// <param name="sources">数据来源（字典与远程选项）。</param>
    public static IReadOnlyList<Field> Filters(Type type, object values, IReadOnlyList<string>? pick = null, IReadOnlyList<string>? omit = null,
        IReadOnlyDictionary<string, Func<SchemaField, Control>>? fields = null, FormSources? sources = null) =>
    [
        .. SchemaModel.Pick(SchemaModel.Of(type), pick, omit)
            .Where(field => field.Control is not (SchemaControl.Object or SchemaControl.List or SchemaControl.Table or SchemaControl.People or SchemaControl.File))
            .Select(field =>
            {
                if (fields?.GetValueOrDefault(field.Key) is { } custom)
                    return new Field { Label = field.Label, IsLabelHidden = field.Control == SchemaControl.Checkbox, Description = field.Description, Content = custom(field) };
                var control = ControlOf(field, sources ?? new FormSources());
                if (control is TextInputBase text and not NumberField) text.Clearable = true;
                if (control is SelectBase select) select.Clearable = true;
                if (Field.Bound.ValueProperty(control, BoundPropertyOf(field.Control)) is { } property)
                {
                    control.SetValue(property, FormController.Convert(FormController.Read(values, field.Key), property.PropertyType) ?? (property.PropertyType == typeof(string) ? "" : null));
                    control.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == property) FormController.Write(values, field.Key, e.NewValue);
                    };
                }
                return new Field { Label = field.Label, IsLabelHidden = field.Control == SchemaControl.Checkbox, Description = field.Description, Content = control };
            }),
    ];

    /// <summary>按字段的描述选控件（上游 controlOf）。</summary>
    internal static Control ControlOf(SchemaField field, FormSources sources)
    {
        // 选项来自登记的来源：固定的列表直接当选项，远程搜索交给 Select 的 Load
        var source = field.Source is { } name ? sources.Options.GetValueOrDefault(name) : null;
        var options = (source?.Items ?? field.Options ?? []).Select(option => new ListboxOption(option.Value, option.Label)).ToList();
        OptionsLoader? load = source?.Search is { } search
            ? async (query, _, cancellation) => new OptionsPage([.. (await search(query, cancellation)).Select(option => (object?)new ListboxOption(option.Value, option.Label))])
            : null;
        switch (field.Control)
        {
            case SchemaControl.People:
                return new PeoplePicker { Source = sources.People ?? throw new InvalidOperationException($"字段 {field.Key} 是人员控件，要在 Provider.FormSources 或 Sources 里给 People"), Placeholder = field.Placeholder };
            case SchemaControl.File:
                return new Upload { Multiple = true, Uploader = sources.Upload };
            case SchemaControl.Textarea:
                return new Textarea { PlaceholderText = field.Placeholder, Rows = 3 };
            case SchemaControl.Number:
                return new NumberField { PlaceholderText = field.Placeholder, Suffix = field.Unit };
            case SchemaControl.Switch:
                return new Switch();
            case SchemaControl.Checkbox:
                // 单个复选框的文字就是标签（字段的标签只给读屏）
                return new Checkbox { Content = field.Label };
            case SchemaControl.Select or SchemaControl.Multiselect:
                var select = new Select { ItemsSource = options, Placeholder = field.Placeholder, SelectionMode = field.Control == SchemaControl.Multiselect ? SelectionMode.Multiple : SelectionMode.Single };
                if (load is not null) (select.Searchable, select.Load) = (true, load);
                return select;
            case SchemaControl.Radio:
                return new RadioGroup { ItemsSource = options, Orientation = Orientation.Horizontal };
            case SchemaControl.Checkboxes:
                return new CheckboxGroup { ItemsSource = options, Orientation = Orientation.Horizontal };
            case SchemaControl.Date:
                return new DatePicker();
            case SchemaControl.Datetime:
                return new DatePicker { Granularity = DateFieldGranularity.Minute };
            default:
                return new TextField { PlaceholderText = field.Placeholder };
        }
    }

    /// <summary>添加一行的按钮（标题旁）。</summary>
    Button AddButton(SchemaField field, string path)
    {
        var add = new Button { Name = SchemaFormParts.AddRow, Size = TextSize.Sm, Variant = ButtonVariant.Soft, Icon = Lucide.Plus, Content = Provider.GetLang(this).FormAddRow(field.Label) };
        add.Click += (_, _) =>
        {
            focusRowOf = path;
            controller!.Array(path).Append(SchemaModel.NewItem(field));
        };
        return add;
    }

    IconButton RemoveButton(SchemaField field, string path, int index)
    {
        var remove = new IconButton { Name = SchemaFormParts.RemoveRow, Icon = Lucide.Trash, Variant = ButtonVariant.Ghost, Tone = StatusTone.Danger };
        AutomationProperties.SetName(remove, Provider.GetLang(this).FormRemoveRow(field.Label, index + 1));
        remove.Click += (_, _) =>
        {
            // 删掉之后焦点到相邻的行，没有行了到"添加"按钮（不然焦点掉回窗口开头）
            var count = Count(controller!, path);
            focusRowOf = null;
            controller!.Array(path).Remove(index);
            Dispatcher.UIThread.Post(() => FocusRow(path, Math.Min(index, count - 2)), DispatcherPriority.Loaded);
        };
        return remove;
    }

    /// <summary>对象的列表：每一行是一组字段加删除按钮，标题旁是添加按钮；只读时不能增删行。</summary>
    FormSection Rows(SchemaField field, string path, bool readOnly)
    {
        var count = Count(controller!, path);
        counts[path] = count;
        var list = new Flex { Name = SchemaFormParts.List, Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch, Tag = path };
        for (var index = 0; index < count; index++)
        {
            var fields = Section(field.Fields ?? [], $"{path}.{index}.", null);
            Flex.SetGrow(fields, true);
            var row = new Flex { Name = SchemaFormParts.Row, Align = FlexAlign.Start, Gap = Space.Sm, Children = { fields } };
            if (!readOnly)
            {
                var remove = RemoveButton(field, path, index);
                // 删除按钮与第一行输入框对齐：让开标签的高度
                remove.Margin = new Thickness(0, 1.75 * 14, 0, 0);
                row.Children.Add(remove);
            }
            list.Children.Add(row);
        }
        return new FormSection
        {
            Name = SchemaFormParts.Rows, Title = field.Label, Description = field.Description, Tag = path,
            Extra = readOnly ? null : AddButton(field, path),
            Children = { new GridItem { Span = GridSpan.Full, Child = list } },
        };
    }

    /// <summary>
    /// 明细表：一行一条、一列一个字段（格子里是去掉标签的控件，标签留给读屏），行末是删除按钮，标题旁是添加按钮；列写了 Sum 时表底是合计行。
    /// 整列都不显示（权限、条件）时不出这一列；只读时不能增删行。
    /// </summary>
    FormSection TableOf(SchemaField field, string path, bool readOnly)
    {
        var form = controller!;
        var lang = Provider.GetLang(this);
        var count = Count(form, path);
        counts[path] = count;
        var columns = VisibleColumns(form, field, path).ToList();
        TableAlign AlignOf(SchemaField column) => column.Sum ? TableAlign.End : TableAlign.Start;
        var head = new TableRow();
        foreach (var column in columns) head.Cells.Add(new TableHeaderCell { Align = AlignOf(column), Content = column.Label });
        if (!readOnly)
        {
            var actions = new TableHeaderCell();
            AutomationProperties.SetName(actions, lang.FormRowActions);
            head.Cells.Add(actions);
        }
        var body = new TableBody();
        if (count == 0) body.Rows.Add(new TableRow { Cells = { new TableCell { ColumnSpan = columns.Count + (readOnly ? 0 : 1), Content = lang.TableEmpty } } });
        for (var index = 0; index < count; index++)
        {
            var row = new TableRow();
            foreach (var column in columns)
            {
                var cellPath = $"{path}.{index}.{column.Key}";
                var cell = new TableCell { Name = SchemaFormParts.TableCell, MinWidth = 8 * 14, VerticalContentAlignment = VerticalAlignment.Top };
                var item = FieldOf(column, cellPath, $"{column.Label}（{lang.FormRowLabel(field.Label, index + 1)}）", labelHidden: true);
                cell.Content = item;
                entries.Add((cellPath, item, item, column));
                row.Cells.Add(cell);
            }
            if (!readOnly) row.Cells.Add(new TableCell { Content = RemoveButton(field, path, index) });
            body.Rows.Add(row);
        }
        var table = new Table { Name = SchemaFormParts.Table, Sections = { new TableHead { Rows = { head } }, body } };
        AutomationProperties.SetName(table, field.Label);
        if (count > 0 && columns.Any(column => column.Sum))
        {
            var rows = form.GetValue(path) as IEnumerable ?? Array.Empty<object>();
            var total = new TableRow();
            for (var at = 0; at < columns.Count; at++)
                total.Cells.Add(new TableCell
                {
                    Name = SchemaFormParts.TableTotal, Align = AlignOf(columns[at]), FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                    Content = SchemaModel.ColumnTotal(columns[at], rows) ?? (at == 0 ? lang.TableSummary : null),
                });
            if (!readOnly) total.Cells.Add(new TableCell());
            table.Sections.Add(new TableFoot { Rows = { total } });
        }
        return new FormSection
        {
            Name = SchemaFormParts.Rows, Title = field.Label, Description = field.Description, Tag = path,
            Extra = readOnly ? null : AddButton(field, path),
            Children = { new GridItem { Span = GridSpan.Full, Child = table } },
        };
    }

    /// <summary>按权限与条件就地改显示、只读、必填（值变了时）；明细表的合计跟着改。</summary>
    void Sync()
    {
        if (controller is not { } form) return;
        foreach (var (path, host, field, schema) in entries)
        {
            var state = form.FieldState(path);
            host.IsVisible = state.Visible;
            if (field is null) continue;
            if (state.Readonly) field.IsReadOnly = true;
            else field.ClearValue(Field.IsReadOnlyProperty);
            // 不可空的选择、数字、日期也是必填（同上游 zod 的非可选字段），标出星号
            if (state.Required || schema.Required && !state.Readonly) field.IsRequired = true;
            else field.ClearValue(Field.IsRequiredProperty);
        }
        // 明细表的合计跟着值改
        foreach (var table in root.GetVisualDescendants().OfType<Table>().Where(table => table.Name == SchemaFormParts.Table))
            if (table.Sections.OfType<TableFoot>().FirstOrDefault() is { } foot && table.FindAncestorOfType<FormSection>()?.Tag is string path && FieldAt(form, path) is { } field)
            {
                var rows = form.GetValue(path) as IEnumerable ?? Array.Empty<object>();
                foreach (var (cell, column) in ((TableRow)foot.Rows[0]).Cells.OfType<TableCell>().Zip(VisibleColumns(form, field, path)))
                    if (column.Sum) cell.Content = SchemaModel.ColumnTotal(column, rows);
            }
    }

    static IEnumerable<SchemaField> VisibleColumns(IFormController form, SchemaField field, string path)
    {
        var count = Count(form, path);
        return (field.Fields ?? []).Where(child => count == 0 || Enumerable.Range(0, count).Any(index => form.FieldState($"{path}.{index}.{child.Key}").Visible));
    }

    /// <summary>按路径找字段的描述（嵌套对象、列表里的也找）。</summary>
    static SchemaField? FieldAt(IFormController form, string path)
    {
        IReadOnlyList<SchemaField> level = SchemaModel.Of(form.ModelType);
        SchemaField? found = null;
        foreach (var key in path.Split('.'))
        {
            if (int.TryParse(key, out _)) continue;
            found = level.FirstOrDefault(field => field.Key == key);
            if (found is null) return null;
            level = found.Fields ?? [];
        }
        return found;
    }

    /// <summary>加了一行：焦点到新行的第一个能聚焦的控件上，接着就能填。</summary>
    void FocusLastRow(string path) => FocusRow(path, Count(controller!, path) - 1);

    /// <summary>焦点到某一行的第一个控件；行号小于 0 时到"添加"按钮。</summary>
    void FocusRow(string path, int index)
    {
        var section = root.GetVisualDescendants().OfType<FormSection>().FirstOrDefault(item => item.Name == SchemaFormParts.Rows && item.Tag as string == path);
        if (section is null) return;
        if (index < 0)
        {
            section.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == SchemaFormParts.AddRow)?.Focus(NavigationMethod.Tab);
            return;
        }
        var prefix = $"{path}.{index}.";
        var target = section.GetVisualDescendants().OfType<Field>().FirstOrDefault(field => field.Path?.StartsWith(prefix, StringComparison.Ordinal) == true);
        target?.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(element => element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled)?.Focus(NavigationMethod.Tab);
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

/// <summary>
/// 按模型生成的整份表单（vue-tsx-pro 的 SchemaForm）：错误汇总、字段（<see cref="SchemaFields"/>）、最后一行是重置与提交按钮，<see cref="Content"/> 在这一行的起始边（提交结果、说明）。
/// <code>
/// new SchemaForm { Controller = new FormController&lt;Apply&gt;(new Apply()) { OnSubmit = api.Apply }, Columns = 2, SubmitText = "提交申请" }
/// </code>
/// 字段的写法同 SchemaFields（属性的类型、Display 与 SchemaField 特性）。部件：SchemaFields 的部件，加上 <see cref="SchemaFormParts.Form"/> 等。
/// </summary>
public class SchemaForm : Control
{
    /// <summary>表单控制器（FormController）。</summary>
    public static readonly StyledProperty<IFormController?> ControllerProperty = AvaloniaProperty.Register<SchemaForm, IFormController?>(nameof(Controller));

    /// <summary>提交按钮的文字，默认取语言包（"提交"）。</summary>
    public static readonly StyledProperty<string?> SubmitTextProperty = AvaloniaProperty.Register<SchemaForm, string?>(nameof(SubmitText));

    /// <summary>显示重置按钮（回到初始值），默认 true。</summary>
    public static readonly StyledProperty<bool> ResettableProperty = AvaloniaProperty.Register<SchemaForm, bool>(nameof(Resettable), true);

    /// <summary>按钮那一行起始边的内容：提交结果、说明（读屏会念出变化）。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<SchemaForm, object?>(nameof(Content));

    readonly Button reset = new() { Name = SchemaFormParts.Reset, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral };
    readonly Button submit = new() { Name = SchemaFormParts.Submit };
    readonly ContentControl start = new() { Name = SchemaFormParts.ActionsStart, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>新建：错误汇总、字段、按钮那一行。</summary>
    public SchemaForm()
    {
        FieldsView = new SchemaFields();
        Form = new Form { Name = SchemaFormParts.Form };
        Flex.SetGrow(start, true);
        AutomationProperties.SetLiveSetting(start, global::Avalonia.Automation.AutomationLiveSetting.Polite);
        start[!TextElement.ForegroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.TextMuted).ToBinding();
        var actions = new Flex { Name = SchemaFormParts.Actions, Align = FlexAlign.Center, Wrap = true, Gap = Space.Sm, Children = { start, reset, submit } };
        Form.Child = new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch, Children = { new FormErrorSummary(), FieldsView, actions } };
        reset.Command = Form.ResetCommand;
        submit.Command = Form.SubmitCommand;
        LogicalChildren.Add(Form);
        VisualChildren.Add(Form);
        SyncText();
    }

    /// <summary>表单控制器。</summary>
    public IFormController? Controller { get => GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }

    /// <summary>提交按钮的文字。</summary>
    public string? SubmitText { get => GetValue(SubmitTextProperty); set => SetValue(SubmitTextProperty, value); }

    /// <summary>显示重置按钮。</summary>
    public bool Resettable { get => GetValue(ResettableProperty); set => SetValue(ResettableProperty, value); }

    /// <summary>按钮那一行起始边的内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>生成的字段（上游 SchemaForm 的 SchemaFields 那部分属性写在它上面：Pick、Omit、Fields、Columns、Sources）。</summary>
    public SchemaFields FieldsView { get; }

    /// <summary>表单（上游的 propsForm）：标签的位置、宽度等写在它上面。</summary>
    public Form Form { get; }

    /// <summary>最多几栏（同 SchemaFields.Columns）。</summary>
    public int Columns { get => FieldsView.Columns; set => FieldsView.Columns = value; }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ControllerProperty)
        {
            if (change.OldValue is IFormController old) old.Changed -= OnChanged;
            Form.Controller = Controller;
            if (Controller is { } next) next.Changed += OnChanged;
            OnChanged(null, EventArgs.Empty);
        }
        else if (change.Property == SubmitTextProperty || change.Property == ResettableProperty || change.Property == Provider.LangProperty) SyncText();
        else if (change.Property == ContentProperty) start.Content = Content;
    }

    void OnChanged(object? sender, EventArgs e) => submit.IsLoading = Controller?.IsSubmitting ?? false;

    void SyncText()
    {
        var lang = Provider.GetLang(this);
        reset.Content = lang.FormReset;
        reset.IsVisible = Resettable;
        submit.Content = SubmitText ?? lang.FormSubmit;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        Form.Measure(availableSize);
        return Form.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        Form.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>SchemaFields、SchemaForm 的部件名。</summary>
public static class SchemaFormParts
{
    /// <summary>每一组（FormSection；TS 的 propsSection）。</summary>
    public const string Section = "PART_Section";

    /// <summary>列表、明细表所在的一组（FormSection）。</summary>
    public const string Rows = "PART_Rows";

    /// <summary>列表的各行（Flex）。</summary>
    public const string List = "PART_List";

    /// <summary>列表的一行：字段与删除按钮（Flex）。</summary>
    public const string Row = "PART_Row";

    /// <summary>"添加"（Button；TS 的 propsAddRow）。</summary>
    public const string AddRow = "PART_AddRow";

    /// <summary>每一行的删除（IconButton；TS 的 propsRemoveRow）。</summary>
    public const string RemoveRow = "PART_RemoveRow";

    /// <summary>明细表（Table）。</summary>
    public const string Table = "PART_Table";

    /// <summary>明细表的格（TableCell）。</summary>
    public const string TableCell = "PART_TableCell";

    /// <summary>明细表合计行的格（TableCell，半粗）。</summary>
    public const string TableTotal = "PART_TableTotal";

    /// <summary>标签页里的内容（Flex）。</summary>
    public const string TabPanel = "PART_TabPanel";

    /// <summary>表单（Form；TS 的 propsForm）。</summary>
    public const string Form = "PART_Form";

    /// <summary>按钮那一行（Flex；TS 的 propsActions）。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>按钮那一行起始边的内容（ContentControl）。</summary>
    public const string ActionsStart = "PART_ActionsStart";

    /// <summary>提交（Button；TS 的 propsSubmit）。</summary>
    public const string Submit = "PART_Submit";

    /// <summary>重置（Button；TS 的 propsReset）。</summary>
    public const string Reset = "PART_Reset";
}
