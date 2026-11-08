using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace Rune.Avalonia;

/// <summary>多选下拉里一个已选的标签：值（移除时交给 <see cref="SelectedTags.RemoveRequested"/>）与显示的文字。</summary>
/// <param name="Value">值。</param>
/// <param name="Label">显示的文字。</param>
public sealed record SelectedTag(string Value, string Label);

/// <summary>移除一个已选标签的事件参数。</summary>
/// <param name="value">被移除的值。</param>
public sealed class SelectedTagEventArgs(string value) : EventArgs
{
    /// <summary>被移除的值。</summary>
    public string Value { get; } = value;
}

/// <summary>
/// 多选下拉触发框里那一排已选标签（vue-tsx-pro 的 SelectedTags）：Select 用，以后的 TreeSelect 共用。
/// <code>
/// var tags = new SelectedTags { Tags = selected, MaxTagCount = TagLimit.Responsive };
/// tags.RemoveRequested += (_, e) => Remove(e.Value);
/// </code>
/// 标签是 sm、soft 的 <see cref="Tag"/>，间隔 space.xs、上下各留 space.xs；订阅了 <see cref="RemoveRequested"/> 才有移除按钮（只读、不可用时不订阅）。
/// 这排标签只给指针用：读屏看不到（下拉自己念全部已选），移除按钮不能聚焦、按下不抢焦点。<see cref="MaxTagCount"/> 是数字时只显示前几个，
/// <see cref="TagLimit.Responsive"/> 时排成一行按宽度放（至少一个），其余收进"+N"。"+N"是部件 <see cref="SelectedTagsParts.More"/>（TS 的 propsMore），
/// 默认的 sm、soft 在主题里，样式能改：<c>rune|SelectedTags rune|Tag#PART_More</c>。
/// </summary>
public class SelectedTags : Panel
{
    /// <summary>要显示的标签。</summary>
    public static readonly StyledProperty<IReadOnlyList<SelectedTag>?> TagsProperty = AvaloniaProperty.Register<SelectedTags, IReadOnlyList<SelectedTag>?>(nameof(Tags));

    /// <summary>最多显示几个；null 时全部显示（会换行）。</summary>
    public static readonly StyledProperty<TagLimit?> MaxTagCountProperty = AvaloniaProperty.Register<SelectedTags, TagLimit?>(nameof(MaxTagCount));

    readonly Tag more = new() { Name = SelectedTagsParts.More };
    EventHandler<SelectedTagEventArgs>? removeRequested;

    /// <summary>新建：没有标签。</summary>
    public SelectedTags()
    {
        ClipToBounds = true;
        Rebuild();
    }

    /// <summary>要显示的标签。</summary>
    public IReadOnlyList<SelectedTag>? Tags { get => GetValue(TagsProperty); set => SetValue(TagsProperty, value); }

    /// <summary>最多显示几个。</summary>
    public TagLimit? MaxTagCount { get => GetValue(MaxTagCountProperty); set => SetValue(MaxTagCountProperty, value); }

    /// <summary>点了标签的移除按钮（标签离场之后）；订阅了才有移除按钮。</summary>
    public event EventHandler<SelectedTagEventArgs>? RemoveRequested
    {
        add
        {
            removeRequested += value;
            Rebuild();
        }
        remove
        {
            removeRequested -= value;
            Rebuild();
        }
    }

    /// <summary>收进"+N"的个数（测试用）。</summary>
    internal int HiddenCount { get; private set; }

    /// <summary>显示出来的标签（测试用，不含"+N"）。</summary>
    internal IEnumerable<Tag> ShownTags => Children.OfType<Tag>().Where(tag => tag != more).Take(Children.Count - 1 - (Responsive ? HiddenCount : 0));

