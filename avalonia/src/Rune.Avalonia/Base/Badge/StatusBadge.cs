using Avalonia;

namespace Rune.Avalonia;

/// <summary>
/// 状态标记（vue-tsx-pro 的 StatusBadge）：名字、色调、图标取自状态映射（<see cref="StatusMap"/>），没有图标的状态前画圆点。
/// <code>
/// &lt;rune:StatusBadge Statuses="{x:Static local:Statuses.Task}" Value="{Binding Status}" /&gt;
/// </code>
/// 不认识的状态显示成中性色、名字就是 key。其余同 Badge（样式、字号、圆角）；色调、图标、内容由状态决定，不另写。
/// </summary>
public class StatusBadge : Badge
{
    /// <summary>状态映射。</summary>
    public static readonly StyledProperty<StatusMap?> StatusesProperty = AvaloniaProperty.Register<StatusBadge, StatusMap?>(nameof(Statuses));

    /// <summary>当前状态的 key。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<StatusBadge, string?>(nameof(Value));

    readonly TemplateResources resources;

    /// <summary>新建：没有图标的状态前画圆点（模板优先级，使用方写的 Dot 盖得过；改默认值不会触发变化，Badge 收不到）。</summary>
    public StatusBadge()
    {
        resources = new TemplateResources(this);
        resources.Set(DotProperty, true);
    }

    /// <summary>状态映射。</summary>
    public StatusMap? Statuses { get => GetValue(StatusesProperty); set => SetValue(StatusesProperty, value); }

    /// <summary>当前状态的 key。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>外观同 Badge（用 Badge 的主题）。</summary>
    protected override Type StyleKeyOverride => typeof(Badge);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != StatusesProperty && change.Property != ValueProperty) return;
        // 模板优先级：盖过主题给的默认色调
        var status = (Statuses ?? []).Get(Value ?? "");
        resources.Set(ToneProperty, status.Tone);
        resources.Set(IconProperty, status.Icon);
        resources.Set(ContentProperty, (object?)status.Label);
    }
}
