namespace Rune.WinForms;

/// <summary>
/// 无窗口元素（决定 12）：WinForms 组件的本体。不占 HWND，由宿主控件（<see cref="IElementHost"/>）布局、绘制、失效。
/// 绘制时读到的信号（主题等）由宿主的绘制 effect 追踪，变化时只让这个元素的区域失效（DESIGN §10）。
/// 尺寸都按设备无关像素算，<see cref="ElementContext"/> 负责换成设备像素。
/// </summary>
public abstract class Element
{
    /// <summary>所在的宿主；还没放进宿主时为 null。</summary>
    public IElementHost? Host { get; internal set; }

    /// <summary>在宿主里的位置（设备像素）。</summary>
    public Rectangle Bounds { get; internal set; }

    /// <summary>想要的大小（设备像素）。</summary>
    /// <param name="context">宿主的环境：DPI、字体、颜色、方向。</param>
    protected internal abstract Size Measure(ElementContext context);

    /// <summary>画在 <see cref="Bounds"/> 里。</summary>
    /// <param name="graphics">宿主的 Graphics。</param>
    /// <param name="context">宿主的环境。</param>
    protected internal abstract void Paint(Graphics graphics, ElementContext context);

    /// <summary>外观变了：重画自己的区域。</summary>
    protected void Invalidate() => Host?.InvalidateElement(this);

    /// <summary>大小可能变了：宿主重新布局。</summary>
    protected void InvalidateMeasure() => Host?.InvalidateLayout(this);
}

/// <summary>承载元素的宿主（单元素的 <see cref="ElementControl{T}"/>，以后还有多元素的宿主）。</summary>
public interface IElementHost
{
    /// <summary>重画某个元素的区域。</summary>
    /// <param name="element">元素。</param>
    void InvalidateElement(Element element);

    /// <summary>某个元素的大小可能变了。</summary>
    /// <param name="element">元素。</param>
    void InvalidateLayout(Element element);
}

/// <summary>元素所在的环境：宿主控件的 DPI、字体、文字颜色、方向，以及当前的主题。</summary>
/// <param name="Dpi">宿主的 DeviceDpi。</param>
/// <param name="Font">宿主的字体（环境属性，跟随父控件）。</param>
/// <param name="ForeColor">宿主的文字颜色（环境属性）。</param>
/// <param name="RightToLeft">宿主是否从右到左。</param>
public readonly record struct ElementContext(int Dpi, Font Font, Color ForeColor, bool RightToLeft)
{
    /// <summary>当前的主题：用到时才读，读了才被绘制 effect 追踪，没读的元素换主题时不重画。</summary>
    public ThemeTokens Theme => RuneTheme.Current.Value;

    /// <summary>设备无关像素 → 设备像素。</summary>
    /// <param name="dip">设备无关像素。</param>
    public float Scale(float dip) => dip * Dpi / 96f;

    /// <summary>1em 是多少设备像素：字体的磅值按 DPI 换算（DESIGN §6）。</summary>
    public float Em => Font.SizeInPoints * Dpi / 72f;
}
