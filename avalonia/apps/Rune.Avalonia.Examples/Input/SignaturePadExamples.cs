using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Rune.Avalonia.Examples;

/// <summary>SignaturePad 的示例（vue-tsx-pro 文档站 input/SignaturePad）。</summary>
static class SignaturePadExamples
{
    public static Example[] All =>
    [
        new("SignaturePad", "receipt", "签收", "Value 是透明底的 PNG（字节），每写完一笔更新，没签时是 null，默认双向绑定。外框同输入框（Field 的出错、不可用、只读都认），底色与墨色是“纸面”：深色主题下仍是白底深色，导出的图放到白纸上看得见。写过之后右上角有撤销上一笔、清空。IsReadOnly 时显示已签的图（父级给的 Value），不能写；编辑页把读回的旧签名放进 Value，可以接着写或清空。", Receipt),
        new("SignaturePad", "export", "导出与笔触", "ToPng()、ToSvg()（笔画是路径，放大不糊）、Undo()、Clear()。PenColor 是墨色，PenMinWidth、PenMaxWidth 是笔宽的范围：写得越快越细，同真的笔一样。", Export),
    ];

    // 签收：签了字才能确认；确认后用只读的 SignaturePad 显示签好的图
    static Control Receipt()
    {
        var host = new ContentControl();
        void Signing()
        {
            var pad = new SignaturePad();
            var confirm = new Button { Content = "确认签收", IsEnabled = false };
            pad.PropertyChanged += (_, e) =>
            {
                if (e.Property == SignaturePad.ValueProperty) confirm.IsEnabled = pad.Value is not null;
            };
            confirm.Click += (_, _) => Confirmed(pad.Value!, DateTime.Now);
            host.Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Sm,
                Children = { new Field { Label = "签收人签字", Description = "用鼠标、触控笔或手指写", IsRequired = true, Content = pad }, confirm },
            };
        }
        void Confirmed(byte[] signature, DateTime at)
        {
            var pad = new SignaturePad { Value = signature, IsReadOnly = true, Height = 7 * 14 };
            AutomationProperties.SetName(pad, "签收人签字");
            var again = new Button { Content = "重新签收", Size = TextSize.Sm, Variant = ButtonVariant.Ghost };
            again.Click += (_, _) => Signing();
            host.Content = new Card
            {
                Padding = Space.Md,
                Content = new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Sm,
                    Children =
                    {
                        new Text { Text = "已签收", Weight = TextWeight.Semibold },
                        pad,
                        new Flex { Justify = FlexJustify.Between, Align = FlexAlign.Center, Children = { new Small { Text = at.ToString("g"), Tone = TextTone.Muted }, again } },
                    },
                },
            };
        }
        Signing();
        return host;
    }

    static readonly (string Key, string Label, Color Color, double Min, double Max)[] Pens =
    [
        ("ink", "墨黑", Color.Parse("#1c2024"), 0.75, 2.75),
        ("blue", "签字笔蓝", Color.Parse("#1d4ed8"), 0.6, 2.2),
        ("marker", "马克笔", Color.Parse("#b91c1c"), 2.5, 5),
    ];

    // 导出：ToPng、ToSvg 存成文件；换墨色与笔宽
    static Control Export()
    {
        var pad = new SignaturePad();
        AutomationProperties.SetName(pad, "签名");
        var pens = new ToggleGroup { IsRequired = true, Value = "ink", Size = TextSize.Sm };
        AutomationProperties.SetName(pens, "笔");
        foreach (var pen in Pens) pens.Children.Add(new ToggleItem { Value = pen.Key, Content = pen.Label });
        void UsePen()
        {
            var (_, _, color, min, max) = Pens.First(pen => pen.Key == pens.Value);
            (pad.PenColor, pad.PenMinWidth, pad.PenMaxWidth) = (color, min, max);
        }
        pens.PropertyChanged += (_, e) => { if (e.Property == ToggleGroup.ValueProperty) UsePen(); };
        UsePen();
        var png = new Button { Content = "PNG", Icon = Lucide.Download, Size = TextSize.Sm, Variant = ButtonVariant.Soft, IsEnabled = false };
        var svg = new Button { Content = "SVG", Icon = Lucide.Download, Size = TextSize.Sm, Variant = ButtonVariant.Soft, IsEnabled = false };
        pad.PropertyChanged += (_, e) =>
        {
            if (e.Property == SignaturePad.ValueProperty) png.IsEnabled = svg.IsEnabled = pad.Value is not null;
        };
        png.Click += async (_, _) => await Save(pad, "signature.png", pad.ToPng());
        svg.Click += async (_, _) => await Save(pad, "signature.svg", pad.ToSvg() is { } text ? Encoding.UTF8.GetBytes(text) : null);
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { pens, pad, new Flex { Gap = Space.Xs, Children = { png, svg } } } };
    }

    /// <summary>平台的保存对话框，存下字节。</summary>
    static async Task Save(Control owner, string name, byte[]? bytes)
    {
        if (bytes is null || TopLevel.GetTopLevel(owner)?.StorageProvider is not { CanSave: true } storage) return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = name });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(bytes);
    }
}
