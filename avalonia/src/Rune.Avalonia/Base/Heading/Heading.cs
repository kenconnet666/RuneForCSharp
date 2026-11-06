using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Data;

namespace Rune.Avalonia;

/// <summary>
/// 标题（vue-tsx-pro 的 H1–H6）：各级是一个组件，读屏念出级别（AutomationProperties.HeadingLevel）。级别要连续，不要为了字号跳级：
/// <code>
/// &lt;rune:H2 Size="lg"&gt;收货地址&lt;/rune:H2&gt;   &lt;!-- 文档结构与视觉层级分开：字号用 Size 改 --&gt;
/// </code>
/// 默认字号按级别（H1 xxxl、H2 xxl、H3 xl、H4 lg、H5 md、H6 sm），字重 semibold，标题字体，行高 tight；
/// xxl、xxxl 收紧字间距（letterSpacings.tight、tighter）。默认值在主题里，使用方的样式能改。
/// TS 的 anchor（末尾指向本节的 # 链接）是文档站用的，桌面上没有本页地址，不做。
/// </summary>
public abstract class Heading : Text
{
    IDisposable? letterSpacing;

    /// <summary>新建某一级标题。</summary>
    /// <param name="level">级别（1–6）。</param>
    protected Heading(int level) => AutomationProperties.SetHeadingLevel(this, level);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SizeProperty || change.Property == FontSizeProperty) UpdateLetterSpacing();
    }

    /// <summary>大字号收紧字间距（em 系数 × 字号），按模板优先级。</summary>
    void UpdateLetterSpacing()
    {
        letterSpacing?.Dispose();
        letterSpacing = null;
        var key = Size.Value switch { "xxl" => "Rune.LetterSpacing.Tight", "xxxl" => "Rune.LetterSpacing.Tighter", _ => null };
        if (key is not null && this.TryFindResource(key, ActualThemeVariant, out var factor) && factor is double em)
            letterSpacing = SetValue(LetterSpacingProperty, em * FontSize, BindingPriority.Template);
    }
}

/// <summary>一级标题：页面标题，默认 xxxl。</summary>
public class H1() : Heading(1);

/// <summary>二级标题：一节，默认 xxl。</summary>
public class H2() : Heading(2);

/// <summary>三级标题：小节，默认 xl。</summary>
public class H3() : Heading(3);

/// <summary>四级标题：段落标题，默认 lg。</summary>
public class H4() : Heading(4);

/// <summary>五级标题：卡片标题，默认 md。</summary>
public class H5() : Heading(5);

/// <summary>六级标题：最小的标题，默认 sm。</summary>
public class H6() : Heading(6);
