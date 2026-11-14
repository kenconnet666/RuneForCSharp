using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 带说明的插图（vue-tsx-pro 的 Figure）：图片、图表、代码、表格下面（或上面）配一行说明，读屏把说明当作这块内容的名称。
/// <code>
/// &lt;rune:Figure Caption="图 1：近 12 个月的采购额"&gt;&lt;rune:Sparkline Values="{Binding Purchases}" /&gt;&lt;/rune:Figure&gt;
/// &lt;rune:Figure Caption="表 2：各供应商的交付周期" CaptionSide="top"&gt;&lt;rune:Table&gt;…&lt;/rune:Table&gt;&lt;/rune:Figure&gt;
/// </code>
/// 说明默认在下面、次要色、小一号，和内容隔 space.sm；表格的说明习惯放上面（CaptionSide=top）。内容与说明都跟着 <see cref="Align"/> 对齐
/// （start 时内容拉满宽度）。部件名在 <see cref="FigureParts"/>。
/// </summary>
public class Figure : ContentControl
{
    /// <summary>说明：文字或控件；不写时就是内容本身。</summary>
    public static readonly StyledProperty<object?> CaptionProperty = AvaloniaProperty.Register<Figure, object?>(nameof(Caption));

    /// <summary>说明在内容的上面还是下面，默认 bottom。</summary>
    public static readonly StyledProperty<FigureCaptionSide> CaptionSideProperty =
        AvaloniaProperty.Register<Figure, FigureCaptionSide>(nameof(CaptionSide), FigureCaptionSide.Bottom);

    /// <summary>内容与说明的对齐，默认 start。</summary>
    public static readonly StyledProperty<FigureAlign> AlignProperty = AvaloniaProperty.Register<Figure, FigureAlign>(nameof(Align), FigureAlign.Start);

    /// <summary>说明。</summary>
    public object? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <summary>说明在哪一边。</summary>
    public FigureCaptionSide CaptionSide { get => GetValue(CaptionSideProperty); set => SetValue(CaptionSideProperty, value); }

    /// <summary>对齐。</summary>
    public FigureAlign Align { get => GetValue(AlignProperty); set => SetValue(AlignProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CaptionProperty) PseudoClasses.Set(":captioned", Caption is not null);
        else if (change.Property == CaptionSideProperty) PseudoClasses.Set(":top", CaptionSide.IsTop);
        else if (change.Property == AlignProperty)
        {
            PseudoClasses.Set(":center", Align == FigureAlign.Center);
            PseudoClasses.Set(":end", Align == FigureAlign.End);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new FigurePeer(this);

    /// <summary>读屏：一个组，名称是说明的文字（TS 的 figcaption 给 figure 命名）。</summary>
    sealed class FigurePeer(Figure owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Field.PlainText(owner.Caption);
    }
}

/// <summary>Figure 模板里的部件名。</summary>
public static class FigureParts
{
    /// <summary>说明（TS 的 propsCaption）：ContentPresenter，次要色、sm。</summary>
    public const string Caption = "PART_Caption";
}
