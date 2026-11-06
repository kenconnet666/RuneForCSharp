using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf;

/// <summary>
/// 标题（vue-tsx-pro 的 H1–H6）：各级是一个组件，读屏念出级别（AutomationProperties.HeadingLevel）。级别要连续，不要为了字号跳级：
/// <code>
/// &lt;rune:H2 Size="lg"&gt;收货地址&lt;/rune:H2&gt;   &lt;!-- 文档结构与视觉层级分开：字号用 Size 改 --&gt;
/// </code>
/// 默认字号按级别（H1 xxxl、H2 xxl、H3 xl、H4 lg、H5 md、H6 sm），字重 semibold，标题字体，行高 tight；默认值在主题样式里，
/// 使用方的样式能改。WPF 的 TextBlock 不能调字间距，TS 在 xxl、xxxl 上的收紧做不到。TS 的 anchor 是文档站用的，不做。
/// </summary>
public abstract class Heading : Text
{
    /// <summary>新建某一级标题。</summary>
    /// <param name="level">级别（1–6）。</param>
    protected Heading(int level) => AutomationProperties.SetHeadingLevel(this, AutomationHeadingLevel.Level1 + (level - 1));
}

/// <summary>一级标题：页面标题，默认 xxxl。</summary>
public class H1() : Heading(1)
{
    static H1() => DefaultStyleKeyProperty.OverrideMetadata(typeof(H1), new FrameworkPropertyMetadata(typeof(H1)));
}

/// <summary>二级标题：一节，默认 xxl。</summary>
public class H2() : Heading(2)
{
    static H2() => DefaultStyleKeyProperty.OverrideMetadata(typeof(H2), new FrameworkPropertyMetadata(typeof(H2)));
}

/// <summary>三级标题：小节，默认 xl。</summary>
public class H3() : Heading(3)
{
    static H3() => DefaultStyleKeyProperty.OverrideMetadata(typeof(H3), new FrameworkPropertyMetadata(typeof(H3)));
}

/// <summary>四级标题：段落标题，默认 lg。</summary>
public class H4() : Heading(4)
{
    static H4() => DefaultStyleKeyProperty.OverrideMetadata(typeof(H4), new FrameworkPropertyMetadata(typeof(H4)));
}

/// <summary>五级标题：卡片标题，默认 md。</summary>
public class H5() : Heading(5)
{
    static H5() => DefaultStyleKeyProperty.OverrideMetadata(typeof(H5), new FrameworkPropertyMetadata(typeof(H5)));
}

/// <summary>六级标题：最小的标题，默认 sm。</summary>
public class H6() : Heading(6)
{
    static H6() => DefaultStyleKeyProperty.OverrideMetadata(typeof(H6), new FrameworkPropertyMetadata(typeof(H6)));
}
