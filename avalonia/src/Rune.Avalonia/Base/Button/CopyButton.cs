using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input.Platform;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 复制按钮（vue-tsx-pro 的 CopyButton）：点击把 <see cref="Value"/> 写进剪贴板，图标换成对勾（缩放弹出），读屏播报"已复制"，
/// <see cref="Timeout"/> 后复原。外观同 <see cref="IconButton"/>（默认 ghost、中性色），读屏名称默认取语言包的"复制"。
/// <code>
/// &lt;rune:CopyButton Value="{Binding ApiKey}" AutomationProperties.Name="复制 API 密钥" /&gt;
/// copy.ValueFactory = () =&gt; api.CreateInviteAsync();   // 点击时才取：等待时转圈，重复点击不再取
/// </code>
/// 复制完成后发 <see cref="Copied"/>（成功与否、失败的原因）。复制成功时伪类 <c>:copied</c>。
/// 读屏播报：Avalonia 没有通知事件，按钮设为礼貌的实时区域，复制后的项状态是"已复制"。
/// </summary>
[PseudoClasses(":copied")]
public class CopyButton : IconButton
{
    /// <summary>要复制的文字。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<CopyButton, string?>(nameof(Value));

    /// <summary>点击时才取的内容（内容按需生成、要等接口时）；写了它时不用 <see cref="Value"/>。</summary>
    public static readonly StyledProperty<Func<Task<string>>?> ValueFactoryProperty = AvaloniaProperty.Register<CopyButton, Func<Task<string>>?>(nameof(ValueFactory));

    /// <summary>对勾保持多久后复原；默认 2 秒。</summary>
    public static readonly StyledProperty<TimeSpan> TimeoutProperty = AvaloniaProperty.Register<CopyButton, TimeSpan>(nameof(Timeout), TimeSpan.FromSeconds(2));

    /// <summary>刚复制成功（对勾显示着）。</summary>
    public static readonly DirectProperty<CopyButton, bool> IsCopiedProperty = AvaloniaProperty.RegisterDirect<CopyButton, bool>(nameof(IsCopied), button => button.IsCopied);

    int copies;

    /// <summary>新建：读屏的实时区域（复制后念出项状态）。</summary>
    public CopyButton() => AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);

    /// <summary>复制完成（成功或失败）。</summary>
    public event EventHandler<CopiedEventArgs>? Copied;

    /// <summary>要复制的文字。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>点击时才取的内容。</summary>
    public Func<Task<string>>? ValueFactory { get => GetValue(ValueFactoryProperty); set => SetValue(ValueFactoryProperty, value); }

    /// <summary>对勾保持多久。</summary>
    public TimeSpan Timeout { get => GetValue(TimeoutProperty); set => SetValue(TimeoutProperty, value); }

    /// <summary>刚复制成功。</summary>
    public bool IsCopied { get; private set => SetAndRaise(IsCopiedProperty, ref field, value); }

    /// <inheritdoc />
    protected override string DefaultAccessibleName => Provider.GetLang(this).Copy;

    /// <summary>点击：取值（异步时转圈）、写剪贴板；取值或写入出错也收在这里，经 <see cref="Copied"/> 告诉使用方。</summary>
    protected override async void OnClick()
    {
        base.OnClick();
        if (IsLoading || IsDisabled) return;
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
            await (TopLevel.GetTopLevel(this)?.Clipboard ?? throw new InvalidOperationException("复制按钮还没放进窗口：没有剪贴板")).SetTextAsync(text);
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

    /// <summary>
    /// 换成对勾（主题按 <c>:copied</c> 换图形；缩放弹出：durations.md、enter 缓动），读屏念"已复制"，<see cref="Timeout"/> 后复原。
    /// 等待用 Task.Delay 再回到 UI 线程（headless 测试的调度器不走 DispatcherTimer）；再次复制作废上一次的复原。
    /// </summary>
    void ShowCopied()
    {
        SetCopied(true);
        (ControlAutomationPeer.FromElement(this) as AutomationPeer)?.RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, "", Provider.GetLang(this).Copied);
        Pop();
        var version = ++copies;
        _ = Task.Delay(Timeout).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            if (version == copies) SetCopied(false);
        }), TaskScheduler.Default);
    }

    void SetCopied(bool value)
    {
        IsCopied = value;
        PseudoClasses.Set(":copied", value);
    }

    /// <summary>对勾从 0.5 倍、透明放大到原样。</summary>
    void Pop()
    {
        if (this.GetVisualDescendants().OfType<Icon>().FirstOrDefault(icon => icon.Name == IconButtonParts.Icon) is not { } icon) return;
        var duration = Motion.Duration(this, ThemeKeys.Duration.Md);
        if (duration <= TimeSpan.Zero) return;
        var easing = this.Resource<Easing>(ThemeKeys.Easing.Enter);
        // 关键帧动画没有 TransformOperations 的插值器：先无过渡地跳到起点，再挂上过渡放大回原样
        icon.Transitions = null;
        icon.RenderTransform = PopStart;
        icon.Opacity = 0;
        icon.Transitions =
        [
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = duration, Easing = easing },
            new DoubleTransition { Property = OpacityProperty, Duration = duration, Easing = easing },
        ];
        icon.RenderTransform = TransformOperations.Identity;
        icon.Opacity = 1;
    }

    static readonly TransformOperations PopStart = TransformOperations.Parse("scale(0.5)");

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new CopyButtonAutomationPeer(this);

    /// <summary>读屏：按钮，复制后的项状态是"已复制"。</summary>
    sealed class CopyButtonAutomationPeer(CopyButton owner) : ButtonBaseAutomationPeer(owner)
    {
        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).Copy;

        protected override string? GetItemStatusCore() => owner.IsCopied ? Provider.GetLang(owner).Copied : base.GetItemStatusCore();
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
