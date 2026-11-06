using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Icon/basic：按名取图标。</summary>
public partial class IconBasic : UserControl
{
    /// <summary>加载示例。</summary>
    public IconBasic() => InitializeComponent();
}

/// <summary>Icon/sizes：档位、像素与线宽。</summary>
public partial class IconSizes : UserControl
{
    /// <summary>加载示例。</summary>
    public IconSizes() => InitializeComponent();
}

/// <summary>Icon/tones：色调。</summary>
public partial class IconTones : UserControl
{
    /// <summary>加载示例。</summary>
    public IconTones() => InitializeComponent();
}

/// <summary>Icon/direction：从右到左。</summary>
public partial class IconDirection : UserControl
{
    /// <summary>加载示例。</summary>
    public IconDirection() => InitializeComponent();
}

/// <summary>Icon/accessibility：装饰与含义。</summary>
public partial class IconAccessibility : UserControl
{
    /// <summary>加载示例。</summary>
    public IconAccessibility() => InitializeComponent();
}

/// <summary>Icon/custom：自己的图标，写成 24 格画布上的路径（三个库都认的写法：绝对坐标的 M、L、C、Z）。</summary>
public partial class IconCustom : UserControl
{
    /// <summary>站点标志：一个 V 加上面的点。</summary>
    public static readonly IconData Logo = new("logo", "M4 6L12 18L20 6M10.5 4C10.5 3.172 11.172 2.5 12 2.5C12.828 2.5 13.5 3.172 13.5 4C13.5 4.828 12.828 5.5 12 5.5C11.172 5.5 10.5 4.828 10.5 4Z");

    /// <summary>认证标记：圆圈、勾，右上角的实心点颜色仍跟随文字。</summary>
    public static readonly IconData Verified = new(
        "verified",
        "M3 12C3 7.029 7.029 3 12 3C16.971 3 21 7.029 21 12C21 16.971 16.971 21 12 21C7.029 21 3 16.971 3 12ZM8 12L11 15L16 9",
        "M16 5.5C16 4.119 17.119 3 18.5 3C19.881 3 21 4.119 21 5.5C21 6.881 19.881 8 18.5 8C17.119 8 16 6.881 16 5.5Z");

    /// <summary>波形：原稿画在 32 格上，按 24/32 换算到 24 格。</summary>
    public static readonly IconData Wave = new("wave", "M1.5 12C4.5 6 7.5 6 10.5 12C13.5 18 16.5 18 19.5 12C22.5 6 22.5 9 22.5 9");

    /// <summary>加载示例。</summary>
    public IconCustom() => InitializeComponent();
}
