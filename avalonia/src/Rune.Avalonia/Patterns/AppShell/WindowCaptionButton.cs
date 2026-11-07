using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Metadata;
using Avalonia.Input;

namespace Rune.Avalonia;

/// <summary>窗口按钮的种类。</summary>
public enum WindowCaptionKind
{
    /// <summary>最小化。</summary>
    Minimize,

    /// <summary>最大化（最大化后是还原）。</summary>
    Maximize,

    /// <summary>关闭。</summary>
    Close,
}

/// <summary>
/// 自绘的窗口按钮（桌面特有，<see cref="AppShell"/> 的顶栏兼作标题栏时用）：46 像素宽、与标题栏等高，悬停 fillHover 底，关闭悬停 danger 底。
/// 同系统标题栏的按钮：不进 Tab 顺序（键盘用 Alt+空格、Win+方向键），名称与提示是 <see cref="Label"/>。
/// 平台的非客户区命中按种类标出（Win32 的 HTMINBUTTON、HTMAXBUTTON、HTCLOSE），所以悬停最大化按钮时 Windows 11 照样弹出贴靠布局。
/// 伪类：<c>:close</c>（关闭按钮）。
/// </summary>
[PseudoClasses(":close")]
public class WindowCaptionButton : ButtonBase
{
    readonly Icon icon = new() { FontSize = 16, StrokeWidth = 1.5 };

    /// <summary>新建某种窗口按钮。</summary>
    /// <param name="kind">种类。</param>
    public WindowCaptionButton(WindowCaptionKind kind)
    {
        Kind = kind;
        Content = icon;
        Focusable = false;
        WindowDecorationProperties.SetElementRole(this, kind switch
        {
            WindowCaptionKind.Minimize => WindowDecorationsElementRole.MinimizeButton,
            WindowCaptionKind.Maximize => WindowDecorationsElementRole.MaximizeButton,
            _ => WindowDecorationsElementRole.CloseButton,
        });
        PseudoClasses.Set(":close", kind == WindowCaptionKind.Close);
        UpdateIcon();
    }

    /// <summary>种类。</summary>
    public WindowCaptionKind Kind { get; }

    /// <summary>名称与提示（"最小化"……）。</summary>
    public string? Label
    {
        get;
        set
        {
            field = value;
            AutomationProperties.SetName(this, value);
            ToolTip.SetTip(this, value);
        }
    }

    /// <summary>窗口最大化了：最大化按钮换成还原的图标。</summary>
    public bool IsMaximized
    {
        get;
        set
        {
            field = value;
            UpdateIcon();
        }
    }

    /// <summary>横线、方框（还原是两个叠着的方框）、叉。</summary>
    void UpdateIcon() => icon.Data = Kind switch
    {
        WindowCaptionKind.Minimize => Lucide.Minus,
        WindowCaptionKind.Maximize => IsMaximized ? Lucide.Copy : Lucide.Square,
        _ => Lucide.X,
    };
}
