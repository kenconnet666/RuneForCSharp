using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>DataTable 的行拖动（上游 DataTable 的 onReorder 与 useSortable）。</summary>
public partial class DataTable
{
    SortableModel<object>? sorting;
    LiveRegion? announcer;
    // 指针拖动中的那一行的 key：指针捕获在表格上（拖动时行会重建，把手跟着换）
    string? pointerDrag;

    /// <summary>
    /// 拖动了行（上游 onReorder）：参数是新的顺序（全部的行），使用方把它写回数据。订阅了才有把手：第一个数据列前面，按下拖动，
    /// 或聚焦后空格拿起、↑↓ 移动、空格放下（Esc 取消）。排序、筛选时显示的顺序不是数据的顺序，这时没有把手。
    /// </summary>
    public event EventHandler<IReadOnlyList<object>>? Reordered
    {
        add
        {
            reordered += value;
            surface.Rebuild();
        }
        remove
        {
            reordered -= value;
            surface.Rebuild();
        }
    }

    EventHandler<IReadOnlyList<object>>? reordered;

    /// <summary>能拖动：订阅了 Reordered，没有排序，也没有表头筛选。</summary>
    internal bool CanReorder => reordered is not null && ActiveSorts.Count == 0 && Filters?.Values.Any(values => values.Count > 0) != true;

    /// <summary>拖动的模型（键盘、指针都交给它；数据立即写回）。</summary>
    SortableModel<object> Sorting => sorting ??= CreateSorting();

    SortableModel<object> CreateSorting()
    {
        var model = new SortableModel<object>(() => View, row => KeyText(row), () => Lang)
        {
            GetLabel = row => DataTableRowView.LabelOf(this, row),
            Reordered = (next, _) => reordered?.Invoke(this, next),
        };
        model.Changed += () =>
        {
            if (announcer is null)
            {
                announcer = new LiveRegion();
                LogicalChildren.Add(announcer);
                VisualChildren.Add(announcer);
            }
            announcer.Announce(model.Announcement);
            surface.SyncDragging();
            // 重排后行重建了：焦点回到拖着的那一行的把手上
            if (model.Active is { } key) Dispatcher.UIThread.Post(() => surface.FocusHandle(key), DispatcherPriority.Loaded);
        };
        return model;
    }

    string KeyText(object row) => Convert.ToString(KeyOf(row), System.Globalization.CultureInfo.InvariantCulture) ?? "";

    /// <summary>拖着的那一行的 key（行画成浮起的样子）；没有时 null。</summary>
    internal string? DraggingKey => sorting?.Active;

    /// <summary>行的把手：拖动的模型给名称（"移动 某某"）与说明（"按空格拿起…"）；键盘、指针都交给模型。</summary>
    internal Control RowHandle(object row)
    {
        var key = KeyText(row);
        var handle = new ButtonBase
        {
            Name = DataTableParts.RowHandle, Content = new Icon { Data = Lucide.GripVertical }, Ripple = false,
            VerticalAlignment = VerticalAlignment.Center, Cursor = new Cursor(StandardCursorType.SizeAll), Tag = key,
        };
        // 同上游 rowHandle：1.5em 见方、图标居中、textMuted；上下 −0.25em 外边距，不撑高这一行
        handle.HorizontalContentAlignment = HorizontalAlignment.Center;
        handle.VerticalContentAlignment = VerticalAlignment.Center;
        handle[!TemplatedControl.ForegroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.TextMuted).ToBinding();
        handle.AttachedToVisualTree += (_, _) =>
        {
            var em = TextElement.GetFontSize(handle);
            (handle.Width, handle.Height, handle.Margin) = (1.5 * em, 1.5 * em, new Thickness(0, -0.25 * em));
            handle.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        };
        AutomationProperties.SetName(handle, Sorting.HandleName(key));
        AutomationProperties.SetHelpText(handle, Sorting.Instructions);
        handle.AddHandler(KeyDownEvent, (_, e) =>
        {
            SortableKey? pressed = e.Key switch
            {
                Key.Space or Key.Enter => SortableKey.Toggle,
                Key.Escape => SortableKey.Escape,
                Key.Up => SortableKey.Up,
                Key.Down => SortableKey.Down,
                Key.Home => SortableKey.Home,
                Key.End => SortableKey.End,
                _ => null,
            };
            if (pressed is { } sortable && Sorting.Press(key, sortable, horizontal: false)) e.Handled = true;
        }, RoutingStrategies.Tunnel);
        handle.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
            Sorting.Grab(key);
            pointerDrag = key;
            e.Pointer.Capture(this);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        handle.LostFocus += (_, _) =>
        {
            // 重建时换下来的把手失焦不算
            if (pointerDrag is null && TopLevel.GetTopLevel(handle) is not null) Sorting.Blur(key);
        };
        return handle;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pointerDrag is not { } key) return;
        // 指针下是第几行：按行在表格内容里的位置
        var y = e.GetPosition(surface).Y;
        var index = surface.RowIndexAt(y);
        if (index >= 0) Sorting.DragOver(key, "", index);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pointerDrag is not { } key) return;
        pointerDrag = null;
        e.Pointer.Capture(null);
        Sorting.Drop(key);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (pointerDrag is not { } key) return;
        pointerDrag = null;
        Sorting.Drop(key);
    }
}
