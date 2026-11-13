using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 漫游焦点（vue-tsx-pro 的 rovingFocus，WAI-ARIA 的 roving tabindex）：一组项里只有一个是 Tab 停靠点，其余不停；
/// 组内用方向键移动，Home / End 到两端。Toolbar 用它，Tabs、Menu、ToggleGroup 同样用。
/// 不可用、隐藏的项跳过；Tab 进入时落在上次聚焦的项，没有时是 <see cref="Preferred"/> 选出的项或第一项。从右到左排版时左右键反过来。
/// </summary>
sealed class RovingFocus
{
    readonly FrameworkElement container;
    readonly Func<DependencyObject, bool> isItem;
    UIElement? current;
    global::Rune.Wpf.Typeahead? search;

    /// <summary>开始管理容器里各项的焦点。</summary>
    /// <param name="container">容器。</param>
    /// <param name="isItem">哪些后代是项（默认的按钮、链接）。</param>
    public RovingFocus(FrameworkElement container, Func<DependencyObject, bool> isItem)
    {
        this.container = container;
        this.isItem = isItem;
        container.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown));
        container.AddHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotFocus));
        // 键盘事件里没有输入的字符：类型搜索接文字输入（焦点所在的项收到）
        container.AddHandler(UIElement.TextInputEvent, new TextCompositionEventHandler(OnTextInput));
    }

    /// <summary>方向键的方向：horizontal 左右键，vertical 上下键，both 四个都行。</summary>
    public string Orientation { get; set; } = "horizontal";

    /// <summary>到两端后从另一端继续。</summary>
    public bool Loop { get; set; } = true;

    /// <summary>还没聚焦过时优先作 Tab 停靠点的项（分段控件的选中项）；没有时是第一项。</summary>
    public Func<UIElement, bool>? Preferred { get; set; }

    /// <summary>按字母跳到以它开头的项（菜单）：匹配 <see cref="TextOf"/> 给的文字，规则同 Listbox（<see cref="global::Rune.Wpf.Typeahead"/>）。</summary>
    public bool Typeahead { get => search is not null; set => search = value ? new global::Rune.Wpf.Typeahead() : null; }

    /// <summary>项的文字（类型搜索用）；不写时取读屏名称。</summary>
    public Func<UIElement, string>? TextOf { get; set; }

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
        var stop = current is not null && items.Contains(current) ? current
            : Preferred is { } preferred && items.FirstOrDefault(preferred) is { } chosen ? chosen : items.FirstOrDefault();
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
        if (e.Handled) return;
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
        Move(items[next]);
    }

    void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        if (search is null || e.Handled || e.Text.Length != 1 || e.OriginalSource is not UIElement element || !isItem(element)) return;
        var items = Items();
        var match = search.Next(e.Text, [.. items.Select(item => TextOf?.Invoke(item) ?? System.Windows.Automation.AutomationProperties.GetName(item))], items.IndexOf(element), Environment.TickCount64);
        if (match < 0) return;
        Move(items[match]);
        e.Handled = true;
    }

    void Move(UIElement item)
    {
        current = item;
        Sync();
        item.Focus();
    }
}
