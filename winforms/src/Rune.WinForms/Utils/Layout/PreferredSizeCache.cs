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
    readonly Control owner;

    public PreferredSizeCache(Control owner)
    {
        this.owner = owner;
        // Layout 事件在布局引擎之前发：这一趟排版里量到的都是新的
        owner.Layout += (_, _) => sizes.Clear();
        owner.FontChanged += (_, _) => sizes.Clear();
        owner.VisibleChanged += (_, _) => sizes.Clear();
    }

    /// <summary>
    /// 先把给的大小夹到 MaximumSize 再量（基类 GetPreferredSize 也这样做，重写后要自己做）：
    /// 否则限了最大宽度的卡片按不限宽量出一行的高度，宽度随后被夹窄，内容换行后被裁掉。
    /// </summary>
    public Size Get(Size proposed, Func<Size, Size> measure)
    {
        var max = owner.MaximumSize;
        if (max.Width > 0 && (proposed.Width is <= 1 || proposed.Width > max.Width)) proposed.Width = max.Width;
        if (max.Height > 0 && (proposed.Height is <= 1 || proposed.Height > max.Height)) proposed.Height = max.Height;
        if (!sizes.TryGetValue(proposed, out var size)) sizes[proposed] = size = measure(proposed);
        return size;
    }
}
