using global::Avalonia;
using global::Avalonia.Animation;
using global::Avalonia.Animation.Easings;
using global::Avalonia.Automation;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Media;
using global::Avalonia.Styling;
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

    /// <summary>
    /// 描线动画（一笔画出）：挂上时为 true 就在挂上时播，之后每次由 false 变为 true 再播（时长 durations.xl、缓动 enter，
    /// 系统关闭动画时直接画好）。不写（null）的图标不建动画。
    /// </summary>
    public static readonly StyledProperty<bool?> DrawProperty = AvaloniaProperty.Register<Icon, bool?>(nameof(Draw));

    /// <summary>颜色（继承自外面的文字颜色）。</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Icon>();

    /// <summary>描线的进度：1 是画完（平常就是 1，只有描线时从 0 走到 1）。</summary>
    static readonly StyledProperty<double> DrawnProperty = AvaloniaProperty.Register<Icon, double>("Drawn", 1);

    /// <summary>字号（继承）：不写 Size 时图标就是这么大。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Icon>();

    /// <summary>Size 是档位名时，从主题资源取到的字号（绑定到 Rune.FontSize.*，主题与作用域换了跟着变）。</summary>
    TemplateResources? resources;

    static readonly StyledProperty<double> ResolvedSizeProperty = AvaloniaProperty.Register<Icon, double>("ResolvedSize", double.NaN);

    IPen? pen;
    CancellationTokenSource? drawing;

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, ForegroundProperty, StrokeWidthProperty, DrawnProperty);
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

    /// <summary>描线动画；不写时不建动画。</summary>
    public bool? Draw { get => GetValue(DrawProperty); set => SetValue(DrawProperty, value); }

    /// <summary>颜色。</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>描线的进度（测试用）。</summary>
    internal double Drawn => GetValue(DrawnProperty);

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
            Resolved.Bind(this, ResolvedSizeProperty, Size.ResourceKey);
        }
        else if (change.Property == ToneProperty)
        {
            PseudoClasses.Swap(change.GetOldValue<TextTone>().Value, change.GetNewValue<TextTone>().Value);
            // 色调的颜色按模板优先级绑定主题资源（TemplateResources）：不写成 7 条伪类样式（图标数量大，DESIGN §9）
            (resources ??= new(this)).Bind(ForegroundProperty, Tone.BrushKey);
        }
        else if (change.Property == FlipInRtlProperty)
        {
            InvalidateMirrorTransform();
        }
        else if (change.Property == ForegroundProperty || change.Property == StrokeWidthProperty)
        {
            pen = null;
        }
        else if (change.Property == DrawProperty && change.GetNewValue<bool?>() == true && change.GetOldValue<bool?>() != true && VisualRoot is not null)
        {
            PlayDraw();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Draw == true) PlayDraw();
    }

    /// <summary>先擦掉再画：挂上时在第一次绘制前就把进度置 0，不会先闪一下完整的图标。</summary>
    async void PlayDraw()
    {
        drawing?.Cancel();
        var duration = Motion.Duration(this, "Rune.Duration.Xl", TimeSpan.FromMilliseconds(500));
        if (duration <= TimeSpan.Zero)
        {
            SetValue(DrawnProperty, 1);
            return;
        }
        var cancel = drawing = new CancellationTokenSource();
        // 先写终值：动画在动画优先级上盖住它，结束或被打断后留下的就是画完的图标
        SetValue(DrawnProperty, 1);
        _ = new Animation
        {
            Duration = duration,
            Easing = this.Resource<Easing>("Rune.Easing.Enter", new CubicEaseOut()),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(DrawnProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(DrawnProperty, 1d) } },
            },
        }.RunAsync(this, cancel.Token);
        // 结束按真实时间算（同 Dismissal；headless 的动画时钟不走），到点停掉动画
        await Task.Delay(duration);
        cancel.Cancel();
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
            context.DrawGeometry(null, pen, Drawn < 1 ? PartialGeometry(data, Drawn) : data.Geometry);
        }
    }

    /// <summary>描线中：每一笔画出的部分连成折线。</summary>
    static StreamGeometry PartialGeometry(IconData data, double progress)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        foreach (var stroke in data.Strokes.Partial(progress))
        {
            context.BeginFigure(new Point(stroke[0].X, stroke[0].Y), false);
            foreach (var (x, y) in stroke.Skip(1)) context.LineTo(new Point(x, y));
            context.EndFigure(false);
        }
        return geometry;
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
