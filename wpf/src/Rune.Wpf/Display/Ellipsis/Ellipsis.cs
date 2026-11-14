using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 截断的文字（vue-tsx-pro 的 Ellipsis）：超出一行（或 <see cref="Lines"/> 行）时省略，真的被截断了才给出看全文的办法——悬停或聚焦时的提示，或展开按钮。
/// 表格里的长名称、卡片里的描述、列表里的备注。
/// <code>
/// &lt;rune:Ellipsis Text="杭州远山精密机械有限公司（萧山二厂）" /&gt;              &lt;!-- 一行，截断时悬停看全文 --&gt;
/// &lt;rune:Ellipsis Text="{Binding Description}" Lines="3" Expandable="True" /&gt;   &lt;!-- 三行，截断时后面有"展开" --&gt;
/// </code>
/// 是否截断按文字不限宽（多行时不限高）量出的大小与实际的比（宽度、内容变了重新量），没截断时就是普通文字：不能聚焦、没有提示、没有按钮。
/// 截断时文字能用 Tab 聚焦（键盘聚焦也显示提示），提示的内容默认是文字本身，<see cref="Tooltip"/> 换成别的，写 null 不提示；
/// <see cref="Expandable"/> 时默认不提示，截断时后面是"展开"（accentText，悬停下划线），展开后显示全部、按钮换成"收起"（<see cref="IsExpanded"/> 可绑定）。
/// 只要省略、不需要看全文时用 Text 的 Truncate / LineClamp。
/// </summary>
public class Ellipsis : CompositeControl
{
    /// <summary>文字。</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(Ellipsis), new PropertyMetadata(null, OnSync));

    /// <summary>最多几行，默认 1。</summary>
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(nameof(Lines), typeof(int), typeof(Ellipsis), new PropertyMetadata(1, OnSync), lines => (int)lines > 0);

    /// <summary>截断时后面放"展开"按钮，展开后显示全部。</summary>
    public static readonly DependencyProperty ExpandableProperty = DependencyProperty.Register(nameof(Expandable), typeof(bool), typeof(Ellipsis), new PropertyMetadata(false, OnSync));

    /// <summary>展开了（默认双向绑定）。</summary>
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(Ellipsis),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSync));

    /// <summary>截断时悬停、聚焦显示的提示；不写时是文字本身（Expandable 时不提示），写 null 不提示。</summary>
    public static readonly DependencyProperty TooltipProperty = DependencyProperty.Register(nameof(Tooltip), typeof(object), typeof(Ellipsis), new PropertyMetadata(null, OnSync));

    readonly Flex root = new() { Align = FlexAlign.Baseline, Gap = Space.Xs };
    readonly Text text = new();
    readonly Text label = new() { Tone = TextTone.Accent, TextWrapping = TextWrapping.NoWrap };
    readonly ButtonBase toggle = new() { Visibility = Visibility.Collapsed, Cursor = Cursors.Hand };
    bool truncated;

    /// <summary>新建：一行，截断时悬停看全文。</summary>
    public Ellipsis()
    {
        toggle.Content = label;
        // 文字缩到放得下为止，按钮不缩（TS 的 min-width: 0 与 flex-shrink: 0）
        Flex.SetShrink(toggle, false);
        root.Children.Add(text);
        root.Children.Add(toggle);
        Root = root;
        toggle.Click += (_, _) => SetCurrentValue(IsExpandedProperty, !IsExpanded);
        toggle.MouseEnter += (_, _) => label.TextDecorations = TextDecorations.Underline;
        toggle.MouseLeave += (_, _) => label.TextDecorations = null;
        text.SizeChanged += (_, _) => CheckTruncation();
        Loaded += (_, _) => Sync();
        Sync();
    }

    /// <summary>文字。</summary>
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>最多几行。</summary>
    public int Lines { get => (int)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

    /// <summary>截断时可以展开。</summary>
    public bool Expandable { get => (bool)GetValue(ExpandableProperty); set => SetValue(ExpandableProperty, value); }

    /// <summary>展开了。</summary>
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>提示。</summary>
    public object? Tooltip { get => GetValue(TooltipProperty); set => SetValue(TooltipProperty, value); }

    /// <summary>真的被截断了。</summary>
    public bool IsTruncated => truncated;

    /// <summary>截断的那段文字（测试用）。</summary>
    internal Text TextPart => text;

    /// <summary>展开按钮（测试用）。</summary>
    internal ButtonBase Toggle => toggle;

    static void OnSync(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ellipsis = (Ellipsis)d;
        ellipsis.Sync();
        // 内容、行数变了截断后的大小可能不变（SizeChanged 不来），排完再量一次
        ellipsis.Dispatcher.BeginInvoke(ellipsis.CheckTruncation, DispatcherPriority.Loaded);
    }

    /// <summary>截断的方式、提示、能否聚焦与展开按钮，按现在量到的截断与否。</summary>
    void Sync()
    {
        var clamped = !IsExpanded;
        text.Text = Text;
        text.Truncate = clamped && Lines == 1;
        text.LineClamp = clamped && Lines > 1 ? Lines : null;
        text.Wrap = Lines > 1 || !clamped ? TextWrap.Wrap : null;
        // 多行时按钮贴着最后一行（TS 的 align-items: flex-end）
        root.Align = Lines > 1 ? FlexAlign.End : FlexAlign.Baseline;
        var tip = !truncated || !clamped ? null : ReadLocalValue(TooltipProperty) != DependencyProperty.UnsetValue ? Tooltip : Expandable ? null : Text;
        text.ToolTip = tip;
        // 截断时能聚焦：键盘用户也能看到提示里的全文
        text.Focusable = tip is not null;
        KeyboardNavigation.SetIsTabStop(text, tip is not null);
        AutomationProperties.SetHelpText(text, tip as string ?? "");
        toggle.Visibility = Expandable && (truncated || IsExpanded) ? Visibility.Visible : Visibility.Collapsed;
        label.Text = IsExpanded ? Provider.GetLang(this).EllipsisCollapse : Provider.GetLang(this).EllipsisExpand;
        toggle.SetValue(Popover.TriggerExpandedProperty, IsExpanded);
    }

    /// <summary>
    /// 量截断与否（同 TS 的 ResizeObserver 加每次渲染后）：同样的字体按不限宽（多行时按现在的宽度、不限高）排一遍，比实际的大就是截断了。
    /// 展开时不量（展开了当然放得下）。
    /// </summary>
    void CheckTruncation()
    {
        if (IsExpanded || !text.IsLoaded || text.ActualWidth <= 0) return;
        var probe = new TextBlock
        {
            Text = text.Text, FontFamily = text.FontFamily, FontSize = text.FontSize, FontWeight = text.FontWeight, FontStyle = text.FontStyle,
            FontStretch = text.FontStretch, LineHeight = text.LineHeight, LineStackingStrategy = text.LineStackingStrategy,
            TextWrapping = Lines > 1 ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };
        // 差 1 像素以内是小数取整
        probe.Measure(new Size(Lines > 1 ? text.ActualWidth : double.PositiveInfinity, double.PositiveInfinity));
        var over = Lines > 1 ? probe.DesiredSize.Height > text.ActualHeight + 1 : probe.DesiredSize.Width > text.ActualWidth + 1;
        if (over == truncated) return;
        truncated = over;
        Sync();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new EllipsisPeer(this);

    /// <summary>Ellipsis 自己不占读屏节点：里面的文字与按钮才是。</summary>
    sealed class EllipsisPeer(Ellipsis owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}
