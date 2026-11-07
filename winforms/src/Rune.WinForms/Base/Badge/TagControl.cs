using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 标签（vue-tsx-pro 的 Tag）：外观同 <see cref="BadgeControl"/>（样式、色调、字号、圆角、图标，作用域里的 Badge 默认值一起改），可以带移除按钮
/// （筛选条件、已选项、输入的关键词）。
/// <code>
/// var tag = new TagControl { Text = "品牌：小米", Radius = Radius.Full };
/// tag.RemoveRequested += (_, _) => filters.Controls.Remove(tag);
/// </code>
/// 订阅了 <see cref="RemoveRequested"/> 才有移除按钮（上游给了 onRemove 才有）：文字后面一个圆形的 <see cref="CloseButtonControl"/> 子控件，
/// 1.5em、字号与颜色同标签，读屏名称是"移除 + 标签文字"（<see cref="RemoveLabel"/> 能改）。使用方移除了标签时，原来在移除按钮上的焦点交给
/// 同一容器里下一个（没有就上一个）能聚焦的控件。出入：没有离场的淡出缩小（子控件不能半透明），点了就请求移除。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(RemoveRequested))]
[Description("标签：外观同 Badge，可以带移除按钮。")]
public class TagControl : BadgeControl
{
    /// <summary>移除按钮的宽高（em）。</summary>
    const float RemoveEm = 1.5f;

    EventHandler? removeRequested;
    CloseButtonControl? remove;

    /// <summary>移除按钮的读屏名称；不写时是"移除"加上标签文字。</summary>
    [Category("Accessibility")]
    [Description("移除按钮的读屏名称；不写时是“移除”加上标签文字。")]
    [DefaultValue(null)]
    public string? RemoveLabel { get; set { field = value; UpdateRemoveLabel(); } }

    /// <summary>请使用方移除这个标签；订阅了才有移除按钮。</summary>
    [Category("Action")]
    [Description("点了移除按钮：请使用方移除这个标签。订阅了才有移除按钮。")]
    public event EventHandler? RemoveRequested
    {
        add
        {
            removeRequested += value;
            SyncRemove();
        }
        remove
        {
            removeRequested -= value;
            SyncRemove();
        }
    }

    /// <summary>移除按钮能不能聚焦（SelectedTagsControl 里不能：焦点留在下拉上，点击也不抢）；要在订阅 RemoveRequested 之前设。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool RemoveFocusable { get; init; } = true;

    /// <summary>移除按钮（测试用）；没有订阅时为 null。</summary>
    internal CloseButtonControl? RemoveButton => remove;

    /// <summary>有订阅时放上移除按钮，没有时拿掉。</summary>
    void SyncRemove()
    {
        if (removeRequested is null)
        {
            if (remove is null) return;
            Controls.Remove(remove);
            remove.Dispose();
            remove = null;
            Element.TrailingEm = 0;
            return;
        }
        if (remove is not null) return;
        remove = RemoveFocusable ? new CloseButtonControl() : new PassiveCloseButton();
        remove.Radius = Radius.Full;
        remove.AutoSize = false;
        remove.Click += (_, _) => RequestRemove();
        Controls.Add(remove);
        Element.TrailingEm = RemoveEm;
        UpdateRemoveLabel();
        PerformLayout();
    }

    void UpdateRemoveLabel()
    {
        if (remove is null) return;
        var lang = ProviderControl.LangOf(this);
        remove.AccessibleName = RemoveLabel ?? (Text.Length > 0 ? $"{lang.Remove} {Text}" : lang.Remove);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        UpdateRemoveLabel();
    }

    /// <summary>
    /// 移除按钮放在文字后面、末端内边距（0.25em）里面，字号档位与颜色同标签。按钮四周留着画焦点环的 Padding，控件比 1.5em 大一圈，
    /// 超出标签上下的部分被裁掉。
    /// </summary>
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (remove is null) return;
        var context = ElementContext.Of(this);
        var em = Element.EmIn(context);
        if (remove.TextSize != Element.Size) remove.TextSize = Element.Size;
        var foreground = Element.ForegroundIn(context);
        if (remove.Sx?.Foreground != foreground) remove.Sx = new ButtonSx { Foreground = foreground };
        var size = (int)MathF.Round(RemoveEm * em);
        var ring = remove.Padding;
        var right = Element.Bounds.Right - (int)MathF.Round(context.Scale(Tokens.BorderWidth.Xs) + 0.25f * em);
        var top = Element.Bounds.Top + (Element.Bounds.Height - size) / 2;
        remove.Bounds = new Rectangle(right - size - ring.Left, top - ring.Top, size + ring.Horizontal, size + ring.Vertical);
    }

    /// <summary>请使用方移除；焦点原来在移除按钮上、标签被移除了时，交给同一容器里下一个（没有就上一个）能聚焦的控件。</summary>
    void RequestRemove()
    {
        var focused = remove?.Focused == true;
        var group = Parent;
        var index = group?.Controls.IndexOf(this) ?? -1;
        removeRequested?.Invoke(this, EventArgs.Empty);
        if (!focused || group is null || Parent == group) return;
        var siblings = group.Controls.Cast<Control>().ToList();
        var candidates = siblings.Skip(index).Concat(siblings.Take(index).Reverse());
        foreach (var candidate in candidates)
        {
            if (!candidate.Visible || !candidate.Enabled) continue;
            if (candidate.CanSelect)
            {
                candidate.Select();
                return;
            }
            if (candidate.SelectNextControl(null, true, true, true, false)) return;
        }
    }

    /// <summary>不能聚焦的移除按钮：点它不抢焦点、不进 Tab 顺序。</summary>
    sealed class PassiveCloseButton : CloseButtonControl
    {
        public PassiveCloseButton()
        {
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }
    }
}
