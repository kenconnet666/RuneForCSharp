using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 复制按钮（vue-tsx-pro 的 CopyButton）：点击把 <see cref="Value"/> 写进剪贴板，图标换成对勾（缩放弹出），读屏播报"已复制"，
/// <see cref="Timeout"/> 后复原。外观同 <see cref="IconButton"/>（默认 ghost、中性色），读屏名称默认取语言包的"复制"。
/// <code>
/// &lt;rune:CopyButton Value="{Binding ApiKey}" AutomationProperties.Name="复制 API 密钥" /&gt;
/// copy.ValueFactory = () =&gt; api.CreateInviteAsync();   // 点击时才取：等待时转圈，重复点击不再取
/// </code>
/// 复制完成后发 <see cref="Copied"/>（成功与否、失败的原因）。复制成功时 <see cref="IsCopied"/>，样式触发器据此换成对勾。
/// </summary>
public class CopyButton : IconButton
{
    /// <summary>要复制的文字。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(CopyButton));

    /// <summary>点击时才取的内容（内容按需生成、要等接口时）；写了它时不用 <see cref="Value"/>。</summary>
    public static readonly DependencyProperty ValueFactoryProperty = DependencyProperty.Register(nameof(ValueFactory), typeof(Func<Task<string>>), typeof(CopyButton));

    /// <summary>对勾保持多久后复原；默认 2 秒。</summary>
    public static readonly DependencyProperty TimeoutProperty = DependencyProperty.Register(nameof(Timeout), typeof(TimeSpan), typeof(CopyButton), new PropertyMetadata(TimeSpan.FromSeconds(2)));

    static readonly DependencyPropertyKey IsCopiedKey = DependencyProperty.RegisterReadOnly(nameof(IsCopied), typeof(bool), typeof(CopyButton), new PropertyMetadata(false));

    /// <summary>刚复制成功（对勾显示着）。</summary>
    public static readonly DependencyProperty IsCopiedProperty = IsCopiedKey.DependencyProperty;

    int copies;

    static CopyButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CopyButton), new FrameworkPropertyMetadata(typeof(CopyButton)));

    /// <summary>复制完成（成功或失败）。</summary>
    public event EventHandler<CopiedEventArgs>? Copied;

    /// <summary>要复制的文字。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>点击时才取的内容。</summary>
    public Func<Task<string>>? ValueFactory { get => (Func<Task<string>>?)GetValue(ValueFactoryProperty); set => SetValue(ValueFactoryProperty, value); }

    /// <summary>对勾保持多久。</summary>
    public TimeSpan Timeout { get => (TimeSpan)GetValue(TimeoutProperty); set => SetValue(TimeoutProperty, value); }

    /// <summary>刚复制成功。</summary>
    public bool IsCopied => (bool)GetValue(IsCopiedProperty);

    /// <inheritdoc />
    protected override string DefaultAccessibleName => Provider.GetLang(this).Copy;

    /// <summary>点击：取值（异步时转圈）、写剪贴板；取值或写入出错也收在这里，经 <see cref="Copied"/> 告诉使用方。</summary>
    protected override async void OnClick()
    {
        if (IsLoading || IsDisabled) return;
        base.OnClick();
        bool success;
        Exception? error = null;
        var factory = ValueFactory;
        try
        {
            var text = Value ?? "";
            if (factory is not null)
            {
                IsLoading = true;
                text = await factory();
            }
            Clipboard.SetText(text);
            success = true;
        }
        catch (Exception exception)
        {
            (success, error) = (false, exception);
        }
        finally
        {
            if (factory is not null) IsLoading = false;
        }
        if (success) ShowCopied();
        Copied?.Invoke(this, new CopiedEventArgs(success, error));
    }

    /// <summary>换成对勾（缩放弹出：durations.md、enter 缓动），读屏念"已复制"，<see cref="Timeout"/> 后复原；再次复制作废上一次的复原。</summary>
    async void ShowCopied()
    {
        SetValue(IsCopiedKey, true);
        var copied = Provider.GetLang(this).Copied;
        UIElementAutomationPeer.FromElement(this)?.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent, copied, "Rune.CopyButton.Copied");
        Pop();
        var version = ++copies;
        await Task.Delay(Timeout);
        if (version == copies) SetValue(IsCopiedKey, false);
    }

    /// <summary>对勾从 0.5 倍、透明放大到原样。</summary>
    void Pop()
    {
        if (GetTemplateChild("PART_Icon") is not Icon icon) return;
        var duration = Motion.Duration(this, "Rune.Duration.Md", TimeSpan.FromMilliseconds(200));
        if (duration <= TimeSpan.Zero) return;
        var easing = TryFindResource("Rune.Easing.Enter") as IEasingFunction;
        var scale = new ScaleTransform();
        icon.RenderTransformOrigin = new Point(0.5, 0.5);
        icon.RenderTransform = scale;
        foreach (var axis in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            scale.BeginAnimation(axis, new DoubleAnimation(0.5, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
        icon.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CopyButtonAutomationPeer(this);

    /// <summary>读屏：按钮，复制后的项状态是"已复制"（另发通知事件念出来）。</summary>
    sealed class CopyButtonAutomationPeer(CopyButton owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).Copy;

        protected override string GetItemStatusCore() => owner.IsCopied ? Provider.GetLang(owner).Copied : base.GetItemStatusCore();
    }
}

/// <summary>复制完成的参数。</summary>
/// <param name="success">写进剪贴板了。</param>
/// <param name="error">失败的原因（取值出错、剪贴板被占用等）；成功时为 null。</param>
public sealed class CopiedEventArgs(bool success, Exception? error) : EventArgs
{
    /// <summary>写进剪贴板了。</summary>
    public bool Success { get; } = success;

    /// <summary>失败的原因；成功时为 null。</summary>
    public Exception? Error { get; } = error;
}
