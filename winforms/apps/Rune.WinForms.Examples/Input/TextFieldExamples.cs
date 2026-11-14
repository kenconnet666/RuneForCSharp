namespace Rune.WinForms.Examples;

/// <summary>
/// TextField 的示例（vue-tsx-pro 文档站 input/TextField）。form 要等 Form；formats 的校验也要等 Form（这里只有格式与必填的标记）。
/// WinForms 里是 TextFieldControl、TextareaControl。
/// </summary>
static class TextFieldExamples
{
    public static Example[] All =>
    [
        new("TextField", "textarea", "Textarea", "TextareaControl 的高度随内容在 Rows 与 MaxRows 之间增长；Fixed 关掉自动增高，Clearable 在右上角放清空按钮。字数计数超出上限时变成危险色，读屏念完整的句子。", Comment),
        new("TextField", "search", "搜索框", "SearchFieldControl 是 Type=search 的 TextField：带搜索图标、清空按钮，名称与占位文字默认是“搜索”。Text 每敲一个字都更新，本地过滤直接用它；SearchRequested 只在该查的时候发——停手 Delay（默认 300ms）、按回车、清空，同一个词不重复，发请求用它。下拉、表头筛选里的搜索框都是它。", Search),
        new("TextField", "appearance", "样式、尺寸与状态", "Variant 是 surface（默认）或 soft；TextSize 缩放整个输入框；Radius = full 是胶囊形。只读、出错可以写在 FieldControl 上，里面的输入框沿用。", Appearance),
        new("TextField", "adornments", "图标、前后缀、清空与字数", "Icon / IconEnd 放在框的两头，Prefix / Suffix 是不算在值里的固定文字，Clearable 有内容时显示清空按钮。FieldControl 的 Error 让框标红并把错误作为说明。自己做的输入组件用 CharacterCountControl 显示字数。", Adornments),
        new("TextField", "formats", "手机号、身份证号、银行卡号", "Format 让输入时自动分组显示（TextFormat.Phone、IdCard、BankCard，别的用 TextFormat.Grouped 写）：Value 是去掉分隔的原文，过滤掉不接受的字符、截到最长，在中间插字光标也不跳。只管显示，校验写在表单里。", Formats),
    ];

    static readonly string[] Parts = ["显示器支架", "支架底座", "铰链盖板", "夹持环", "加强筋", "防尘罩"];

    static Control Search()
    {
        var list = new UnorderedListControl();
        var search = new SearchFieldControl { PlaceholderText = "搜索零件", AccessibleName = "搜索零件" };
        var requests = new List<string>();
        var log = new SmallControl { Tone = TextTone.Muted };
        void Filter()
        {
            list.SuspendLayout();
            list.Controls.Cast<Control>().ToList().ForEach(item => item.Dispose());
            list.Controls.AddRange([.. Parts.Where(part => part.Contains(search.Text.Trim())).Select(part => new ListItemControl { Text = part })]);
            list.ResumeLayout();
            log.Text = $"列表跟着每个字过滤；SearchRequested 只在停手 300ms、回车、清空时发，最近几次：{(requests.Count > 0 ? string.Join("、", requests) : "还没有")}";
        }
        search.TextChanged += (_, _) => Filter();
        search.SearchRequested += (_, text) =>
        {
            requests.Add(text.Length > 0 ? text : "（清空）");
            if (requests.Count > 3) requests.RemoveAt(0);
            Filter();
        };
        Filter();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md }, search, list, log);
    }

    static Control Comment()
    {
        var comment = new TextareaControl { Rows = 2, MaxRows = 6, MaxLength = 200, ShowCount = true, PlaceholderText = "说点什么…" };
        var publish = new ButtonControl { TextSize = TextSize.Sm, Text = "发布", Enabled = false, AutoSize = true };
        comment.TextChanged += (_, _) => publish.Enabled = !string.IsNullOrWhiteSpace(comment.Text);
        var form = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Grow = true },
            new FieldControl { Label = "发表评论", Description = "随内容自动增高，最多 6 行后出现滚动条", Content = comment },
            Build.Flex(new FlexControl { Justify = FlexJustify.End }, publish));
        var card = Build.Card(new CardControl(), Build.Flex(new FlexControl { Gap = Space.Md, Align = FlexAlign.Start }, new AvatarControl { TextSize = TextSize.Sm, DisplayName = "林小满" }, form));
        var notes = new FieldControl
        {
            Label = "固定高度（fixed）",
            Description = "不自动增高，右下角可以拖动调整；clearable 在右上角放清空按钮",
            Content = new TextareaControl { Text = "第一行\r\n第二行\r\n第三行", Rows = 3, Fixed = true, Clearable = true },
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md }, card, notes);
    }

    static Control Adornments()
    {
        const string bio = "喜欢做小而美的工具，周末在山里跑步。";
        return Build.Grid(new GridControl { MinItemWidth = "16rem", Gap = Space.Lg },
            new FieldControl { Label = "搜索", Content = new TextFieldControl { Text = "设计规范", Type = TextFieldType.Search, Icon = Lucide.Search, Clearable = true, Radius = Radius.Full } },
            new FieldControl { Label = "个人主页", Description = "prefix 是不算在值里的固定文字，iconEnd 在框尾", Content = new TextFieldControl { Text = "github.com/linxiaoman", Prefix = "https://", IconEnd = Lucide.Link2 } },
            new FieldControl { Label = "用户名", Error = "用户名已被占用，换一个试试", Content = new TextFieldControl { Text = "xiaoman", Icon = Lucide.AtSign } },
            new FieldControl
            {
                Label = "简介", Description = "自己的输入组件（富文本、代码编辑器）用 CharacterCount 显示字数",
                Content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs }, new TextareaControl { Text = bio, Rows = 3 }, new CharacterCountControl { Count = bio.Length, Max = 30 }),
            });
    }

    static Control Formats()
    {
        var phone = new TextFieldControl { Format = TextFormat.Phone, Icon = Lucide.Smartphone, Value = "138000011" };
        var phoneField = new FieldControl { Label = "手机号", IsRequired = true, Description = $"值：{phone.Value}", Content = phone };
        phone.ValueChanged += (_, _) => phoneField.Description = $"值：{(phone.Value.Length > 0 ? phone.Value : "（空）")}";
        return Build.Grid(new GridControl { MinItemWidth = "15rem", Gap = Space.Lg },
            phoneField,
            new FieldControl { Label = "身份证号", Description = "18 位，最后一位可以是 X（自动转大写）", Content = new TextFieldControl { Format = TextFormat.IdCard, Icon = Lucide.IdCard, Value = "11010519491231002X" } },
            new FieldControl { Label = "银行卡号", IsRequired = true, Description = "每 4 位一组，最长 19 位", Content = new TextFieldControl { Format = TextFormat.BankCard, Icon = Lucide.CreditCard, Clearable = true } });
    }

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
