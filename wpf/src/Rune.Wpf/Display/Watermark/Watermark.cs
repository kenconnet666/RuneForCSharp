using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 水印（vue-tsx-pro 的 Watermark）：在内容上（或整个窗口）铺满斜着的文字或图片，截图、拍屏时能看出来源（查看人、时间、密级）。
/// <code>
/// &lt;rune:Watermark Text="林小满&amp;#10;2026-10-24"&gt;&lt;local:Report /&gt;&lt;/rune:Watermark&gt;
/// &lt;rune:Watermark Text="内部资料" Fullscreen="True" /&gt;                    整个窗口
/// &lt;rune:Watermark Image="{Binding Logo}" ImageWidth="120" ImageHeight="40"&gt;…&lt;/rune:Watermark&gt;
/// </code>
/// 水印是盖在内容上面的一层：不挡点击与选择，读屏不念。默认颜色是半透明的灰，亮色、暗色主题上都看得见。多行文字用换行分开（TS 的 content 数组）。
/// 整个窗口时画在窗口内容的装饰层（AdornerLayer）里。只是提示性的：真正的保密要靠服务端（导出时打水印、权限）。
/// </summary>
public class Watermark : Decorator
{
    /// <summary>文字：多行用换行分开（姓名、日期）。</summary>
    public static readonly DependencyProperty TextProperty = Drawn(nameof(Text), typeof(string), null);

    /// <summary>图片：给了就画图片，不画文字。</summary>
    public static readonly DependencyProperty ImageProperty = Drawn(nameof(Image), typeof(ImageSource), null);

    /// <summary>图片的宽（px），默认 120。</summary>
    public static readonly DependencyProperty ImageWidthProperty = Drawn(nameof(ImageWidth), typeof(double), WatermarkTiles.ImageWidth);

    /// <summary>图片的高（px），默认 40。</summary>
    public static readonly DependencyProperty ImageHeightProperty = Drawn(nameof(ImageHeight), typeof(double), WatermarkTiles.ImageHeight);

    /// <summary>旋转角度（度），默认 -22。</summary>
    public static readonly DependencyProperty RotateProperty = Drawn(nameof(Rotate), typeof(double), WatermarkTiles.Rotate);

    /// <summary>每格之间的空隙（横、竖，px），默认 120 × 100。</summary>
    public static readonly DependencyProperty GapProperty = Drawn(nameof(Gap), typeof(Size), new Size(WatermarkTiles.GapX, WatermarkTiles.GapY));

    /// <summary>字号（px），默认 14：固定值，不跟着外面的字号。</summary>
    public static readonly DependencyProperty FontSizeProperty = Drawn(nameof(FontSize), typeof(double), WatermarkTiles.FontSize);

    /// <summary>颜色，默认半透明的灰。</summary>
    public static readonly DependencyProperty ColorProperty = Drawn(nameof(Color), typeof(Color), ColorOf(WatermarkTiles.Color));

    /// <summary>盖住整个窗口，而不只是包着的内容。</summary>
    public static readonly DependencyProperty FullscreenProperty = DependencyProperty.Register(
        nameof(Fullscreen), typeof(bool), typeof(Watermark), new PropertyMetadata(false, (d, _) => ((Watermark)d).Place()));

    // 字体是主题的 token（资源引用）：换主题时跟着变、重画
    static readonly DependencyProperty FaceProperty = Drawn("Face", typeof(FontFamily), null);

    readonly WatermarkLayer layer;
    WatermarkAdorner? adorner;
    bool inline = true;

    /// <summary>新建：内容在下，水印层在上。</summary>
    public Watermark()
    {
        layer = new WatermarkLayer(this);
        AddVisualChild(layer);
        SetResourceReference(FaceProperty, ThemeKeys.Font.Sans);
        Loaded += (_, _) => Place();
        Unloaded += (_, _) => Unplace();
    }

