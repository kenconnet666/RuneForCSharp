using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 气泡确认（vue-tsx-pro 的 Popconfirm，role="alertdialog"）：在触发按钮旁边问一句再执行（移出成员、撤回、清空），比确认对话框轻。
/// <code>
/// &lt;rune:Popconfirm Title="删除这条记录？" Description="删除后无法恢复" Tone="danger" ConfirmText="删除" Confirm="{Binding Remove}"&gt;
///   &lt;rune:Popconfirm.Trigger&gt;&lt;rune:IconButton Icon="{x:Static rune:Lucide.Trash}" AutomationProperties.Name="删除" /&gt;&lt;/rune:Popconfirm.Trigger&gt;
/// &lt;/rune:Popconfirm&gt;
/// </code>
/// 建在 <see cref="Popover"/> 上（默认在上方）：点外面、Esc 等于取消，关上后焦点回到触发器；打开时焦点落在"取消"上，误按 Enter 不会执行。
/// <see cref="Confirm"/> 的任务进行中确认按钮加载、取消不可用，完成后关闭；结果为 false 或失败时保持打开，失败原因由调用方提示。
/// 取消（取消按钮、点外面、Esc）发 <see cref="Canceled"/>；确认后关闭的不算。图标 <see cref="Icon"/> 不写时按 <see cref="Tone"/> 选，写 null 不显示。
/// 浮层以问题为读屏名称、说明为帮助文字。后果严重、要多写几句或填理由的用 <see cref="AlertDialog"/>。
/// </summary>
public class Popconfirm : Popover
{
    /// <summary>问题，同时是浮层的读屏名称。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(Popconfirm), new PropertyMetadata(null, OnSync));

    /// <summary>补充说明（读屏的帮助文字）。</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(Popconfirm), new PropertyMetadata(null, OnSync));

    /// <summary>确认按钮与图标的色调，默认 warning；删除这类破坏性操作用 danger。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(Popconfirm), new PropertyMetadata(StatusTone.Warning, OnSync));

    /// <summary>图标；不写时按色调选，写 null 不显示。</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconData), typeof(Popconfirm), new PropertyMetadata(null, OnSync));

    /// <summary>确认时做的事：任务完成后关闭；结果为 false 或失败时保持打开。不写时点了就关。</summary>
    public static readonly DependencyProperty ConfirmProperty = DependencyProperty.Register(nameof(Confirm), typeof(Func<Task<bool>>), typeof(Popconfirm));

    /// <summary>确认按钮的文字；不写时取语言包（确定）。</summary>
    public static readonly DependencyProperty ConfirmTextProperty = DependencyProperty.Register(nameof(ConfirmText), typeof(string), typeof(Popconfirm), new PropertyMetadata(null, OnSync));

    /// <summary>取消按钮的文字；不写时取语言包（取消）。</summary>
    public static readonly DependencyProperty CancelTextProperty = DependencyProperty.Register(nameof(CancelText), typeof(string), typeof(Popconfirm), new PropertyMetadata(null, OnSync));

    /// <summary><see cref="Canceled"/> 的路由事件（直接）。</summary>
    public static readonly RoutedEvent CanceledEvent = EventManager.RegisterRoutedEvent(nameof(Canceled), RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(Popconfirm));

    readonly Icon icon = new();
    readonly Text title = new() { Weight = TextWeight.Medium };
    readonly Small description = new() { Tone = TextTone.Muted };
    readonly Flex footer = new() { Justify = FlexJustify.End, Gap = Space.Sm };
    readonly Button cancel = new() { Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral };
    readonly Button confirm = new() { Size = TextSize.Sm };
    readonly LayoutGrid body = new();
    bool confirming;
    // 确认后关闭的不算取消
    bool confirmed;

    static Popconfirm() => PlacementProperty.OverrideMetadata(typeof(Popconfirm), new FrameworkPropertyMetadata(FloatingPlacement.Top));

