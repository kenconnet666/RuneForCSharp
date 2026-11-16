using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>怎样开始编辑一格（键盘的 Enter、F2、直接打字都能开始）。</summary>
public enum EditTrigger
{
    /// <summary>双击（默认）：单击只是选中这一格，适合以查看为主、偶尔改一下的表。</summary>
    DoubleClick,

    /// <summary>单击：适合主要用来改数据的表。</summary>
    Click,
}

/// <summary>DataTable 的单元格导航与就地编辑（上游 DataTable 的 editing 部分与 DataTableView 的 gridFocus）。</summary>
public partial class DataTable
{
    /// <summary>写回编辑：参数是行、列的 Key、新值；返回原因（非空文字）就不收起编辑器、显示它；返回的任务进行中算保存中。</summary>
    public static readonly StyledProperty<Func<object, string, object?, Task<string?>>?> CellChangeProperty =
        AvaloniaProperty.Register<DataTable, Func<object, string, object?, Task<string?>>?>(nameof(CellChange));

    /// <summary>整行校验：返回列的 Key → 原因，标在对应的格上（红色内描边，悬停看原因）。</summary>
    public static readonly StyledProperty<Func<object, IReadOnlyDictionary<string, string>?>?> ValidateRowProperty =
        AvaloniaProperty.Register<DataTable, Func<object, IReadOnlyDictionary<string, string>?>?>(nameof(ValidateRow));

    /// <summary>单元格导航：方向键在格之间走，只有一格能 Tab 进入；不写时有可编辑的列就开。</summary>
    public static readonly StyledProperty<bool?> CellNavigationProperty = AvaloniaProperty.Register<DataTable, bool?>(nameof(CellNavigation));

    /// <summary>怎样开始编辑，默认双击。</summary>
    public static readonly StyledProperty<EditTrigger> EditTriggerProperty = AvaloniaProperty.Register<DataTable, EditTrigger>(nameof(EditTrigger));

    /// <summary>写回编辑。</summary>
    public Func<object, string, object?, Task<string?>>? CellChange { get => GetValue(CellChangeProperty); set => SetValue(CellChangeProperty, value); }

    /// <summary>整行校验。</summary>
    public Func<object, IReadOnlyDictionary<string, string>?>? ValidateRow { get => GetValue(ValidateRowProperty); set => SetValue(ValidateRowProperty, value); }

    /// <summary>单元格导航。</summary>
    public bool? CellNavigation { get => GetValue(CellNavigationProperty); set => SetValue(CellNavigationProperty, value); }

    /// <summary>怎样开始编辑。</summary>
    public EditTrigger EditTrigger { get => GetValue(EditTriggerProperty); set => SetValue(EditTriggerProperty, value); }

    /// <summary>编辑中的格：行的 key、列、草稿、校验没过的原因、正在保存。</summary>
    internal sealed record EditState(object Key, string Column, object? Draft, string? Error = null, bool Saving = false);

    /// <summary>正在编辑的格（测试用）。</summary>
    internal EditState? Editing { get; private set; }

    /// <summary>能 Tab 进入的那一格（行的 key、列）；没走过时是第一行第一列。</summary>
    internal (object Key, string Column)? Active { get; private set; }

    // 正在编辑的格里的编辑器外壳（校验的原因显示在它下面）
    CellEditorHost? editorHost;

    /// <summary>表格是网格：方向键在格之间走（写了 CellNavigation 时按它，否则有可编辑的列就是）。</summary>
    internal bool GridMode => CellNavigation ?? Columns.Any(column => column.Editable || column.IsEditable is not null);

    internal bool IsActiveCell(object key, string column) =>
        Active is { } active ? Equals(active.Key, key) && active.Column == column : View.Count > 0 && Equals(KeyOf(View[0]), key) && Shown.Count > 0 && Shown[0].Key == column;

    internal bool IsEditing(object key, string column) => Editing is { } editing && Equals(editing.Key, key) && editing.Column == column;

