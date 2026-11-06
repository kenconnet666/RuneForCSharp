using System.Windows;

namespace Rune.Wpf;

/// <summary>
/// 状态标记（vue-tsx-pro 的 StatusBadge）：名字、色调、图标取自状态映射（<see cref="StatusMap"/>），没有图标的状态前画圆点。
/// <code>
/// &lt;rune:StatusBadge Statuses="{x:Static local:Statuses.Task}" Value="{Binding Status}" /&gt;
/// </code>
/// 不认识的状态显示成中性色、名字就是 key。其余同 Badge（样式、字号、圆角，用 Badge 的主题样式）；色调、图标、内容由状态决定，不另写。
/// </summary>
public class StatusBadge : Badge
{
    /// <summary>状态映射。</summary>
    public static readonly DependencyProperty StatusesProperty = DependencyProperty.Register(
        nameof(Statuses), typeof(StatusMap), typeof(StatusBadge), new FrameworkPropertyMetadata(null, (d, _) => ((StatusBadge)d).Update()));

    /// <summary>当前状态的 key。</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(StatusBadge), new FrameworkPropertyMetadata(null, (d, _) => ((StatusBadge)d).Update()));

    /// <summary>新建：没有图标的状态前画圆点（当前值，使用方写的 Dot 盖得过；改默认值不会触发变化回调，Badge 收不到）。</summary>
    public StatusBadge()
    {
        SetCurrentValue(DotProperty, true);
        Update();
    }

    /// <summary>状态映射。</summary>
    public StatusMap? Statuses { get => (StatusMap?)GetValue(StatusesProperty); set => SetValue(StatusesProperty, value); }

    /// <summary>当前状态的 key。</summary>
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    void Update()
    {
        var status = (Statuses ?? []).Get(Value ?? "");
        Tone = status.Tone;
        Icon = status.Icon;
        Content = status.Label;
    }
}
