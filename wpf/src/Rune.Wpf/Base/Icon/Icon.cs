using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 图标（vue-tsx-pro 的 Icon）：按 Lucide 的画法描边画一个 <see cref="IconData"/>。
/// <code>
/// &lt;rune:Icon Data="{x:Static rune:Lucide.Search}" /&gt;                                  &lt;!-- 装饰性：读屏不读 --&gt;
/// &lt;rune:Icon Data="{x:Static rune:Lucide.Trash}" AutomationProperties.Name="删除" /&gt;   &lt;!-- 有名称：读屏当作图片 --&gt;
/// </code>
/// 大小默认 1em，跟随所在的字号；<see cref="Size"/> 改成档位或像素。颜色跟随文字（Foreground），<see cref="Tone"/> 按状态着色。
/// 自绘（OnRender）而不用模板（DESIGN §9）；从 Control 派生，外层的隐式样式才进得了别的组件的模板（DESIGN §7）。
/// 从右到左排版里默认不翻转，有方向的图标设 <see cref="FlipInRtl"/>。
/// </summary>
public class Icon : Control
{
    /// <summary>图标数据：<c>{x:Static rune:Lucide.Search}</c>（TS 的 icon；C# 的成员不能与类同名）。</summary>
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(IconData), typeof(Icon), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>大小：档位（xs–xl，取主题字号）或像素数；不写时 1em，跟随所在的字号。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(TextSize), typeof(Icon), new FrameworkPropertyMetadata(default(TextSize), FrameworkPropertyMetadataOptions.AffectsMeasure, OnSizeChanged));

    /// <summary>色调（状态图标）；不写时跟随文字颜色。主题样式里按取值设 Foreground。</summary>
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(Tone), typeof(Icon));

    /// <summary>线宽（24 格画布上的单位），主题里默认取 strokeWidths.md。</summary>
    public static readonly DependencyProperty StrokeWidthProperty = DependencyProperty.Register(
        nameof(StrokeWidth), typeof(double), typeof(Icon), new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>从右到左排版时水平翻转：箭头、chevron 这类有方向的图标。</summary>
    public static readonly DependencyProperty FlipInRtlProperty = DependencyProperty.Register(
        nameof(FlipInRtl), typeof(bool), typeof(Icon), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Size 是档位名时，从主题资源取到的字号（资源引用 Rune.FontSize.*，主题与作用域换了跟着变）。</summary>
    static readonly DependencyProperty ResolvedSizeProperty = DependencyProperty.Register(
        "ResolvedSize", typeof(double), typeof(Icon), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

    Pen? pen;

    static Icon()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(typeof(Icon)));
        // 图标不可交互：不进 Tab 顺序
        FocusableProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(false));
        // 颜色、方向变了要重画（Control 的这几个属性元数据里不带 AffectsRender）
        ForegroundProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
        FlowDirectionProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(FlowDirection.LeftToRight, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
        FontSizeProperty.OverrideMetadata(typeof(Icon), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));
    }

    /// <summary>图标数据。</summary>
    public IconData? Data { get => (IconData?)GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <summary>大小：档位或像素数；不写时 1em。</summary>
    public TextSize Size { get => (TextSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>色调；不写时跟随文字颜色。</summary>
    public Tone Tone { get => (Tone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>线宽（24 格画布上的单位）。</summary>
    public double StrokeWidth { get => (double)GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }

    /// <summary>从右到左排版时水平翻转。</summary>
    public bool FlipInRtl { get => (bool)GetValue(FlipInRtlProperty); set => SetValue(FlipInRtlProperty, value); }

    /// <summary>实际的边长：像素数 &gt; 档位的主题字号 &gt; 继承的字号。</summary>
    public double ActualSize => Size.Pixels ?? (Size.ResourceKey is not null && GetValue(ResolvedSizeProperty) is double resolved && !double.IsNaN(resolved) ? resolved : FontSize);

    /// <summary>外面从右到左时整棵子树已经镜像：不翻转的图标在这里再镜像一次抵消。</summary>
    internal bool CancelsMirroring => FlowDirection == FlowDirection.RightToLeft && !FlipInRtl;

    static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var icon = (Icon)d;
        if (((TextSize)e.NewValue).ResourceKey is { } key) icon.SetResourceReference(ResolvedSizeProperty, key);
        else icon.ClearValue(ResolvedSizeProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ForegroundProperty || e.Property == StrokeWidthProperty) pen = null;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint) => new(ActualSize, ActualSize);

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Data is not { } data || Foreground is not { } brush) return;
        var size = ActualSize;
        var scale = size / IconData.Size;
        drawingContext.PushTransform(CancelsMirroring ? new MatrixTransform(-scale, 0, 0, scale, size, 0) : new ScaleTransform(scale, scale));
        if (data.FillGeometry is { } fill) drawingContext.DrawGeometry(brush, null, fill);
        drawingContext.DrawGeometry(null, pen ??= CreatePen(brush), data.Geometry);
        drawingContext.Pop();
    }

    Pen CreatePen(Brush brush)
    {
        var created = new Pen(brush, StrokeWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (created.CanFreeze) created.Freeze();
        return created;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new IconAutomationPeer(this);

    /// <summary>有名称时是一张图片，没有名称时是装饰（不进控件树与内容树）。</summary>
    sealed class IconAutomationPeer(Icon owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => nameof(Icon);

        protected override bool IsControlElementCore() => !string.IsNullOrEmpty(AutomationProperties.GetName(Owner));

        protected override bool IsContentElementCore() => IsControlElementCore();
    }
}
