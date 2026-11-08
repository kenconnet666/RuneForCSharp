using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Rune.Wpf;

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
/// <see cref="TagLimit.Responsive"/> 时排成一行按宽度放（至少一个），其余收进"+N"。"+N"的部件样式键是 <see cref="SelectedTagsParts.More"/>（TS 的 propsMore）。
/// </summary>
public class SelectedTags : Panel
{
    /// <summary>要显示的标签。</summary>
    public static readonly DependencyProperty TagsProperty = DependencyProperty.Register(nameof(Tags), typeof(IReadOnlyList<SelectedTag>), typeof(SelectedTags),
        new FrameworkPropertyMetadata(null, (d, _) => ((SelectedTags)d).Rebuild()));

    /// <summary>最多显示几个；null 时全部显示（会换行）。</summary>
    public static readonly DependencyProperty MaxTagCountProperty = DependencyProperty.Register(nameof(MaxTagCount), typeof(TagLimit?), typeof(SelectedTags),
        new FrameworkPropertyMetadata(null, (d, _) => ((SelectedTags)d).Rebuild()));

    readonly Tag more = new() { Name = "PART_More" };
    EventHandler<SelectedTagEventArgs>? removeRequested;

    /// <summary>新建：没有标签。</summary>
    public SelectedTags()
    {
        ClipToBounds = true;
        more.SetResourceReference(StyleProperty, SelectedTagsParts.More);
        Rebuild();
    }

    /// <summary>要显示的标签。</summary>
    public IReadOnlyList<SelectedTag>? Tags { get => (IReadOnlyList<SelectedTag>?)GetValue(TagsProperty); set => SetValue(TagsProperty, value); }

    /// <summary>最多显示几个。</summary>
    public TagLimit? MaxTagCount { get => (TagLimit?)GetValue(MaxTagCountProperty); set => SetValue(MaxTagCountProperty, value); }

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
    internal IEnumerable<Tag> ShownTags => TagChildren.Take(TagChildren.Count - (Responsive ? HiddenCount : 0));

    List<Tag> TagChildren => [.. Children.OfType<Tag>().Where(tag => tag != more)];

    bool Responsive => MaxTagCount is { Count: null };

    double Em => TextElement.GetFontSize(this);

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

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var gap = 0.25 * Em;
        var tags = TagChildren;
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var tag in tags) tag.Measure(unbounded);
        // 数字上限时没建出来的那些；按宽度时先当全部放得下，下面再算
        HiddenCount = (Tags?.Count ?? 0) - tags.Count;
        if (Responsive)
        {
            var used = 0.0;
            var fit = 0;
            for (; fit < tags.Count; fit++)
            {
                var next = used + (fit > 0 ? gap : 0) + tags[fit].DesiredSize.Width;
                // 后面还有放不下的：给"+N"留位置
                var reserve = fit < tags.Count - 1 ? gap + MoreWidth(tags.Count - fit - 1) : 0;
                if (fit > 0 && next + reserve > availableSize.Width) break;
                used = next;
            }
            HiddenCount = tags.Count - fit;
        }
        more.Content = Provider.GetLang(this).SelectMoreTags(HiddenCount);
        more.Visibility = HiddenCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        more.Measure(unbounded);
        var lines = Lines(availableSize.Width);
        var height = lines.Sum(line => line.Height) + Math.Max(0, lines.Count - 1) * gap + 2 * gap;
        var widest = lines.Count == 0 ? 0 : lines.Max(line => line.Width);
        return new Size(Responsive ? Math.Min(widest, availableSize.Width) : widest, height);
    }

    double MoreWidth(int hidden)
    {
        more.Content = Provider.GetLang(this).SelectMoreTags(hidden);
        more.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return more.DesiredSize.Width;
    }

    /// <summary>显示的标签加"+N"排成行：按宽度时一行，否则放不下就换行。</summary>
    List<(List<UIElement> Items, double Width, double Height)> Lines(double width)
    {
        var gap = 0.25 * Em;
        var visible = new List<UIElement>(ShownTags);
        if (more.Visibility == Visibility.Visible) visible.Add(more);
        var lines = new List<(List<UIElement> Items, double Width, double Height)>();
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
        var placed = new HashSet<UIElement>();
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
        // 按宽度收起的：挪到看不见的地方（裁掉），不改 Visibility（测量里改会反复失效）
        foreach (UIElement child in Children)
            if (!placed.Contains(child)) child.Arrange(new Rect(-10000, 0, child.DesiredSize.Width, child.DesiredSize.Height));
        return finalSize;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectedTagsPeer(this);

    /// <summary>读屏看不到这一排（上游 aria-hidden）：下拉自己念全部已选。</summary>
    sealed class SelectedTagsPeer(SelectedTags owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>SelectedTags 的部件样式键：Provider.Styles 里写 <c>&lt;Style x:Key="{x:Static rune:SelectedTagsParts.More}" TargetType="rune:Tag"&gt;</c>。</summary>
public static class SelectedTagsParts
{
    /// <summary>收起其余标签的"+N"（TS 的 propsMore）：Tag，sm、soft。</summary>
    public static readonly PartKey More = new(typeof(SelectedTags), nameof(More), typeof(Tag));
}
