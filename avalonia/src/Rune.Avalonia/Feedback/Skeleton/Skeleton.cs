using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Styling;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 骨架屏（vue-tsx-pro 的 Skeleton）：内容加载时的占位，一道光带从起始边扫过。包着真实内容时，内容照常排版但不可见、不可交互，
/// 占位正好是内容的大小，加载完换成内容时布局不跳；不包内容时用 <see cref="Shape"/> 选常用形状。
/// <code>
/// &lt;rune:Skeleton IsLoading="{Binding Loading}"&gt;&lt;rune:Text Text="{Binding Name}" /&gt;&lt;/rune:Skeleton&gt;
/// &lt;rune:Skeleton Shape="circle" /&gt;
/// &lt;rune:Skeleton Shape="heading" Delay="120" /&gt;     &lt;!-- 几块骨架依次扫过 --&gt;
/// </code>
/// 底色与光带都是半透明（9%）的正文色，叠在任何表面上都深一档；圆角 radii.sm（圆 full，按钮、图片 md）。扫光 2 秒一轮
/// （1.4 秒扫过、0.6 秒停顿，先慢后快再慢），从右到左时反向；系统关闭动画时不扫。占位对读屏隐藏；整块区域在加载时设读屏的"忙"状态。
/// 内容是部件 <see cref="SkeletonParts.Content"/>（加载时包在一层不可用、不可见的容器里）。
/// </summary>
public class Skeleton : Control
{
    /// <summary>是否在加载（默认 true）；false 时直接显示内容。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Skeleton, bool>(nameof(IsLoading), true);

    /// <summary>形状；不写时大小由内容决定。</summary>
    public static readonly StyledProperty<SkeletonShape> ShapeProperty = AvaloniaProperty.Register<Skeleton, SkeletonShape>(nameof(Shape));

    /// <summary>扫光的延迟（毫秒）：几块骨架依次扫过、连成一道波时用。</summary>
    public static readonly StyledProperty<int> DelayProperty = AvaloniaProperty.Register<Skeleton, int>(nameof(Delay));

    /// <summary>内容。</summary>
    public static readonly StyledProperty<Control?> ContentProperty = AvaloniaProperty.Register<Skeleton, Control?>(nameof(Content));

    /// <summary>字号（继承），形状的 em 尺寸按它算。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<Skeleton>();

    /// <summary>光带的位置：-1 在起始边外，1 在结束边外（动画驱动）。</summary>
    static readonly StyledProperty<double> PhaseProperty = AvaloniaProperty.Register<Skeleton, double>("Phase", -1);

    static readonly StyledProperty<Color> TextColorProperty = AvaloniaProperty.Register<Skeleton, Color>("TextColor");
    static readonly StyledProperty<double> RadiusFactorProperty = AvaloniaProperty.Register<Skeleton, double>("RadiusFactor");

    readonly Border holder = new() { Name = SkeletonParts.Content };
    readonly TemplateResources resources;
    CancellationTokenSource? sweep;

    static Skeleton()
    {
        AffectsMeasure<Skeleton>(ShapeProperty, IsLoadingProperty, FontSizeProperty);
        AffectsRender<Skeleton>(PhaseProperty, TextColorProperty, RadiusFactorProperty);
    }

    /// <summary>新建：颜色、圆角绑到主题。</summary>
    public Skeleton()
    {
        resources = new TemplateResources(this);
        resources.Bind(TextColorProperty, "Rune.Color.Text");
        BindRadius();
        LogicalChildren.Add(holder);
        VisualChildren.Add(holder);
        UpdateLoading();
    }

