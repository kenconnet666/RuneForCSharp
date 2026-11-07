namespace Rune.WinForms;

/// <summary>
/// 标题（vue-tsx-pro 的 H1–H6）：默认字号按级别（H1 xxxl、H2 xxl、H3 xl、H4 lg、H5 md、H6 sm），字重 semibold，标题字体。
/// GDI 画不出 TS 的 tight 行高与收紧的字间距；读屏没有标题级别（MSAA 没有这个概念）。
/// </summary>
public abstract class Heading : Text
{
    /// <inheritdoc />
    protected internal override TextWeight DefaultWeight => TextWeight.Semibold;

    /// <inheritdoc />
    protected override bool HeadingFont => true;
}

/// <summary>一级标题：页面标题，默认 xxxl。</summary>
public class H1 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Xxxl;
}

/// <summary>二级标题：一节，默认 xxl。</summary>
public class H2 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Xxl;
}

/// <summary>三级标题：小节，默认 xl。</summary>
public class H3 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Xl;
}

/// <summary>四级标题：段落标题，默认 lg。</summary>
public class H4 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Lg;
}

/// <summary>五级标题：卡片标题，默认 md。</summary>
public class H5 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Md;
}

/// <summary>六级标题：最小的标题，默认 sm。</summary>
public class H6 : Heading
{
    /// <inheritdoc />
    protected internal override TextSize DefaultSize => TextSize.Sm;
}
