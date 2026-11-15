using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia;

/// <summary>
/// 主题缩略图（vue-tsx-pro 的 ThemeThumbnail）：用这套主题画一个缩小的页面：底色上一张卡片，卡片里标题、正文两行字和一个品牌色按钮。
/// 主题选择器、主题列表里代替主题名旁边的色点，一眼看出底色、表面、文字与品牌色的搭配。
/// <code>&lt;rune:ThemeThumbnail ThemeVariant="{x:Static rune:RuneThemeVariants.Forest}" /&gt;</code>
/// 颜色按 <see cref="ThemeVariant"/> 从主题资源里取（不用套 ThemeVariantScope；Theme 是 Avalonia 控件主题的名字，所以叫 ThemeVariant），纯装饰，读屏跳过，名字由使用方写在旁边。尺寸按字号：6em × 3.75em。
/// </summary>
public class ThemeThumbnail : Control
{
    /// <summary>画哪一套主题。</summary>
    public static readonly StyledProperty<ThemeVariant?> ThemeVariantProperty = AvaloniaProperty.Register<ThemeThumbnail, ThemeVariant?>(nameof(ThemeVariant));

    static ThemeThumbnail()
    {
        AffectsRender<ThemeThumbnail>(ThemeVariantProperty);
        AffectsMeasure<ThemeThumbnail>(global::Avalonia.Controls.Documents.TextElement.FontSizeProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<ThemeThumbnail>(AccessibilityView.Raw);
        // 定宽的一块（上游 inline-flex）：放在竖排、格子里也不被拉宽
        HorizontalAlignmentProperty.OverrideDefaultValue<ThemeThumbnail>(global::Avalonia.Layout.HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<ThemeThumbnail>(global::Avalonia.Layout.VerticalAlignment.Top);
    }

    /// <summary>画哪一套主题。</summary>
    public ThemeVariant? ThemeVariant { get => GetValue(ThemeVariantProperty); set => SetValue(ThemeVariantProperty, value); }

    double Em => global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);

    /// <summary>这套主题的资源（没写主题时按自己所在的主题）。</summary>
    T Resource<T>(string key) where T : class =>
        this.TryFindResource(key, ThemeVariant ?? ActualThemeVariant, out var value) && value is T typed ? typed : (ThemeKeys.Fallback(key) as T)!;

    double Number(string key) => this.TryFindResource(key, ThemeVariant ?? ActualThemeVariant, out var value) && value is double number ? number : ThemeKeys.Fallback(key) is double fallback ? fallback : 0;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(6 * Em, 3.75 * Em);

    /// <summary>
    /// 底色（md 圆角）里，卡片从右下角探出来（上 0.5em、左 0.75em 处开始，左上 sm 圆角、上与左一条 border 细线，surfaceRaised 底，内边距 0.5em）：
    /// 标题条（70% 宽、0.35em、text）、正文条（90%、0.25em、textMuted）隔 0.3em，底部一个 45% 宽、0.6em 高的 accent 按钮（sm 圆角）；外圈正文色 14% 的细线。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        var em = Em;
        var bounds = new Rect(Bounds.Size);
        var radius = Number(ThemeKeys.Radius.Md) * em;
        var small = Number(ThemeKeys.Radius.Sm) * em;
        using (context.PushClip(new RoundedRect(bounds, radius)))
        {
            context.FillRectangle(Resource<IBrush>(ThemeKeys.Brush.Background), bounds);
            var card = new Rect(0.75 * em, 0.5 * em, bounds.Width - 0.75 * em, bounds.Height - 0.5 * em);
            context.DrawRectangle(Resource<IBrush>(ThemeKeys.Brush.SurfaceRaised), null, new RoundedRect(card, small, 0, 0, 0));
            var line = new Pen(Resource<IBrush>(ThemeKeys.Brush.Border), 1);
            context.DrawLine(line, new Point(card.X + small, card.Y + 0.5), new Point(card.Right, card.Y + 0.5));
            context.DrawLine(line, new Point(card.X + 0.5, card.Y + small), new Point(card.X + 0.5, card.Bottom));
            var inner = card.Deflate(0.5 * em);
            Bar(context, ThemeKeys.Brush.Text, inner.X, inner.Y, inner.Width * 0.7, 0.35 * em);
            Bar(context, ThemeKeys.Brush.TextMuted, inner.X, inner.Y + 0.65 * em, inner.Width * 0.9, 0.25 * em);
            var button = new Rect(inner.X, inner.Bottom - 0.6 * em, inner.Width * 0.45, 0.6 * em);
            context.DrawRectangle(Resource<IBrush>(ThemeKeys.Brush.Accent), null, new RoundedRect(button, small));
        }
        if (Resource<IBrush>(ThemeKeys.Brush.Text) is ISolidColorBrush text)
            context.DrawRectangle(null, new Pen(new SolidColorBrush(text.Color, 0.14), 1), new RoundedRect(bounds.Deflate(0.5), Math.Max(0, radius - 0.5)));
    }

    /// <summary>一条圆头的横条（标题、正文）。</summary>
    void Bar(DrawingContext context, string brush, double x, double y, double width, double height) =>
        context.DrawRectangle(Resource<IBrush>(brush), null, new RoundedRect(new Rect(x, y, width, height), height / 2));
}
