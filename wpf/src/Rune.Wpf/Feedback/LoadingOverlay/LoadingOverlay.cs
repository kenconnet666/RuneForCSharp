using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using LayoutGrid = System.Windows.Controls.Grid;

namespace Rune.Wpf;

/// <summary>
/// 加载遮罩（vue-tsx-pro 的 LoadingOverlay）：盖住一块区域（卡片、表单、图表），内容保留在下面，中间显示加载指示。
/// <code>
/// &lt;rune:LoadingOverlay IsLoading="{Binding Saving}" Label="正在保存" Blur="True"&gt;
///   &lt;rune:Card&gt;…表单…&lt;/rune:Card&gt;
/// &lt;/rune:LoadingOverlay&gt;
/// </code>
/// 读屏的"忙"按 <see cref="IsLoading"/> 立即生效（TS 的 aria-busy，这里是 ItemStatus），遮罩防闪烁（<see cref="DelayedLoading"/>：200 毫秒内完成的不出现，
/// 出现后至少显示 500 毫秒），durations.md 内淡入淡出。遮罩是 surface 70% 的底、居中 xl 的 accent 加载指示（读屏念 <see cref="Label"/>，默认"加载中"），
/// <see cref="Blur"/> 时下面的内容模糊 2 像素。遮罩挡住指针（加载一开始就挡，不等遮罩出现）；键盘仍能操作下面的内容，需要禁止时由内容自己处理（表单的 IsEnabled）。
/// </summary>
[ContentProperty(nameof(Content))]
public class LoadingOverlay : CompositeControl
{
    /// <summary>里面的内容。</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(LoadingOverlay),
        new PropertyMetadata(null, (d, _) => ((LoadingOverlay)d).content.Content = ((LoadingOverlay)d).Content));

    /// <summary>加载中：盖上遮罩。</summary>
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LoadingOverlay),
        new PropertyMetadata(false, (d, _) => ((LoadingOverlay)d).OnLoadingChanged()));

    /// <summary>读屏念的说明；不写时取语言包（加载中）。</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(LoadingOverlay),
        new PropertyMetadata(null, (d, _) => ((LoadingOverlay)d).UpdateLabel()));

    /// <summary>遮罩下的内容模糊。</summary>
    public static readonly DependencyProperty BlurProperty = DependencyProperty.Register(nameof(Blur), typeof(bool), typeof(LoadingOverlay),
        new PropertyMetadata(false, (d, _) => ((LoadingOverlay)d).UpdateBlur()));

    readonly LayoutGrid root = new();
    readonly ContentPresenter content = new();
    // 遮罩延迟出现（防闪烁），拦指针不能等：加载一开始先盖一层看不见的
    readonly Border blocker = new() { Background = Brushes.Transparent, Visibility = Visibility.Collapsed };
    readonly LayoutGrid overlay = new() { Visibility = Visibility.Collapsed, Opacity = 0 };
    // surface 70% 的底（TS 的 color-mix）：单独一层半透明，指示不跟着变淡
    readonly Border backdrop = new() { Opacity = 0.7 };
    readonly Spinner spinner = new() { Size = TextSize.Xl, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly DelayedLoading delayed;
    int fade;

    /// <summary>新建：没在加载。</summary>
    public LoadingOverlay()
    {
        delayed = new DelayedLoading(Present);
        backdrop.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Brush.Surface);
        overlay.Children.Add(backdrop);
        overlay.Children.Add(spinner);
        root.Children.Add(content);
        root.Children.Add(blocker);
        root.Children.Add(overlay);
        Root = root;
        spinner.SetResourceReference(ForegroundProperty, ThemeKeys.Brush.Accent);
        Loaded += (_, _) =>
        {
            UpdateLabel();
            // 挂上时已经在加载：遮罩立即出现（没有内容可看，空白比指示更糟）
            if (IsLoading && overlay.Visibility != Visibility.Visible) Present(true);
        };
        Unloaded += (_, _) => delayed.Stop();
    }

    /// <summary>内容。</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>读屏念的说明。</summary>
    public string? Label { get => (string?)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>内容模糊。</summary>
    public bool Blur { get => (bool)GetValue(BlurProperty); set => SetValue(BlurProperty, value); }

    /// <summary>遮罩（测试用）。</summary>
    internal LayoutGrid Overlay => overlay;

    /// <summary>加载指示（测试用）。</summary>
    internal Spinner Spinner => spinner;

    /// <summary>拦指针的那一层（测试用）。</summary>
    internal Border Blocker => blocker;

    /// <summary>内容（测试用）。</summary>
    internal ContentPresenter ContentPart => content;

    void OnLoadingChanged()
    {
        AutomationProperties.SetItemStatus(this, IsLoading ? "busy" : "");
        blocker.Visibility = IsLoading && overlay.Visibility != Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        delayed.Update(IsLoading);
    }

    void UpdateLabel() => spinner.Label = Label ?? Provider.GetLang(this).Loading;

    /// <summary>遮罩显示、隐藏：淡入淡出 durations.md（进场 enter 曲线、离场 exit 曲线），淡出后从排版里拿掉。</summary>
    void Present(bool shown)
    {
        var version = ++fade;
        var duration = IsLoaded ? Motion.Duration(this, ThemeKeys.Duration.Md) : TimeSpan.Zero;
        var animation = new DoubleAnimation(shown ? 1 : 0, duration)
        {
            EasingFunction = this.Resource<IEasingFunction>(shown ? ThemeKeys.Easing.Enter : ThemeKeys.Easing.Exit),
        };
        if (shown)
        {
            overlay.Visibility = Visibility.Visible;
            blocker.Visibility = Visibility.Collapsed;
        }
        else
        {
            blocker.Visibility = IsLoading ? Visibility.Visible : Visibility.Collapsed;
            animation.Completed += (_, _) =>
            {
                if (version == fade) overlay.Visibility = Visibility.Collapsed;
                UpdateBlur();
            };
        }
        overlay.BeginAnimation(OpacityProperty, animation);
        UpdateBlur();
    }

    /// <summary>遮罩出现时内容模糊（TS 的 backdrop-filter: blur(2px)，这里模糊内容本身）。</summary>
    void UpdateBlur() => content.Effect = Blur && overlay.Visibility == Visibility.Visible ? new BlurEffect { Radius = 2 } : null;
}
