using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rune.Wpf;

/// <summary>
/// 抽屉（vue-tsx-pro 的 Drawer）：贴着窗口一侧滑入的模态面板。内核同 <see cref="Dialog"/>（遮罩、焦点、Esc、点遮罩、BeforeClose、确认、命令式 ShowAsync）。
/// <code>
/// &lt;rune:Drawer IsOpen="{Binding Filtering}" Side="end" Title="筛选"&gt;…&lt;/rune:Drawer&gt;
/// &lt;rune:Drawer IsOpen="{Binding Editing}" Size="md" Width="{Binding PanelWidth}" Title="订单详情"&gt;…&lt;/rune:Drawer&gt;
/// </code>
/// <see cref="Side"/> 是从哪一侧滑入（默认结束边，从右到左排版时左右互换）：左右两侧占满窗口高度，宽度按档位 <see cref="Dialog.Size"/>（默认 xs，不超出窗口），
/// 写了 Width 时按它（默认双向绑定，记住拖动后的宽度）；上下两侧占满窗口宽度、高度按内容（不超出窗口）。贴边的那一侧没有描边，也没有圆角。
/// 打开时从所在一侧滑入（durations.lg、enter；系统关闭动画时不播）。左右两侧默认 <see cref="Resizable"/>：靠正文的一边是宽度调整手柄
/// （<see cref="ResizeHandle"/>，拖动、聚焦后 ← / →、双击回到打开时的宽度），能一直拖到只剩 MinWidth（默认 48）那么一窄条、让出后面的窗口；
/// 拖窄以后标题行、说明、正文、页脚保持能读的最小宽度（20em，不超出窗口减 3em），多出来的被面板裁掉。部件样式键同 <see cref="DialogParts"/>，
/// 手柄是 <see cref="DrawerParts.ResizeHandle"/>。
/// </summary>
public class Drawer : Dialog
{
    /// <summary>从哪一侧滑入，默认结束边。</summary>
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(nameof(Side), typeof(DrawerSide), typeof(Drawer),
        new FrameworkPropertyMetadata(DrawerSide.End, (d, _) => ((Drawer)d).ApplySide()));

    /// <summary>左右两侧时靠正文的一边能拖动调整宽度，默认 true。</summary>
    public static readonly DependencyProperty ResizableProperty = DependencyProperty.Register(nameof(Resizable), typeof(bool), typeof(Drawer),
        new FrameworkPropertyMetadata(true, (d, _) => ((Drawer)d).ApplySide()));

    /// <summary>给拖动留出的窗口宽度：手柄最多拖到窗口宽减它（同上游的 innerWidth - 48）。</summary>
    const double Reserve = 48;

    readonly ResizeHandle handle = new();
    readonly TranslateTransform slide = new();
    double? initialWidth;

