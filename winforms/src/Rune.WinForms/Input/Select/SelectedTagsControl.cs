using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>多选下拉里一个已选的标签：值（移除时交给 <see cref="SelectedTagsControl.RemoveRequested"/>）与显示的文字。</summary>
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
/// 多选下拉触发框里那一排已选标签（vue-tsx-pro 的 SelectedTags）：<see cref="SelectControl"/> 用，以后的 TreeSelect 共用。
/// 标签是 sm、soft 的 <see cref="TagControl"/>，间隔 space.xs、上下各留 space.xs；订阅了 <see cref="RemoveRequested"/> 才有移除按钮（只读、不可用时不订阅）。
/// 这排标签只给指针用：读屏看不到（下拉自己念全部已选），移除按钮不能聚焦、点击不抢焦点。<see cref="MaxTagCount"/> 是数字时只显示前几个，
/// <see cref="TagLimit.Responsive"/> 时排成一行按宽度放（至少一个），其余收进"+N"。
/// </summary>
public class SelectedTagsControl : Control
{
    readonly TagControl more = new() { TextSize = TextSize.Sm, Variant = StatusVariant.Soft, Visible = false };
    readonly List<TagControl> tags = [];
    EventHandler<SelectedTagEventArgs>? removeRequested;

    /// <summary>新建：没有标签。</summary>
    public SelectedTagsControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        Controls.Add(more);
    }

    /// <summary>要显示的标签。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<SelectedTag> Tags { get; set { field = value; Rebuild(); } } = [];

    /// <summary>最多显示几个；null 时全部显示（会换行）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TagLimit? MaxTagCount { get; set { field = value; Rebuild(); } }

    /// <summary>点了标签的移除按钮；订阅了才有移除按钮。</summary>
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
    internal IEnumerable<TagControl> ShownTags => tags.Where(tag => tag.Visible);

    bool Responsive => MaxTagCount is { Count: null };

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>每个已选项一个标签（数字上限时只建前几个），最后是"+N"。</summary>
    void Rebuild()
    {
        SuspendLayout();
        foreach (var tag in tags)
        {
            Controls.Remove(tag);
            tag.Dispose();
        }
        tags.Clear();
        var shown = MaxTagCount?.Count is { } count ? Tags.Take(count) : Tags;
        foreach (var item in shown)
        {
            var tag = new TagControl { Text = item.Label, TextSize = TextSize.Sm, Variant = StatusVariant.Soft, RemoveFocusable = false };
            if (removeRequested is not null) tag.RemoveRequested += (_, _) => removeRequested?.Invoke(this, new SelectedTagEventArgs(item.Value));
            tags.Add(tag);
        }
        Controls.AddRange([.. tags]);
        Controls.SetChildIndex(more, Controls.Count - 1);
        ResumeLayout();
        PerformLayout();
    }

    /// <summary>按宽度时数出放得下几个（其余收起），再排成行。</summary>
    List<List<Control>> Arrange(int width, bool apply)
    {
        var gap = (int)MathF.Round(0.25f * Em);
        HiddenCount = Tags.Count - tags.Count;
        var fit = tags.Count;
        if (Responsive)
        {
            var used = 0;
            for (fit = 0; fit < tags.Count; fit++)
            {
                var next = used + (fit > 0 ? gap : 0) + tags[fit].GetPreferredSize(Size.Empty).Width;
                var reserve = fit < tags.Count - 1 ? gap + MoreWidth(tags.Count - fit - 1) : 0;
                if (fit > 0 && next + reserve > width) break;
                used = next;
            }
            HiddenCount = tags.Count - fit;
        }
        more.Text = ProviderControl.LangOf(this).SelectMoreTags(HiddenCount);
        if (apply)
        {
            for (var index = 0; index < tags.Count; index++) tags[index].Visible = index < fit;
            more.Visible = HiddenCount > 0;
        }
        var visible = tags.Take(fit).Cast<Control>().ToList();
        if (HiddenCount > 0) visible.Add(more);
        var lines = new List<List<Control>>();
        var lineWidth = 0;
        foreach (var child in visible)
        {
            var size = child.GetPreferredSize(Size.Empty).Width;
            if (lines.Count > 0 && (Responsive || lineWidth + gap + size <= width))
            {
                lines[^1].Add(child);
                lineWidth += gap + size;
            }
            else
            {
                lines.Add([child]);
                lineWidth = size;
            }
        }
        return lines;
    }

    int MoreWidth(int hidden)
    {
        more.Text = ProviderControl.LangOf(this).SelectMoreTags(hidden);
        return more.GetPreferredSize(Size.Empty).Width;
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width > 0 ? proposedSize.Width : int.MaxValue;
        var gap = (int)MathF.Round(0.25f * Em);
        var lines = Arrange(width, apply: false);
        var height = lines.Sum(line => line.Max(child => child.GetPreferredSize(Size.Empty).Height)) + Math.Max(0, lines.Count - 1) * gap + 2 * gap;
        var widest = lines.Count == 0 ? 0 : lines.Max(line => line.Sum(child => child.GetPreferredSize(Size.Empty).Width) + (line.Count - 1) * gap);
        return new Size(Responsive ? Math.Min(widest, width) : widest, height);
    }

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var gap = (int)MathF.Round(0.25f * Em);
        var y = gap;
        foreach (var line in Arrange(ClientSize.Width, apply: true))
        {
            var height = line.Max(child => child.GetPreferredSize(Size.Empty).Height);
            var x = 0;
            foreach (var child in line)
            {
                var size = child.GetPreferredSize(Size.Empty);
                child.Bounds = new Rectangle(x, y + (height - size.Height) / 2, size.Width, size.Height);
                x += size.Width + gap;
            }
            y += height + gap;
        }
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new HiddenAccessibleObject(this);

    /// <summary>读屏看不到这一排（上游 aria-hidden）：下拉自己念全部已选。</summary>
    sealed class HiddenAccessibleObject(Control owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleStates State => base.State | AccessibleStates.Invisible;

        public override int GetChildCount() => 0;
    }
}
