namespace Rune.WinForms;

/// <summary>
/// 浮层里放可交互内容的窗口（<see cref="FloatingSurface"/> 的上层）：无边框、不进任务栏的窗体，盖在 <see cref="LayeredWindow"/> 画好的面板上。
/// 内容的主题、语言作用域接着从 <see cref="Origin"/> 往外找（<see cref="ProviderControl.Of"/>）。
/// 不激活时（<see cref="Activates"/> 为 false，下拉的选项列表）点它不抢激活，焦点留在弹出它的控件上。
/// </summary>
sealed class FloatingForm : Form
{
    const int WsExNoActivate = 0x8000000;

    /// <summary>新建。</summary>
    /// <param name="origin">浮层是从哪个控件弹出来的。</param>
    public FloatingForm(Control origin)
    {
        Origin = origin;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
    }

    /// <summary>浮层是从哪个控件弹出来的：作用域沿它的 Parent 链接着找。</summary>
    public Control Origin { get; }

    /// <summary>显示时激活、拿键盘焦点（默认）；false 时点它也不激活。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Activates
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (IsHandleCreated) RecreateHandle();
        }
    } = true;

    /// <summary>按了 Esc（窗体里的控件没有自己处理）。</summary>
    public event EventHandler? Escape;

    /// <inheritdoc />
    protected override bool ShowWithoutActivation => !Activates;

    /// <inheritdoc />
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (!Activates) parameters.ExStyle |= WsExNoActivate;
            return parameters;
        }
    }

    /// <inheritdoc />
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData != Keys.Escape || Escape is null) return base.ProcessDialogKey(keyData);
        Escape(this, EventArgs.Empty);
        return true;
    }
}
