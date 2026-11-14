using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 右键菜单（vue-tsx-pro 的 ContextMenu）：在一块区域里右键（或键盘的 Shift+F10、菜单键）时弹出菜单；菜单项与 <see cref="Menu"/> 完全相同。
/// <code>
/// &lt;rune:ContextMenu&gt;
///   &lt;rune:ContextMenu.Items&gt;&lt;rune:MenuItem Click="OnRename"&gt;重命名&lt;/rune:MenuItem&gt;&lt;/rune:ContextMenu.Items&gt;
///   &lt;local:FileCard /&gt;
/// &lt;/rune:ContextMenu&gt;
/// </code>
/// 指针右键时从指针所在处弹出；键盘打开时从获得焦点的元素下方弹出。关闭后焦点回到打开前的元素。
/// 区域本身不可聚焦：键盘用户要能打开，里面得有可聚焦的元素（同 Radix ContextMenu）。在菜单上右键不再打开；不可用（IsEnabled=False）时不弹出。
/// 菜单的读屏名称默认是语言包的"更多操作"（<see cref="Lang.ContextMenuLabel"/>），<see cref="Label"/> 改。用平台的 ContextMenuOpening 接右键与键盘的请求。
/// </summary>
[ContentProperty(nameof(Content))]
public class ContextMenu : FrameworkElement
{
    /// <summary>区域里的内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(ContextMenu),
        new FrameworkPropertyMetadata(null, (d, _) => { ((ContextMenu)d).content.Content = ((ContextMenu)d).Content; }));

    /// <summary>菜单的读屏名称；不写时取语言包。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ContextMenu),
        new FrameworkPropertyMetadata(null, (d, _) => ((ContextMenu)d).UpdateLabel()));

    readonly ContentPresenter content = new();
    readonly Menu menu = new();
    IInputElement? origin;

    /// <summary>新建：内容与（不占位的）菜单。</summary>
    public ContextMenu()
    {
        AddLogicalChild(content);
        AddVisualChild(content);
        AddLogicalChild(menu);
        AddVisualChild(menu);
        ContextMenuOpening += (_, e) =>
        {
            // 键盘打开时没有指针坐标（-1）
            if (e.OriginalSource is DependencyObject source && Request(source, e.CursorLeft >= 0 && e.CursorTop >= 0)) e.Handled = true;
        };
        menu.OpenChanged += (_, e) =>
        {
            RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, e.IsOpen, e.Reason));
            if (!e.IsOpen) RestoreFocus();
        };
        Loaded += (_, _) => UpdateLabel();
    }

    /// <summary>区域里的内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>菜单项：同 <see cref="Menu.Items"/>。</summary>
    public UIElementCollection Items => menu.Items;

    /// <summary>菜单的读屏名称。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>菜单开着。</summary>
    public bool IsOpen { get => menu.IsOpen; set => menu.IsOpen = value; }

    /// <summary>用户开、关了菜单。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary><see cref="OpenChanged"/> 的路由事件（直接：不往上传，嵌套的浮层发的同名事件不会串到外层）。</summary>
    public static readonly RoutedEvent OpenChangedEvent = EventManager.RegisterRoutedEvent(nameof(OpenChanged), RoutingStrategy.Direct, typeof(EventHandler<OpenChangedEventArgs>), typeof(ContextMenu));

    /// <summary>里面的菜单（测试用）。</summary>
    internal Menu Menu => menu;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { content, menu }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 2;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index == 0 ? content : menu;

    void UpdateLabel() => AutomationProperties.SetName(menu, Label ?? Provider.GetLang(this).ContextMenuLabel);

    /// <summary>
    /// 右键、Shift+F10、菜单键：从指针所在处（键盘时获得焦点的元素下方）弹出；已经开着时换到新的位置。在菜单上、不可用时不弹出。
    /// </summary>
    /// <param name="source">请求从哪个元素来。</param>
    /// <param name="atPointer">指针右键（不是键盘）。</param>
    /// <returns>弹出了（平台自己的右键菜单不再出现）。</returns>
    internal bool Request(DependencyObject source, bool atPointer)
    {
        if (!IsEnabled || InMenu(source)) return false;
        origin = Keyboard.FocusedElement;
        if (menu.IsOpen) menu.IsOpen = false;
        menu.OpenAt(atPointer ? this : source as UIElement ?? this, atPointer);
        return true;
    }

    /// <summary>在菜单（面板）里。</summary>
    bool InMenu(DependencyObject source) => source == menu.Panel || menu.Panel.IsAncestorOf(source);

    /// <summary>关上后：焦点丢了或还在菜单里时还给打开前的元素。</summary>
    void RestoreFocus()
    {
        var keep = origin;
        origin = null;
        if (keep is null) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (Keyboard.FocusedElement is not DependencyObject focused || InMenu(focused) || focused is Window) keep.Focus();
        });
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        content.Measure(availableSize);
        menu.Measure(availableSize);
        return content.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        content.Arrange(new Rect(finalSize));
        menu.Arrange(new Rect(0, 0, 0, 0));
        return finalSize;
    }
}
