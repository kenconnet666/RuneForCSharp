using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 漫游焦点（vue-tsx-pro 的 rovingFocus，WAI-ARIA 的 roving tabindex）：一组项里只有一个是 Tab 停靠点，其余不停；
/// 组内用方向键移动，Home / End 到两端。Toolbar 用它，以后的 Tabs、Menu、ToggleGroup 同样用。
/// 不可用、隐藏的项跳过；Tab 进入时落在上次聚焦的项，没有时是第一项。从右到左排版时左右键反过来。
/// </summary>
sealed class RovingFocus
{
    readonly FrameworkElement container;
    readonly Func<DependencyObject, bool> isItem;
    UIElement? current;

    /// <summary>开始管理容器里各项的焦点。</summary>
    /// <param name="container">容器。</param>
    /// <param name="isItem">哪些后代是项（默认的按钮、链接）。</param>
    public RovingFocus(FrameworkElement container, Func<DependencyObject, bool> isItem)
    {
        this.container = container;
        this.isItem = isItem;
        container.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown));
        container.AddHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotFocus));
    }

    /// <summary>方向键的方向：horizontal 左右键，vertical 上下键，both 四个都行。</summary>
    public string Orientation { get; set; } = "horizontal";

    /// <summary>到两端后从另一端继续。</summary>
    public bool Loop { get; set; } = true;

    IEnumerable<UIElement> All() => Descendants(container).OfType<UIElement>().Where(element => isItem(element));

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>项：可见、可用（可聚焦的不可用也跳过）。</summary>
    List<UIElement> Items() => [.. All().Where(item => item.IsVisible && item.IsEnabled && item is not ButtonBase { IsDisabled: true })];

    /// <summary>定下哪一项是 Tab 停靠点，其余不停。</summary>
    public void Sync()
    {
        var items = Items();
        var stop = current is not null && items.Contains(current) ? current : items.FirstOrDefault();
        foreach (var item in All()) KeyboardNavigation.SetIsTabStop(item, item == stop);
    }

    void OnGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is not UIElement element || !isItem(element)) return;
        current = element;
        Sync();
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not UIElement element || !isItem(element)) return;
        var items = Items();
        var index = items.IndexOf(element);
        if (index < 0) return;
        var rtl = container.FlowDirection == FlowDirection.RightToLeft;
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
        e.Handled = true;
        if (next < 0 || next >= items.Count)
        {
            if (!Loop) return;
            next = (next + items.Count) % items.Count;
        }
        current = items[next];
        Sync();
        current.Focus();
    }
}
