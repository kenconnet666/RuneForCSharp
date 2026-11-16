using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Rune.Avalonia.Tests;

/// <summary>
/// DataTable 的单元格导航与编辑（上游 cell-editing.test）：有可编辑的列时方向键在格之间走、只有一格能 Tab 进入；Enter 开始编辑、Enter 提交、Esc 放弃、
/// Tab 提交并到下一个可编辑的格、直接打字替换原值；列的 Validate 与 CellChange 返回的原因不提交并显示；ValidateRow 标在格上。
/// </summary>
public class DataTableEditingTests
{
    record Line(string Id, string Part, decimal Quantity, string Start, string End);

    sealed class Setup
    {
        public List<Line> Lines = [new("a", "支架", 10, "2026-10-01", "2026-10-20"), new("b", "底座", 4, "2026-10-10", "2026-10-05")];
        public readonly List<(object Row, string Key, object? Value)> Calls = [];
        public DataTable Table = null!;
        public Window Window = null!;

        public TableCell Cell(string row, string column) => Table.Surface.Realized.Values.Single(view => ((Line)view.Row!).Id == row).CellOf(column)!;

        public CellEditorHost? Host(string row, string column) => Cell(row, column).Content as CellEditorHost;
    }

    static Setup Render(Func<Setup, Func<object, string, object?, Task<string?>>>? change = null, EditTrigger trigger = EditTrigger.DoubleClick)
    {
        var setup = new Setup();
        DataTableColumn[] columns =
        [
            new("Id", "编号"),
            new("Part", "零件") { Editable = true },
            new("Quantity", "数量") { Type = ColumnType.Number, Editable = true, Validate = (value, _) => value is decimal number && number > 0 ? null : "数量要大于 0" },
            new("End", "交期") { Editable = true },
        ];
        var table = new DataTable
        {
            ItemsSource = setup.Lines, Columns = columns, GetKey = row => ((Line)row).Id, EditTrigger = trigger, Width = 640, Height = 300,
            ValidateRow = row => string.CompareOrdinal(((Line)row).End, ((Line)row).Start) < 0 ? new Dictionary<string, string> { ["End"] = "交期早于开工" } : null,
        };
        table.CellChange = change?.Invoke(setup) ?? ((row, key, value) =>
        {
            setup.Calls.Add((row, key, value));
            setup.Lines = [.. setup.Lines.Select(line => !ReferenceEquals(line, row) ? line : key switch
            {
                "Part" => line with { Part = (string)value! },
                "Quantity" => line with { Quantity = (decimal)value! },
                _ => line with { End = (string)value! },
            })];
            table.ItemsSource = setup.Lines;
            return Task.FromResult<string?>(null);
        });
        setup.Table = table;
        setup.Window = HeadlessUi.Show(table);
        HeadlessUi.Flush(setup.Window);
        return setup;
    }

