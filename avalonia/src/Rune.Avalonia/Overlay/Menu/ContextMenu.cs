using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 右键菜单（vue-tsx-pro 的 ContextMenu）：在一块区域里右键（或键盘的 Shift+F10、菜单键）时弹出菜单；菜单项与 <see cref="Menu"/> 完全相同。
/// <code>
/// &lt;rune:ContextMenu&gt;
///   &lt;rune:ContextMenu.Items&gt;&lt;rune:MenuItem Click="OnRename"&gt;重命名&lt;/rune:MenuItem&gt;&lt;/rune:ContextMenu.Items&gt;
///   &lt;local:FileCard /&gt;
/// &lt;/rune:ContextMenu&gt;
/// </code>
/// 指针右键时从指针所在处弹出；键盘打开时从获得焦点的元素左下角弹出。关闭后焦点回到打开前的元素。
/// 区域本身不可聚焦：键盘用户要能打开，里面得有可聚焦的元素（同 Radix ContextMenu）。在菜单上右键不再打开；不可用（IsEnabled=False）时不弹出。
/// 菜单的读屏名称默认是语言包的"更多操作"（<see cref="Lang.ContextMenuLabel"/>），<see cref="Label"/> 改。
/// </summary>
public class ContextMenu : Control
{
    /// <summary>区域里的内容。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<ContextMenu, object?>(nameof(Content));

    /// <summary>菜单的读屏名称；不写时取语言包。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<ContextMenu, string?>(nameof(Label));

    /// <summary>菜单开着（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = Menu.IsOpenProperty.AddOwner<ContextMenu>();

    readonly ContentPresenter content = new();
    readonly Menu menu = new();
    IInputElement? origin;

    /// <summary>新建：内容与（不占位的）菜单。</summary>
    public ContextMenu()
    {
        LogicalChildren.AddRange([content, menu]);
        VisualChildren.AddRange([content, menu]);
        AddHandler(ContextRequestedEvent, OnContextRequested);
        menu.OpenChanged += (_, e) => OpenChanged?.Invoke(this, e);
        menu.PropertyChanged += (_, e) =>
        {
            if (e.Property != Menu.IsOpenProperty) return;
            SetCurrentValue(IsOpenProperty, menu.IsOpen);
            if (!menu.IsOpen) RestoreFocus();
        };
        UpdateLabel();
    }

    /// <summary>区域里的内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>菜单项：同 <see cref="Menu.Items"/>。</summary>
    public Controls Items => menu.Items;

    /// <summary>菜单的读屏名称。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>菜单开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>用户开、关了菜单。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>里面的菜单（测试用）。</summary>
    internal Menu Menu => menu;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty) content.Content = Content;
        else if (change.Property == IsOpenProperty) menu.IsOpen = IsOpen;
        else if (change.Property == LabelProperty || change.Property == Provider.LangProperty) UpdateLabel();
    }

    void UpdateLabel() => AutomationProperties.SetName(menu, Label ?? Provider.GetLang(this).ContextMenuLabel);

    /// <summary>右键、Shift+F10、菜单键：从指针所在处（键盘时获得焦点的元素左下角）弹出；已经开着时换到新的位置。</summary>
    void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (!IsEffectivelyEnabled || e.Handled || InMenu(e.Source)) return;
        e.Handled = true;
        origin = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var atPointer = e.TryGetPosition(this, out _);
        var anchor = atPointer ? this : (e.Source as Control ?? this);
        if (menu.IsOpen) menu.IsOpen = false;
        menu.OpenAt(anchor, atPointer);
    }

    /// <summary>在菜单（面板）里。</summary>
    bool InMenu(object? source) => source is Visual visual && (visual == menu.Panel || menu.Panel.IsVisualAncestorOf(visual));

    /// <summary>关上后：焦点丢了或还在菜单里时还给打开前的元素。</summary>
    void RestoreFocus()
    {
        var keep = origin;
        origin = null;
        if (keep is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
            if (focused is null || focused is TopLevel || InMenu(focused)) keep.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Loaded);
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
