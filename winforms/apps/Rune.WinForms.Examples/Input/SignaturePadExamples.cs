using System.Text;

namespace Rune.WinForms.Examples;

/// <summary>SignaturePadControl 的示例（vue-tsx-pro 文档站 input/SignaturePad）。</summary>
static class SignaturePadExamples
{
    public static Example[] All =>
    [
        new("SignaturePad", "receipt", "签收", "Value 是透明底的 PNG（字节），每写完一笔更新、报告 ValueChanged，没签时是 null。外框同输入框（FieldControl 的出错、不可用、只读都认），底色与墨色是“纸面”：深色主题下仍是白底深色，导出的图放到白纸上看得见。写过之后右上角有撤销上一笔、清空。IsReadOnly 时显示已签的图（代码里给的 Value），不能写；编辑页把读回的旧签名放进 Value，可以接着写或清空。", Receipt),
        new("SignaturePad", "export", "导出与笔触", "ToPng()、ToSvg()（笔画是路径，放大不糊）、Undo()、Clear()。PenColor 是墨色，PenMinWidth、PenMaxWidth 是笔宽的范围：写得越快越细，同真的笔一样。", Export),
    ];

    // 签收：签了字才能确认；确认后用只读的 SignaturePadControl 显示签好的图
    static Control Receipt()
    {
        var host = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        void Show(Control content)
        {
            foreach (Control old in host.Controls.Cast<Control>().ToArray()) old.Dispose();
            content.Dock = DockStyle.Top;
            host.Controls.Add(content);
        }
        void Signing()
        {
            var pad = new SignaturePadControl();
            var confirm = new ButtonControl { Text = "确认签收", Enabled = false };
            pad.ValueChanged += (_, _) => confirm.Enabled = pad.Value is not null;
            confirm.Click += (_, _) => Confirmed(pad.Value!, DateTime.Now);
            Show(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch },
                new FieldControl { Label = "签收人签字", Description = "用鼠标、触控笔或手指写", IsRequired = true, Content = pad }, confirm));
        }
        void Confirmed(byte[] signature, DateTime at)
        {
            var pad = new SignaturePadControl { Value = signature, IsReadOnly = true, AccessibleName = "签收人签字", AutoSize = false };
            pad.Height = pad.LogicalToDeviceUnits(7 * 14);
            var again = new ButtonControl { Text = "重新签收", TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost };
            again.Click += (_, _) => Signing();
            var card = new CardControl { CardPadding = Space.Md, AutoSize = true };
            card.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Dock = DockStyle.Top },
                new TextControl { Text = "已签收", Weight = TextWeight.Semibold },
                pad,
                Build.Flex(new FlexControl { Justify = FlexJustify.Between, Align = FlexAlign.Center }, new SmallControl { Text = at.ToString("g"), Tone = TextTone.Muted }, again)));
            Show(card);
        }
        Signing();
        return host;
    }

    static readonly (string Key, string Label, Color Color, double Min, double Max)[] Pens =
    [
        ("ink", "墨黑", Color.FromArgb(0x1c, 0x20, 0x24), 0.75, 2.75),
        ("blue", "签字笔蓝", Color.FromArgb(0x1d, 0x4e, 0xd8), 0.6, 2.2),
        ("marker", "马克笔", Color.FromArgb(0xb9, 0x1c, 0x1c), 2.5, 5),
    ];

    // 导出：ToPng、ToSvg 存成文件；换墨色与笔宽
    static Control Export()
    {
        var pad = new SignaturePadControl { AccessibleName = "签名" };
        var pens = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = "笔" };
        pens.Controls.AddRange([.. Pens.Select(pen => new ToggleItemControl { Value = pen.Key, Text = pen.Label })]);
        pens.Value = "ink";
        void UsePen()
        {
            var (_, _, color, min, max) = Pens.First(pen => pen.Key == pens.Value);
            (pad.PenColor, pad.PenMinWidth, pad.PenMaxWidth) = (color, min, max);
        }
        pens.ValueChanged += (_, _) => UsePen();
        UsePen();
        var png = new ButtonControl { Text = "PNG", Icon = Lucide.Download, TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Enabled = false };
        var svg = new ButtonControl { Text = "SVG", Icon = Lucide.Download, TextSize = TextSize.Sm, Variant = ButtonVariant.Soft, Enabled = false };
        pad.ValueChanged += (_, _) => png.Enabled = svg.Enabled = pad.Value is not null;
        png.Click += (_, _) => Save("signature.png", pad.ToPng());
        svg.Click += (_, _) => Save("signature.svg", pad.ToSvg() is { } text ? Encoding.UTF8.GetBytes(text) : null);
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch, Dock = DockStyle.Top },
            Build.Flex(new FlexControl { Gap = Space.Xs }, pens), pad, Build.Flex(new FlexControl { Gap = Space.Xs }, png, svg));
    }

    /// <summary>平台的保存对话框，存下字节。</summary>
    static void Save(string name, byte[]? bytes)
    {
        if (bytes is null) return;
        using var dialog = new SaveFileDialog { FileName = name };
        if (dialog.ShowDialog() == DialogResult.OK) File.WriteAllBytes(dialog.FileName, bytes);
    }
}
