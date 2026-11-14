using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;

namespace Rune.Avalonia;

/// <summary>
/// 抽屉（vue-tsx-pro 的 Drawer）：贴着窗口一侧滑入的模态面板。内核同 <see cref="Dialog"/>（遮罩、焦点、Esc、点遮罩、BeforeClose、确认、命令式 ShowAsync）。
/// <code>
/// &lt;rune:Drawer IsOpen="{Binding Filtering}" Side="end" Title="筛选"&gt;…&lt;/rune:Drawer&gt;
/// &lt;rune:Drawer IsOpen="{Binding Editing}" Size="md" Width="{Binding PanelWidth, Mode=TwoWay}" Title="订单详情"&gt;…&lt;/rune:Drawer&gt;
/// </code>
/// <see cref="Side"/> 是从哪一侧滑入（默认结束边，从右到左排版时左右互换）：左右两侧占满窗口高度，宽度按档位 <see cref="Dialog.Size"/>（默认 xs，不超出窗口），
/// 写了 Width 时按它（双向绑定记住拖动后的宽度）；上下两侧占满窗口宽度、高度按内容（不超出窗口）。贴边的那一侧没有描边，也没有圆角。
/// 打开时从所在一侧滑入（durations.lg、enter；系统关闭动画时不播）。左右两侧默认 <see cref="Resizable"/>：靠正文的一边是宽度调整手柄
/// （<see cref="ResizeHandle"/>，拖动、聚焦后 ← / →、双击回到打开时的宽度），能一直拖到只剩 MinWidth（默认 48）那么一窄条、让出后面的窗口；
/// 拖窄以后标题行、说明、正文、页脚保持能读的最小宽度（20em，不超出窗口减 3em），多出来的被面板裁掉。部件名同 <see cref="DialogParts"/>，
/// 手柄是 <see cref="DrawerParts.ResizeHandle"/>。
/// </summary>
public class Drawer : Dialog
{
    /// <summary>从哪一侧滑入，默认结束边。</summary>
    public static readonly StyledProperty<DrawerSide> SideProperty = AvaloniaProperty.Register<Drawer, DrawerSide>(nameof(Side), DrawerSide.End);

    /// <summary>左右两侧时靠正文的一边能拖动调整宽度，默认 true。</summary>
    public static readonly StyledProperty<bool> ResizableProperty = AvaloniaProperty.Register<Drawer, bool>(nameof(Resizable), true);

    /// <summary>滑入的进度：0 在所在一侧之外，1 到位（动画改它，面板的位移跟着）。</summary>
    static readonly AttachedProperty<double> SlideProperty = AvaloniaProperty.RegisterAttached<Drawer, Border, double>("Slide", 1);

    /// <summary>给拖动留出的窗口宽度：手柄最多拖到窗口宽减它（同上游的 innerWidth - 48）。</summary>
    const double Reserve = 48;

    readonly ResizeHandle handle = new() { Name = DrawerParts.ResizeHandle };
    readonly TranslateTransform slide = new();
    double? initialWidth;

    static Drawer()
    {
        // Width 是面板的宽度（抽屉自己不参与排版）：默认双向绑定，记住拖动后的宽度；MinWidth 是拖动时的最小宽度
        WidthProperty.OverrideMetadata<Drawer>(new StyledPropertyMetadata<double>(double.NaN, BindingMode.TwoWay));
        MinWidthProperty.OverrideDefaultValue<Drawer>(48);
    }

    /// <summary>新建：结束边、xs 宽、能拖动调整宽度。</summary>
    public Drawer()
    {
        handle.Override = (() => Panel.Bounds.Width, width => SetCurrentValue(WidthProperty, width));
        Layer.Children.Add(handle);
        Panel.RenderTransform = slide;
        Panel.PropertyChanged += (_, e) =>
        {
            if (e.Property == SlideProperty) UpdateSlide();
        };
        ApplySide();
    }

    /// <summary>从哪一侧滑入。</summary>
    public DrawerSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }

    /// <summary>能拖动调整宽度。</summary>
    public bool Resizable { get => GetValue(ResizableProperty); set => SetValue(ResizableProperty, value); }

    /// <summary>宽度调整手柄（测试用）。</summary>
    internal ResizeHandle Handle => handle;

    bool Resizing => Side.Inline && Resizable;

    double Em => global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SideProperty || change.Property == ResizableProperty) ApplySide();
        else if (change.Property == WidthProperty || change.Property == MinWidthProperty) Layer.InvalidateMeasure();
        else if (change.Property == IsOpenProperty && IsOpen) initialWidth ??= double.IsNaN(Width) ? null : Width;
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
        handle.IsVisible = Resizing;
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
        if (Panel.Child is Control content) content.MinWidth = Resizing ? Math.Min(20 * Em, Math.Max(0, area.Width - 3 * Em)) : 0;
        if (!Side.Inline) return area;
        var width = double.IsNaN(Width) ? this.Resource<double>((Size.ResourceKey ?? DialogSize.Xs.ResourceKey)!) : Width;
        return new Size(Math.Min(width, area.Width), area.Height);
    }

    /// <inheritdoc />
    private protected override Rect PanelBox(Size area, Size constraint, Size desired) => Side.Value switch
    {
        "start" => new Rect(0, 0, constraint.Width, area.Height),
        "top" => new Rect(0, 0, area.Width, Math.Min(desired.Height, area.Height)),
        "bottom" => new Rect(0, area.Height - Math.Min(desired.Height, area.Height), area.Width, Math.Min(desired.Height, area.Height)),
        _ => new Rect(area.Width - constraint.Width, 0, constraint.Width, area.Height),
    };

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

    /// <summary>从所在一侧滑入（系统关闭动画时直接到位）。</summary>
    private protected override void OnPanelOpened()
    {
        var duration = Motion.Duration(this, ThemeKeys.Duration.Lg);
        if (duration <= TimeSpan.Zero)
        {
            Panel.SetValue(SlideProperty, 1d);
            return;
        }
        _ = new Animation
        {
            Duration = duration,
            Easing = this.Resource<Easing>(ThemeKeys.Easing.Enter),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(SlideProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(SlideProperty, 1d) } },
            },
        }.RunAsync(Panel);
    }

    /// <summary>进度 → 位移：没到位时往所在一侧之外推（推出去一整块面板那么远）。</summary>
    void UpdateSlide()
    {
        var rest = 1 - Panel.GetValue(SlideProperty);
        var area = Area;
        var width = Side.Inline ? PanelConstraint(area).Width : area.Width;
        (slide.X, slide.Y) = Side.Value switch
        {
            "start" => (-rest * width, 0d),
            "top" => (0d, -rest * area.Height),
            "bottom" => (0d, rest * area.Height),
            _ => (rest * width, 0d),
        };
    }
}

/// <summary>Drawer 的部件名（另见 <see cref="DialogParts"/>）：<c>rune|Drawer rune|ResizeHandle#PART_ResizeHandle</c>。</summary>
public static class DrawerParts
{
    /// <summary>宽度调整手柄（TS 的 propsResizeHandle）。</summary>
    public const string ResizeHandle = "PART_ResizeHandle";
}
