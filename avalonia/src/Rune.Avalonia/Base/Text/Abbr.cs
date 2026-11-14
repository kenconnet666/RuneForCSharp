using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace Rune.Avalonia;

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
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<Abbr, string?>(nameof(Title));

    static Abbr() => FocusableProperty.OverrideDefaultValue<Abbr>(true);

    /// <summary>新建：点状下划线（离文字 0.2em）、帮助指针、提示停留 200 毫秒显示。</summary>
    public Abbr()
    {
        SetValue(CursorProperty, new Cursor(StandardCursorType.Help), BindingPriority.Template);
        SetValue(ToolTip.ShowDelayProperty, 200, BindingPriority.Template);
        SetValue(TextDecorationsProperty, new TextDecorationCollection
        {
            new TextDecoration
            {
                Location = TextDecorationLocation.Underline,
                StrokeDashArray = new AvaloniaList<double> { 1, 2 },
                StrokeOffset = 0.2,
                StrokeOffsetUnit = TextDecorationUnit.FontRenderingEmSize,
            },
        }, BindingPriority.Template);
    }

    /// <summary>全称。</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty) ToolTip.SetTip(this, Title);
    }

    /// <summary>
    /// 点按也显示全称：触屏没有悬停，点出来的焦点也不算键盘聚焦。在松开时开（平台按下时会关掉提示）。
    /// </summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!string.IsNullOrEmpty(Title)) ToolTip.SetIsOpen(this, true);
    }
}