    /// <summary>是否在加载。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>形状。</summary>
    public SkeletonShape Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    /// <summary>扫光的延迟（毫秒）。</summary>
    public int Delay { get => GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    double Em => FontSize;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty) holder.Child = Content;
        else if (change.Property == IsLoadingProperty) UpdateLoading();
        else if (change.Property == ShapeProperty) BindRadius();
        else if (change.Property == DelayProperty) Restart();
    }

    /// <summary>圆角：circle 是 full，button、image 是 md，其余 sm。</summary>
    void BindRadius() => resources.Bind(RadiusFactorProperty, Shape.Value switch
    {
        "circle" => null,
        "button" or "image" => "Rune.Radius.Md",
        _ => "Rune.Radius.Sm",
    });

    /// <summary>加载时内容不可见、不可交互，占位对读屏隐藏；加载完内容照常显示。</summary>
    void UpdateLoading()
    {
        holder.Opacity = IsLoading ? 0 : 1;
        holder.IsEnabled = !IsLoading;
        holder.IsHitTestVisible = !IsLoading;
        AutomationProperties.SetAccessibilityView(this, IsLoading ? AccessibilityView.Raw : AccessibilityView.Default);
        AutomationProperties.SetAccessibilityView(holder, IsLoading ? AccessibilityView.Raw : AccessibilityView.Default);
        Restart();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Restart();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        sweep?.Cancel();
        sweep = null;
    }

    /// <summary>扫光：加载中、在可视树里时一直扫（-1 → 1 用 1.4 秒，再停 0.6 秒）。</summary>
    void Restart()
    {
        sweep?.Cancel();
        sweep = null;
        // 系统关闭动画时不扫：静止的占位已经说明在加载
        if (!IsLoading || VisualRoot is null || !Motion.Enabled) return;
        sweep = new CancellationTokenSource();
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(2),
            Delay = TimeSpan.FromMilliseconds(Delay),
            IterationCount = IterationCount.Infinite,
            Easing = new SineEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(PhaseProperty, -1d) } },
                new KeyFrame { Cue = new Cue(0.7), Setters = { new Setter(PhaseProperty, 1d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(PhaseProperty, 1d) } },
            },
        };
        _ = animation.RunAsync(this, sweep.Token);
    }

    /// <summary>形状决定的大小（em）；没有形状时为 null，跟着内容。</summary>
    LayoutSize? ShapeSize(LayoutSize available) => Shape.Value switch
    {
        "text" => new LayoutSize(double.IsInfinity(available.Width) ? 0 : available.Width, Em),
        "heading" => new LayoutSize(double.IsInfinity(available.Width) ? 0 : 0.4 * available.Width, 1.5 * Em),
        "circle" => new LayoutSize(2.5 * Em, 2.5 * Em),
        "button" => new LayoutSize(6 * Em, ControlHeight.Of(Em, ControlHeight.GetFactor(this))),
        "image" => double.IsInfinity(available.Width) ? new LayoutSize(0, 0) : new LayoutSize(available.Width, available.Width * 9 / 16),
        _ => null,
    };

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        holder.Measure(availableSize);
        return IsLoading && ShapeSize(availableSize) is { } shape ? shape : holder.DesiredSize;
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        holder.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (!IsLoading || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var bounds = new Rect(Bounds.Size);
        var radius = Shape.Value == "circle" ? bounds.Height / 2 : Math.Min(GetValue(RadiusFactorProperty) * Em, bounds.Height / 2);
        var text = GetValue(TextColorProperty);
        var tint = Color.FromArgb((byte)Math.Round(255 * 0.09), text.R, text.G, text.B);
        var clip = new RoundedRect(bounds, radius);
        context.DrawRectangle(new SolidColorBrush(tint), null, clip);
        // 光带与元素一样宽，从起始边外扫到结束边外（从右到左时 Avalonia 整体镜像，反向扫）
        var offset = GetValue(PhaseProperty) * bounds.Width;
        var band = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.Transparent, 0.15),
                new GradientStop(tint, 0.5),
                new GradientStop(Colors.Transparent, 0.85),
            },
        };
        using (context.PushClip(clip))
            context.DrawRectangle(band, null, new Rect(offset, 0, bounds.Width, bounds.Height));
    }
}

/// <summary>Skeleton 的部件名。</summary>
public static class SkeletonParts
{
    /// <summary>包着内容的那一层（TS 的 propsContent）：加载时不可见、不可用。</summary>
    public const string Content = "PART_Content";
}

/// <summary>骨架的常用形状（vue-tsx-pro 的 SkeletonShapes）。</summary>
/// <param name="Value">取值（text、heading、circle、button、image）。</param>
[TypeConverter(typeof(VariantConverter<SkeletonShape>))]
public readonly record struct SkeletonShape(string Value) : IVariant<SkeletonShape>
{
    /// <summary>一行文字：高 1em，占满宽度。</summary>
    public static readonly SkeletonShape Text = new("text");

    /// <summary>标题：高 1.5em，四成宽度。</summary>
    public static readonly SkeletonShape Heading = new("heading");

    /// <summary>圆：头像，边长 2.5em（与同字号的 Avatar 一样大）。</summary>
    public static readonly SkeletonShape Circle = new("circle");

    /// <summary>按钮：控件高度，宽 6em。</summary>
    public static readonly SkeletonShape Button = new("button");

    /// <summary>图片：占满宽度，16:9。</summary>
    public static readonly SkeletonShape Image = new("image");

    /// <inheritdoc />
    public static IReadOnlyList<SkeletonShape> Known { get; } = [Text, Heading, Circle, Button, Image];

    /// <inheritdoc />
    public static SkeletonShape Parse(string value)
    {
        var shape = new SkeletonShape(value);
        Variants.WarnIfUnknown(shape);
        return shape;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