    /// <summary>文字。</summary>
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>图片。</summary>
    public ImageSource? Image { get => (ImageSource?)GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    /// <summary>图片的宽。</summary>
    public double ImageWidth { get => (double)GetValue(ImageWidthProperty); set => SetValue(ImageWidthProperty, value); }

    /// <summary>图片的高。</summary>
    public double ImageHeight { get => (double)GetValue(ImageHeightProperty); set => SetValue(ImageHeightProperty, value); }

    /// <summary>旋转角度（度）。</summary>
    public double Rotate { get => (double)GetValue(RotateProperty); set => SetValue(RotateProperty, value); }

    /// <summary>每格之间的空隙。</summary>
    public Size Gap { get => (Size)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>字号。</summary>
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>颜色。</summary>
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>盖住整个窗口。</summary>
    public bool Fullscreen { get => (bool)GetValue(FullscreenProperty); set => SetValue(FullscreenProperty, value); }

    /// <summary>水印那一层：包着内容时是自己的视觉子元素，整个窗口时是窗口内容上的装饰（测试用）。</summary>
    internal FrameworkElement Layer => inline ? layer : adorner!;

    static Color ColorOf(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    static DependencyProperty Drawn(string name, Type type, object? defaultValue) => DependencyProperty.Register(
        name, type, typeof(Watermark), new PropertyMetadata(defaultValue, (d, _) => ((Watermark)d).Redraw()));

    void Redraw()
    {
        layer.InvalidateVisual();
        adorner?.InvalidateVisual();
    }

    /// <summary>整个窗口时水印画在窗口内容的装饰层里（在所有内容上面）；否则画在自己的内容上面。</summary>
    void Place()
    {
        Unplace();
        if (Fullscreen && IsLoaded && Window.GetWindow(this)?.Content is UIElement root && AdornerLayer.GetAdornerLayer(root) is { } layers)
        {
            adorner = new WatermarkAdorner(root, this);
            layers.Add(adorner);
            inline = false;
            RemoveVisualChild(layer);
        }
        InvalidateMeasure();
    }

    void Unplace()
    {
        if (adorner is not null) AdornerLayer.GetAdornerLayer(adorner.AdornedElement)?.Remove(adorner);
        adorner = null;
        if (!inline) AddVisualChild(layer);
        inline = true;
    }

    /// <inheritdoc />
    protected override int VisualChildrenCount => base.VisualChildrenCount + (inline ? 1 : 0);

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index < base.VisualChildrenCount ? base.GetVisualChild(index) : layer;

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var size = base.ArrangeOverride(arrangeSize);
        if (inline) layer.Arrange(new Rect(size));
        return size;
    }

    /// <summary>一份内容：排好的各行文字与内容的大小（图片时是图片的大小）；没有内容时为 null。</summary>
    internal (FormattedText[] Texts, Size Size)? Content(double pixelsPerDip)
    {
        if (Image is not null) return ([], new Size(ImageWidth, ImageHeight));
        var lines = WatermarkTiles.Lines(Text);
        if (lines.Length == 0) return null;
        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        var typeface = new Typeface((FontFamily?)GetValue(FaceProperty) ?? SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var texts = lines.Select(line => new FormattedText(line, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, FontSize, brush, pixelsPerDip)).ToArray();
        return (texts, new Size(Math.Max(1, texts.Max(text => text.WidthIncludingTrailingWhitespace)), texts.Length * WatermarkTiles.LineHeight(FontSize)));
    }

    /// <summary>画水印：按平铺单元逐块画，每块裁在单元里。</summary>
    void Draw(DrawingContext context, Size area, double pixelsPerDip)
    {
        if (Content(pixelsPerDip) is not var (texts, size)) return;
        var lineHeight = WatermarkTiles.LineHeight(FontSize);
        foreach (var tile in WatermarkTiles.Layout(area.Width, area.Height, size.Width + Gap.Width, size.Height + Gap.Height))
        {
            context.PushClip(new RectangleGeometry(new Rect(tile.X, tile.Y, tile.Width, tile.Height)));
            foreach (var (x, y) in new[] { (tile.FirstX, tile.FirstY), (tile.SecondX, tile.SecondY) })
            {
                context.PushTransform(new TranslateTransform(x, y));
                context.PushTransform(new RotateTransform(Rotate));
                if (Image is { } image) context.DrawImage(image, new Rect(-ImageWidth / 2, -ImageHeight / 2, ImageWidth, ImageHeight));
                else
                    for (var i = 0; i < texts.Length; i++)
                        context.DrawText(texts[i], new Point(-texts[i].WidthIncludingTrailingWhitespace / 2, WatermarkTiles.LineOffset(i, texts.Length, lineHeight) - texts[i].Height / 2));
                context.Pop();
                context.Pop();
            }
            context.Pop();
        }
    }

    /// <summary>包着内容时的水印层：不挡点击，读屏里没有它，画不出自己的范围（单元可以伸出去）。</summary>
    sealed class WatermarkLayer : FrameworkElement
    {
        readonly Watermark owner;

        public WatermarkLayer(Watermark owner)
        {
            this.owner = owner;
            IsHitTestVisible = false;
            ClipToBounds = true;
        }

        protected override void OnRender(DrawingContext drawingContext) =>
            owner.Draw(drawingContext, RenderSize, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    /// <summary>整个窗口时的水印层：盖在窗口内容上的装饰，大小跟着窗口内容。</summary>
    sealed class WatermarkAdorner : Adorner
    {
        readonly Watermark owner;

        public WatermarkAdorner(UIElement root, Watermark owner) : base(root)
        {
            this.owner = owner;
            IsHitTestVisible = false;
            ClipToBounds = true;
        }

        protected override void OnRender(DrawingContext drawingContext) =>
            owner.Draw(drawingContext, RenderSize, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }
}
