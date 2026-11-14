using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PlatformGrid = Avalonia.Controls.Grid;

namespace Rune.Avalonia;

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
/// 浮层以问题为读屏名称、说明为帮助文字。后果严重、要多写几句或填理由的用 <see cref="AlertDialog"/>。部件名在 <see cref="PopconfirmParts"/>。
/// </summary>
public class Popconfirm : Popover
{
    /// <summary>问题，同时是浮层的读屏名称。</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Popconfirm, string?>(nameof(Title));

    /// <summary>补充说明（读屏的帮助文字）。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<Popconfirm, string?>(nameof(Description));

    /// <summary>确认按钮与图标的色调，默认 warning；删除这类破坏性操作用 danger。</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty = AvaloniaProperty.Register<Popconfirm, StatusTone>(nameof(Tone), StatusTone.Warning);

    /// <summary>图标；不写时按色调选，写 null 不显示。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<Popconfirm, IconData?>(nameof(Icon));

    /// <summary>确认时做的事：任务完成后关闭；结果为 false 或失败时保持打开。不写时点了就关。</summary>
    public static readonly StyledProperty<Func<Task<bool>>?> ConfirmProperty = AvaloniaProperty.Register<Popconfirm, Func<Task<bool>>?>(nameof(Confirm));

    /// <summary>确认按钮的文字；不写时取语言包（确定）。</summary>
    public static readonly StyledProperty<string?> ConfirmTextProperty = AvaloniaProperty.Register<Popconfirm, string?>(nameof(ConfirmText));

    /// <summary>取消按钮的文字；不写时取语言包（取消）。</summary>
    public static readonly StyledProperty<string?> CancelTextProperty = AvaloniaProperty.Register<Popconfirm, string?>(nameof(CancelText));

    readonly Icon icon = new() { Name = PopconfirmParts.Icon };
    readonly Text title = new() { Name = PopconfirmParts.Title, Weight = TextWeight.Medium };
    readonly Small description = new() { Name = PopconfirmParts.Description, Tone = TextTone.Muted };
    readonly Flex footer = new() { Name = PopconfirmParts.Footer, Justify = FlexJustify.End, Gap = Space.Sm };
    readonly Button cancel = new() { Name = PopconfirmParts.Cancel, Size = TextSize.Sm, Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral };
    readonly Button confirm = new() { Name = PopconfirmParts.Confirm, Size = TextSize.Sm };
    readonly PlatformGrid body = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };
    bool confirming;
    // 确认后关闭的不算取消
    bool confirmed;

    static Popconfirm() => PlacementProperty.OverrideDefaultValue<Popconfirm>(FloatingPlacement.Top);

    /// <summary>新建：图标、问题、说明与"取消 / 确认"。</summary>
    public Popconfirm()
    {
        PlatformGrid.SetRowSpan(icon, 2);
        PlatformGrid.SetColumn(title, 1);
        PlatformGrid.SetColumn(description, 1);
        PlatformGrid.SetRow(description, 1);
        PlatformGrid.SetRow(footer, 2);
        PlatformGrid.SetColumnSpan(footer, 2);
        footer.Children.AddRange([cancel, confirm]);
        body.Children.AddRange([icon, title, description, footer]);
        Content = body;
        cancel.Click += (_, _) => SetOpen(false, OpenChangeReason.Close);
        confirm.Click += (_, _) => _ = RunConfirm();
        Sync();
    }

    /// <summary>问题。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>补充说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>色调。</summary>
    public StatusTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>确认时做的事。</summary>
    public Func<Task<bool>>? Confirm { get => GetValue(ConfirmProperty); set => SetValue(ConfirmProperty, value); }

    /// <summary>确认按钮的文字。</summary>
    public string? ConfirmText { get => GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }

    /// <summary>取消按钮的文字。</summary>
    public string? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <summary>取消了：取消按钮、点外面、Esc 关闭（确认后关闭的不算；确认进行中被关掉时，等结果出来、没确认成才算）。</summary>
    public event EventHandler<RoutedEventArgs>? Canceled
    {
        add => AddHandler(CanceledEvent, value);
        remove => RemoveHandler(CanceledEvent, value);
    }

    /// <summary><see cref="Canceled"/> 的路由事件（直接）。</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CanceledEvent = RoutedEvent.Register<Popconfirm, RoutedEventArgs>(nameof(Canceled), RoutingStrategies.Direct);

    /// <summary>取消、确认两个按钮（测试用）。</summary>
    internal (Button Cancel, Button Confirm) Actions => (cancel, confirm);

    // 打开时焦点落在取消上：误按 Enter 不会执行
    private protected override InputElement? FocusTarget() => cancel;

    // 最宽 20rem（按根字号 16 换算）；图标与文字之间 space.sm，行间 space.xs，按钮行上面再空 space.sm
    private protected override void OnMetrics(double em)
    {
        Panel.MaxWidth = 320;
        body.ColumnSpacing = this.Resource<double>(ThemeKeys.Space.Sm) * em;
        body.RowSpacing = this.Resource<double>(ThemeKeys.Space.Xs) * em;
        footer.Margin = new Thickness(0, this.Resource<double>(ThemeKeys.Space.Sm) * em, 0, 0);
        icon.Margin = new Thickness(0, 0.15 * em, 0, 0);
        title.LineHeight = this.Resource<double>(ThemeKeys.LineHeight.Tight) * em;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 按钮的默认文字取所在位置的语言包
        Sync();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (IsOpen) confirmed = false;
            // 确认进行中被 Esc、点外面关掉：结果出来之前不算取消
            else if (!confirmed && !confirming) RaiseEvent(new RoutedEventArgs(CanceledEvent));
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == ToneProperty || change.Property == IconProperty
                 || change.Property == ConfirmTextProperty || change.Property == CancelTextProperty) Sync();
    }

    /// <summary>文字、图标与按钮；没写的部分不占位置。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        var shown = IsSet(IconProperty) ? Icon : Alert.IconFor(Tone);
        icon.Data = shown;
        icon.IsVisible = shown is not null;
        icon.Tone = new TextTone(Tone.Value);
        title.Text = Title;
        description.Text = Description;
        description.IsVisible = !string.IsNullOrEmpty(Description);
        AutomationProperties.SetName(Panel, Title);
        AutomationProperties.SetHelpText(Panel, Description);
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

/// <summary>Popconfirm 的部件名：样式里用 <c>rune|Popconfirm rune|Text#PART_Title</c> 这样选中（在弹出层里，逻辑上仍是 Popconfirm 的后代）。</summary>
public static class PopconfirmParts
{
    /// <summary>图标（TS 的 propsIcon）。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>问题（TS 的 propsTitle）。</summary>
    public const string Title = "PART_Title";

    /// <summary>说明（TS 的 propsDescription）。</summary>
    public const string Description = "PART_Description";

    /// <summary>按钮行（TS 的 propsFooter）。</summary>
    public const string Footer = "PART_Footer";

    /// <summary>取消按钮（TS 的 propsCancel）。</summary>
    public const string Cancel = "PART_Cancel";

    /// <summary>确认按钮（TS 的 propsConfirm）。</summary>
    public const string Confirm = "PART_Confirm";
}
