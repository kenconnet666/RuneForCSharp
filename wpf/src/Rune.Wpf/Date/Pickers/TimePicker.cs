using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 时间选择器（vue-tsx-pro 的 TimePicker）：TimeField 加一个打开时间列表的按钮，按 Step 列出可选的时间（同 Google 日历的时间下拉）；可以直接键入，也可以点选。
/// 参考 Mantine TimePicker、MUI 的 digital clock；列表是 <see cref="Listbox"/>（↑↓、Home / End、按数字跳转）。
/// <code>
/// &lt;rune:Field Label="开会时间"&gt;&lt;rune:TimePicker Value="{Binding Time}" Min="09:00" Max="18:00" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:TimePicker Step="15" Value="{Binding Remind}" /&gt;
/// </code>
/// 值同 TimeField（HH:mm，精确到秒时 HH:mm:ss）。列表从 Min（默认 00:00）起每 Step 分钟一项，到 Max 为止，按语言的 12 / 24 小时制显示；
/// 不在列表里的时间（14:47）照样能键入；IsTimeUnavailable 的时间在列表里不可用、键入时显示出错。
/// 列表下方"此刻"选当前时间（此刻不可选时不可用）。弹层与输入框同宽；选好后关闭，焦点回到按钮；Esc、点外部关闭，分段里 Alt + ↓ 打开。
/// 部件样式键：同 TimeField，另有 <see cref="PickerParts"/>（打开按钮、弹层、时间列表、"此刻"）。
/// </summary>
public class TimePicker : TimeField
{
    /// <summary>弹层开着（双向）。</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(TimePicker),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((TimePicker)d).OnOpenChanged()));

    /// <summary>弹出位置，默认 bottom-start；空间不够时翻到另一边。</summary>
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(nameof(Placement), typeof(FloatingPlacement), typeof(TimePicker),
        new PropertyMetadata(FloatingPlacement.BottomStart, (d, _) => ((TimePicker)d).SyncPicker()));

