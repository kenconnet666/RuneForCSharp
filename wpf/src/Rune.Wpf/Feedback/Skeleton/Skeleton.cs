using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 骨架屏（vue-tsx-pro 的 Skeleton）：内容加载时的占位，一道光带从起始边扫过。包着真实内容时，内容照常排版但不可见、不可交互，
/// 占位正好是内容的大小，加载完换成内容时布局不跳；不包内容时用 <see cref="Shape"/> 选常用形状。
/// <code>
/// &lt;rune:Skeleton IsLoading="{Binding Loading}"&gt;&lt;rune:Text Text="{Binding Name}" /&gt;&lt;/rune:Skeleton&gt;
/// &lt;rune:Skeleton Shape="circle" /&gt;
/// &lt;rune:Skeleton Shape="heading" Delay="120" /&gt;     &lt;!-- 几块骨架依次扫过 --&gt;
/// </code>
/// 底色与光带都是半透明（9%）的正文色；圆角 radii.sm（圆 full，按钮、图片 md）。扫光 2 秒一轮（1.4 秒扫过、0.6 秒停顿，先慢后快再慢），
/// 从右到左时反向；系统关闭动画时不扫（静止的占位已经说明在加载）；只在可见时扫。占位对读屏隐藏。
/// 包着内容的那一层引用部件样式键 <see cref="SkeletonParts.Content"/>。
/// </summary>
[ContentProperty(nameof(Content))]
public class Skeleton : FrameworkElement
{
    /// <summary>是否在加载（默认 true）；false 时直接显示内容。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(Skeleton), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Skeleton)d).UpdateLoading()));