    /// <summary>表体的格：网格时可以聚焦、只有一格是 Tab 停靠点、方向键走格、开始编辑；校验没过的格标出原因；编辑中的格换成编辑器。</summary>
    internal void PrepareCell(TableCell cell, object row, DataTableColumn column)
    {
        if (!GridMode) return;
        var key = KeyOf(row);
        cell.Focusable = true;
        KeyboardNavigation.SetIsTabStop(cell, IsActiveCell(key, column.Key));
        // 单击就能编辑的格：文字光标提示能改
        if (EditTrigger == EditTrigger.Click && TableColumns.IsCellEditable(column, row)) cell.Cursor = new Cursor(StandardCursorType.Ibeam);
        if (ValidateRow?.Invoke(row) is { } errors && errors.TryGetValue(column.Key, out var message))
        {
            cell.Classes.Add("invalid");
            ToolTip.SetTip(cell, message);
            AutomationProperties.SetHelpText(cell, message);
        }
        if (IsEditing(key, column.Key))
        {
            cell.Classes.Add("editing");
            editorHost = new CellEditorHost(this, row, column, Editing!);
            cell.Content = editorHost;
        }
        cell.GotFocus += (_, e) =>
        {
            if (e.Source != cell) return;
            Active = (key, column.Key);
            surface.SyncTabStops();
            (cell.GetVisualParent() as Visual)?.InvalidateVisual();
        };
        cell.LostFocus += (_, _) => (cell.GetVisualParent() as Visual)?.InvalidateVisual();
        cell.KeyDown += (_, e) => OnCellKeyDown(e, cell, row, column);
        cell.TextInput += (_, e) =>
        {
            // 直接打字：用打的字替换原值开始编辑
            if (e.Source != cell || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
            e.Handled = StartEdit(row, column, e.Text);
        };
    }

    /// <summary>
    /// 按下了一格（行按位置找到的：格子透明，点在文字以外的地方时命中的是行）：聚焦它，单击编辑时开始编辑；点在编辑中的格里（编辑器）不重新开始。
    /// </summary>
    internal void CellPressed(TableCell cell, object row, DataTableColumn column)
    {
        if (!GridMode || IsEditing(KeyOf(row), column.Key)) return;
        cell.Focus(NavigationMethod.Pointer);
        if (EditTrigger == EditTrigger.Click) StartEdit(row, column);
    }

    /// <summary>双击了一格：双击编辑时开始编辑。</summary>
    internal void CellDoubleTapped(object row, DataTableColumn column)
    {
        if (GridMode && EditTrigger == EditTrigger.DoubleClick && !IsEditing(KeyOf(row), column.Key)) StartEdit(row, column);
    }

    void OnCellKeyDown(KeyEventArgs e, TableCell cell, object row, DataTableColumn column)
    {
        if (e.Source != cell || e.Handled) return;
        var (rowStep, columnStep) = e.Key switch
        {
            Key.Up => (-1, 0),
            Key.Down => (1, 0),
            Key.Left => (0, FlowDirection == FlowDirection.RightToLeft ? 1 : -1),
            Key.Right => (0, FlowDirection == FlowDirection.RightToLeft ? -1 : 1),
            _ => (0, 0),
        };
        if (rowStep != 0 || columnStep != 0)
        {
            var rowIndex = View.ToList().IndexOf(row) + rowStep;
            var columnIndex = Shown.ToList().IndexOf(column) + columnStep;
            if (rowIndex >= 0 && rowIndex < View.Count && columnIndex >= 0 && columnIndex < Shown.Count) surface.FocusCell(rowIndex, Shown[columnIndex].Key);
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.F2) e.Handled = StartEdit(row, column);
    }

    /// <summary>开始编辑一格：草稿是原值，直接打字时是打的字（数字列只认数字）；编辑器建好后焦点移进去。</summary>
    internal bool StartEdit(object row, DataTableColumn column, string? text = null)
    {
        if (!TableColumns.IsCellEditable(column, row)) return false;
        var numeric = TableColumns.AlignOf(TableColumns.TypeOf(column, View)) == TableAlign.End && column.Statuses is null;
        object? typed = text is null ? null : numeric ? TableColumns.Numeric(text) : text;
        Editing = new EditState(KeyOf(row), column.Key, typed ?? TableColumns.ValueOf(column, row));
        Active = (Editing.Key, column.Key);
        surface.RebuildRow(Editing.Key);
        Dispatcher.UIThread.Post(() => editorHost?.FocusEditor(), DispatcherPriority.Loaded);
        return true;
    }

    /// <summary>放弃：回到原来的值，焦点回到格上。</summary>
    internal void CancelEdit()
    {
        if (Editing is not { } current) return;
        Editing = null;
        editorHost = null;
        surface.RebuildRow(current.Key);
        surface.FocusCell(current.Key, current.Column);
    }

    internal void ChangeDraft(object? value)
    {
        if (Editing is null) return;
        Editing = Editing with { Draft = value, Error = null };
        editorHost?.ShowError(null);
    }

    /// <summary>提交：列的 Validate、CellChange 都接受了才收起；step 是提交后去哪一格（Tab 往后、Shift + Tab 往前），0 时焦点回到格上。</summary>
    internal async Task CommitEdit(int step = 0)
    {
        if (Editing is not { Saving: false } current || View.FirstOrDefault(item => Equals(KeyOf(item), current.Key)) is not { } row
            || Columns.FirstOrDefault(item => item.Key == current.Column) is not { } column) return;
        if (column.Validate?.Invoke(current.Draft, row) is { Length: > 0 } invalid)
        {
            Editing = current with { Error = invalid };
            editorHost?.ShowError(invalid);
            return;
        }
        Editing = current with { Saving = true };
        var result = CellChange is { } change ? await change(row, column.Key, current.Draft) : null;
        if (!string.IsNullOrEmpty(result))
        {
            Editing = current with { Error = result, Saving = false };
            editorHost?.ShowError(result);
            return;
        }
        // 保存的时候已经点开了别的格（点别处提交时常这样）：不动它
        if (Editing is { } now && (!Equals(now.Key, current.Key) || now.Column != current.Column)) return;
        Editing = null;
        editorHost = null;
        var next = step == 0 ? null : NeighbourEditable(current.Key, current.Column, step);
        surface.RebuildRow(current.Key);
        if (next is { } target) StartEdit(target.Row, target.Column);
        else surface.FocusCell(current.Key, current.Column);
    }

    /// <summary>这一行里往后（往前）的下一个可编辑的格。</summary>
    (object Row, DataTableColumn Column)? NeighbourEditable(object key, string column, int step)
    {
        if (View.FirstOrDefault(item => Equals(KeyOf(item), key)) is not { } row) return null;
        var from = Shown.ToList().FindIndex(item => item.Key == column);
        for (var index = from + step; index >= 0 && index < Shown.Count; index += step)
            if (TableColumns.IsCellEditable(Shown[index], row)) return (row, Shown[index]);
        return null;
    }

    /// <summary>默认的编辑器：状态列是下拉（选了就提交），数字类是数字框（不要步进按钮），其余是文字框；都是 sm、占满格子。</summary>
    internal Control DefaultEditor(object row, DataTableColumn column, EditState state)
    {
        var label = TableColumns.Label(column);
        Control editor;
        if (column.Statuses is { } statuses)
        {
            var select = new Select { Size = TextSize.Sm, ItemsSource = statuses.Options(), Value = state.Draft as string };
            select.PropertyChanged += (_, e) =>
            {
                if (e.Property != SelectBase.ValueProperty) return;
                ChangeDraft(select.Value);
                _ = CommitEdit();
            };
            editor = select;
        }
        else if (TableColumns.AlignOf(TableColumns.TypeOf(column, View)) == TableAlign.End)
        {
            var number = new NumberField { Size = TextSize.Sm, Stepper = false, Value = TableColumns.Numeric(state.Draft) };
            number.PropertyChanged += (_, e) =>
            {
                if (e.Property == NumberField.ValueProperty) ChangeDraft(number.Value);
            };
            editor = number;
        }
        else
        {
            var text = new TextField { Size = TextSize.Sm, Text = state.Draft is string or IFormattable ? Convert.ToString(state.Draft, System.Globalization.CultureInfo.CurrentCulture) : "" };
            text.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty) ChangeDraft(text.Text);
            };
            editor = text;
        }
        AutomationProperties.SetName(editor, label);
        return editor;
    }
}

