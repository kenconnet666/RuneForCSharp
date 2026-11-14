using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 水印（vue-tsx-pro 的 Watermark）：在内容上（或整个窗口）铺满斜着的文字或图片，截图、拍屏时能看出来源（查看人、时间、密级）。
/// <code>
/// &lt;rune:Watermark Text="林小满&amp;#10;2026-10-24"&gt;&lt;local:Report /&gt;&lt;/rune:Watermark&gt;
/// &lt;rune:Watermark Text="内部资料" Fullscreen="True" /&gt;                    整个窗口
/// &lt;rune:Watermark Image="{Binding Logo}" ImageWidth="120" ImageHeight="40"&gt;…&lt;/rune:Watermark&gt;
/// </code>
/// 水印是盖在内容上面的一层：不挡点击与选择，读屏不念。默认颜色是半透明的灰，亮色、暗色主题上都看得见。多行文字用换行分开（TS 的 content 数组）。
/// 只是提示性的：真正的保密要靠服务端（导出时打水印、权限）。
/// </summary>
public class Watermark : Decorator
{
    /// <summary>文字：多行用换行分开（姓名、日期）。</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Watermark, string?>(nameof(Text));

    /// <summary>图片：给了就画图片，不画文字。</summary>
    public static readonly StyledProperty<IImage?> ImageProperty = AvaloniaProperty.Register<Watermark, IImage?>(nameof(Image));

    /// <summary>图片的宽（px），默认 120。</summary>
    public static readonly StyledProperty<double> ImageWidthProperty = AvaloniaProperty.Register<Watermark, double>(nameof(ImageWidth), WatermarkTiles.ImageWidth);

    /// <summary>图片的高（px），默认 40。</summary>
    public static readonly StyledProperty<double> ImageHeightProperty = AvaloniaProperty.Register<Watermark, double>(nameof(ImageHeight), WatermarkTiles.ImageHeight);

    /// <summary>旋转角度（度），默认 -22。</summary>
    public static readonly StyledProperty<double> RotateProperty = AvaloniaProperty.Register<Watermark, double>(nameof(Rotate), WatermarkTiles.Rotate);

    /// <summary>每格之间的空隙（横、竖，px），默认 120 × 100。</summary>
    public static readonly StyledProperty<LayoutSize> GapProperty = AvaloniaProperty.Register<Watermark, LayoutSize>(nameof(Gap), new LayoutSize(WatermarkTiles.GapX, WatermarkTiles.GapY));

    /// <summary>字号（px），默认 14：固定值，不跟着外面的字号。</summary>
    public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<Watermark, double>(nameof(FontSize), WatermarkTiles.FontSize);

    /// <summary>颜色，默认半透明的灰。</summary>
    public static readonly StyledProperty<Color> ColorProperty = AvaloniaProperty.Register<Watermark, Color>(nameof(Color), Color.FromUInt32(WatermarkTiles.Color));

    /// <summary>盖住整个窗口，而不只是包着的内容。</summary>
    public static readonly StyledProperty<bool> FullscreenProperty = AvaloniaProperty.Register<Watermark, bool>(nameof(Fullscreen));

    readonly WatermarkLayer layer;
    OverlayLayer? overlay;

    // 水印层画的内容跟着这些属性
    static readonly AvaloniaProperty[] Drawn = [TextProperty, ImageProperty, ImageWidthProperty, ImageHeightProperty, RotateProperty, GapProperty, FontSizeProperty, ColorProperty];

    /// <summary>新建：内容在下，水印层在上。</summary>
    public Watermark()
    {
        layer = new WatermarkLayer(this);
        VisualChildren.Add(layer);
    }

    /// <summary>文字。</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>图片。</summary>
    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    /// <summary>图片的宽。</summary>
    public double ImageWidth { get => GetValue(ImageWidthProperty); set => SetValue(ImageWidthProperty, value); }

    /// <summary>图片的高。</summary>
    public double ImageHeight { get => GetValue(ImageHeightProperty); set => SetValue(ImageHeightProperty, value); }

    /// <summary>旋转角度（度）。</summary>
    public double Rotate { get => GetValue(RotateProperty); set => SetValue(RotateProperty, value); }

    /// <summary>每格之间的空隙。</summary>
    public LayoutSize Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>颜色。</summary>
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>盖住整个窗口。</summary>
    public bool Fullscreen { get => GetValue(FullscreenProperty); set => SetValue(FullscreenProperty, value); }

