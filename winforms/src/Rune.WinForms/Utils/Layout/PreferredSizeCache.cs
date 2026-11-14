namespace Rune.WinForms;

/// <summary>
/// 容器想要的大小按给的大小记下来，到它下一次排版、换字体或显隐为止。WinForms 不缓存自定义控件的 GetPreferredSize，
/// 嵌套的容器每量一次都把整棵子树重量一遍，一页文档要几秒才排出来（WPF、Avalonia 有测量失效标记，不会这样）。
/// 只在这几处清空就够：子控件的大小、文字、可见性变了时 WinForms 会让父级重排，自动大小的父级排完又让它的父级重排，一路清到顶；
/// 不自动大小的子控件，父级用的是它的当前大小，不量它。
/// </summary>
sealed class PreferredSizeCache
{
    readonly Dictionary<Size, Size> sizes = [];

    public PreferredSizeCache(Control owner)
    {
        // Layout 事件在布局引擎之前发：这一趟排版里量到的都是新的
        owner.Layout += (_, _) => sizes.Clear();
        owner.FontChanged += (_, _) => sizes.Clear();
        owner.VisibleChanged += (_, _) => sizes.Clear();
    }

    public Size Get(Size proposed, Func<Size, Size> measure)
    {
        if (!sizes.TryGetValue(proposed, out var size)) sizes[proposed] = size = measure(proposed);
        return size;
    }
}
