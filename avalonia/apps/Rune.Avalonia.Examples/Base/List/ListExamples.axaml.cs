using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>List 的示例（vue-tsx-pro 文档站 base/List）。icons（功能清单）要等 Card、Flex、Grid。</summary>
static class ListExamples
{
    public static Example[] All =>
    [
        new("List", "basic", "无序、有序与嵌套", "正文里的列表：List 是圆点，OrderedList 是编号（Start 从几开始），Gap 调项之间的间距。项里先是文字、再是嵌套的列表时，ListItem 的内容放一个 StackPanel。", () => new ListBasic()),
    ];
}

/// <summary>List/basic：无序、有序与嵌套。</summary>
public partial class ListBasic : UserControl
{
    /// <summary>加载示例。</summary>
    public ListBasic() => InitializeComponent();
}
