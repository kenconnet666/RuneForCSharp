using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 色块（vue-tsx-pro 的 ColorSwatch）：一块颜色加它的名称与取值。主题文档里列调色板、设计 token 用。
/// <code>
/// &lt;rune:ColorSwatch Color="#3e63dd" Label="品牌色" /&gt;
/// &lt;rune:ColorSwatch Color="{DynamicResource Rune.Brush.Accent}" Label="accent" /&gt;   &lt;!-- 主题 token：换主题时取值跟着变 --&gt;
/// </code>
/// 取值不写时显示颜色本身（#rrggbb，半透明时 #rrggbbaa）；<see cref="ShowValue"/> 为 false 时不显示。
/// 半透明的颜色（如 surfaceTranslucent）下面垫棋盘格，看得出透明度。色块是装饰（读屏跳过），名称与取值是文字。部件：<see cref="ColorSwatchParts"/>。
/// </summary>
public class ColorSwatch : Control
{
    /// <summary>颜色：画刷或颜色值（#3e63dd），或主题的画刷资源（DynamicResource）。</summary>
    public static readonly StyledProperty<IBrush?> ColorProperty = AvaloniaProperty.Register<ColorSwatch, IBrush?>(nameof(Color));

    /// <summary>名称。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<ColorSwatch, string?>(nameof(Label));

    /// <summary>显示的取值；不写时是颜色本身。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<ColorSwatch, string?>(nameof(Value));

    /// <summary>显示取值，默认 true（上游的 value={false}）。</summary>
    public static readonly StyledProperty<bool> ShowValueProperty = AvaloniaProperty.Register<ColorSwatch, bool>(nameof(ShowValue), true);

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
    readonly SwatchView swatch = new() { Name = ColorSwatchParts.Swatch };
    readonly Text label = new() { Name = ColorSwatchParts.Label, IsVisible = false };
    readonly Text value = new() { Name = ColorSwatchParts.Value };

    /// <summary>新建：色块、名称、取值竖排，隔 xs。</summary>
    public ColorSwatch()
    {
        AutomationProperties.SetAccessibilityView(swatch, AccessibilityView.Raw);
        root.Children.AddRange([swatch, label, value]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Sync();
    }

    /// <summary>颜色。</summary>
    public IBrush? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>名称。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>显示的取值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>显示取值。</summary>
    public bool ShowValue { get => GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }

    /// <summary>纯色的写法：#rrggbb，不透明度不满时 #rrggbbaa；渐变等没有单一取值时 null。</summary>
    internal static string? Hex(IBrush? brush)
    {
        if (brush is not ISolidColorBrush solid) return null;
        var color = solid.Color;
        var alpha = (byte)Math.Round(color.A * solid.Opacity);
        var hex = string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}");
        return alpha == 255 ? hex : hex + alpha.ToString("x2", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty || change.Property == LabelProperty || change.Property == ValueProperty || change.Property == ShowValueProperty) Sync();
    }

    void Sync()
    {
        swatch.Fill = Color;
        label.Text = Label;
        label.IsVisible = !string.IsNullOrEmpty(Label);
        var shown = Value ?? Hex(Color);
        value.Text = shown;
        value.IsVisible = ShowValue && !string.IsNullOrEmpty(shown);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>
    /// 色块：3rem 高、md 圆角，棋盘格（fill 与 surface 相间、8 像素一格）垫底，上面是颜色，内描边是正文色 14% 的细线（亮色、暗色主题里都看得见边界）。
    /// </summary>
    sealed class SwatchView : Control
    {
        public IBrush? Fill
        {
            get;
            set
            {
                field = value;
                InvalidateVisual();
            }
        }

        public SwatchView()
        {
            Height = 48;
            ClipToBounds = true;
            ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            var em = global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);
            var radius = this.Resource<double>(ThemeKeys.Radius.Md) * em;
            var shape = new RoundedRect(bounds, radius);
            using (context.PushClip(shape))
            {
                context.FillRectangle(this.Resource<IBrush>(ThemeKeys.Brush.Surface), bounds);
                var checker = this.Resource<IBrush>(ThemeKeys.Brush.Fill);
                const double cell = 8;
                for (var y = 0; y * cell < bounds.Height; y++)
                    for (var x = y % 2; x * cell < bounds.Width; x += 2)
                        context.FillRectangle(checker, new Rect(x * cell, y * cell, cell, cell));
                if (Fill is { } fill) context.FillRectangle(fill, bounds);
            }
            if (this.Resource<IBrush>(ThemeKeys.Brush.Text) is ISolidColorBrush text)
            {
                var ring = new Pen(new SolidColorBrush(text.Color, 0.14), 1);
                context.DrawRectangle(null, ring, new RoundedRect(bounds.Deflate(0.5), Math.Max(0, radius - 0.5)));
            }
        }
    }
}

/// <summary>ColorSwatch 的部件名。外层是色块自己（竖排，隔 xs）。</summary>
public static class ColorSwatchParts
{
    /// <summary>色块（TS 的 propsSwatch）：3rem 高、md 圆角，棋盘格垫底、内描边；读屏跳过。</summary>
    public const string Swatch = "PART_Swatch";

    /// <summary>名称：Text，sm、medium。</summary>
    public const string Label = "PART_Label";

    /// <summary>取值：Text，xs、等宽、textMuted。</summary>
    public const string Value = "PART_Value";
}
