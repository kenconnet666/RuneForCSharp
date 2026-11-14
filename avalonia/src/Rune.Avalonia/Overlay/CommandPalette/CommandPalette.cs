using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Rune.Avalonia;

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
/// 触发按钮默认画成输入框外壳（放大镜、占位文字、快捷键的 Kbd），<see cref="Trigger"/> 换成自己的按钮，设为 null 时只用快捷键。
/// </summary>
public class CommandPalette : Control
{
    /// <summary>开着（双向绑定）；快捷键、触发按钮、执行、Esc 都会改它。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<CommandPalette, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<CommandPalette, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据 → 选项。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<CommandPalette, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>怎样算匹配（数据、输入）；不写时选项文字或说明包含输入（忽略大小写与重音）。</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterProperty = AvaloniaProperty.Register<CommandPalette, Func<object?, string, bool>?>(nameof(Filter));

    /// <summary>输入框里的文字（双向绑定，远程搜索时据此取数据）；每次打开清空。</summary>
    public static readonly StyledProperty<string> InputTextProperty = AvaloniaProperty.Register<CommandPalette, string>(nameof(InputText), "", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>正在取结果：列表换成骨架行。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<CommandPalette, bool>(nameof(IsLoading));

    /// <summary>打开面板的快捷键，默认 Mod+K；null 时不注册。</summary>
    public static readonly StyledProperty<string?> HotkeyProperty = AvaloniaProperty.Register<CommandPalette, string?>(nameof(Hotkey), "Mod+K");

    /// <summary>搜索框（与默认触发按钮）的占位文字；不写时取语言包（搜索文档）。</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty = AvaloniaProperty.Register<CommandPalette, string?>(nameof(Placeholder));

    /// <summary>面板的名称（读屏念的）；不写时取语言包（搜索）。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<CommandPalette, string?>(nameof(Label));

    /// <summary>触发按钮（点它、即里面按钮的 Click 打开）；默认画成输入框外壳的按钮，null 时只用快捷键。</summary>
    public static readonly StyledProperty<Control?> TriggerProperty = AvaloniaProperty.Register<CommandPalette, Control?>(nameof(Trigger));

    /// <summary>选项的内容模板（拿到原始数据）。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<CommandPalette, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>没有匹配时列表里显示的内容；不写时是"没有找到相关内容"。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<CommandPalette, object?>(nameof(Empty));

    readonly ContentPresenter trigger = new();
    readonly Dialog dialog = new() { Size = DialogSize.Sm, Closable = false, HeaderHidden = true, TopFraction = 0.12, BodyFlush = true };
    readonly Border search = new() { Name = CommandPaletteParts.Search };
    readonly Icon searchIcon = new() { Data = Lucide.Search };
    readonly TextField input = new() { Name = CommandPaletteParts.Input, Bare = true };
    readonly Listbox listbox = new() { Name = CommandPaletteParts.Listbox, Focusable = false };
    readonly Button defaultTrigger = new() { Name = CommandPaletteParts.Trigger, Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Icon = Lucide.Search };
    readonly Text triggerLabel = new();
    readonly Kbd triggerKbd = new();
    IReadOnlyList<object?> all = [];
    OpenChangeReason? pending;
    TopLevel? host;

    /// <summary>新建：默认触发按钮、对话框（搜索行与列表）。</summary>
    public CommandPalette()
    {
        Flex.SetGrow(triggerLabel, true);
        defaultTrigger.Content = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { triggerLabel, triggerKbd } };
        defaultTrigger.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        // 同 Popover：触发器里按钮的 Click 打开（默认的与自己换上的都是）
        trigger.AddHandler(Button.ClickEvent, (_, _) => SetOpen(true, OpenChangeReason.Trigger));
        Flex.SetGrow(input, true);
        search.Child = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { searchIcon, input } };
        var body = new DockPanel();
        DockPanel.SetDock(search, Dock.Top);
        body.Children.AddRange([search, listbox]);
        dialog.Content = body;
        dialog.OpenChanged += (_, e) => OnDialogChanged(e);
        dialog.PropertyChanged += (_, e) =>
        {
            if (e.Property == Dialog.IsOpenProperty && !dialog.IsOpen && IsOpen) SetCurrentValue(IsOpenProperty, false);
        };
        input.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) SetCurrentValue(InputTextProperty, input.Text ?? "");
        };
        input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        listbox.Picked += (_, e) => Pick(e.Value);
        LogicalChildren.AddRange([trigger, dialog]);
        VisualChildren.AddRange([trigger, dialog]);
        SetCurrentValue(TriggerProperty, defaultTrigger);
    }

    /// <summary>开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>数据。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>怎样算匹配。</summary>
    public Func<object?, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>输入框里的文字。</summary>
    public string InputText { get => GetValue(InputTextProperty); set => SetValue(InputTextProperty, value); }

    /// <summary>正在取结果。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>快捷键。</summary>
    public string? Hotkey { get => GetValue(HotkeyProperty); set => SetValue(HotkeyProperty, value); }

    /// <summary>占位文字。</summary>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>面板的名称。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>触发按钮。</summary>
    public Control? Trigger { get => GetValue(TriggerProperty); set => SetValue(TriggerProperty, value); }

    /// <summary>选项的内容模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

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
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == ItemsSourceProperty || change.Property == ToOptionProperty || change.Property == FilterProperty)
        {
            all = ItemsSource?.Cast<object?>().ToList() ?? [];
            UpdateItems();
        }
        else if (change.Property == InputTextProperty)
        {
            if (input.Text != InputText) input.Text = InputText;
            UpdateItems();
        }
        else if (change.Property == TriggerProperty) trigger.Content = Trigger;
        else if (change.Property == IsLoadingProperty) listbox.IsLoading = IsLoading;
        else if (change.Property == ItemTemplateProperty) listbox.ItemTemplate = ItemTemplate;
        else if (change.Property == EmptyProperty || change.Property == PlaceholderProperty || change.Property == LabelProperty || change.Property == HotkeyProperty) Sync();
        else if (change.Property == TextElement.FontSizeProperty) UpdateMetrics();
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
        var hotkey = Hotkey is { } combo ? global::Rune.Avalonia.Hotkey.Parse(combo) : null;
        triggerKbd.Content = hotkey?.Text;
        triggerKbd.IsVisible = hotkey is not null;
        // 名称来自看得见的文字，快捷键由读屏的快捷键属性念（显示它的 Kbd 不算进名称）
        AutomationProperties.SetName(defaultTrigger, placeholder);
        AutomationProperties.SetAcceleratorKey(defaultTrigger, hotkey?.AccessKey ?? "");
        listbox.Empty = Empty ?? new Empty { Size = TextSize.Sm, Description = lang.SearchEmpty };
    }

    /// <summary>按 em 的尺寸：搜索行上下 space.sm、左右 space.md、lg 字号；列表最高 min(24rem, 窗口高度一半)；触发按钮至少 12rem、文字 textMuted。</summary>
    void UpdateMetrics()
    {
        var em = TextElement.GetFontSize(this);
        search.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Md) * em, this.Resource<double>(ThemeKeys.Space.Sm) * em);
        search.SetValue(TextElement.FontSizeProperty, this.Resource<double>(ThemeKeys.FontSize.Lg));
        listbox.ScrollMaxHeight = Math.Min(24 * 16, (host?.ClientSize.Height ?? 800) / 2);
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
        // 列表在弹出层里，进了逻辑树才建选项：打开之后再高亮第一个
        if (IsOpen) global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            listbox.State.MoveToEnd(last: false);
            input.Focus(NavigationMethod.Tab);
        }, global::Avalonia.Threading.DispatcherPriority.Loaded);
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
    void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Home or Key.End or Key.Space || Listbox.ToListKey(e.Key) is not { } key) return;
        e.Handled = listbox.State.Handle(key, space: false, picked: Pick);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        host = TopLevel.GetTopLevel(this);
        // 快捷键在窗口上隧道接：焦点在输入框里也有效
        host?.AddHandler(KeyDownEvent, OnHostKeyDown, RoutingStrategies.Tunnel);
        Sync();
        UpdateMetrics();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        host?.RemoveHandler(KeyDownEvent, OnHostKeyDown);
        host = null;
    }

    /// <summary>窗口里按了快捷键：开关面板。</summary>
    void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        if (Hotkey is not { } combo || e.Handled || !global::Rune.Avalonia.Hotkey.Parse(combo).Matches(e.Key, e.KeyModifiers)) return;
        e.Handled = true;
        SetOpen(!IsOpen, OpenChangeReason.Imperative);
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
        return finalSize;
    }
}

/// <summary>CommandPalette 的部件名（对话框里的在逻辑上仍是 CommandPalette 的后代）：<c>rune|CommandPalette Border#PART_Search</c>。</summary>
public static class CommandPaletteParts
{
    /// <summary>默认的触发按钮（TS 的 propsTrigger）。</summary>
    public const string Trigger = "PART_Trigger";

    /// <summary>搜索行（放大镜与输入框，下边一条分隔线）。</summary>
    public const string Search = "PART_Search";

    /// <summary>搜索框（TS 的 propsInput）。</summary>
    public const string Input = "PART_Input";

    /// <summary>列表（TS 的 propsListbox）。</summary>
    public const string Listbox = "PART_Listbox";
}
