namespace Rune.Avalonia;

/// <summary>
/// 关闭按钮（vue-tsx-pro 的 CloseButton）：X 图标的 <see cref="IconButton"/>，读屏名称默认取语言包的"关闭"（<see cref="Lang.Close"/>），
/// 写 <c>AutomationProperties.Name</c> 能改。关闭弹层、标签、提示条用。
/// <code>
/// &lt;rune:CloseButton Click="OnClose" /&gt;
/// </code>
/// </summary>
public class CloseButton : IconButton
{
    /// <inheritdoc />
    protected override string DefaultAccessibleName => Provider.GetLang(this).Close;
}

/// <summary>
/// 清空按钮（vue-tsx-pro 的 ClearButton）：圆圈 X 图标的 <see cref="IconButton"/>，读屏名称默认取语言包的"清空"（<see cref="Lang.Clear"/>）。
/// 输入框里清空内容用。
/// </summary>
public class ClearButton : IconButton
{
    /// <inheritdoc />
    protected override string DefaultAccessibleName => Provider.GetLang(this).Clear;
}