    static Drawer()
    {
        // Width 是面板的宽度（抽屉自己不参与排版）：默认双向绑定，记住拖动后的宽度；MinWidth 是拖动时的最小宽度
        WidthProperty.OverrideMetadata(typeof(Drawer), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        MinWidthProperty.OverrideMetadata(typeof(Drawer), new FrameworkPropertyMetadata(48d));
    }

    /// <summary>新建：结束边、xs 宽、能拖动调整宽度。</summary>
    public Drawer()
    {
        handle.SetResourceReference(StyleProperty, DrawerParts.ResizeHandle);
        handle.Override = (() => Panel.ActualWidth, width => SetCurrentValue(WidthProperty, width));
        Layer.Children.Add(handle);
        Panel.RenderTransform = Shadow.RenderTransform = slide;
        ApplySide();
    }

    /// <summary>从哪一侧滑入。</summary>
    public DrawerSide Side { get => (DrawerSide)GetValue(SideProperty); set => SetValue(SideProperty, value); }

    /// <summary>能拖动调整宽度。</summary>
    public bool Resizable { get => (bool)GetValue(ResizableProperty); set => SetValue(ResizableProperty, value); }

    /// <summary>宽度调整手柄（测试用）。</summary>
    internal ResizeHandle Handle => handle;

    bool Resizing => Side.Inline && Resizable;

    double Em => TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == WidthProperty || e.Property == MinWidthProperty) Layer.InvalidateMeasure();
        else if (e.Property == IsOpenProperty && IsOpen) initialWidth ??= double.IsNaN(Width) ? null : Width;
    }

    /// <summary>贴边的那一侧没有描边；手柄贴在靠正文的一边（结束边的抽屉在它的起始边）。</summary>
    void ApplySide()
    {
        Panel.BorderThickness = Side.Value switch
        {
            "start" => new Thickness(0, 1, 1, 1),
            "top" => new Thickness(1, 0, 1, 1),
            "bottom" => new Thickness(1, 1, 1, 0),
            _ => new Thickness(1, 1, 0, 1),
        };
        handle.Visibility = Resizing ? Visibility.Visible : Visibility.Collapsed;
        handle.Edge = Side == DrawerSide.Start ? PaneSide.End : PaneSide.Start;
        AutomationProperties.SetName(handle, Provider.GetLang(this).DrawerResize);
        Layer.InvalidateMeasure();
    }

    /// <inheritdoc />
    private protected override CornerRadius PanelCornerRadius(double em) => default;

    /// <summary>左右两侧：Width 或档位的宽度（不超出窗口）、窗口高；上下两侧：窗口宽、高度按内容（不超出窗口）。</summary>
    private protected override Size PanelConstraint(Size area)
    {
        // 拖窄以后内容保持能读的最小宽度，多出来的被面板裁掉
        if (Panel.Child is FrameworkElement content) content.MinWidth = Resizing ? Math.Min(20 * Em, Math.Max(0, area.Width - 3 * Em)) : 0;
        if (!Side.Inline) return area;
        var width = double.IsNaN(Width) ? this.Resource<double>((Size.ResourceKey ?? DialogSize.Xs.ResourceKey)!) : Width;
        return new Size(Math.Min(width, area.Width), area.Height);
    }

    /// <inheritdoc />
    private protected override Rect PanelBox(Size area, Size constraint, Size desired)
    {
        var height = Math.Min(desired.Height, area.Height);
        return Side.Value switch
        {
            "start" => new Rect(0, 0, constraint.Width, area.Height),
            "top" => new Rect(0, 0, area.Width, height),
            "bottom" => new Rect(0, area.Height - height, area.Width, height),
            _ => new Rect(area.Width - constraint.Width, 0, constraint.Width, area.Height),
        };
    }

    /// <summary>手柄贴着面板靠正文的一边、上下铺满；能拖到的范围是 MinWidth 到窗口宽减 48。</summary>
    private protected override void ArrangeExtras(Rect panelBox)
    {
        if (!Resizing) return;
        handle.MinLength = MinWidth;
        handle.MaxLength = Math.Max(MinWidth, Area.Width - Reserve);
        handle.DefaultLength = initialWidth;
        var thickness = handle.DesiredSize.Width;
        var x = Side == DrawerSide.Start ? panelBox.Right - thickness : panelBox.X;
        handle.Arrange(new Rect(x, panelBox.Y, thickness, panelBox.Height));
    }

    /// <summary>从所在一侧滑入（系统关闭动画时直接到位）：先推到所在一侧之外（一整块面板那么远），再动画回到原位。</summary>
    private protected override void OnPanelOpened()
    {
        var area = Area;
        var width = Side.Inline ? PanelConstraint(area).Width : area.Width;
        var (property, from) = Side.Value switch
        {
            "start" => (TranslateTransform.XProperty, -width),
            "top" => (TranslateTransform.YProperty, -area.Height),
            "bottom" => (TranslateTransform.YProperty, area.Height),
            _ => (TranslateTransform.XProperty, width),
        };
        slide.BeginAnimation(TranslateTransform.XProperty, null);
        slide.BeginAnimation(TranslateTransform.YProperty, null);
        var duration = Motion.Duration(this, ThemeKeys.Duration.Lg);
        if (duration <= TimeSpan.Zero) return;
        slide.BeginAnimation(property, new DoubleAnimation(from, 0, duration) { EasingFunction = this.Resource<IEasingFunction>(ThemeKeys.Easing.Enter), FillBehavior = FillBehavior.Stop });
    }
}

/// <summary>Drawer 的部件样式键（另见 <see cref="DialogParts"/>）：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:DrawerParts.ResizeHandle}" TargetType="rune:ResizeHandle"&gt;</c>。</summary>
public static class DrawerParts
{
    /// <summary>宽度调整手柄（TS 的 propsResizeHandle）。</summary>
    public static readonly PartKey ResizeHandle = new(typeof(Drawer), nameof(ResizeHandle), typeof(Rune.Wpf.ResizeHandle));
}
