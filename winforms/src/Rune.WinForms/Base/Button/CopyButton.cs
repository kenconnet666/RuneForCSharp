using System.ComponentModel;
using System.Windows.Forms.Automation;

namespace Rune.WinForms;

/// <summary>
/// 复制按钮（vue-tsx-pro 的 CopyButton）的元素：复制图标的 <see cref="IconButton"/>，<see cref="IsCopied"/> 时换成对勾并缩放弹出
/// （从 0.5 倍、透明放大到原样，durations.md、enter 缓动）。放进窗体用 <see cref="CopyButtonControl"/>，复制由它做。
/// </summary>
public class CopyButton : IconButton
{
    readonly Signal<double> pop = new(1);

    /// <summary>新建复制按钮。</summary>
    public CopyButton() => Icon = Lucide.Copy;

    /// <summary>刚复制成功（对勾显示着）。</summary>
    public bool IsCopied
    {
        get;
        internal set
        {
            if (field == value) return;
            field = value;
            Icon = value ? Lucide.Check : Lucide.Copy;
            if (!value) return;
            pop.Value = 0;
            Animator.Animate(pop, 1, Tokens.Duration.Md, Easing.Enter);
        }
    }

    /// <summary>弹出的进度：0 刚换成对勾，1 原样（测试用）。</summary>
    internal double PopProgress => pop.Peek();

    /// <summary>停掉进行中的弹出（控件释放时）。</summary>
    internal void Stop() => Animator.Stop(pop);

    /// <summary>弹出途中按进度缩放、淡入图标。</summary>
    protected override void PaintContent(Graphics graphics, ElementContext inner, Rectangle bounds)
    {
        var progress = (float)pop.Value;
        if (progress >= 1)
        {
            base.PaintContent(graphics, inner, bounds);
            return;
        }
        var state = graphics.Save();
        try
        {
            var scale = 0.5f + 0.5f * progress;
            graphics.TranslateTransform(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            graphics.ScaleTransform(scale, scale);
            graphics.TranslateTransform(-(bounds.X + bounds.Width / 2f), -(bounds.Y + bounds.Height / 2f));
            base.PaintContent(graphics, inner with { ForeColor = Fade(inner.ForeColor, progress) }, bounds);
        }
        finally
        {
            graphics.Restore(state);
        }
    }
}

/// <summary>
/// 放进窗体、设计器里用的 <see cref="CopyButton"/>：点击把 <see cref="Value"/> 写进剪贴板，图标换成对勾，读屏播报"已复制"，
/// <see cref="Timeout"/> 后复原。读屏名称默认取语言包的"复制"（<see cref="Lang.Copy"/>），写 AccessibleName 能改。
/// <code>
/// new CopyButtonControl { Value = apiKey, AccessibleName = "复制 API 密钥" };
/// copy.ValueFactory = () =&gt; api.CreateInviteAsync();   // 点击时才取：等待时转圈，重复点击不再取
/// </code>
/// 复制完成后发 <see cref="Copied"/>（成功与否、失败的原因）。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Copied))]
[DefaultProperty(nameof(Value))]
[Description("复制按钮：点击复制文字，图标换成对勾并播报。")]
public class CopyButtonControl : ButtonControlBase<CopyButton>
{
    int copies;

    /// <summary>复制完成（成功或失败）。</summary>
    [Category("Action")]
    [Description("复制完成：成功与否、失败的原因。")]
    public event EventHandler<CopiedEventArgs>? Copied;

    /// <summary>要复制的文字。</summary>
    [Category("Data")]
    [Description("要复制的文字。")]
    [DefaultValue(null)]
    public string? Value { get; set; }

    /// <summary>点击时才取的内容（内容按需生成、要等接口时）；写了它时不用 <see cref="Value"/>。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Task<string>>? ValueFactory { get; set; }

    /// <summary>对勾保持多久后复原；默认 2 秒。</summary>
    [Category("Behavior")]
    [Description("对勾保持多久后复原。")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>刚复制成功（对勾显示着）。</summary>
    [Browsable(false)]
    public bool IsCopied => Element.IsCopied;

    bool ShouldSerializeTimeout() => Timeout != TimeSpan.FromSeconds(2);

    void ResetTimeout() => Timeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    protected override string DefaultAccessibleName => ProviderControl.LangOf(this).Copy;

    /// <summary>点击：取值（异步时转圈）、写剪贴板；取值或写入出错也收在这里，经 <see cref="Copied"/> 告诉使用方。</summary>
    protected override async void OnClick(EventArgs e)
    {
        if (IsLoading || !Enabled) return;
        base.OnClick(e);
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
            // 剪贴板可能正被别的程序占着：重试几次
            Clipboard.SetDataObject(text, true, 5, 100);
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

    /// <summary>换成对勾，读屏念"已复制"，<see cref="Timeout"/> 后复原；再次复制作废上一次的复原。</summary>
    async void ShowCopied()
    {
        Element.IsCopied = true;
        if (IsHandleCreated)
            AccessibilityObject.RaiseAutomationNotification(AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent, ProviderControl.LangOf(this).Copied);
        var version = ++copies;
        await Task.Delay(Timeout);
        if (version == copies && !IsDisposed) Element.IsCopied = false;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) Element.Stop();
        base.Dispose(disposing);
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
