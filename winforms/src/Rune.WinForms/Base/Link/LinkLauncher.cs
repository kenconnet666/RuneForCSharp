using System.Diagnostics;

namespace Rune.WinForms;

/// <summary>用系统打开链接（Shell 的默认程序：浏览器、邮件……）：链接、链接按钮、整条是链接的条目共用。</summary>
static class LinkLauncher
{
    public static void Open(Uri href) => Process.Start(new ProcessStartInfo(href.AbsoluteUri) { UseShellExecute = true })?.Dispose();
}
