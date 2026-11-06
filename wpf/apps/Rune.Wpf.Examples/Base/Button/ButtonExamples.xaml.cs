using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>Button 的示例（vue-tsx-pro 文档站 base/Button）。ButtonGroup、Card、Kbd 做出来后补上其余几节。</summary>
static class ButtonExamples
{
    public static Example[] All =>
    [
        new("Button", "variants", "样式", "solid 是一组操作里的主要操作，每组最多一个；其余按强调程度递减。", () => new ButtonVariants()),
        new("Button", "tones", "色调", "Tone 与 Badge、Alert 同一套：neutral 用于取消这类不需要强调的操作，danger 用于删除等破坏性操作。", () => new ButtonTones()),
        new("Button", "sizes", "尺寸", "Size 是字号档位，内边距、高度、图标都按 em 随之缩放；不写时继承外面的字号。", () => new ButtonSizes()),
        new("Button", "radius", "圆角", "Radius 默认 md；full 是胶囊形。", () => new ButtonRadius()),
        new("Button", "icons", "图标", "Icon 在文字前、IconEnd 在文字后，大小跟随按钮的字号；内容也可以放别的组件（Badge）。", () => new ButtonIcons()),
        new("Button", "loading", "加载中", "IsLoading 显示旋转指示，宽度不变、点击无效、焦点保留；很快完成的加载不出现指示，出现了至少显示 0.5 秒。", () => new ButtonLoading()),
        new("Button", "links", "链接按钮", "点了去别处用 LinkButton 而不是 Button 加点击：读屏念“链接”。外观同 Button；桌面上 Href 用系统打开，External 显示外链图标并告诉读屏在新窗口打开。", () => new ButtonLinks()),
    ];
}

/// <summary>Button/variants。</summary>
public partial class ButtonVariants : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonVariants() => InitializeComponent();
}

/// <summary>Button/tones。</summary>
public partial class ButtonTones : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonTones() => InitializeComponent();
}

/// <summary>Button/sizes。</summary>
public partial class ButtonSizes : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonSizes() => InitializeComponent();
}

/// <summary>Button/radius。</summary>
public partial class ButtonRadius : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonRadius() => InitializeComponent();
}

/// <summary>Button/icons。</summary>
public partial class ButtonIcons : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonIcons() => InitializeComponent();
}

/// <summary>Button/loading：点击后模拟一次请求。</summary>
public partial class ButtonLoading : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonLoading()
    {
        InitializeComponent();
        Save.Click += (_, _) => Request(Save, TimeSpan.FromSeconds(1.5));
        Quick.Click += (_, _) => Request(Quick, TimeSpan.FromSeconds(0.1));
    }

    static async void Request(Rune.Wpf.Button button, TimeSpan duration)
    {
        button.IsLoading = true;
        await Task.Delay(duration);
        button.IsLoading = false;
    }
}

/// <summary>Button/links。</summary>
public partial class ButtonLinks : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonLinks() => InitializeComponent();
}