    /// <summary>新建：图标、问题、说明与"取消 / 确认"。</summary>
    public Popconfirm()
    {
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 3; i++) body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        LayoutGrid.SetRowSpan(icon, 2);
        LayoutGrid.SetColumn(title, 1);
        LayoutGrid.SetColumn(description, 1);
        LayoutGrid.SetRow(description, 1);
        LayoutGrid.SetRow(footer, 2);
        LayoutGrid.SetColumnSpan(footer, 2);
        footer.Children.Add(cancel);
        footer.Children.Add(confirm);
        foreach (var part in new UIElement[] { icon, title, description, footer }) body.Children.Add(part);
        Content = body;
        cancel.Click += (_, _) => SetOpen(false, OpenChangeReason.Close);
        confirm.Click += (_, _) => _ = RunConfirm();
        Loaded += (_, _) => Sync();
        Sync();
    }

    /// <summary>问题。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>补充说明。</summary>
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>图标。</summary>
    public IconData? Icon { get => (IconData?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>确认时做的事。</summary>
    public Func<Task<bool>>? Confirm { get => (Func<Task<bool>>?)GetValue(ConfirmProperty); set => SetValue(ConfirmProperty, value); }

    /// <summary>确认按钮的文字。</summary>
    public string? ConfirmText { get => (string?)GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }

    /// <summary>取消按钮的文字。</summary>
    public string? CancelText { get => (string?)GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <summary>取消了：取消按钮、点外面、Esc 关闭（确认后关闭的不算；确认进行中被关掉时，等结果出来、没确认成才算）。</summary>
    public event RoutedEventHandler Canceled { add => AddHandler(CanceledEvent, value); remove => RemoveHandler(CanceledEvent, value); }

    /// <summary>取消、确认两个按钮（测试用）。</summary>
    internal (Button Cancel, Button Confirm) Actions => (cancel, confirm);

    // 打开时焦点落在取消上：误按 Enter 不会执行
    private protected override UIElement? FocusTarget() => cancel;

    // 最宽 20rem（按根字号 16 换算）；图标与文字之间 space.sm，行间 space.xs，按钮行上面再空 space.sm
    private protected override void OnMetrics(double em)
    {
        Panel.MaxWidth = 320;
        var sm = this.Resource<double>(ThemeKeys.Space.Sm) * em;
        var xs = this.Resource<double>(ThemeKeys.Space.Xs) * em;
        icon.Margin = new Thickness(0, 0.15 * em, sm, 0);
        description.Margin = new Thickness(0, xs, 0, 0);
        footer.Margin = new Thickness(0, xs + sm, 0, 0);
        title.LineHeight = this.Resource<double>(ThemeKeys.LineHeight.Tight) * em;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != IsOpenProperty) return;
        if (IsOpen) confirmed = false;
        // 确认进行中被 Esc、点外面关掉：结果出来之前不算取消
        else if (!confirmed && !confirming) RaiseEvent(new RoutedEventArgs(CanceledEvent));
    }

    static void OnSync(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Popconfirm)d).Sync();

    /// <summary>文字、图标与按钮；没写的部分不占位置。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        var shown = ReadLocalValue(IconProperty) == DependencyProperty.UnsetValue ? Alert.IconFor(Tone) : Icon;
        icon.Data = shown;
        icon.Visibility = shown is null ? Visibility.Collapsed : Visibility.Visible;
        icon.Tone = new TextTone(Tone.Value);
        title.Text = Title;
        description.Text = Description;
        description.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(Panel, Title ?? "");
        AutomationProperties.SetHelpText(Panel, Description ?? "");
        cancel.Content = CancelText ?? lang.Cancel;
        confirm.Content = ConfirmText ?? lang.Confirm;
        confirm.Tone = Tone;
        cancel.IsEnabled = !confirming;
        confirm.IsLoading = confirming;
    }

    /// <summary>确认：进行中按钮加载、取消不可用；结果为 false 或失败时保持打开（调用方负责提示原因），否则关闭。</summary>
    async Task RunConfirm()
    {
        if (confirming) return;
        confirming = true;
        Sync();
        bool done;
        try
        {
            done = Confirm is not { } action || await action();
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceWarning($"Popconfirm 的 Confirm 失败，保持打开：{error}");
            done = false;
        }
        finally
        {
            confirming = false;
            Sync();
        }
        if (!done)
        {
            // 没确认成，浮层又已经在等的时候关掉了：这时才算取消
            if (!IsOpen) RaiseEvent(new RoutedEventArgs(CanceledEvent));
            return;
        }
        confirmed = true;
        SetOpen(false, OpenChangeReason.Confirm);
    }
}
