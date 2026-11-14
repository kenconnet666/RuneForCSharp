using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>
/// 截断的文字（vue-tsx-pro 的 Ellipsis）：超出一行（或 <see cref="Lines"/> 行）时省略，真的被截断了才给出看全文的办法——悬停或聚焦时的提示，或展开按钮。
/// 表格里的长名称、卡片里的描述、列表里的备注。
/// <code>
/// &lt;rune:Ellipsis Text="杭州远山精密机械有限公司（萧山二厂）" /&gt;              &lt;!-- 一行，截断时悬停看全文 --&gt;
/// &lt;rune:Ellipsis Text="{Binding Description}" Lines="3" Expandable="True" /&gt;   &lt;!-- 三行，截断时后面有"展开" --&gt;
/// </code>
/// 是否截断按排好的文字量出（宽度、内容变了重新量），没截断时就是普通文字：不能聚焦、没有提示、没有按钮。
/// 截断时文字能用 Tab 聚焦（键盘用户也能看全文），提示的内容默认是文字本身，<see cref="Tooltip"/> 换成别的，写 null 不提示；
/// <see cref="Expandable"/> 时默认不提示，截断时后面是"展开"，展开后显示全部、按钮换成"收起"（<see cref="IsExpanded"/> 可绑定）。
/// 只要省略、不需要看全文时用 Text 的 Truncate / LineClamp。部件名在 <see cref="EllipsisParts"/>。
/// </summary>
public class Ellipsis : Control
{
    /// <summary>文字。</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Ellipsis, string?>(nameof(Text));

    /// <summary>最多几行，默认 1。</summary>
    public static readonly StyledProperty<int> LinesProperty = AvaloniaProperty.Register<Ellipsis, int>(nameof(Lines), 1, validate: lines => lines > 0);

    /// <summary>截断时后面放"展开"按钮，展开后显示全部。</summary>
    public static readonly StyledProperty<bool> ExpandableProperty = AvaloniaProperty.Register<Ellipsis, bool>(nameof(Expandable));

    /// <summary>展开了（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<Ellipsis, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>截断时悬停、聚焦显示的提示；不写时是文字本身（Expandable 时不提示），写 null 不提示。</summary>
    public static readonly StyledProperty<object?> TooltipProperty = AvaloniaProperty.Register<Ellipsis, object?>(nameof(Tooltip));

    readonly Flex root = new() { Align = FlexAlign.Baseline, Gap = Space.Xs };
    readonly Text text = new() { Name = EllipsisParts.Text };
    readonly Link toggle = new() { Name = EllipsisParts.Toggle, Underline = LinkUnderline.Hover, IsVisible = false };
    bool truncated;
    bool measuring;

    /// <summary>新建：一行，截断时悬停看全文。</summary>
    public Ellipsis()
    {
        // 文字缩到放得下为止，按钮不缩（TS 的 min-width: 0 与 flex-shrink: 0）
        Flex.SetShrink(toggle, false);
        root.Children.AddRange([text, toggle]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        toggle.Click += (_, _) => SetCurrentValue(IsExpandedProperty, !IsExpanded);
        text.LayoutUpdated += (_, _) => CheckTruncation();
        Sync();
    }

    /// <summary>文字。</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>最多几行。</summary>
    public int Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

    /// <summary>截断时可以展开。</summary>
    public bool Expandable { get => GetValue(ExpandableProperty); set => SetValue(ExpandableProperty, value); }

    /// <summary>展开了。</summary>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>提示。</summary>
    public object? Tooltip { get => GetValue(TooltipProperty); set => SetValue(TooltipProperty, value); }

    /// <summary>真的被截断了（排好的文字有省略的行）。</summary>
    public bool IsTruncated => truncated;

    /// <summary>截断的那段文字（测试用）。</summary>
    internal Text TextPart => text;

    /// <summary>展开按钮（测试用）。</summary>
    internal Link Toggle => toggle;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == LinesProperty || change.Property == ExpandableProperty
            || change.Property == IsExpandedProperty || change.Property == TooltipProperty) Sync();
    }

    /// <summary>截断的方式、提示、能否聚焦与展开按钮，按现在量到的截断与否。</summary>
    void Sync()
    {
        var clamped = !IsExpanded;
        text.Text = Text;
        text.Truncate = clamped && Lines == 1;
        text.LineClamp = clamped && Lines > 1 ? Lines : null;
        text.Wrap = Lines > 1 || !clamped ? TextWrap.Wrap : null;
        // 多行时块级、按钮贴着最后一行（TS 的 align-items: flex-end）
        root.Align = Lines > 1 ? FlexAlign.End : FlexAlign.Baseline;
        var tip = !truncated || !clamped ? null : IsSet(TooltipProperty) ? Tooltip : Expandable ? null : Text;
        ToolTip.SetTip(text, tip);
        // 截断时能聚焦：键盘用户也能看到提示里的全文
        text.Focusable = tip is not null;
        KeyboardNavigation.SetIsTabStop(text, tip is not null);
        AutomationProperties.SetHelpText(text, tip as string);
        toggle.IsVisible = Expandable && (truncated || IsExpanded);
        toggle.Content = IsExpanded ? Provider.GetLang(this).EllipsisCollapse : Provider.GetLang(this).EllipsisExpand;
        toggle.SetValue(Popover.TriggerExpandedProperty, IsExpanded);
        PseudoClasses.Set(":truncated", truncated);
        PseudoClasses.Set(":expanded", IsExpanded);
    }

    /// <summary>
    /// 排好以后量一次（同 TS 的 ResizeObserver 加每次渲染后）：排好的文字里有省略过的行就是截断了。展开时不量（展开了当然放得下）。
    /// 改了截断与否会再排一次，排完的这一下不再量，免得来回。
    /// </summary>
    void CheckTruncation()
    {
        if (measuring || IsExpanded || text.TextLayout is not { } layout) return;
        var over = layout.TextLines.Any(line => line.HasCollapsed);
        if (over == truncated) return;
        measuring = true;
        Dispatcher.UIThread.Post(() =>
        {
            truncated = over;
            Sync();
            measuring = false;
        }, DispatcherPriority.Loaded);
    }

    /// <inheritdoc />
    protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override global::Avalonia.Size ArrangeOverride(global::Avalonia.Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new EllipsisPeer(this);

    /// <summary>Ellipsis 自己不占读屏节点：里面的文字与按钮才是。</summary>
    sealed class EllipsisPeer(Ellipsis owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>Ellipsis 的部件名：样式里用 <c>rune|Ellipsis rune|Text#PART_Text</c> 这样选中。</summary>
public static class EllipsisParts
{
    /// <summary>截断的那段文字（TS 的 propsText）。</summary>
    public const string Text = "PART_Text";

    /// <summary>展开按钮（TS 的 propsToggle）。</summary>
    public const string Toggle = "PART_Toggle";
}