    static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = target });

    static Point Center(Control control, Window window) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    static void Click(Setup setup, Control control)
    {
        var at = Center(control, setup.Window);
        setup.Window.MouseDown(at, MouseButton.Left);
        setup.Window.MouseUp(at, MouseButton.Left);
        HeadlessUi.Flush(setup.Window);
    }

    [Fact]
    [Upstream("DataTable 的单元格编辑", "有可编辑的列：role=\"grid\"，只有一格能 Tab 进入，方向键在格之间走；validateRow 的错误标在格上")]
    public Task Grid_navigation_and_row_errors() => HeadlessUi.Run(() =>
    {
        var setup = Render();
        Assert.True(setup.Table.GridMode);
        var cells = setup.Table.Surface.Realized.Values.SelectMany(row => row.Cells.OfType<TableCell>()).ToList();
        Assert.Single(cells, cell => KeyboardNavigation.GetIsTabStop(cell));
        setup.Cell("a", "Id").Focus();
        Press(setup.Cell("a", "Id"), Key.Right);
        HeadlessUi.Flush(setup.Window);
        Assert.True(setup.Cell("a", "Part").IsFocused);
        Press(setup.Cell("a", "Part"), Key.Down);
        HeadlessUi.Flush(setup.Window);
        Assert.True(setup.Cell("b", "Part").IsFocused);
        Assert.Single(setup.Table.Surface.Realized.Values.SelectMany(row => row.Cells.OfType<TableCell>()), cell => KeyboardNavigation.GetIsTabStop(cell));
        Assert.Contains("invalid", setup.Cell("b", "End").Classes);
        Assert.Equal("交期早于开工", ToolTip.GetTip(setup.Cell("b", "End")));
        Assert.DoesNotContain("invalid", setup.Cell("a", "End").Classes);
    });

    [Fact]
    [Upstream("DataTable 的单元格编辑", "Enter 开始编辑、Enter 提交写回、焦点回到格上；Esc 放弃")]
    public Task Enter_edits_and_commits_escape_cancels() => HeadlessUi.Run(() =>
    {
        var setup = Render();
        setup.Cell("a", "Part").Focus();
        Press(setup.Cell("a", "Part"), Key.Enter);
        HeadlessUi.Flush(setup.Window);
        var editor = Assert.IsType<TextField>(setup.Host("a", "Part")!.Editor);
        Assert.Equal("支架", editor.Text);
        // 开始编辑后焦点在编辑器里
        Assert.True(editor.IsKeyboardFocusWithin);
        editor.Text = "加强支架";
        Press(editor, Key.Enter);
        HeadlessUi.Flush(setup.Window);
        var call = Assert.Single(setup.Calls);
        Assert.Equal(("a", "Part", (object?)"加强支架"), (((Line)call.Row).Id, call.Key, call.Value));
        Assert.Equal("加强支架", setup.Lines[0].Part);
        Assert.Null(setup.Host("a", "Part"));
        Assert.True(setup.Cell("a", "Part").IsFocused);

        Press(setup.Cell("a", "Part"), Key.F2);
        HeadlessUi.Flush(setup.Window);
        Press(setup.Host("a", "Part")!.Editor, Key.Escape);
        HeadlessUi.Flush(setup.Window);
        Assert.Null(setup.Host("a", "Part"));
        Assert.Single(setup.Calls);
    });

    [Fact]
    [Upstream("DataTable 的单元格编辑", "直接打字替换原值；列的 validate 不通过时不提交并显示原因；Tab 提交并到下一个可编辑的格")]
    public Task Typing_validation_and_tab() => HeadlessUi.Run(() =>
    {
        var setup = Render();
        var cell = setup.Cell("a", "Quantity");
        cell.Focus();
        cell.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "0", Source = cell });
        HeadlessUi.Flush(setup.Window);
        var host = setup.Host("a", "Quantity")!;
        var number = Assert.IsType<NumberField>(host.Editor);
        Assert.Equal(0m, number.Value);
        Press(number, Key.Enter);
        HeadlessUi.Flush(setup.Window);
        Assert.Empty(setup.Calls);
        Assert.Equal("数量要大于 0", host.Error);

        number.Value = 12;
        Press(number, Key.Tab);
        HeadlessUi.Flush(setup.Window);
        Assert.Equal(("Quantity", (object?)12m), (setup.Calls.Single().Key, setup.Calls.Single().Value));
        // 下一个可编辑的是交期：直接进入编辑
        Assert.NotNull(setup.Host("a", "End"));
    });

    [Fact]
    [Upstream("DataTable 的单元格编辑", "editTrigger=\"click\"：单击进入编辑，双击不再单独开始；点在编辑器里不重新开始")]
    public Task Click_trigger_starts_editing() => HeadlessUi.Run(() =>
    {
        var setup = Render(trigger: EditTrigger.Click);
        Click(setup, setup.Cell("a", "Part"));
        var editor = Assert.IsType<TextField>(setup.Host("a", "Part")!.Editor);
        editor.Text = "改了";
        Click(setup, editor);
        // 草稿还在：点在编辑器里没有重新开始
        Assert.Equal("改了", Assert.IsType<TextField>(setup.Host("a", "Part")!.Editor).Text);
        // 不可编辑的格单击没有反应
        Click(setup, setup.Cell("a", "Id"));
        Assert.Null(setup.Host("a", "Id"));
    });

    [Fact]
    [Upstream("DataTable 的单元格编辑", "默认双击才进入编辑，单击不开始")]
    public Task Double_click_starts_editing_by_default() => HeadlessUi.Run(() =>
    {
        var setup = Render();
        Click(setup, setup.Cell("a", "Part"));
        Assert.Null(setup.Host("a", "Part"));
        var at = Center(setup.Cell("a", "Part"), setup.Window);
        setup.Window.MouseDown(at, MouseButton.Left);
        setup.Window.MouseUp(at, MouseButton.Left);
        setup.Window.MouseDown(at, MouseButton.Left);
        setup.Window.MouseUp(at, MouseButton.Left);
        HeadlessUi.Flush(setup.Window);
        Assert.NotNull(setup.Host("a", "Part"));
    });

    [Fact]
    [Upstream("DataTable 的单元格编辑", "保存的时候点开了别的格：保存完不把新打开的编辑器收掉")]
    public Task Saving_does_not_close_another_editor() => HeadlessUi.Run(() =>
    {
        var finish = new TaskCompletionSource<string?>();
        var calls = 0;
        var setup = Render(_ => (_, _, _) =>
        {
            calls++;
            return finish.Task;
        }, EditTrigger.Click);
        Click(setup, setup.Cell("a", "Part"));
        Press(setup.Host("a", "Part")!.Editor, Key.Enter);
        HeadlessUi.Flush(setup.Window);
        Assert.Equal(1, calls);
        Click(setup, setup.Cell("b", "Part"));
        Assert.NotNull(setup.Host("b", "Part"));
        finish.SetResult(null);
        HeadlessUi.Flush(setup.Window);
        Assert.NotNull(setup.Host("b", "Part"));
    });

    [Fact]
    [Upstream("DataTable 的单元格编辑", "onCellChange 返回原因：编辑器留着并显示它")]
    public Task Rejected_change_keeps_the_editor() => HeadlessUi.Run(() =>
    {
        var setup = Render(_ => (_, _, _) => Task.FromResult<string?>("这个零件已经下单，不能改名"));
        setup.Cell("a", "Part").Focus();
        Press(setup.Cell("a", "Part"), Key.Enter);
        HeadlessUi.Flush(setup.Window);
        Press(setup.Host("a", "Part")!.Editor, Key.Enter);
        HeadlessUi.Flush(setup.Window);
        Assert.NotNull(setup.Host("a", "Part"));
        Assert.Equal("这个零件已经下单，不能改名", setup.Host("a", "Part")!.Error);
    });
}
