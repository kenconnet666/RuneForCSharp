namespace Rune.WinForms.Examples;

/// <summary>
/// TextField 的示例（vue-tsx-pro 文档站 input/TextField）。form 要等 Form，textarea、adornments 要等 Textarea，formats 要等 format。
/// WinForms 里是 TextFieldControl。
/// </summary>
static class TextFieldExamples
{
    public static Example[] All =>
    [
        new("TextField", "appearance", "样式、尺寸与状态", "Variant 是 surface（默认）或 soft；TextSize 缩放整个输入框；Radius = full 是胶囊形。只读、出错可以写在 FieldControl 上，里面的输入框沿用。", Appearance),
    ];

    static Control Appearance()
    {
        var variants = Build.Grid(new GridControl { Columns = 2, Gap = Space.Md });
        foreach (var variant in new[] { ControlVariant.Surface, ControlVariant.Soft })
        {
            var column = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm }, new SmallControl { Text = $"variant=\"{variant}\"", Tone = TextTone.Muted });
            foreach (var size in new[] { TextSize.Sm, TextSize.Md, TextSize.Lg })
                column.Controls.Add(new TextFieldControl { Variant = variant, TextSize = size, Icon = Lucide.Search, PlaceholderText = $"size=\"{size}\"", AccessibleName = $"{variant} {size}" });
            column.Controls.Add(new TextFieldControl { Variant = variant, Radius = Radius.Full, Icon = Lucide.Search, PlaceholderText = "radius=\"full\"", AccessibleName = $"{variant} 圆角" });
            variants.Controls.Add(column);
        }
        var states = Build.Grid(new GridControl { MinItemWidth = "11rem", Gap = Space.Md },
            new FieldControl { Label = "不可用", Enabled = false, Content = new TextFieldControl { Text = "不能改" } },
            new FieldControl { Label = "只读", IsReadOnly = true, Content = new TextFieldControl { Text = "可以选中复制" } },
            new FieldControl { Label = "校验未通过", Error = "这个用户名已经被占用了", Content = new TextFieldControl { Text = "admin" } });
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg }, variants, states);
    }
}
