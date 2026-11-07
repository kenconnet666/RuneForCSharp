using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 展开收起的内容区（vue-tsx-pro 的 Collapse），带高度过渡。内容加成子控件（竖着排、撑满宽度），触发器另外写：
/// <code>
/// var advanced = new CollapseControl { Dock = DockStyle.Top };
/// advanced.Controls.Add(filters);
/// toggle.Click += (_, _) => advanced.IsOpen = !advanced.IsOpen;
/// </code>
/// 高度在 0 与内容高度之间过渡（durations.md、easings.standard，系统关闭动画时直接到位）；子控件一直按完整高度排，露不出来的部分裁掉。
/// 完全收起（过渡结束）后子控件隐藏：不能 Tab 聚焦，读屏也读不到；收起的过程中仍可见。隐藏的控件不建句柄、不参与布局，
/// 本来就是第一次展开才真正创建，所以没有 TS 的 lazy。页内搜索命中收起的内容时自动展开是浏览器的能力，桌面上没有。
/// </summary>
public class CollapseControl : Panel
{
    readonly VerticalStack layout = new(() => 0);
    readonly Signal<double> openness = new(0);
    readonly Effect frame;

    /// <summary>新建：收起，高度跟着内容与展开的比例。</summary>
    public CollapseControl()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        frame = new Effect(OnFrame);
        using (frame.Track()) _ = openness.Value;
    }

    /// <summary>是否展开。</summary>
    [Category("Behavior")]
    [Description("是否展开；改变时高度过渡。")]
    [DefaultValue(false)]
    public bool IsOpen
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Toggle();
        }
    }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => layout;

    /// <summary>完全收起（过渡已结束）：这时子控件隐藏。</summary>
    bool Collapsed => !IsOpen && openness.Peek() == 0;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = layout.Measure(this, proposedSize);
        return size with { Height = (int)Math.Round(size.Height * openness.Peek()) };
    }

    /// <summary>展开时先显示子控件再长高；收起时缩到 0、过渡结束才隐藏。还没显示出来时直接到位。</summary>
    void Toggle()
    {
        if (IsOpen) ShowContent(true);
        var duration = Parent is null || !IsHandleCreated ? TimeSpan.Zero : Tokens.Duration.Md;
        Animator.Animate(openness, IsOpen ? 1 : 0, duration, Easing.Standard);
    }

    /// <summary>每一帧：父容器按新高度重排；收到 0 时隐藏子控件。</summary>
    void OnFrame()
    {
        using (frame.Track()) _ = openness.Value;
        if (Collapsed) ShowContent(false);
        if (Parent is { } parent) parent.PerformLayout(this, nameof(Bounds));
        else PerformLayout();
    }

    void ShowContent(bool shown)
    {
        foreach (Control child in Controls) child.Visible = shown;
    }

    /// <inheritdoc />
    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (Collapsed) e.Control!.Visible = false;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Animator.Stop(openness);
            frame.Dispose();
        }
        base.Dispose(disposing);
    }
}
