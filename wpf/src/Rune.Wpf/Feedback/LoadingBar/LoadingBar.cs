using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Rune.Wpf;

/// <summary>
/// 窗口顶部的加载条（vue-tsx-pro 的 LoadingBar）：换页、整页刷新数据时最上方一条细线从起始边往结束边走，显示 <see cref="LoadingBarController"/> 的进度。
/// <code>
/// &lt;Grid&gt;
///   &lt;ContentControl Content="{Binding Page}" /&gt;
///   &lt;rune:LoadingBar Controller="{x:Static local:App.Loading}" /&gt;    贴在顶边、盖在内容上面
/// &lt;/Grid&gt;
/// </code>
/// 高 borderWidths.md、accent 色带一圈同色的光，出错是 danger；进度、显隐在 durations.md 内过渡（系统关闭动画时直接到位）。不挡点击。
/// 只是视觉提示：读屏里是进度条（名称"加载中"），空闲时读屏里没有它；真正的"加载中"应由内容区域告诉读屏。
/// 平台没有网页的 position: fixed：放在窗口根部的 Grid 里、排在内容后面（默认贴着顶边）。
/// </summary>
public class LoadingBar : FrameworkElement
{
    /// <summary>控制器。</summary>
    public static readonly DependencyProperty ControllerProperty = DependencyProperty.Register(
        nameof(Controller), typeof(LoadingBarController), typeof(LoadingBar), new PropertyMetadata(null, (d, e) => ((LoadingBar)d).OnControllerChanged(e)));

    /// <summary>画出来的进度（0–1）：跟着控制器过渡。</summary>
    static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        "Fraction", typeof(double), typeof(LoadingBar), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    static LoadingBar()
    {
        IsHitTestVisibleProperty.OverrideMetadata(typeof(LoadingBar), new UIPropertyMetadata(false));
        VerticalAlignmentProperty.OverrideMetadata(typeof(LoadingBar), new FrameworkPropertyMetadata(VerticalAlignment.Top));
        OpacityProperty.OverrideMetadata(typeof(LoadingBar), new UIPropertyMetadata(0d));
    }

    /// <summary>新建。</summary>
    public LoadingBar() => Loaded += (_, _) => Update(animate: false);

    /// <summary>控制器。</summary>
    public LoadingBarController? Controller { get => (LoadingBarController?)GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }

    void OnControllerChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoadingBarController old) old.PropertyChanged -= OnProgressChanged;
        if (e.NewValue is LoadingBarController next) next.PropertyChanged += OnProgressChanged;
        Update(animate: false);
    }

    void OnProgressChanged(object? sender, PropertyChangedEventArgs e) => Update(animate: true);

    /// <summary>进度、显隐跟着控制器：durations.md 内过渡。</summary>
    void Update(bool animate)
    {
        var controller = Controller;
        var duration = animate && IsLoaded ? Motion.Duration(this, ThemeKeys.Duration.Md) : TimeSpan.Zero;
        var easing = this.Resource<IEasingFunction>(ThemeKeys.Easing.Standard);
        var opacity = controller is { IsVisible: true } ? 1d : 0d;
        BeginAnimation(OpacityProperty, duration > TimeSpan.Zero ? new DoubleAnimation(opacity, duration) { EasingFunction = easing } : null);
        Opacity = opacity;
        var failed = controller is { IsFailed: true };
        Effect = failed ? null : new DropShadowEffect { ShadowDepth = 0, BlurRadius = 8, Color = this.Resource<Color>(ThemeKeys.Color.Accent) };
        InvalidateVisual();

        var from = (double)GetValue(FractionProperty);
        var to = (controller?.Progress ?? 0) / 100;
        SetValue(FractionProperty, to);
        // 归零是在淡出之后：不倒着走回去
        BeginAnimation(FractionProperty, duration > TimeSpan.Zero && to > from
            ? new DoubleAnimation(from, to, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop }
            : null);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(0, this.Resource<double>(ThemeKeys.BorderWidth.Md));

    /// <summary>从起始边画到进度处（从右到左排版时 WPF 自己镜像），accent 色；出错是 danger、没有光。</summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth * (double)GetValue(FractionProperty);
        if (width <= 0) return;
        var color = this.Resource<Color>(Controller is { IsFailed: true } ? ThemeKeys.Color.Danger : ThemeKeys.Color.Accent);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        drawingContext.DrawRectangle(brush, null, new Rect(0, 0, width, ActualHeight));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LoadingBarPeer(this);

    /// <summary>读屏：进度条，0–100，值是控制器的进度，名称"加载中"；空闲时不算控件（读屏里没有它）。</summary>
    sealed class LoadingBarPeer(LoadingBar owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        bool Shown => owner.Controller is { IsVisible: true };

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string GetClassNameCore() => nameof(LoadingBar);

        protected override string GetNameCore() => Provider.GetLang(owner).Loading;

        protected override bool IsControlElementCore() => Shown;

        protected override bool IsContentElementCore() => Shown;

        public override object GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

        public bool IsReadOnly => true;

        public double Minimum => 0;

        public double Maximum => 100;

        public double Value => Math.Round(owner.Controller?.Progress ?? 0);

        public double SmallChange => 0;

        public double LargeChange => 0;

        public void SetValue(double value) => throw new InvalidOperationException("加载条的进度只读");
    }
}
