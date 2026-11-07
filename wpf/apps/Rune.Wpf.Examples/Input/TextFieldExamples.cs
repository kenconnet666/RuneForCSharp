using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>TextField 的示例（vue-tsx-pro 文档站 input/TextField）。form 要等 Form，textarea、adornments 要等 Textarea，formats 要等 format。</summary>
static class TextFieldExamples
{
    public static Example[] All =>
    [
        new("TextField", "appearance", "样式、尺寸与状态", "Variant 是 surface（默认）或 soft；Size 缩放整个输入框；Radius=\"full\" 是胶囊形。不可用、只读、出错可以写在 Field 上，里面的输入框沿用。", Appearance),
    ];

    static FrameworkElement Appearance()
    {
        var variants = new Grid { Columns = 2, Gap = Space.Md };
        foreach (var variant in new[] { ControlVariant.Surface, ControlVariant.Soft })
        {
            var column = new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { new Small { Text = $"variant=\"{variant}\"", Tone = TextTone.Muted } } };
            foreach (var size in new[] { TextSize.Sm, TextSize.Md, TextSize.Lg })
            {
                var field = new TextField { Variant = variant, Size = size, Icon = Lucide.Search, PlaceholderText = $"size=\"{size}\"" };
                AutomationProperties.SetName(field, $"{variant} {size}");
                column.Children.Add(field);
            }
            var round = new TextField { Variant = variant, Radius = Radius.Full, Icon = Lucide.Search, PlaceholderText = "radius=\"full\"" };
            AutomationProperties.SetName(round, $"{variant} 圆角");
            column.Children.Add(round);
            variants.Children.Add(column);
        }
        var states = new Grid
        {
            MinItemWidth = "11rem", Gap = Space.Md,
            Children =
            {
                new Field { Label = "不可用", IsEnabled = false, Content = new TextField { Text = "不能改" } },
                new Field { Label = "只读", IsReadOnly = true, Content = new TextField { Text = "可以选中复制" } },
                new Field { Label = "校验未通过", Error = "这个用户名已经被占用了", Content = new TextField { Text = "admin" } },
            },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Lg, Children = { variants, states } };
    }
}
