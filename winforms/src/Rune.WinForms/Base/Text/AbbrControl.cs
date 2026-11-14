using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 缩写（vue-tsx-pro 的 Abbr）：点状下划线、帮助指针，悬停、键盘聚焦、点按时在提示里显示全称。
/// <code>
/// var sla = new AbbrControl { Text = "SLA", Title = "服务等级协议" };
/// </code>
/// 全称放在自带的提示（<see cref="TooltipProvider"/>，停留 200 毫秒显示）里：能聚焦（Tab 能到）、点一下就显示，读屏把全称作为说明念出来。
/// 第一次出现时最好仍把全称写在正文里。WinForms 的文字是整段画的：是单独的控件，和旁边的文字并排放（同 Code、Mark）。
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Title))]
[Description("缩写：悬停、聚焦、点按时显示全称。")]
public class AbbrControl : TextControl
{
    readonly TooltipProvider tips = new() { Delay = 200 };

    /// <summary>新建：能聚焦、帮助指针。</summary>
    public AbbrControl()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Help;
    }

    /// <summary>全称：显示在提示里。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Title
    {
        get;
        set
        {
            field = value;
            tips.SetTooltip(this, value);
        }
    }

    /// <summary>点按也显示全称：触屏没有悬停，点出来的焦点也不算键盘聚焦。</summary>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left) tips.Show(this);
    }

    /// <summary>点状下划线：离文字的基线 0.2em（TS 的 text-underline-offset），颜色跟文字。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var font = Font;
        var family = font.FontFamily;
        var ascent = font.GetHeight(e.Graphics) * family.GetCellAscent(font.Style) / family.GetLineSpacing(font.Style);
        var y = Padding.Top + ascent + 0.2f * font.GetHeight(e.Graphics) * family.GetEmHeight(font.Style) / family.GetLineSpacing(font.Style);
        var line = Math.Max(1f, DeviceDpi / 96f);
        using var pen = new Pen(ProviderControl.ThemeOf(this).Colors.Text, line) { DashStyle = DashStyle.Custom, DashPattern = [1, 2] };
        e.Graphics.DrawLine(pen, Padding.Left, y, Width - Padding.Right, y);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) tips.Dispose();
        base.Dispose(disposing);
    }
}
