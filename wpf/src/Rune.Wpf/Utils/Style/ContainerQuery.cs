using System.Windows;

namespace Rune.Wpf;

/// <summary>容器的宽度档位：断点同 vue-tsx-pro 的 breakpoints（40 / 48 / 64 / 80rem，按 16px 一个 rem）。</summary>
public enum ContainerSize
{
    /// <summary>窄于 640。</summary>
    Xs,

    /// <summary>640 起。</summary>
    Sm,

    /// <summary>768 起。</summary>
    Md,

    /// <summary>1024 起。</summary>
    Lg,

    /// <summary>1280 起。</summary>
    Xl,
}

/// <summary>
/// 容器查询（WPF 没有，Avalonia 有原生的 ContainerQuery）：设了 <c>Watch</c> 的元素按自己的宽度写 <c>Size</c>，
/// Size 会继承给后代，样式触发器直接用它：
/// <code>
/// &lt;Grid rune:ContainerQuery.Watch="True"&gt; … &lt;/Grid&gt;
/// &lt;Trigger Property="rune:ContainerQuery.Size" Value="Xs"&gt; … &lt;/Trigger&gt;
/// </code>
/// 用会继承的属性，不用向上找祖先的绑定：断点变化时只通知订阅了它的触发器，没有每个元素一个绑定的开销。
/// </summary>
public static class ContainerQuery
{
    /// <summary>最近一个被观察的容器的宽度档位（会继承）；没有被观察的容器时为 Lg。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.RegisterAttached(
        "Size", typeof(ContainerSize), typeof(ContainerQuery),
        new FrameworkPropertyMetadata(ContainerSize.Lg, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>按这个元素的宽度更新它的 Size。</summary>
    public static readonly DependencyProperty WatchProperty = DependencyProperty.RegisterAttached(
        "Watch", typeof(bool), typeof(ContainerQuery), new PropertyMetadata(false, OnWatchChanged));

    /// <summary>读宽度档位。</summary>
    public static ContainerSize GetSize(DependencyObject d) => (ContainerSize)d.GetValue(SizeProperty);

    /// <summary>设宽度档位（通常由 Watch 自动写）。</summary>
    public static void SetSize(DependencyObject d, ContainerSize value) => d.SetValue(SizeProperty, value);

    /// <summary>读是否观察宽度。</summary>
    public static bool GetWatch(DependencyObject d) => (bool)d.GetValue(WatchProperty);

    /// <summary>设是否观察宽度。</summary>
    public static void SetWatch(DependencyObject d, bool value) => d.SetValue(WatchProperty, value);

    /// <summary>宽度对应的档位。</summary>
    /// <param name="width">设备无关像素。</param>
    public static ContainerSize SizeFor(double width) => width switch
    {
        < 640 => ContainerSize.Xs,
        < 768 => ContainerSize.Sm,
        < 1024 => ContainerSize.Md,
        < 1280 => ContainerSize.Lg,
        _ => ContainerSize.Xl,
    };

    static void OnWatchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        if ((bool)e.NewValue) element.SizeChanged += OnSizeChanged;
        else element.SizeChanged -= OnSizeChanged;
    }

    static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var size = SizeFor(e.NewSize.Width);
        // 档位没变就不写：写入会通知所有后代的触发器
        if (GetSize(element) != size) SetSize(element, size);
    }
}
