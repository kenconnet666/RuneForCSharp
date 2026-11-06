using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 多行文字的骨架（vue-tsx-pro 的 SkeletonText）：一段正文加载时的占位，几行 <see cref="SkeletonControl"/>，最后一行短一些（六成宽）。
/// 行高 1em、行间 space.sm，接近正文 1.5 的行高，换成内容时高度大致不变。各行的扫光依次晚 120 毫秒，像一道波从上往下扫过。
/// 宽度由父容器给（停靠、锚定），高度跟着行数。读屏里没有角色。
/// </summary>
public class SkeletonTextControl : Panel
{
    /// <summary>相邻两行的扫光间隔（毫秒，TS 的 SHIMMER_STAGGER）。</summary>
    public const int Stagger = 120;

    readonly LinesEngine engine;

    /// <summary>新建：3 行，读屏里没有角色。</summary>
    public SkeletonTextControl()
    {
        engine = new LinesEngine(this);
        AccessibleRole = AccessibleRole.None;
        Rebuild();
    }

    /// <summary>行数，默认 3。</summary>
    [Category("Appearance")]
    [Description("行数。")]
    [DefaultValue(3)]
    public int Lines { get; set { field = value; Rebuild(); } } = 3;

    /// <summary>第一行扫光的延迟（毫秒），之后每行再晚 <see cref="Stagger"/>。</summary>
    [Category("Appearance")]
    [Description("第一行扫光的延迟（毫秒）。")]
    [DefaultValue(0)]
    public int Delay { get; set { field = value; Rebuild(); } }

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => engine;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    void Rebuild()
    {
        Controls.Clear();
        for (var i = 0; i < Math.Max(0, Lines); i++) Controls.Add(new SkeletonControl { Shape = SkeletonShape.Text, Delay = Delay + i * Stagger, AutoSize = false });
        Height = engine.Height;
        PerformLayout();
    }

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Height = engine.Height;
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width is > 1 and < int.MaxValue ? proposedSize.Width : Width, engine.Height);

    /// <summary>各行 1em 高、隔 0.5em，最后一行六成宽（只有一行时不缩短）。</summary>
    sealed class LinesEngine(SkeletonTextControl owner) : LayoutEngine
    {
        public int Height => (int)Math.Round(owner.Controls.Count * 1.5 * owner.Em - 0.5 * owner.Em);

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            var count = owner.Controls.Count;
            var line = (int)Math.Round(owner.Em);
            for (var i = 0; i < count; i++)
            {
                var width = count > 1 && i == count - 1 ? (int)Math.Round(0.6 * owner.ClientSize.Width) : owner.ClientSize.Width;
                owner.Controls[i].SetBounds(0, (int)Math.Round(i * 1.5 * owner.Em), width, line);
            }
            return false;
        }
    }
}
