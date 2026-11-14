namespace Rune.WinForms;

/// <summary>为什么开、关（vue-tsx-pro 的 OpenChangeReason）：只报告用户的操作，代码里（父级、绑定）改 IsOpen 时不报告。</summary>
public enum OpenChangeReason
{
    /// <summary>点了触发器。</summary>
    Trigger,

    /// <summary>点了浮层外面（WinForms 是浮层窗口失去了激活）。</summary>
    Outside,

    /// <summary>按了 Esc。</summary>
    Escape,

    /// <summary>点了关闭按钮、取消按钮。</summary>
    Close,

    /// <summary>点了确认按钮（对话框）。</summary>
    Confirm,

    /// <summary>选中了一项（单选的下拉选中后关闭）。</summary>
    Select,

    /// <summary>焦点离开了（Tab 移走）。</summary>
    Focus,

    /// <summary>代码调用：快捷键打开（命令面板）、命令式打开的对话框。</summary>
    Imperative,
}
