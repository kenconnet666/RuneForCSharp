namespace Rune.WinForms;

/// <summary>重建子控件时用。</summary>
static class ChildControls
{
    /// <summary>
    /// 释放这些控件（释放时会从父控件里移走）。重建子控件不能只用 <c>Controls.Clear()</c>：
    /// 移走的控件不会被释放，窗口句柄、信号订阅、动画都还留着，每重建一次漏一批。
    /// </summary>
    public static void DisposeAll(this IEnumerable<Control> controls)
    {
        foreach (var control in controls.ToList()) control.Dispose();
    }
}