    /// <summary>形状；不写时大小由内容决定。</summary>
    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(
        nameof(Shape), typeof(SkeletonShape), typeof(Skeleton), new FrameworkPropertyMetadata(default(SkeletonShape), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Skeleton)d).BindRadius()));

    /// <summary>扫光的延迟（毫秒）：几块骨架依次扫过、连成一道波时用。</summary>
    public static readonly DependencyProperty DelayProperty = DependencyProperty.Register(
        nameof(Delay), typeof(int), typeof(Skeleton), new FrameworkPropertyMetadata(0, (d, _) => ((Skeleton)d).Sweep()));

    /// <summary>内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(UIElement), typeof(Skeleton), new FrameworkPropertyMetadata(null, (d, e) => ((Skeleton)d).holder.Child = (UIElement?)e.NewValue));

    static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        "Phase", typeof(double), typeof(Skeleton), new FrameworkPropertyMetadata(-1d, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty TextColorProperty = DependencyProperty.Register(
        "TextColor", typeof(Color), typeof(Skeleton), new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly DependencyProperty RadiusFactorProperty = DependencyProperty.Register(
        "RadiusFactor", typeof(double), typeof(Skeleton), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    readonly Border holder = new();

    static Skeleton() => System.Windows.Documents.TextElement.FontSizeProperty.OverrideMetadata(typeof(Skeleton), new FrameworkPropertyMetadata(
        SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>新建：颜色、圆角绑到主题；可见时才扫。</summary>
    public Skeleton()
    {
        holder.SetResourceReference(StyleProperty, SkeletonParts.Content);
        AddVisualChild(holder);
        AddLogicalChild(holder);
        SetResourceReference(TextColorProperty, ThemeKeys.Color.Text);
        BindRadius();
        UpdateLoading();
        IsVisibleChanged += (_, _) => Sweep();
    }

    /// <summary>是否在加载。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>形状。</summary>
    public SkeletonShape Shape { get => (SkeletonShape)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    /// <summary>扫光的延迟（毫秒）。</summary>
    public int Delay { get => (int)GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    /// <summary>内容。</summary>
    public UIElement? Content { get => (UIElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    double Em => System.Windows.Documents.TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => holder;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new[] { holder }.GetEnumerator();

    /// <summary>圆角：circle 是 full，button、image 是 md，其余 sm。</summary>
    void BindRadius()
    {
        if (Shape.Value == "circle") ClearValue(RadiusFactorProperty);
        else SetResourceReference(RadiusFactorProperty, Shape.Value is "button" or "image" ? ThemeKeys.Radius.Md : ThemeKeys.Radius.Sm);
    }

    /// <summary>加载时内容不可见、不可交互；加载完内容照常显示。</summary>
    void UpdateLoading()
    {
        holder.Opacity = IsLoading ? 0 : 1;
        holder.IsEnabled = !IsLoading;
        holder.IsHitTestVisible = !IsLoading;
        Sweep();
    }

    /// <summary>扫光：加载中、可见、系统没关动画时一直扫（-1 → 1 用 1.4 秒，再停 0.6 秒）。</summary>
    void Sweep()
    {
        if (!IsLoading || !IsVisible || !Motion.Enabled)
        {
            BeginAnimation(PhaseProperty, null);
            return;
        }
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        BeginAnimation(PhaseProperty, new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(2),
            BeginTime = TimeSpan.FromMilliseconds(Delay),
            RepeatBehavior = RepeatBehavior.Forever,
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(-1, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.4)), ease),
                new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2))),
            },
        });
    }

    /// <summary>形状决定的大小（em）；没有形状时为 null，跟着内容。</summary>
    Size? ShapeSize(Size available) => Shape.Value switch
    {
        "text" => new Size(double.IsInfinity(available.Width) ? 0 : available.Width, Em),
        "heading" => new Size(double.IsInfinity(available.Width) ? 0 : 0.4 * available.Width, 1.5 * Em),
        "circle" => new Size(2.5 * Em, 2.5 * Em),
        "button" => new Size(6 * Em, ControlHeight.Of(Em, ControlHeight.GetFactor(this))),
        "image" => double.IsInfinity(available.Width) ? new Size(0, 0) : new Size(available.Width, available.Width * 9 / 16),
        _ => null,
    };

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        holder.Measure(availableSize);
        return IsLoading && ShapeSize(availableSize) is { } shape ? shape : holder.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        holder.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        if (!IsLoading || RenderSize.Width <= 0 || RenderSize.Height <= 0) return;
        var bounds = new Rect(RenderSize);
        var radius = Shape.Value == "circle" ? bounds.Height / 2 : Math.Min((double)GetValue(RadiusFactorProperty) * Em, bounds.Height / 2);
        var text = (Color)GetValue(TextColorProperty);
        var tint = Color.FromArgb((byte)Math.Round(255 * 0.09), text.R, text.G, text.B);
        var clip = new RectangleGeometry(bounds, radius, radius);
        drawingContext.DrawGeometry(new SolidColorBrush(tint), null, clip);
        // 光带与元素一样宽，从起始边外扫到结束边外（从右到左时 WPF 整体镜像，反向扫）
        var offset = (double)GetValue(PhaseProperty) * bounds.Width;
        var band = new LinearGradientBrush(
            [new GradientStop(Colors.Transparent, 0.15), new GradientStop(tint, 0.5), new GradientStop(Colors.Transparent, 0.85)],
            new Point(0, 0), new Point(1, 0));
        drawingContext.PushClip(clip);
        drawingContext.DrawRectangle(band, null, new Rect(offset, 0, bounds.Width, bounds.Height));
        drawingContext.Pop();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SkeletonAutomationPeer(this);

    /// <summary>加载时占位与里面的内容对读屏隐藏；加载完读屏照常读内容。</summary>
    sealed class SkeletonAutomationPeer(Skeleton owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Skeleton);

        protected override bool IsControlElementCore() => false;

        protected override List<AutomationPeer>? GetChildrenCore() => owner.IsLoading ? null : base.GetChildrenCore();
    }
}

/// <summary>Skeleton 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:SkeletonParts.Content}" TargetType="Border"&gt;</c>。</summary>
public static class SkeletonParts
{
    /// <summary>包着内容的那一层（TS 的 propsContent）：加载时不可见、不可用。</summary>
    public static readonly PartKey Content = new(typeof(Skeleton), nameof(Content), typeof(Border));
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
