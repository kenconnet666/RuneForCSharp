using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Metadata;
using Avalonia.Styling;
using LayoutSize = Avalonia.Size;

namespace Rune.Avalonia;

/// <summary>
/// 展开收起的内容区（vue-tsx-pro 的 Collapse），带高度过渡。触发器另外写：
/// <code>
/// &lt;rune:Collapse IsOpen="{Binding ShowAdvanced}"&gt;&lt;rune:Grid MinItemWidth="9em"&gt;…&lt;/rune:Grid&gt;&lt;/rune:Collapse&gt;
/// </code>
/// 高度在 0 与内容高度之间过渡（durations.md、easings.standard，系统关闭动画时直接到位）；内容一直按完整高度排版，露不出来的部分裁掉。
/// 完全收起（过渡结束）后内容隐藏：不能 Tab 聚焦，读屏也读不到；收起的过程中仍可见。展开时有伪类 :open（TS 的 data-state）。
/// <see cref="Lazy"/> 时第一次展开才放进内容，之后收起也保留。内容层是部件 <see cref="CollapseParts.Content"/>。
/// 页内搜索命中收起的内容时自动展开（hidden="until-found"）是浏览器的能力，桌面上没有。
/// </summary>
public class Collapse : Control
{
    /// <summary>是否展开（默认双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Collapse, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>第一次展开时才放进内容。</summary>
    public static readonly StyledProperty<bool> LazyProperty = AvaloniaProperty.Register<Collapse, bool>(nameof(Lazy));

    /// <summary>内容。</summary>
    public static readonly StyledProperty<Control?> ContentProperty = AvaloniaProperty.Register<Collapse, Control?>(nameof(Content));

    /// <summary>露出来的比例：0 收起，1 展开（过渡驱动）。</summary>
    static readonly StyledProperty<double> OpennessProperty = AvaloniaProperty.Register<Collapse, double>("Openness");

    readonly Border holder = new() { Name = CollapseParts.Content, IsVisible = false };
    CancellationTokenSource? transition;
    bool opened;

    static Collapse()
    {
        AffectsMeasure<Collapse>(OpennessProperty);
        ClipToBoundsProperty.OverrideDefaultValue<Collapse>(true);
    }

    /// <summary>新建：收起。</summary>
    public Collapse()
    {
        LogicalChildren.Add(holder);
        VisualChildren.Add(holder);
    }

    /// <summary>是否展开。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>第一次展开时才放进内容（内容很重、多数人不会展开时用）。</summary>
    public bool Lazy { get => GetValue(LazyProperty); set => SetValue(LazyProperty, value); }

    /// <summary>内容。</summary>
    [Content]
    public Control? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == ContentProperty || change.Property == LazyProperty) UpdateContent();
    }

    void UpdateContent() => holder.Child = opened || !Lazy ? Content : null;

    /// <summary>展开时先显示内容再长高；收起时缩到 0、过渡结束才隐藏。没挂上可视树时直接到位。</summary>
    void Toggle()
    {
        var open = IsOpen;
        PseudoClasses.Set(":open", open);
        if (open)
        {
            opened = true;
            UpdateContent();
            holder.IsVisible = true;
        }
        var from = GetValue(OpennessProperty);
        transition?.Cancel();
        transition = null;
        // 先写终值：过渡在动画优先级上盖住它，结束或被打断后留下的就是终值
        SetValue(OpennessProperty, open ? 1d : 0d);
        var duration = VisualRoot is null ? TimeSpan.Zero
            : Motion.Duration(this.TryFindResource("Rune.Duration.Md", ActualThemeVariant, out var value) && value is TimeSpan md ? md : TimeSpan.FromMilliseconds(200));
        if (duration <= TimeSpan.Zero)
        {
            Settle();
            return;
        }
        transition = new CancellationTokenSource();
        _ = Run(from, open ? 1d : 0d, duration, transition.Token);
    }

    async Task Run(double from, double to, TimeSpan duration, CancellationToken token)
    {
        await new Animation
        {
            Duration = duration,
            Easing = this.TryFindResource("Rune.Easing.Standard", ActualThemeVariant, out var found) && found is Easing standard ? standard : new CubicEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpennessProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpennessProperty, to) } },
            },
        }.RunAsync(this, token);
        if (!token.IsCancellationRequested) Settle();
    }

    /// <summary>过渡结束：完全收起时隐藏内容（不能聚焦、读屏读不到）。</summary>
    void Settle()
    {
        if (!IsOpen) holder.IsVisible = false;
    }

    /// <inheritdoc />
    protected override LayoutSize MeasureOverride(LayoutSize availableSize)
    {
        holder.Measure(availableSize.WithHeight(double.PositiveInfinity));
        return holder.DesiredSize.WithHeight(holder.DesiredSize.Height * GetValue(OpennessProperty));
    }

    /// <inheritdoc />
    protected override LayoutSize ArrangeOverride(LayoutSize finalSize)
    {
        holder.Arrange(new Rect(0, 0, finalSize.Width, holder.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>Collapse 的部件名。</summary>
public static class CollapseParts
{
    /// <summary>内容层（TS 的 propsContent）：按完整高度排版，收起后隐藏。</summary>
    public const string Content = "PART_Content";
}