    /// <summary>水印那一层（测试用）。</summary>
    internal Control Layer => layer;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FullscreenProperty) Place();
        else if (Drawn.Contains(change.Property)) layer.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Place();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unplace();
    }

    /// <summary>整个窗口时水印层放进窗口的浮层（在所有内容上面、铺满窗口）；否则放在自己的内容上面。</summary>
    void Place()
    {
        Unplace();
        if (Fullscreen && OverlayLayer.GetOverlayLayer(this) is { } target)
        {
            overlay = target;
            overlay.Children.Add(layer);
            // 浮层像 Canvas，不拉伸子元素：水印层跟着浮层（窗口）的大小
            overlay.PropertyChanged += OnOverlayChanged;
            Fill(overlay.Bounds.Size);
        }
        else VisualChildren.Add(layer);
        InvalidateArrange();
    }

    void OnOverlayChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty) Fill(((Rect)e.NewValue!).Size);
    }

    void Fill(LayoutSize size) => (layer.Width, layer.Height) = (size.Width, size.Height);

    void Unplace()
    {
        if (overlay is not null)
        {
            overlay.PropertyChanged -= OnOverlayChanged;
            overlay.Children.Remove(layer);
            (layer.Width, layer.Height) = (double.NaN, double.NaN);
        }
        overlay = null;
        VisualChildren.Remove(layer);
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (layer.GetVisualParent() == this) layer.Arrange(new Rect(size));
        return size;
    }

    /// <summary>一份内容：排好的各行文字与内容的大小（图片时是图片的大小）；没有内容时为 null。</summary>
    internal (FormattedText[] Texts, LayoutSize Size)? Content()
    {
        var lines = WatermarkTiles.Lines(Text);
        if (Image is not null) return ([], new LayoutSize(ImageWidth, ImageHeight));
        if (lines.Length == 0) return null;
        var brush = new ImmutableSolidColorBrush(Color);
        var typeface = new Typeface(this.Resource<FontFamily>(ThemeKeys.Font.Sans) ?? FontFamily.Default);
        var texts = lines.Select(line => new FormattedText(line, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, FontSize, brush)).ToArray();
        return (texts, new LayoutSize(Math.Max(1, texts.Max(text => text.WidthIncludingTrailingWhitespace)), texts.Length * WatermarkTiles.LineHeight(FontSize)));
    }

    /// <summary>画水印：按平铺单元逐块画，每块裁在单元里。</summary>
    internal void Draw(DrawingContext context, LayoutSize area)
    {
        if (Content() is not var (texts, size)) return;
        var lineHeight = WatermarkTiles.LineHeight(FontSize);
        var (contentWidth, contentHeight) = (size.Width, size.Height);
        var rotation = Matrix.CreateRotation(Rotate * Math.PI / 180);
        foreach (var tile in WatermarkTiles.Layout(area.Width, area.Height, contentWidth + Gap.Width, contentHeight + Gap.Height))
        {
            using var clip = context.PushClip(new Rect(tile.X, tile.Y, tile.Width, tile.Height));
            foreach (var (x, y) in new[] { (tile.FirstX, tile.FirstY), (tile.SecondX, tile.SecondY) })
            {
                using var transform = context.PushTransform(rotation * Matrix.CreateTranslation(x, y));
                if (Image is { } image) context.DrawImage(image, new Rect(-ImageWidth / 2, -ImageHeight / 2, ImageWidth, ImageHeight));
                else
                    for (var i = 0; i < texts.Length; i++)
                        context.DrawText(texts[i], new Point(-texts[i].WidthIncludingTrailingWhitespace / 2, WatermarkTiles.LineOffset(i, texts.Length, lineHeight) - texts[i].Height / 2));
            }
        }
    }

    /// <summary>水印那一层：不挡点击，读屏不念。</summary>
    sealed class WatermarkLayer : Control
    {
        readonly Watermark owner;

        public WatermarkLayer(Watermark owner)
        {
            this.owner = owner;
            IsHitTestVisible = false;
            // 单元可以伸出铺的范围（最后一行、一列的单元不完整）：裁在自己的范围里，同网页的背景
            ClipToBounds = true;
            // 永远画在内容上面：Decorator 换内容时新的子元素加在视觉子元素的最后
            ZIndex = int.MaxValue;
            Focusable = false;
            AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
            // 字体是主题的 token：换主题、作用域改了 token 时重画
            this.RedrawOnResourceChanges(owner);
        }

        public override void Render(DrawingContext context) => owner.Draw(context, Bounds.Size);

        // 铺的范围跟着大小（整个窗口时窗口一变就要重铺）
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            InvalidateVisual();
        }
    }
}
