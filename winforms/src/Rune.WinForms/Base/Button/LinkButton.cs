using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 外观是按钮的链接（vue-tsx-pro 的 LinkButton）：外观、图标、加载中、波纹同 <see cref="Button"/>；外部链接没写 IconEnd 时显示外链图标。
/// 放进窗体用 <see cref="LinkButtonControl"/>（点击后打开链接，读屏是链接）。
/// </summary>
public class LinkButton : Button, ILink
{
    /// <summary>链接地址（绝对地址，如 https://、mailto:）。</summary>
    public Uri? Href { get; set; }

    /// <summary>外部链接：没写 IconEnd 时显示外链图标，读屏名称后补上"在新窗口打开"。</summary>
    public bool External { get; set { field = value; RebuildIconEnd(); } }

    /// <inheritdoc />
    protected override IconData? IconEndFallback => External ? Lucide.ArrowUpRight : null;
}

/// <summary>
/// 只有图标的链接（vue-tsx-pro 的 IconLinkButton）：外观同 <see cref="IconButton"/>，行为同 <see cref="LinkButton"/>。
/// 放进窗体用 <see cref="IconLinkButtonControl"/>。
/// </summary>
public class IconLinkButton : IconButton, ILink
{
    /// <summary>链接地址（绝对地址）。</summary>
    public Uri? Href { get; set; }

    /// <summary>外部链接：读屏名称后补上"在新窗口打开"。</summary>
    public bool External { get; set; }
}

/// <summary>链接按钮的元素：宿主（<see cref="ButtonControlBase{T}"/>）据此在点击后打开链接、在读屏里报成链接。</summary>
interface ILink
{
    Uri? Href { get; }

    bool External { get; }
}

/// <summary>
/// 放进窗体、设计器里用的 <see cref="LinkButton"/>：点了去别处时用它，读屏念"链接"。
/// 桌面没有站内地址：<see cref="Href"/> 是绝对地址，Click 之后用系统打开（浏览器、邮件……）；应用里的跳转直接处理 Click。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Href))]
[Description("链接按钮：外观同按钮，点击后用系统打开链接。")]
public class LinkButtonControl : ButtonControl<LinkButton>
{
    /// <summary>链接地址。</summary>
    [Category("Behavior")]
    [Description("链接地址（绝对地址，如 https://、mailto:）。")]
    [DefaultValue(null)]
    public Uri? Href { get => Element.Href; set => Element.Href = value; }

    /// <summary>外部链接。</summary>
    [Category("Behavior")]
    [Description("外部链接：显示外链图标，读屏补上\"在新窗口打开\"。")]
    [DefaultValue(false)]
    public bool External { get => Element.External; set => Element.External = value; }
}

/// <summary>放进窗体、设计器里用的 <see cref="IconLinkButton"/>：读屏名称写 AccessibleName（必须写）。</summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Href))]
[Description("图标链接：外观同图标按钮，点击后用系统打开链接。")]
public class IconLinkButtonControl : ButtonControlBase<IconLinkButton>
{
    /// <summary>链接地址。</summary>
    [Category("Behavior")]
    [Description("链接地址（绝对地址，如 https://、mailto:）。")]
    [DefaultValue(null)]
    public Uri? Href { get => Element.Href; set => Element.Href = value; }

    /// <summary>外部链接。</summary>
    [Category("Behavior")]
    [Description("外部链接：读屏补上\"在新窗口打开\"。")]
    [DefaultValue(false)]
    public bool External { get => Element.External; set => Element.External = value; }
}
