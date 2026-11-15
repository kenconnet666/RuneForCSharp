using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>
/// 按权限点显示（vue-tsx-pro 的 Can）：有权限才显示里面的内容，没有时显示 Fallback（默认什么都不显示）。
/// 权限从哪来由应用在 <see cref="Provider.Permissions"/> 里接入；没接入时一律放行。
/// <code>
/// &lt;rune:Can Permission="cost:project:add"&gt;&lt;rune:Button Content="新建" /&gt;&lt;/rune:Can&gt;
/// &lt;rune:Can Permission="cost:project:edit, cost:project:add" Fallback="只读"&gt;……&lt;/rune:Can&gt;
/// </code>
/// 只控制界面，不是安全边界：接口上的权限校验才是。要自己判断（禁用按钮、过滤菜单项）时用 <see cref="Provider.Can"/>。
/// </summary>
public class Can : Decorator
{
    /// <summary>需要的权限点；几个时用逗号分开，有其中任意一个即可。</summary>
    public static readonly StyledProperty<string?> PermissionProperty = AvaloniaProperty.Register<Can, string?>(nameof(Permission));

    /// <summary>有权限时显示的内容。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<Can, object?>(nameof(Content));

    /// <summary>没有权限时显示的内容（文字或控件），默认不显示。</summary>
    public static readonly StyledProperty<object?> FallbackProperty = AvaloniaProperty.Register<Can, object?>(nameof(Fallback));

    readonly ContentPresenter presenter = new();

    /// <summary>新建：内容由 ContentPresenter 呈现（文字、控件都行）。</summary>
    public Can() => Child = presenter;

    /// <summary>需要的权限点。</summary>
    public string? Permission { get => GetValue(PermissionProperty); set => SetValue(PermissionProperty, value); }

    /// <summary>有权限时显示的内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>没有权限时显示的内容。</summary>
    public object? Fallback { get => GetValue(FallbackProperty); set => SetValue(FallbackProperty, value); }

    /// <summary>现在有没有权限（没写 Permission 时不设限）。</summary>
    public bool Allowed => (Permission ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is not { Length: > 0 } needed || Provider.Can(this, needed);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PermissionProperty || change.Property == ContentProperty || change.Property == FallbackProperty || change.Property == Provider.PermissionsProperty)
            presenter.Content = Allowed ? Content : Fallback;
    }
}
