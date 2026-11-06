using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>关闭按钮（vue-tsx-pro 的 CloseButton）：X 图标的 <see cref="IconButton"/>。放进窗体用 <see cref="CloseButtonControl"/>。</summary>
public class CloseButton : IconButton
{
    /// <summary>新建关闭按钮。</summary>
    public CloseButton() => Icon = Lucide.X;
}

/// <summary>清空按钮（vue-tsx-pro 的 ClearButton）：圆圈 X 图标的 <see cref="IconButton"/>。放进窗体用 <see cref="ClearButtonControl"/>。</summary>
public class ClearButton : IconButton
{
    /// <summary>新建清空按钮。</summary>
    public ClearButton() => Icon = Lucide.CircleX;
}

/// <summary>
/// 放进窗体、设计器里用的 <see cref="CloseButton"/>：读屏名称默认取语言包的"关闭"（<see cref="Lang.Close"/>），写 AccessibleName 能改。
/// 关闭弹层、标签、提示条用。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[Description("关闭按钮：X 图标，读屏名称取语言包。")]
public class CloseButtonControl : ButtonControlBase<CloseButton>
{
    /// <inheritdoc />
    protected override string DefaultAccessibleName => RuneLang.Current.Value.Close;
}

/// <summary>放进窗体、设计器里用的 <see cref="ClearButton"/>：读屏名称默认取语言包的"清空"（<see cref="Lang.Clear"/>）。输入框里清空内容用。</summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(Click))]
[Description("清空按钮：圆圈 X 图标，读屏名称取语言包。")]
public class ClearButtonControl : ButtonControlBase<ClearButton>
{
    /// <inheritdoc />
    protected override string DefaultAccessibleName => RuneLang.Current.Value.Clear;
}
