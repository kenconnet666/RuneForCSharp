using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 带说明的插图（vue-tsx-pro 的 Figure）：图片、图表、代码、表格下面（或上面）配一行说明，读屏把说明当作这块内容的名称。
/// <code>
/// &lt;rune:Figure Caption="图 1：近 12 个月的采购额"&gt;&lt;rune:Sparkline Data="{Binding Purchases}" /&gt;&lt;/rune:Figure&gt;
/// &lt;rune:Figure Caption="表 2：各供应商的交付周期" CaptionSide="top"&gt;&lt;rune:Table&gt;…&lt;/rune:Table&gt;&lt;/rune:Figure&gt;
/// </code>
/// 说明默认在下面、次要色、小一号，和内容隔 space.sm（按 Figure 的字号算）；表格的说明习惯放上面（CaptionSide=top）。
/// 内容与说明都跟着 <see cref="Align"/> 对齐（start 时内容拉满宽度）。说明的部件样式键是 <see cref="FigureParts.Caption"/>。
/// </summary>
[TemplatePart(Name = "PART_Caption", Type = typeof(ContentPresenter))]
[TemplatePart(Name = "PART_Content", Type = typeof(ContentPresenter))]
public class Figure : ContentControl
{
    /// <summary>说明：文字或控件；不写时就是内容本身。</summary>
    public static readonly DependencyProperty CaptionProperty = Register(nameof(Caption), typeof(object), null);

    /// <summary>说明在内容的上面还是下面，默认 bottom。</summary>
    public static readonly DependencyProperty CaptionSideProperty = Register(nameof(CaptionSide), typeof(FigureCaptionSide), FigureCaptionSide.Bottom);

    /// <summary>内容与说明的对齐，默认 start。</summary>
    public static readonly DependencyProperty AlignProperty = Register(nameof(Align), typeof(FigureAlign), FigureAlign.Start);

    ContentPresenter? caption;
    ContentPresenter? content;

    static DependencyProperty Register(string name, Type type, object? defaultValue) =>
        DependencyProperty.Register(name, type, typeof(Figure), new FrameworkPropertyMetadata(defaultValue, (d, _) => ((Figure)d).UpdateParts()));

    static Figure()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Figure), new FrameworkPropertyMetadata(typeof(Figure)));
        FocusableProperty.OverrideMetadata(typeof(Figure), new FrameworkPropertyMetadata(false));
        FontSizeProperty.OverrideMetadata(typeof(Figure), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((Figure)d).UpdateParts()));
    }

    /// <summary>说明。</summary>
    public object? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <summary>说明在哪一边。</summary>
    public FigureCaptionSide CaptionSide { get => (FigureCaptionSide)GetValue(CaptionSideProperty); set => SetValue(CaptionSideProperty, value); }

    /// <summary>对齐。</summary>
    public FigureAlign Align { get => (FigureAlign)GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        caption = GetTemplateChild("PART_Caption") as ContentPresenter;
        content = GetTemplateChild("PART_Content") as ContentPresenter;
        UpdateParts();
    }

    /// <summary>说明停在哪一边、和内容的间距（space.sm，按 Figure 的字号算）、两者的对齐。</summary>
    void UpdateParts()
    {
        if (caption is null || content is null) return;
        var top = CaptionSide.IsTop;
        var gap = Caption is null ? 0 : 0.5 * FontSize;
        DockPanel.SetDock(caption, top ? Dock.Top : Dock.Bottom);
        caption.Visibility = Caption is null ? Visibility.Collapsed : Visibility.Visible;
        content.Margin = new Thickness(0, top ? gap : 0, 0, top ? 0 : gap);
        var alignment = Align == FigureAlign.Center ? HorizontalAlignment.Center : Align == FigureAlign.End ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        content.HorizontalAlignment = alignment;
        caption.HorizontalAlignment = alignment == HorizontalAlignment.Stretch ? HorizontalAlignment.Left : alignment;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new FigurePeer(this);

    /// <summary>读屏：一个组，名称是说明的文字（TS 的 figcaption 给 figure 命名）。</summary>
    sealed class FigurePeer(Figure owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Figure);

        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Field.PlainText(owner.Caption) ?? "";
    }
}

/// <summary>Figure 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:FigureParts.Caption}" TargetType="ContentPresenter"&gt;</c>。</summary>
public static class FigureParts
{
    /// <summary>说明（TS 的 propsCaption）：ContentPresenter，次要色、sm。</summary>
    public static readonly PartKey Caption = new(typeof(Figure), nameof(Caption), typeof(ContentPresenter));
}