    double Em => TextElement.GetFontSize(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TagsProperty || change.Property == MaxTagCountProperty) Rebuild();
    }

    /// <summary>每个已选项一个标签（数字上限时只建前几个），最后是"+N"。</summary>
    void Rebuild()
    {
        Children.Clear();
        var tags = Tags ?? [];
        var shown = MaxTagCount?.Count is { } count ? tags.Take(count) : tags;
        foreach (var item in shown)
        {
            var tag = new Tag { Size = TextSize.Sm, Variant = StatusVariant.Soft, Content = item.Label, RemoveFocusable = false };
            if (removeRequested is not null) tag.RemoveRequested += (_, _) => removeRequested?.Invoke(this, new SelectedTagEventArgs(item.Value));
            Children.Add(tag);
        }
        Children.Add(more);
        InvalidateMeasure();
    }

    bool Responsive => MaxTagCount is { Count: null };

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var em = Em;
        var gap = 0.25 * em;
        var tags = Children.Where(child => child != more).ToList();
        foreach (var tag in tags) tag.Measure(Size.Infinity);
        // 数字上限时没建出来的那些；按宽度时先当全部放得下，下面再算
        HiddenCount = (Tags?.Count ?? 0) - tags.Count;
        if (Responsive)
        {
            var width = availableSize.Width;
            var used = 0.0;
            var fit = 0;
            for (; fit < tags.Count; fit++)
            {
                var next = used + (fit > 0 ? gap : 0) + tags[fit].DesiredSize.Width;
                // 后面还有放不下的：给"+N"留位置
                var reserve = fit < tags.Count - 1 ? gap + MoreWidth(tags.Count - fit - 1) : 0;
                if (fit > 0 && next + reserve > width) break;
                used = next;
            }
            HiddenCount = tags.Count - fit;
        }
        more.Content = Provider.GetLang(this).SelectMoreTags(HiddenCount);
        more.IsVisible = HiddenCount > 0;
        more.Measure(Size.Infinity);
        var lines = Lines(availableSize.Width);
        var height = lines.Sum(line => line.Height) + Math.Max(0, lines.Count - 1) * gap + 2 * gap;
        var widest = lines.Count == 0 ? 0 : lines.Max(line => line.Width);
        return new Size(Responsive ? Math.Min(widest, availableSize.Width) : widest, height);
    }

    double MoreWidth(int hidden)
    {
        more.Content = Provider.GetLang(this).SelectMoreTags(hidden);
        more.Measure(Size.Infinity);
        return more.DesiredSize.Width;
    }

    /// <summary>显示的标签加"+N"排成行：按宽度时一行，否则放不下就换行。</summary>
    List<(List<Control> Items, double Width, double Height)> Lines(double width)
    {
        var gap = 0.25 * Em;
        var visible = Children.Where(child => child != more).Take(Children.Count - 1 - (Responsive ? HiddenCount : 0)).ToList();
        if (more.IsVisible) visible.Add(more);
        var lines = new List<(List<Control> Items, double Width, double Height)>();
        foreach (var child in visible)
        {
            var size = child.DesiredSize;
            if (lines.Count > 0 && (Responsive || lines[^1].Width + gap + size.Width <= width))
            {
                var line = lines[^1];
                line.Items.Add(child);
                lines[^1] = (line.Items, line.Width + gap + size.Width, Math.Max(line.Height, size.Height));
            }
            else lines.Add(([child], size.Width, size.Height));
        }
        return lines;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var gap = 0.25 * Em;
        var y = gap;
        var placed = new HashSet<Control>();
        foreach (var (items, _, height) in Lines(finalSize.Width))
        {
            var x = 0.0;
            foreach (var child in items)
            {
                child.Arrange(new Rect(x, y + (height - child.DesiredSize.Height) / 2, child.DesiredSize.Width, child.DesiredSize.Height));
                x += child.DesiredSize.Width + gap;
                placed.Add(child);
            }
            y += height + gap;
        }
        // 按宽度收起的：挪到看不见的地方（裁掉），不改 IsVisible（测量里改会反复失效）
        foreach (var child in Children.Where(child => !placed.Contains(child))) child.Arrange(new Rect(-10000, 0, child.DesiredSize.Width, child.DesiredSize.Height));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectedTagsPeer(this);

    /// <summary>读屏看不到这一排（上游 aria-hidden）：下拉自己念全部已选。</summary>
    sealed class SelectedTagsPeer(SelectedTags owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>SelectedTags 的部件名（代码里组合的元素）：<c>rune|SelectedTags rune|Tag#PART_More</c>。</summary>
public static class SelectedTagsParts
{
    /// <summary>收起其余标签的"+N"（TS 的 propsMore）。</summary>
    public const string More = "PART_More";
}
