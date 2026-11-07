using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf;

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
/// 最大化按钮在非客户区（HTMAXBUTTON，Windows 11 悬停时弹出贴靠布局），收不到客户区的鼠标：悬停、按下由外壳从非客户区的消息转过来，
/// 所以样式按 <see cref="IsHovered"/>、<see cref="IsPushed"/> 写，不按 IsMouseOver、IsPressed。
/// </summary>
public class WindowCaptionButton : ButtonBase
{
    static readonly DependencyPropertyKey KindKey = DependencyProperty.RegisterReadOnly(nameof(Kind), typeof(WindowCaptionKind), typeof(WindowCaptionButton),
        new PropertyMetadata(WindowCaptionKind.Close));

    /// <summary>种类。</summary>
    public static readonly DependencyProperty KindProperty = KindKey.DependencyProperty;

    static readonly DependencyPropertyKey IsHoveredKey = DependencyProperty.RegisterReadOnly(nameof(IsHovered), typeof(bool), typeof(WindowCaptionButton), new PropertyMetadata(false));

    /// <summary>指针在按钮上（客户区或非客户区）。</summary>
    public static readonly DependencyProperty IsHoveredProperty = IsHoveredKey.DependencyProperty;

    static readonly DependencyPropertyKey IsPushedKey = DependencyProperty.RegisterReadOnly(nameof(IsPushed), typeof(bool), typeof(WindowCaptionButton), new PropertyMetadata(false));

    /// <summary>按下着（客户区或非客户区）。</summary>
    public static readonly DependencyProperty IsPushedProperty = IsPushedKey.DependencyProperty;

    readonly Icon icon = new() { FontSize = 16, StrokeWidth = 1.5 };
    bool nonClientHover;

    static WindowCaptionButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(WindowCaptionButton), new FrameworkPropertyMetadata(typeof(WindowCaptionButton)));

    /// <summary>新建某种窗口按钮。</summary>
    /// <param name="kind">种类。</param>
    public WindowCaptionButton(WindowCaptionKind kind)
    {
        SetValue(KindKey, kind);
        Content = icon;
        Focusable = false;
        IsTabStop = false;
        UpdateIcon();
    }

    /// <summary>种类。</summary>
    public WindowCaptionKind Kind => (WindowCaptionKind)GetValue(KindProperty);

    /// <summary>指针在按钮上。</summary>
    public bool IsHovered => (bool)GetValue(IsHoveredProperty);

    /// <summary>按下着。</summary>
    public bool IsPushed => (bool)GetValue(IsPushedProperty);

    /// <summary>名称与提示（"最小化"……）。</summary>
    public string? Label
    {
        get;
        set
        {
            field = value;
            AutomationProperties.SetName(this, value);
            ToolTip = value;
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

    /// <summary>非客户区里按下了（外壳在松开时判断要不要切换最大化）。</summary>
    internal bool IsNonClientPressed { get; private set; }

    /// <summary>非客户区的悬停（最大化按钮）。</summary>
    internal void SetNonClientHover(bool hover)
    {
        nonClientHover = hover;
        Update();
    }

    /// <summary>非客户区的按下（最大化按钮）。</summary>
    internal void SetNonClientPressed(bool pressed)
    {
        IsNonClientPressed = pressed;
        Update();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty || e.Property == IsPressedProperty) Update();
    }

    void Update()
    {
        SetValue(IsHoveredKey, IsMouseOver || nonClientHover);
        SetValue(IsPushedKey, IsPressed || IsNonClientPressed);
    }

    /// <summary>横线、方框（还原是两个叠着的方框）、叉。</summary>
    void UpdateIcon() => icon.Data = Kind switch
    {
        WindowCaptionKind.Minimize => Lucide.Minus,
        WindowCaptionKind.Maximize => IsMaximized ? Lucide.Copy : Lucide.Square,
        _ => Lucide.X,
    };
}
