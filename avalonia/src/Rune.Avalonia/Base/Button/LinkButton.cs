using global::Avalonia;
using global::Avalonia.Automation.Peers;
using global::Avalonia.Controls;

namespace Rune.Avalonia;

/// <summary>
/// 外观是按钮的链接（vue-tsx-pro 的 LinkButton）：点了去别处时用它，读屏念"链接"。外观、图标、加载中、波纹同 <see cref="Button"/>。
/// <code>
/// &lt;rune:LinkButton Href="https://example.com/docs" External="True" Variant="outline"&gt;文档&lt;/rune:LinkButton&gt;
/// </code>
/// 桌面没有站内地址（ROADMAP"平台化的约定"）：<see cref="Href"/> 是绝对地址，点击后用系统打开（浏览器、邮件……）；
/// 应用里的跳转用 Command 或 Click。Click、Command 先于打开链接执行；加载中、不可用时都不执行。
/// </summary>
public class LinkButton : Button
{
    /// <summary>链接地址（绝对地址，如 https://、mailto:）。</summary>
    public static readonly StyledProperty<Uri?> HrefProperty = AvaloniaProperty.Register<LinkButton, Uri?>(nameof(Href));

    /// <summary>外部链接：没写 IconEnd 时显示外链图标，读屏名称后补上"在新窗口打开"（<see cref="Lang.LinkNewWindow"/>）。</summary>
    public static readonly StyledProperty<bool> ExternalProperty = AvaloniaProperty.Register<LinkButton, bool>(nameof(External));

    /// <summary>链接地址。</summary>
    public Uri? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>外部链接。</summary>
    public bool External { get => GetValue(ExternalProperty); set => SetValue(ExternalProperty, value); }

    /// <summary>先触发 Click、Command，再打开链接；加载中都不执行。</summary>
    protected override void OnClick()
    {
        base.OnClick();
        if (!IsLoading && Href is { IsAbsoluteUri: true } href) Navigate(href);
    }

    /// <summary>用系统打开链接（浏览器、邮件……）。</summary>
    /// <param name="href">绝对地址。</param>
    protected virtual void Navigate(Uri href) => LinkLauncher.Open(this, href);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LinkAutomationPeer(this, () => External);
}

/// <summary>
/// 只有图标的链接（vue-tsx-pro 的 IconLinkButton）：外观同 <see cref="IconButton"/>（宽高相等、默认 ghost），行为同 <see cref="LinkButton"/>。
/// 顶栏里的仓库、社交账号这类"点了去别处"的图标用它；必须写 <c>AutomationProperties.Name</c>。
/// <code>
/// &lt;rune:IconLinkButton Href="https://github.com/…" External="True" Icon="{x:Static rune:Lucide.Github}" AutomationProperties.Name="GitHub 仓库" /&gt;
/// </code>
/// </summary>
public class IconLinkButton : IconButton
{
    /// <summary>链接地址（绝对地址）。</summary>
    public static readonly StyledProperty<Uri?> HrefProperty = LinkButton.HrefProperty.AddOwner<IconLinkButton>();

    /// <summary>外部链接：读屏名称后补上"在新窗口打开"。</summary>
    public static readonly StyledProperty<bool> ExternalProperty = LinkButton.ExternalProperty.AddOwner<IconLinkButton>();

    /// <summary>链接地址。</summary>
    public Uri? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>外部链接。</summary>
    public bool External { get => GetValue(ExternalProperty); set => SetValue(ExternalProperty, value); }

    /// <summary>先触发 Click、Command，再打开链接；加载中都不执行。</summary>
    protected override void OnClick()
    {
        base.OnClick();
        if (!IsLoading && Href is { IsAbsoluteUri: true } href) Navigate(href);
    }

    /// <summary>用系统打开链接。</summary>
    /// <param name="href">绝对地址。</param>
    protected virtual void Navigate(Uri href) => LinkLauncher.Open(this, href);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LinkAutomationPeer(this, () => External);
}

/// <summary>用系统打开链接（TopLevel 的 Launcher：浏览器、邮件……）。</summary>
static class LinkLauncher
{
    public static void Open(Visual visual, Uri href) => _ = TopLevel.GetTopLevel(visual)?.Launcher.LaunchUriAsync(href);
}

/// <summary>链接按钮在读屏里是超链接（仍能 Invoke）；外部链接的名称后补上语言包的"在新窗口打开"。</summary>
sealed class LinkAutomationPeer(ButtonBase owner, Func<bool> external) : ButtonAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

    protected override string? GetNameCore() => external() ? base.GetNameCore() + Provider.GetLang(Owner).LinkNewWindow : base.GetNameCore();
}