    /// <summary>列表里相邻两项相隔几分钟，默认 30。</summary>
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(int), typeof(TimePicker),
        new PropertyMetadata(30), step => (int)step >= 1);

    /// <summary>用户开、关了（按钮、Alt + ↓、选好了、Esc、点外部）；代码里改 IsOpen 时不发。</summary>
    public static readonly RoutedEvent OpenChangedEvent = EventManager.RegisterRoutedEvent(nameof(OpenChanged), RoutingStrategy.Direct, typeof(EventHandler<OpenChangedEventArgs>), typeof(TimePicker));

    readonly PickerPopover picker;
    OpenChangeReason? pending;

    /// <summary>新建：时分。</summary>
    public TimePicker()
    {
        picker = new PickerPopover(this, Lucide.Clock, SetOpen, FocusPanel) { MatchWidth = true, Inset = Space.Xs };
        Suffix = picker.Button;
        AddLogicalChild(picker.Popup);
        Loaded += (_, _) =>
        {
            RefreshPanel();
            // 一开始就开着（IsOpen 写在初始化里）：等外壳排好版再弹出，平台的弹出层要锚点已经在窗口里
            Dispatcher.BeginInvoke(SyncPicker, DispatcherPriority.Loaded);
        };
        SizeChanged += (_, _) => SyncPicker();
    }

    /// <summary>弹层开着。</summary>
    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>弹出位置。</summary>
    public FloatingPlacement Placement { get => (FloatingPlacement)GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }

    /// <summary>列表的步长（分钟）。</summary>
    public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <summary>用户开、关了。</summary>
    public event EventHandler<OpenChangedEventArgs> OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>弹层（测试用）。</summary>
    internal Border Panel => picker.Panel;

    /// <summary>打开按钮（测试用）。</summary>
    internal IconButton Trigger => picker.Button;

    /// <summary>此刻（测试里换掉）。</summary>
    internal Func<TimeOnly> Now { get; set; } = () => TimeOnly.FromDateTime(DateTime.Now);

    bool Editable => IsEnabled && !ReadOnly;

    /// <summary>改开关并记下原因（属性变化时报告）；已经是这样时不动。</summary>
    void SetOpen(bool open, OpenChangeReason reason)
    {
        if (IsOpen == open || open && !Editable) return;
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    void OnOpenChanged()
    {
        var reason = pending;
        RefreshPanel();
        SyncPicker();
        if (reason is { } known) RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, IsOpen, known));
    }

    /// <inheritdoc />
    private protected override void Synced() => SyncPicker();

    void SyncPicker()
    {
        // 基类的构造里就会同步一次（设段的时候），那时打开按钮还没建
        if (picker is null) return;
        picker.Sync(IsOpen, Placement, Provider.GetLang(this).TimePickerOpen, Editable);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty) SyncPicker();
    }

    /// <summary>内容只在开着、进了界面时建（"此刻"按打开的那一刻算）。</summary>
    void RefreshPanel() => picker.Panel.Child = IsOpen && IsLoaded ? BuildPanel() : null;

    /// <summary>打开后：焦点进时间列表。</summary>
    void FocusPanel()
    {
        var list = LogicalTreeHelper.GetChildren(picker.Panel).OfType<Flex>().SelectMany(flex => flex.Children.OfType<Listbox>()).FirstOrDefault();
        if (list is not null) list.Focus();
        else picker.Panel.Focus();
    }

    /// <summary>能不能选：在 Min–Max 之内、不是不可选的时间。</summary>
    bool Selectable(TimeOnly time) =>
        !(TimeSlots.Parse(Min) is { } low && time < low || TimeSlots.Parse(Max) is { } high && time > high || IsTimeUnavailable?.Invoke(time) == true);

    /// <summary>写回（精确到秒时补 :00），关上，焦点回到按钮。</summary>
    void Write(TimeOnly time)
    {
        SetCurrentValue(ValueProperty, TimeSlots.Iso(time, Granularity == TimeFieldGranularity.Second));
        SetOpen(false, OpenChangeReason.Select);
        picker.Picked(null);
    }

    /// <summary>弹层的内容：时间列表（最高 16em，其余滚动）与"此刻"。</summary>
    FrameworkElement BuildPanel()
    {
        var lang = Provider.GetLang(this);
        var culture = CultureInfo.GetCultureInfo(lang.Code);
        var cycle = HourCycle ?? lang.HourCycle;
        var options = TimeSlots.Between(TimeSlots.Parse(Min), TimeSlots.Parse(Max), Step)
            .Select(time => new ListboxOption(TimeSlots.Iso(time, false), TimeSlots.Label(time, culture, cycle)) { IsDisabled = !Selectable(time) })
            .ToList();
        // 当前值正好是列表里的一项时高亮它（只比时分）
        var list = new Listbox
        {
            ItemsSource = options,
            Value = Time is { } current ? TimeSlots.Iso(new TimeOnly(current.Hour, current.Minute), false) : null,
            IsRequired = true,
            MaxHeight = 16 * FontSize,
        };
        list.SetResourceReference(StyleProperty, PickerParts.List);
        AutomationProperties.SetName(list, lang.TimePickerOpen);
        list.Picked += (_, e) =>
        {
            if (TimeSlots.Parse(e.Value) is { } time) Write(time);
        };
        var now = Now();
        now = new TimeOnly(now.Hour, now.Minute);
        var button = new Button
        {
            Name = "PART_Now",
            Content = lang.TimePickerNow,
            Variant = ButtonVariant.Ghost,
            Size = TextSize.Sm,
            FullWidth = true,
            IsEnabled = Selectable(now),
        };
        button.SetResourceReference(StyleProperty, PickerParts.Now);
        button.Click += (_, _) => Write(now);
        var body = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
        body.Children.Add(list);
        body.Children.Add(button);
        return body;
    }
}
