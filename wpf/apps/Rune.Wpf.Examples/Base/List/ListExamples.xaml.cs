using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>List 的示例（vue-tsx-pro 文档站 base/List）。</summary>
static class ListExamples
{
    public static Example[] All =>
    [
        new("List", "basic", "无序、有序与嵌套", "正文里的列表：List 是圆点，OrderedList 是编号（Start 从几开始），Gap 调项之间的间距。项里先是文字、再是嵌套的列表时，ListItem 的内容放一个 StackPanel。", () => new ListBasic()),
        new("List", "icons", "图标代替圆点", "Icon 让每项前面画一个图标（功能清单的对勾），部件样式键 ListParts.Icon 改颜色；ListItem 的 Icon 逐项覆盖。图标是装饰，读屏照常念\"列表，几项\"。Size 是字号档位。", Features),
    ];

    static readonly (string Name, string Price, (string Feature, bool Included)[] Features)[] Plans =
    [
        ("专业版", "¥39 / 月", [("不限项目数", true), ("100 GB 存储", true), ("版本历史 30 天", true), ("单点登录", false)]),
        ("团队版", "¥99 / 人 / 月", [("专业版的全部功能", true), ("共享空间与权限", true), ("版本历史 1 年", true), ("单点登录", true)]),
    ];

    /// <summary>图标的部件样式：色调。</summary>
    static Style IconTone(TextTone tone) => new(typeof(Icon)) { Setters = { new Setter(Icon.ToneProperty, tone) } };

    /// <summary>功能清单：包含的打勾（success），不包含的画减号、文字淡一些（muted）。</summary>
    static FrameworkElement Features()
    {
        var grid = new Grid { MinItemWidth = "14em", Gap = Space.Md };
        foreach (var (name, price, features) in Plans)
        {
            var list = new List { Icon = Lucide.Check, Gap = Space.Sm, Size = TextSize.Sm };
            list.Resources[ListParts.Icon] = IconTone(TextTone.Success);
            foreach (var (feature, included) in features)
            {
                var item = new ListItem { Content = new Text { Text = feature, Tone = included ? default : TextTone.Muted } };
                if (!included)
                {
                    item.Icon = Lucide.Minus;
                    item.Resources[ListItemParts.Icon] = IconTone(TextTone.Muted);
                }
                list.Children.Add(item);
            }
            grid.Children.Add(new Card
            {
                Content = new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Md,
                    Children =
                    {
                        new Flex { Direction = FlexDirection.Column, Children = { new Strong { Text = name, Size = TextSize.Lg }, new Text { Text = price, Tone = TextTone.Muted, Size = TextSize.Sm } } },
                        list,
                    },
                },
            });
        }
        return grid;
    }
}

/// <summary>List/basic：无序、有序与嵌套。</summary>
public partial class ListBasic : UserControl
{
    /// <summary>加载示例。</summary>
    public ListBasic() => InitializeComponent();
}
