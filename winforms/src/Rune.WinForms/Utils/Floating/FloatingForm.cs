namespace Rune.WinForms;

/// <summary>
/// 浮层里放可交互内容的窗口（弹出框）：无边框、不进任务栏的普通窗体，盖在 <see cref="LayeredWindow"/> 画好的面板上（只盖内边距以内，
/// 面板的圆角、描边、阴影露在外面）。内容的主题、语言作用域接着从 <see cref="Origin"/> 往外找（<see cref="ProviderControl.Of"/>）。
/// </summary>
sealed class FloatingForm : Form
{
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

    /// <summary>按了 Esc（窗体里的控件没有自己处理）。</summary>
    public event EventHandler? Escape;

    /// <inheritdoc />
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData != Keys.Escape || Escape is null) return base.ProcessDialogKey(keyData);
        Escape(this, EventArgs.Empty);
        return true;
    }
}
