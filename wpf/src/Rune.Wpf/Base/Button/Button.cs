using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf;

/// <summary>
/// 按钮（vue-tsx-pro 的 Button）：外观 = 样式 × 色调 × 字号档位 × 圆角（<see cref="ButtonCore"/>），内部尺寸都是 em，随字号缩放。
/// <code>
/// &lt;rune:Button Icon="{x:Static rune:Lucide.Plus}"&gt;新建项目&lt;/rune:Button&gt;
/// &lt;rune:Button Variant="outline" Tone="neutral"&gt;取消&lt;/rune:Button&gt;
/// &lt;rune:Button IsLoading="True"&gt;保存&lt;/rune:Button&gt;   &lt;!-- 显示旋转指示，宽度不变，点击无效，焦点保留 --&gt;
/// </code>
/// 部件（<see cref="ButtonParts"/>）引用部件样式键，Provider 的部件样式能改。
/// 效果同 TS（<see cref="ButtonBase"/>）：按下有波纹，背景色过渡，键盘聚焦时外面一圈焦点环，加载指示防闪烁。
/// </summary>
[TemplatePart(Name = "PART_Icon", Type = typeof(Icon))]
[TemplatePart(Name = "PART_IconEnd", Type = typeof(Icon))]
public class Button : ButtonCore
{
    /// <summary>文字后的图标。</summary>
    public static readonly DependencyProperty IconEndProperty = DependencyProperty.Register(nameof(IconEnd), typeof(IconData), typeof(Button));

    /// <summary>占满容器宽度。</summary>
    public static readonly DependencyProperty FullWidthProperty = DependencyProperty.Register(
        nameof(FullWidth), typeof(bool), typeof(Button), new FrameworkPropertyMetadata(false, OnFullWidthChanged));

    Icon? icon;
    Icon? iconEnd;

    static Button() => DefaultStyleKeyProperty.OverrideMetadata(typeof(Button), new FrameworkPropertyMetadata(typeof(Button)));

    /// <summary>文字后的图标。</summary>
    public IconData? IconEnd { get => (IconData?)GetValue(IconEndProperty); set => SetValue(IconEndProperty, value); }

    /// <summary>占满容器宽度。</summary>
    public bool FullWidth { get => (bool)GetValue(FullWidthProperty); set => SetValue(FullWidthProperty, value); }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        icon = GetTemplateChild("PART_Icon") as Icon;
        iconEnd = GetTemplateChild("PART_IconEnd") as Icon;
        UpdateParts();
    }

    static void OnFullWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue) d.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        else d.ClearValue(HorizontalAlignmentProperty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontSizeProperty || e.Property == IconProperty || e.Property == IconEndProperty) UpdateParts();
    }

    /// <summary>有图标才显示图标部件；图标与文字间距 0.5em（gap: sm）。</summary>
    void UpdateParts()
    {
        var gap = 0.5 * FontSize;
        if (icon is not null)
        {
            icon.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible;
            icon.Margin = new Thickness(0, 0, Content is null ? 0 : gap, 0);
        }
        if (iconEnd is not null)
        {
            iconEnd.Visibility = IconEnd is null ? Visibility.Collapsed : Visibility.Visible;
            iconEnd.Margin = new Thickness(Content is null ? 0 : gap, 0, 0, 0);
        }
    }
}

/// <summary>按钮的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:ButtonParts.Icon}" TargetType="rune:Icon"&gt;</c>。</summary>
public static class ButtonParts
{
    /// <summary>内容层（包着图标与文字，TS 的 propsContent）。</summary>
    public static readonly PartKey Content = new(typeof(Button), nameof(Content), typeof(StackPanel));

    /// <summary>前置图标（TS 的 propsIcon）。</summary>
    public static readonly PartKey Icon = new(typeof(Button), nameof(Icon), typeof(Icon));

    /// <summary>后置图标（TS 的 propsIconEnd）。</summary>
    public static readonly PartKey IconEnd = new(typeof(Button), nameof(IconEnd), typeof(Icon));

    /// <summary>加载指示（TS 的 propsSpinner）。</summary>
    public static readonly PartKey Spinner = new(typeof(Button), nameof(Spinner), typeof(Spinner));
}
