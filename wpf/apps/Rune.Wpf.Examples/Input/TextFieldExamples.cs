using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;

namespace Rune.Wpf.Examples;

/// <summary>
/// TextField 的示例（vue-tsx-pro 文档站 input/TextField）。form 要等 Form；formats 的校验也要等 Form（这里只有格式与必填的标记），
/// 手机号的说明里 TS 用 Code 显示值，Field 的说明是纯文字。
/// </summary>
static class TextFieldExamples
{
    public static Example[] All =>
    [
        new("TextField", "textarea", "Textarea", "高度随内容在 Rows 与 MaxRows 之间增长（改 Text 也会跟着变）；Fixed 关掉自动增高，Clearable 在右上角放清空按钮。字数计数超出上限时变成危险色，读屏念完整的句子。", Comment),
        new("TextField", "appearance", "样式、尺寸与状态", "Variant 是 surface（默认）或 soft；Size 缩放整个输入框；Radius=\"full\" 是胶囊形。不可用、只读、出错可以写在 Field 上，里面的输入框沿用。", Appearance),
        new("TextField", "adornments", "图标、前后缀、清空与字数", "Icon / IconEnd 放在框的两头，Prefix / Suffix 是不算在值里的固定文字，Clearable 有内容时显示清空按钮。Field 的 Error 让框标红并把错误作为说明。自己做的输入组件用 CharacterCount 显示字数。", Adornments),
        new("TextField", "formats", "手机号、身份证号、银行卡号", "Format 让输入时自动分组显示（TextFormat.Phone、IdCard、BankCard，别的用 TextFormat.Grouped 写）：Value 是去掉分隔的原文，过滤掉不接受的字符、截到最长，在中间插字光标也不跳。只管显示，校验写在表单里。", Formats),
    ];

    static FrameworkElement Comment()
    {
        var comment = new Textarea { Rows = 2, MaxRows = 6, MaxLength = 200, ShowCount = true, PlaceholderText = "说点什么…" };
        var publish = new Button { Size = TextSize.Sm, Content = "发布", IsEnabled = false };
        comment.TextChanged += (_, _) => publish.IsEnabled = !string.IsNullOrWhiteSpace(comment.Text);
        var form = new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm,
            Children =
            {
                new Field { Label = "发表评论", Description = "随内容自动增高，最多 6 行后出现滚动条", Content = comment },
                new Flex { Justify = FlexJustify.End, Children = { publish } },
            },
        };
        Flex.SetGrow(form, true);
        var card = new Card { Content = new Flex { Gap = Space.Md, Align = FlexAlign.Start, Children = { new Avatar { Size = TextSize.Sm, DisplayName = "林小满" }, form } } };
        var notes = new Field
        {
            Label = "固定高度（fixed）",
            Description = "不自动增高，右下角可以拖动调整；clearable 在右上角放清空按钮",
            Content = new Textarea { Text = "第一行\n第二行\n第三行", Rows = 3, Fixed = true, Clearable = true },
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { card, notes } };
    }

    static FrameworkElement Adornments()
    {
        const string bio = "喜欢做小而美的工具，周末在山里跑步。";
        return new Grid
        {
            MinItemWidth = "16rem", Gap = Space.Lg,
            Children =
            {
                new Field { Label = "搜索", Content = new TextField { Text = "设计规范", Type = TextFieldType.Search, Icon = Lucide.Search, Clearable = true, Radius = Radius.Full } },
                new Field { Label = "个人主页", Description = "prefix 是不算在值里的固定文字，iconEnd 在框尾", Content = new TextField { Text = "github.com/linxiaoman", Prefix = "https://", IconEnd = Lucide.Link2 } },
                new Field { Label = "用户名", Error = "用户名已被占用，换一个试试", Content = new TextField { Text = "xiaoman", Icon = Lucide.AtSign } },
                new Field
                {
                    Label = "简介", Description = "自己的输入组件（富文本、代码编辑器）用 CharacterCount 显示字数",
                    Content = new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new Textarea { Text = bio, Rows = 3 }, new CharacterCount { Count = bio.Length, Max = 30 } } },
                },
            },
        };
    }

    static FrameworkElement Formats()
    {
        var phone = new TextField { Format = TextFormat.Phone, Icon = Lucide.Smartphone, Value = "138000011" };
        var phoneField = new Field { Label = "手机号", IsRequired = true, Description = $"值：{phone.Value}", Content = phone };
        DependencyPropertyDescriptor.FromProperty(TextField.ValueProperty, typeof(TextField)).AddValueChanged(phone, (_, _) =>
            phoneField.Description = $"值：{(phone.Value.Length > 0 ? phone.Value : "（空）")}");
        return new Grid
        {
            MinItemWidth = "15rem", Gap = Space.Lg,
            Children =
            {
                phoneField,
                new Field { Label = "身份证号", Description = "18 位，最后一位可以是 X（自动转大写）", Content = new TextField { Format = TextFormat.IdCard, Icon = Lucide.IdCard, Value = "11010519491231002X" } },
                new Field { Label = "银行卡号", IsRequired = true, Description = "每 4 位一组，最长 19 位", Content = new TextField { Format = TextFormat.BankCard, Icon = Lucide.CreditCard, Clearable = true } },
            },
        };
    }

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
