using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 缩写（vue-tsx-pro 的 Abbr）：点状下划线、帮助指针，悬停、键盘聚焦、点按时在提示里显示全称。
/// <code>
/// &lt;rune:Paragraph&gt;&lt;InlineUIContainer&gt;&lt;rune:Abbr Title="服务等级协议"&gt;SLA&lt;/rune:Abbr&gt;&lt;/InlineUIContainer&gt; 承诺 99.9% 可用&lt;/rune:Paragraph&gt;
/// </code>
/// 全称放在提示（平台的 ToolTip，外观与时机见 <see cref="Tooltip"/>）里：能聚焦（Tab 能到）、点一下就显示，读屏把全称作为帮助文字念出来。
/// 停留 200 毫秒显示（TS 的 delay 200）。第一次出现时最好仍把全称写在正文里。是一段控件：放在段落里时包一层 InlineUIContainer（同 Kbd）。
/// </summary>
public class Abbr : Text
{
    /// <summary>全称：显示在提示里。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Abbr), new PropertyMetadata(null, (d, _) => ((Abbr)d).ShowTitle()));

    readonly ToolTip tip = new();

    static Abbr()
    {
        FocusableProperty.OverrideMetadata(typeof(Abbr), new FrameworkPropertyMetadata(true));
        CursorProperty.OverrideMetadata(typeof(Abbr), new FrameworkPropertyMetadata(Cursors.Help));
        ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(Abbr), new FrameworkPropertyMetadata(200));
        // 下划线的画笔跟着文字颜色换（TextBlock 的 OnPropertyChanged 是密封的，回调挂在元数据上）
        ForegroundProperty.OverrideMetadata(typeof(Abbr), new FrameworkPropertyMetadata(
            SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Abbr)d).Underline()));
    }

    /// <summary>新建：点状下划线（离文字 0.2em）。</summary>
    public Abbr() => Underline();

    /// <summary>全称。</summary>
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>点状下划线：虚线画笔要有颜色（平台只在没有画笔时才用文字颜色），跟着前景色换。</summary>
    void Underline()
    {
        var pen = new Pen(Foreground, 1) { DashStyle = new DashStyle([1, 2], 0) };
        pen.Freeze();
        SetCurrentValue(TextDecorationsProperty, new TextDecorationCollection
        {
            new TextDecoration(TextDecorationLocation.Underline, pen, 0.2, TextDecorationUnit.FontRenderingEmSize, TextDecorationUnit.Pixel),
        });
    }

    void ShowTitle()
    {
        tip.Content = Title;
        ToolTip = string.IsNullOrEmpty(Title) ? null : tip;
    }

    /// <summary>点按也显示全称：触屏没有悬停，点出来的焦点也不算键盘聚焦。</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (ToolTip is not ToolTip shown) return;
        shown.PlacementTarget = this;
        shown.IsOpen = true;
    }
}
