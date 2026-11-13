using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>执行了命令面板里一项的事件参数。</summary>
/// <param name="item">ItemsSource 里的那一项。</param>
public sealed class CommandPaletteItemEventArgs(object? item) : EventArgs
{
    /// <summary>执行的那一项。</summary>
    public object? Item { get; } = item;
}

/// <summary>
/// 命令面板（vue-tsx-pro 的 CommandPalette）：快捷键（默认 Mod+K，Windows 上是 Ctrl+K）或点触发按钮打开的搜索对话框，边输入边过滤，Enter 执行。
/// <code>
/// &lt;rune:CommandPalette ItemsSource="{Binding Pages}" ToOption="{Binding PageOption}" ItemInvoked="OnGo" /&gt;
/// </code>
/// 结构是 sm 宽、停在窗口上部的 <see cref="Dialog"/>（标题只给读屏，是面板的名称），里面一行搜索框（lg 字号、下边分隔线）与不可聚焦的 <see cref="Listbox"/>
/// （最高 min(24rem, 窗口高度一半)）：焦点一直在搜索框，↑↓、Page Up / Down 移动高亮，Enter 执行；数据层同 Select（<see cref="ListState"/>）。
/// 面板不记选择：执行就是选中，<see cref="ItemInvoked"/> 报告那一项，面板随即关闭。默认按选项文字与说明过滤（忽略大小写与重音），<see cref="Filter"/> 自定义
/// （远程搜索：读 <see cref="InputText"/> 取数据，Filter 一律返回 true，取数据时给 <see cref="IsLoading"/>）。每次打开清空输入；输入、结果变了高亮第一个。
/// 触发按钮默认画成输入框外壳（放大镜、占位文字、快捷键的 Kbd），<see cref="Trigger"/> 换成自己的按钮（里面按钮的 Click 打开），设为 null 时只用快捷键。
/// 部件样式键见 <see cref="CommandPaletteParts"/>。
/// </summary>
public class CommandPalette : FrameworkElement
{
    /// <summary>开着（默认双向绑定）；快捷键、触发按钮、执行、Esc 都会改它。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(CommandPalette),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((CommandPalette)d).Toggle()));

    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(CommandPalette),
        new FrameworkPropertyMetadata(null, OnItemsChanged));

    /// <summary>数据 → 选项。</summary>
    public static readonly DependencyProperty ToOptionProperty = DependencyProperty.Register(nameof(ToOption), typeof(Func<object?, ListboxOption>), typeof(CommandPalette),
        new FrameworkPropertyMetadata(null, OnItemsChanged));

    /// <summary>怎样算匹配（数据、输入）；不写时选项文字或说明包含输入（忽略大小写与重音）。</summary>
    public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(Filter), typeof(Func<object?, string, bool>), typeof(CommandPalette),
        new FrameworkPropertyMetadata(null, OnItemsChanged));

    /// <summary>输入框里的文字（默认双向绑定，远程搜索时据此取数据）；每次打开清空。</summary>
    public static readonly DependencyProperty InputTextProperty = DependencyProperty.Register(nameof(InputText), typeof(string), typeof(CommandPalette),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((CommandPalette)d).OnInputTextChanged()));

    /// <summary>正在取结果：列表换成骨架行。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(CommandPalette),
        new FrameworkPropertyMetadata(false, (d, e) => ((CommandPalette)d).listbox.IsLoading = (bool)e.NewValue));

    /// <summary>打开面板的快捷键，默认 Mod+K；null 时不注册。</summary>
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(nameof(Hotkey), typeof(string), typeof(CommandPalette),
        new FrameworkPropertyMetadata("Mod+K", OnSync));

    /// <summary>搜索框（与默认触发按钮）的占位文字；不写时取语言包（搜索文档）。</summary>
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(CommandPalette),
        new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>面板的名称（读屏念的）；不写时取语言包（搜索）。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(CommandPalette),
        new FrameworkPropertyMetadata(null, OnSync));

    /// <summary>触发按钮（点它、即里面按钮的 Click 打开）；默认画成输入框外壳的按钮，null 时只用快捷键。</summary>
    public static readonly DependencyProperty TriggerProperty = DependencyProperty.Register(nameof(Trigger), typeof(object), typeof(CommandPalette),
        new FrameworkPropertyMetadata(null, (d, e) => ((CommandPalette)d).trigger.Content = e.NewValue));

    /// <summary>选项的内容模板（拿到原始数据）。</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(CommandPalette),
        new FrameworkPropertyMetadata(null, (d, e) => ((CommandPalette)d).listbox.ItemTemplate = (DataTemplate?)e.NewValue));

    /// <summary>没有匹配时列表里显示的内容；不写时是"没有找到相关内容"。</summary>
    public static readonly DependencyProperty EmptyProperty = DependencyProperty.Register(nameof(Empty), typeof(object), typeof(CommandPalette),
        new FrameworkPropertyMetadata(null, OnSync));

    readonly ContentPresenter trigger = new();
    readonly Dialog dialog = new() { Size = DialogSize.Sm, Closable = false, HeaderHidden = true, TopFraction = 0.12, BodyFlush = true };
    readonly Border search = new();
    readonly Icon searchIcon = new() { Data = Lucide.Search };
    readonly TextField input = new() { Bare = true };
    readonly Listbox listbox = new() { Focusable = false };
    readonly Button defaultTrigger = new() { Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Icon = Lucide.Search, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    readonly Text triggerLabel = new();
    readonly Kbd triggerKbd = new();
    IReadOnlyList<object?> all = [];
    OpenChangeReason? pending;
    Window? host;

    /// <summary>新建：默认触发按钮、对话框（搜索行与列表）。</summary>
    public CommandPalette()
    {
        search.SetResourceReference(StyleProperty, CommandPaletteParts.Search);
        // 文字 textMuted：本地值压过按钮配方里样式 × 色调的触发器
        defaultTrigger.SetResourceReference(Control.ForegroundProperty, "Rune.Brush.TextMuted");
        searchIcon.SetResourceReference(Control.ForegroundProperty, "Rune.Brush.TextMuted");
        Flex.SetGrow(triggerLabel, true);
        defaultTrigger.Content = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { triggerLabel, triggerKbd } };
        Flex.SetGrow(input, true);
        search.Child = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { searchIcon, input } };
        var body = new DockPanel();
        DockPanel.SetDock(search, Dock.Top);
        body.Children.Add(search);
        body.Children.Add(listbox);
        dialog.Content = body;
        dialog.OpenChanged += (_, e) => OnDialogChanged(e);
        input.TextChanged += (_, _) => SetCurrentValue(InputTextProperty, input.Text ?? "");
        input.PreviewKeyDown += OnInputKeyDown;
        listbox.Picked += (_, e) => Pick(e.Value);
        // 同 Popover：触发器里按钮的 Click 打开（默认的与自己换上的都是）
        trigger.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => SetOpen(true, OpenChangeReason.Trigger)));
        AddLogicalChild(trigger);
        AddVisualChild(trigger);
        AddLogicalChild(dialog);
        AddVisualChild(dialog);
        SetCurrentValue(TriggerProperty, defaultTrigger);
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        Sync();
    }

    /// <summary>开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => (Func<object?, ListboxOption>?)GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>怎样算匹配。</summary>
    public Func<object?, string, bool>? Filter { get => (Func<object?, string, bool>?)GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>输入框里的文字。</summary>
    public string InputText { get => (string)GetValue(InputTextProperty); set => SetValue(InputTextProperty, value); }

    /// <summary>正在取结果。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>快捷键。</summary>
    public string? Hotkey { get => (string?)GetValue(HotkeyProperty); set => SetValue(HotkeyProperty, value); }

    /// <summary>占位文字。</summary>
    public string? Placeholder { get => (string?)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>面板的名称。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>触发按钮。</summary>
    public object? Trigger { get => GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>选项的内容模板。</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>没有匹配时显示的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>执行了一项（Enter 或点击）：面板随即关闭。</summary>
    public event EventHandler<CommandPaletteItemEventArgs>? ItemInvoked;

    /// <summary>用户开、关了（快捷键、触发按钮、执行、Esc、点遮罩）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>对话框（测试用）。</summary>
    internal Dialog Dialog => dialog;

    /// <summary>搜索框（测试用）。</summary>
    internal TextField Input => input;

    /// <summary>列表（测试用）。</summary>
    internal Listbox List => listbox;

    /// <summary>默认的触发按钮（测试用）。</summary>
    internal Button DefaultTrigger => defaultTrigger;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new object[] { trigger, dialog }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 2;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index == 0 ? trigger : dialog;

    static void OnSync(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CommandPalette)d).Sync();

    static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var palette = (CommandPalette)d;
        palette.all = palette.ItemsSource?.Cast<object?>().ToList() ?? [];
        palette.UpdateItems();
    }

    void OnInputTextChanged()
    {
        if (input.Text != InputText) input.Text = InputText;
        UpdateItems();
    }

    /// <summary>名称、占位文字、快捷键的显示与读屏写法。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        var placeholder = Placeholder ?? lang.SearchPlaceholder;
        var label = Label ?? lang.SearchLabel;
        dialog.Title = label;
        input.PlaceholderText = placeholder;
        AutomationProperties.SetName(input, label);
        triggerLabel.Text = placeholder;
        var hotkey = Hotkey is { } combo ? global::Rune.Wpf.Hotkey.Parse(combo) : null;
        triggerKbd.Content = hotkey?.Text;
        triggerKbd.Visibility = hotkey is null ? Visibility.Collapsed : Visibility.Visible;
        // 名称来自看得见的文字，快捷键由读屏的快捷键属性念（显示它的 Kbd 不算进名称）
        AutomationProperties.SetName(defaultTrigger, placeholder);
        AutomationProperties.SetAcceleratorKey(defaultTrigger, hotkey?.AccessKey ?? "");
        listbox.Empty = Empty ?? new Empty { Size = TextSize.Sm, Description = lang.SearchEmpty };
    }

    /// <summary>按 em 的尺寸：搜索行上下 space.sm、左右 space.md、lg 字号；列表最高 min(24rem, 窗口高度一半)；触发按钮至少 12rem。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        double Resource(string key, double fallback) => TryFindResource(key) as double? ?? fallback;
        search.Padding = new Thickness(Resource("Rune.Space.Md", 0.75) * em, Resource("Rune.Space.Sm", 0.5) * em, Resource("Rune.Space.Md", 0.75) * em, Resource("Rune.Space.Sm", 0.5) * em);
        TextElement.SetFontSize(search, Resource("Rune.FontSize.Lg", 18));
        listbox.ScrollMaxHeight = Math.Min(24 * 16, (host?.ActualHeight ?? 800) / 2);
        defaultTrigger.MinWidth = 12 * 16;
    }

    /// <summary>列表里的数据：输入不空时按过滤留下匹配的；高亮第一个。</summary>
    void UpdateItems()
    {
        listbox.ToOption = ToOption;
        var text = InputText;
        listbox.ItemsSource = text.Length == 0 ? all : all.Where(item => Matches(item, text)).ToList();
        listbox.Value = null;
        listbox.State.MoveToEnd(last: false);
    }

    bool Matches(object? item, string text)
    {
        if (Filter is { } filter) return filter(item, text);
        var option = ListboxOption.From(item, ToOption);
        return SelectSearch.Matches(option.Label, text) || SelectSearch.Matches(option.Description ?? "", text);
    }

    void SetOpen(bool open, OpenChangeReason reason)
    {
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    /// <summary>开关：每次打开清空输入、高亮第一个，焦点进搜索框。</summary>
    void Toggle()
    {
        var reason = pending;
        if (IsOpen)
        {
            UpdateMetrics();
            SetCurrentValue(InputTextProperty, "");
            UpdateItems();
        }
        if (dialog.IsOpen != IsOpen) dialog.IsOpen = IsOpen;
        // 对话框把焦点给第一个能聚焦的元素（这里就是搜索框）；保险起见打开后再给一次
        if (IsOpen) Dispatcher.BeginInvoke(DispatcherPriority.Input, () => input.Focus());
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(IsOpen, known));
    }

    /// <summary>对话框自己关了（Esc、点遮罩）：报告它的原因。</summary>
    void OnDialogChanged(OpenChangedEventArgs e)
    {
        if (e.IsOpen || !IsOpen) return;
        pending = null;
        SetCurrentValue(IsOpenProperty, false);
        OpenChanged?.Invoke(this, e);
    }

    /// <summary>执行一项：不可用的不执行；关上面板，报告那一项。</summary>
    void Pick(string value)
    {
        var index = listbox.State.IndexOf(value);
        if (index < 0) return;
        var entry = listbox.State.Options[index];
        listbox.Value = null;
        if (entry.Option.IsDisabled) return;
        SetOpen(false, OpenChangeReason.Select);
        ItemInvoked?.Invoke(this, new CommandPaletteItemEventArgs(entry.Item));
    }

    /// <summary>搜索框里：↑↓、Page Up / Down 移动高亮，Enter 执行（Home / End、空格留给输入）。</summary>
    void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Home or Key.End or Key.Space || Listbox.ToListKey(e.Key) is not { } key) return;
        e.Handled = listbox.State.Handle(key, space: false, picked: Pick);
    }

    /// <summary>进了窗口：快捷键在窗口上隧道接（焦点在输入框里也有效）。</summary>
    void Attach()
    {
        Detach();
        host = Window.GetWindow(this);
        if (host is not null) host.PreviewKeyDown += OnHostKeyDown;
        Sync();
        UpdateMetrics();
    }

    void Detach()
    {
        if (host is not null) host.PreviewKeyDown -= OnHostKeyDown;
        host = null;
    }

    void OnHostKeyDown(object sender, KeyEventArgs e)
    {
        if (!e.Handled) e.Handled = OnHotkey(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
    }

    /// <summary>窗口里按了键：是快捷键时开关面板（测试直接调，键盘的修饰键状态造不出来）。</summary>
    /// <returns>是快捷键。</returns>
    internal bool OnHotkey(Key key, ModifierKeys modifiers)
    {
        if (Hotkey is not { } combo || !global::Rune.Wpf.Hotkey.Parse(combo).Matches(key, modifiers)) return false;
        SetOpen(!IsOpen, OpenChangeReason.Imperative);
        return true;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        trigger.Measure(availableSize);
        dialog.Measure(availableSize);
        return trigger.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        trigger.Arrange(new Rect(finalSize));
        dialog.Arrange(default);
        return finalSize;
    }
}

/// <summary>CommandPalette 的部件样式键（DESIGN §7）。</summary>
public static class CommandPaletteParts
{
    /// <summary>搜索行（放大镜与输入框，下边一条分隔线）。</summary>
    public static readonly PartKey Search = new(typeof(CommandPalette), nameof(Search), typeof(Border));
}
