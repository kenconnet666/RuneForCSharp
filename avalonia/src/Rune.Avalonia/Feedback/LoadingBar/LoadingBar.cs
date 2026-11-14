using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace Rune.Avalonia;

/// <summary>
/// 窗口顶部的加载条（vue-tsx-pro 的 LoadingBar）：换页、整页刷新数据时最上方一条细线从起始边往结束边走，显示 <see cref="LoadingBarController"/> 的进度。
/// <code>
/// &lt;Panel&gt;
///   &lt;ContentControl Content="{Binding Page}" /&gt;
///   &lt;rune:LoadingBar Controller="{x:Static local:App.Loading}" /&gt;    贴在顶边、盖在内容上面
/// &lt;/Panel&gt;
/// </code>
/// 高 borderWidths.md、accent 色带一圈同色的光，出错是 danger；进度、显隐在 durations.md 内过渡（系统关闭动画时直接到位）。不挡点击。
/// 只是视觉提示：读屏里是进度条（名称"加载中"），空闲时读屏里没有它；真正的"加载中"应由内容区域告诉读屏。
/// 平台没有网页的 position: fixed：放在窗口根部的 Panel 里、排在内容后面（默认贴着顶边）。
/// </summary>
public class LoadingBar : Control
{
    /// <summary>控制器。</summary>
    public static readonly StyledProperty<LoadingBarController?> ControllerProperty = AvaloniaProperty.Register<LoadingBar, LoadingBarController?>(nameof(Controller));

    /// <summary>画出来的进度（0–1）：跟着控制器过渡。</summary>
    static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<LoadingBar, double>("Fraction");

    CancellationTokenSource? animation;

    static LoadingBar()
    {
        AffectsRender<LoadingBar>(FractionProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LoadingBar>(false);
        VerticalAlignmentProperty.OverrideDefaultValue<LoadingBar>(VerticalAlignment.Top);
        OpacityProperty.OverrideDefaultValue<LoadingBar>(0);
    }

    /// <summary>新建：颜色跟着主题重画。</summary>
    public LoadingBar() => this.RedrawOnResourceChanges(this);

    /// <summary>控制器。</summary>
    public LoadingBarController? Controller { get => GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ControllerProperty) return;
        if (change.OldValue is LoadingBarController old) old.PropertyChanged -= OnControllerChanged;
        if (change.NewValue is LoadingBarController next) next.PropertyChanged += OnControllerChanged;
        Update(animate: false);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Update(animate: false);
    }

    void OnControllerChanged(object? sender, PropertyChangedEventArgs e) => Update(animate: true);

    /// <summary>进度、显隐跟着控制器：durations.md 内过渡。</summary>
    void Update(bool animate)
    {
        var controller = Controller;
        var visible = controller is { IsVisible: true };
        var to = (controller?.Progress ?? 0) / 100;
        var duration = animate && VisualRoot is not null ? Motion.Duration(this, ThemeKeys.Duration.Md) : TimeSpan.Zero;
        var easing = this.Resource<Easing>(ThemeKeys.Easing.Standard);
        Transitions = duration > TimeSpan.Zero ? [new DoubleTransition { Property = OpacityProperty, Duration = duration, Easing = easing }] : null;
        Opacity = visible ? 1 : 0;
        AutomationProperties.SetAccessibilityView(this, visible ? AccessibilityView.Control : AccessibilityView.Raw);
        AutomationProperties.SetName(this, Provider.GetLang(this).Loading);
        InvalidateVisual();

        var from = GetValue(FractionProperty);
        animation?.Cancel();
        animation = null;
        SetValue(FractionProperty, to);
        // 归零是在淡出之后：不倒着走回去
        if (duration <= TimeSpan.Zero || to <= from) return;
        animation = new CancellationTokenSource();
        _ = new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(FractionProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(FractionProperty, to) } },
            },
        }.RunAsync(this, animation.Token);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(0, this.Resource<double>(ThemeKeys.BorderWidth.Md));

    /// <summary>从起始边画到进度处（从右到左排版时从右边起），accent 色加一圈 8 像素的同色光；出错是 danger、没有光。</summary>
    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width * GetValue(FractionProperty);
        if (width <= 0) return;
        var failed = Controller is { IsFailed: true };
        var color = this.Resource<Color>(failed ? ThemeKeys.Color.Danger : ThemeKeys.Color.Accent);
        var left = FlowDirection == FlowDirection.RightToLeft ? Bounds.Width - width : 0;
        var glow = failed ? default : new BoxShadows(new BoxShadow { Blur = 8, Color = color });
        context.DrawRectangle(new ImmutableSolidColorBrush(color), null, new RoundedRect(new Rect(left, 0, width, Bounds.Height)), glow);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LoadingBarPeer(this);

    /// <summary>读屏：进度条，0–100，值是控制器的进度。</summary>
    sealed class LoadingBarPeer(LoadingBar owner) : ControlAutomationPeer(owner), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string GetClassNameCore() => nameof(LoadingBar);

        public bool IsReadOnly => true;

        public double Minimum => 0;

        public double Maximum => 100;

        public double Value => Math.Round(owner.Controller?.Progress ?? 0);

        public double SmallChange => 0;

        public double LargeChange => 0;

        public void SetValue(double value) => throw new InvalidOperationException("加载条的进度只读");
    }
}
