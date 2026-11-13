using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 外壳顶栏的品牌（vue-tsx-pro 的 AppShellBrand）：品牌色的标志与产品名。无窗口元素，放进窗体用 <see cref="AppShellBrandControl"/>。
/// 是一个没有底色、没有内边距的 lg 按钮：图标 accentText，文字 text；悬停不变色（TS 是链接，没有悬停底色）。
/// </summary>
public class AppShellBrand : Button
{
    /// <summary>新建：lg 档、图标用品牌色。</summary>
    public AppShellBrand()
    {
        Size = TextSize.Lg;
        Sx = new ButtonSx { Background = Color.Transparent, PaddingInline = 0, PaddingBlock = 0 };
        PropsIcon = new IconProps { Tone = TextTone.Accent };
    }

    /// <inheritdoc />
    protected override ButtonVariant DefaultVariant => ButtonVariant.Ghost;

    /// <inheritdoc />
    protected override StatusTone DefaultTone => StatusTone.Neutral;
}

/// <summary>
/// 放进 <see cref="AppShellControl.Brand"/> 的品牌：产品名就是控件的 <see cref="Control.Text"/>，也是读屏的名称；点了回首页（Click，桌面上没有路由）。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Text))]
[Description("外壳顶栏的品牌：品牌色的标志与产品名。")]
public class AppShellBrandControl : ButtonControl<AppShellBrand>
{
}
