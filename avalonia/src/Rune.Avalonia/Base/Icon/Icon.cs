using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Data;
using global::Avalonia.Media;
using LayoutSize = global::Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 图标（vue-tsx-pro 的 Icon）：按 Lucide 的画法描边画一个 <see cref="IconData"/>。
/// <code>
/// &lt;rune:Icon Data="{x:Static rune:Lucide.Search}" /&gt;                                  &lt;!-- 装饰性：读屏不读 --&gt;
/// &lt;rune:Icon Data="{x:Static rune:Lucide.Trash}" AutomationProperties.Name="删除" /&gt;  &lt;!-- 有名称：读屏当作图片 --&gt;
/// </code>
/// 大小默认 1em，跟随所在的字号；<see cref="Size"/> 改成档位或像素。颜色跟随文字（Foreground），<see cref="Tone"/> 按状态着色。
/// 图标数量大，自绘而不用模板（DESIGN §9）。从右到左排版里默认不翻转，有方向的图标设 <see cref="FlipInRtl"/>。
/// </summary>
public class Icon : Control
{
    /// <summary>图标数据：<c>{x:Static rune:Lucide.Search}</c>（TS 的 icon；C# 的成员不能与类同名）。</summary>
    public static readonly StyledProperty<IconData?> DataProperty = AvaloniaProperty.Register<Icon, IconData?>(nameof(Data));

    /// <summary>大小：档位（xs–xl，取主题字号）或像素数；不写时 1em，跟随所在的字号。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<Icon, TextSize>(nameof(Size));

    /// <summary>色调（状态图标）；不写时跟随文字颜色。</summary>
    public static readonly StyledProperty<TextTone> ToneProperty = AvaloniaProperty.Register<Icon, TextTone>(nameof(Tone));

    /// <summary>线宽（24 格画布上的单位），主题里默认取 strokeWidths.md。</summary>
    public static readonly StyledProperty<double> StrokeWidthProperty = AvaloniaProperty.Register<Icon, double>(nameof(StrokeWidth), 2);

    /// <summary>从右到左排版时水平翻转：箭头、chevron 这类有方向的图标。</summary>
    public static readonly StyledProperty<bool> FlipInRtlProperty = AvaloniaProperty.Register<Icon, bool>(nameof(FlipInRtl));

    /// <summary>颜色（继承自外面的文字颜色）。</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Icon>();

    /// <summary>字号（继承）：不写 Size 时图标就是这么大。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Icon>();

    /// <summary>Size 是档位名时，从主题资源取到的字号（绑定到 Rune.FontSize.*，主题与作用域换了跟着变）。</summary>
    static readonly StyledProperty<double> ResolvedSizeProperty = AvaloniaProperty.Register<Icon, double>("ResolvedSize", double.NaN);

    IDisposable? sizeBinding;
    IDisposable? toneBinding;
    IPen? pen;

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, ForegroundProperty, StrokeWidthProperty);
        AffectsMeasure<Icon>(SizeProperty, FontSizeProperty, ResolvedSizeProperty);
    }

    /// <summary>图标数据。</summary>
    public IconData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <summary>大小：档位或像素数；不写时 1em。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>色调；不写时跟随文字颜色。</summary>
    public TextTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>线宽（24 格画布上的单位）。</summary>
    public double StrokeWidth { get => GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }

    /// <summary>从右到左排版时水平翻转。</summary>
    public bool FlipInRtl { get => GetValue(FlipInRtlProperty); set => SetValue(FlipInRtlProperty, value); }

    /// <summary>颜色。</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>实际的边长：像素数 &gt; 档位的主题字号 &gt; 继承的字号。</summary>
    public double ActualSize => Size.Pixels ?? (Size.ResourceKey is not null && GetValue(ResolvedSizeProperty) is var resolved && !double.IsNaN(resolved) ? resolved : FontSize);

    /// <summary>默认不跟着从右到左翻转（Avalonia 默认整棵子树镜像）；FlipInRtl 时照常翻转。</summary>
    protected override bool BypassFlowDirectionPolicies => !FlipInRtl;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty)
        {
            sizeBinding?.Dispose();
            sizeBinding = Size.ResourceKey is { } key ? Bind(ResolvedSizeProperty, this.GetResourceObservable(key)) : null;
        }
        else if (change.Property == ToneProperty)
        {
            PseudoClasses.Swap(change.GetOldValue<TextTone>().Value, change.GetNewValue<TextTone>().Value);
            // 色调的颜色按模板优先级绑定主题资源：盖过继承的文字颜色，使用方带伪类的样式仍能盖过它（同主题里的伪类样式），
            // 但每个图标不用为 7 条色调样式各建一个激活器（图标数量大，DESIGN §9）
            toneBinding?.Dispose();
            toneBinding = Tone.BrushKey is { } key ? Bind(ForegroundProperty, this.GetResourceObservable(key), BindingPriority.Template) : null;
        }
        else if (change.Property == FlipInRtlProperty)
        {
            InvalidateMirrorTransform();
        }
        else if (change.Property == ForegroundProperty || change.Property == StrokeWidthProperty)
        {
            pen = null;
        }
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize) => new(ActualSize, ActualSize);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush) return;
        var scale = ActualSize / IconData.Size;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            if (data.FillGeometry is { } fill) context.DrawGeometry(brush, null, fill);
            pen ??= new Pen(brush, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, pen, data.Geometry);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new IconAutomationPeer(this);

    /// <summary>有名称时是一张图片，没有名称时是装饰（不进控件树与内容树）。</summary>
    sealed class IconAutomationPeer(Icon owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsControlElementCore() => !string.IsNullOrEmpty(AutomationProperties.GetName(Owner));

        protected override bool IsContentElementCore() => IsControlElementCore();
    }
}