/// <summary>
/// 编辑中的格里的外壳：编辑器占满格子；校验没过的原因浮在下面（danger 底、sm 字号，不撑高这一行，读屏立即念）。
/// Enter 提交、Tab 提交并到下一个可编辑的格、Esc 放弃；焦点离开这一格（点了别处）时提交。
/// </summary>
internal sealed class CellEditorHost : Control
{
    readonly DataTable owner;
    readonly Control editor;
    readonly Border error = new() { Name = DataTableParts.EditorError, IsVisible = false, ZIndex = 3 };
    readonly TextBlock errorText = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 20 * 16 };

    public CellEditorHost(DataTable owner, object row, DataTableColumn column, DataTable.EditState state)
    {
        this.owner = owner;
        var context = new CellEditorContext(row, state.Draft, owner.ChangeDraft, () => _ = owner.CommitEdit(), owner.CancelEdit, state.Error);
        editor = column.Editor?.Invoke(context) as Control ?? owner.DefaultEditor(row, column, state);
        editor.Name ??= DataTableParts.Editor;
        error.Child = errorText;
        AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Assertive);
        LogicalChildren.AddRange([editor, error]);
        VisualChildren.AddRange([editor, error]);
        // 编辑器先处理（数字框的回车先读进值、下拉的回车先打开列表），这里再看
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            // 重建时换下来的外壳不算；焦点还在这一格里（编辑器里的弹出层）不算
            if (!IsAttachedToVisualTree() || IsKeyboardFocusWithin || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused && this.IsVisualAncestorOf(focused)) return;
            // 只提交自己这一格：失焦是点了别的格时，那一格可能已经开始编辑了
            if (owner.IsEditing(state.Key, state.Column)) _ = owner.CommitEdit();
        });
        ShowError(state.Error);
    }

    bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    /// <summary>编辑器（测试用）。</summary>
    internal Control Editor => editor;

    /// <summary>校验没过的原因（测试用）；没有时 null。</summary>
    internal string? Error => error.IsVisible ? errorText.Text : null;

    internal void FocusEditor()
    {
        var target = editor.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(element => element.Focusable && element.IsEffectivelyVisible) ?? editor;
        target.Focus(NavigationMethod.Tab);
    }

    internal void ShowError(string? message)
    {
        error.IsVisible = !string.IsNullOrEmpty(message);
        errorText.Text = message;
        if (editor is TextInputBase input) input.IsInvalid = error.IsVisible;
        if (!error.IsVisible) return;
        error.Padding = new Thickness(0.5 * TextElement.GetFontSize(this), 0.25 * TextElement.GetFontSize(this));
        error.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * TextElement.GetFontSize(this));
        error[!Border.BackgroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.Danger).ToBinding();
        errorText[!TextBlock.ForegroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.DangerContrast).ToBinding();
        errorText[!TextBlock.FontSizeProperty] = this.GetResourceObservable(ThemeKeys.FontSize.Sm).ToBinding();
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // 下拉自己处理了的回车（打开列表）不当作提交；输入法组合中的键是给输入法的
        if (e.Key == Key.ImeProcessed || (e.Handled && e.Key == Key.Enter && editor is SelectBase)) return;
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                owner.CancelEdit();
                break;
            case Key.Enter:
                e.Handled = true;
                _ = owner.CommitEdit();
                break;
            case Key.Tab:
                e.Handled = true;
                _ = owner.CommitEdit(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                break;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        editor.Measure(availableSize);
        error.Measure(Size.Infinity);
        return editor.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        editor.Arrange(new Rect(finalSize));
        // 原因浮在格子下面 2px，按文字宽度，不被窄列挤成一字一行
        error.Arrange(new Rect(0, finalSize.Height + 2, error.DesiredSize.Width, error.DesiredSize.Height));
        return finalSize;
    }
}
