using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Rune.Avalonia;

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
/// 部件名在 <see cref="LoadingOverlayParts"/>。
/// </summary>
public class LoadingOverlay : Control
{
    /// <summary>里面的内容。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<LoadingOverlay, object?>(nameof(Content));

    /// <summary>加载中：盖上遮罩。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<LoadingOverlay, bool>(nameof(IsLoading));

    /// <summary>读屏念的说明；不写时取语言包（加载中）。</summary>
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<LoadingOverlay, string?>(nameof(Label));

    /// <summary>遮罩下的内容模糊。</summary>
    public static readonly StyledProperty<bool> BlurProperty = AvaloniaProperty.Register<LoadingOverlay, bool>(nameof(Blur));

    readonly Panel root = new();
    readonly ContentPresenter content = new();
    // 遮罩延迟出现（防闪烁），拦指针不能等：加载一开始先盖一层看不见的
    readonly Border blocker = new() { Background = Brushes.Transparent, IsVisible = false };
    readonly Panel overlay = new() { Name = LoadingOverlayParts.Overlay, IsVisible = false, Opacity = 0 };
    // surface 70% 的底（TS 的 color-mix）：单独一层半透明，指示不跟着变淡
    readonly Border backdrop = new() { Opacity = 0.7 };
    readonly Spinner spinner = new() { Name = LoadingOverlayParts.Spinner, Size = TextSize.Xl, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly DelayedLoading delayed;
    int fade;

    /// <summary>新建：没在加载。</summary>
    public LoadingOverlay()
    {
        delayed = new DelayedLoading(Present);
        backdrop.Bind(Border.BackgroundProperty, backdrop.GetResourceObservable(ThemeKeys.Brush.Surface));
        overlay.Children.AddRange([backdrop, spinner]);
        root.Children.AddRange([content, blocker, overlay]);
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        spinner.Bind(TemplatedControl.ForegroundProperty, spinner.GetResourceObservable(ThemeKeys.Brush.Accent));
    }

    /// <summary>内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>加载中。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>读屏念的说明。</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>内容模糊。</summary>
    public bool Blur { get => GetValue(BlurProperty); set => SetValue(BlurProperty, value); }

    /// <summary>遮罩（测试用）。</summary>
    internal Panel Overlay => overlay;

    /// <summary>加载指示（测试用）。</summary>
    internal Spinner Spinner => spinner;

    /// <summary>拦指针的那一层（测试用）。</summary>
    internal Border Blocker => blocker;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty) content.Content = Content;
        else if (change.Property == IsLoadingProperty)
        {
            AutomationProperties.SetItemStatus(this, IsLoading ? "busy" : null);
            blocker.IsVisible = IsLoading && !overlay.IsVisible;
            delayed.Update(IsLoading);
        }
        else if (change.Property == LabelProperty) spinner.Label = Label ?? Provider.GetLang(this).Loading;
        else if (change.Property == BlurProperty) UpdateBlur();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        spinner.Label = Label ?? Provider.GetLang(this).Loading;
        // 挂上时已经在加载：遮罩立即出现（没有内容可看，空白比指示更糟）
        if (IsLoading && !overlay.IsVisible) Present(true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        delayed.Stop();
    }

    /// <summary>遮罩显示、隐藏：淡入淡出 durations.md（进场 enter 曲线），淡出后从排版里拿掉。</summary>
    void Present(bool shown)
    {
        var version = ++fade;
        var duration = VisualRoot is null ? TimeSpan.Zero : Motion.Duration(this, ThemeKeys.Duration.Md);
        overlay.Transitions = duration > TimeSpan.Zero
            ? [new DoubleTransition { Property = OpacityProperty, Duration = duration, Easing = this.Resource<Easing>(shown ? ThemeKeys.Easing.Enter : ThemeKeys.Easing.Exit) }]
            : null;
        if (shown)
        {
            overlay.IsVisible = true;
            blocker.IsVisible = false;
            overlay.Opacity = 1;
        }
        else
        {
            overlay.Opacity = 0;
            blocker.IsVisible = IsLoading;
            UiDelay.Run(duration, () =>
            {
                if (version == fade) overlay.IsVisible = false;
            });
        }
        UpdateBlur();
    }

    /// <summary>遮罩出现时内容模糊（TS 的 backdrop-filter: blur(2px)，这里模糊内容本身）。</summary>
    void UpdateBlur() => content.Effect = Blur && overlay.IsVisible && overlay.Opacity > 0 ? new BlurEffect { Radius = 2 } : null;
}

/// <summary>LoadingOverlay 的部件名：样式里用 <c>rune|LoadingOverlay Panel#PART_Overlay</c> 这样选中。</summary>
public static class LoadingOverlayParts
{
    /// <summary>遮罩（TS 的 propsOverlay）。</summary>
    public const string Overlay = "PART_Overlay";

    /// <summary>加载指示（TS 的 propsSpinner）。</summary>
    public const string Spinner = "PART_Spinner";
}
