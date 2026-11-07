using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 漫游焦点（vue-tsx-pro 的 rovingFocus，WAI-ARIA 的 roving tabindex）：一组项里只有一个是 Tab 停靠点，其余不停；
/// 组内用方向键移动，Home / End 到两端。Toolbar 用它，以后的 Tabs、Menu、ToggleGroup 同样用。
/// 不可用、隐藏的项跳过；Tab 进入时落在上次聚焦的项，没有时是 <see cref="Preferred"/> 选出的项或第一项。从右到左排版时左右键反过来。
/// </summary>
sealed class RovingFocus
{
    readonly Control container;
    readonly Func<Control, bool> isItem;
    Control? current;

    /// <summary>开始管理容器里各项的焦点。</summary>
    /// <param name="container">容器。</param>
    /// <param name="isItem">哪些后代是项（默认的按钮、链接）。</param>
    public RovingFocus(Control container, Func<Control, bool> isItem)
    {
        this.container = container;
        this.isItem = isItem;
        container.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        container.AddHandler(InputElement.GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
    }

    /// <summary>方向键的方向：horizontal 左右键，vertical 上下键，both 四个都行。</summary>
    public string Orientation { get; set; } = "horizontal";

    /// <summary>到两端后从另一端继续。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>还没聚焦过时优先作 Tab 停靠点的项（分段控件的选中项）；没有时是第一项。</summary>
    public Func<Control, bool>? Preferred { get; set; }

    /// <summary>项：可见、可用（可聚焦的不可用也跳过），不在里面另一个漫游焦点的组里。</summary>
    List<Control> Items() =>
    [
        .. container.GetVisualDescendants().OfType<Control>().Where(control =>
            isItem(control) && control.IsEffectivelyVisible && control.IsEffectivelyEnabled && control is not ButtonBase { IsDisabled: true }),
    ];

    /// <summary>定下哪一项是 Tab 停靠点，其余不停。</summary>
    public void Sync()
    {
        var items = Items();
        var stop = current is not null && items.Contains(current) ? current
            : Preferred is { } preferred && items.FirstOrDefault(preferred) is { } chosen ? chosen : items.FirstOrDefault();
        foreach (var item in container.GetVisualDescendants().OfType<Control>().Where(isItem))
            KeyboardNavigation.SetIsTabStop(item, item == stop);
    }

    void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is not Control control || !isItem(control)) return;
        current = control;
        Sync();
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not Control control || !isItem(control)) return;
        var items = Items();
        var index = items.IndexOf(control);
        if (index < 0) return;
        var rtl = container.FlowDirection == global::Avalonia.Media.FlowDirection.RightToLeft;
        var horizontal = Orientation is "horizontal" or "both";
        var vertical = Orientation is "vertical" or "both";
        int? target = e.Key switch
        {
            Key.Right when horizontal => rtl ? index - 1 : index + 1,
            Key.Left when horizontal => rtl ? index + 1 : index - 1,
            Key.Down when vertical => index + 1,
            Key.Up when vertical => index - 1,
            Key.Home => 0,
            Key.End => items.Count - 1,
            // 另一个方向的方向键不动（平台默认会用方向键在控件之间移动，TS 里浏览器什么都不做）
            Key.Left or Key.Right or Key.Up or Key.Down => index,
            _ => null,
        };
        if (target is not { } next) return;
        if (next < 0 || next >= items.Count)
        {
            if (!Loop)
            {
                e.Handled = true;
                return;
            }
            next = (next + items.Count) % items.Count;
        }
        current = items[next];
        Sync();
        current.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
