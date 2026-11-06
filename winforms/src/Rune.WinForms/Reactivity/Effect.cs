namespace Rune.WinForms;

/// <summary>
/// 副作用的追踪者：追踪期间读到的信号变了，就调用回调。组件用它做“绘制 effect”：
/// 在 OnPaint 里追踪，回调让对应的控件或区域失效，只有读到变化的地方重绘。
/// <code>
/// readonly Effect paint = new(Invalidate);          // 列表的每一行：new(() => Invalidate(RowRect(i)))
/// protected override void OnPaint(PaintEventArgs e)
/// {
///     using var _ = paint.Track();
///     … 读信号、画 …
/// }
/// protected override void Dispose(bool disposing) { if (disposing) paint.Dispose(); base.Dispose(disposing); }
/// </code>
/// 回调在写信号的那一刻同步调用：失效（Invalidate）只是标记区域，真正的重绘由系统合并后再做，多次写入只画一次。
/// 一次追踪只回调一次：回调之后到下次追踪之前，别的依赖再变也不重复回调（同 <see cref="Computed{T}"/> 的脏标记）。
/// 换主题时一次写入几个 token，读到它们的元素各失效一次，而不是每个 token 一次。
/// </summary>
/// <param name="onChanged">依赖变了时调用。</param>
public sealed class Effect(Action onChanged) : Tracker
{
    bool notified;

    /// <summary>开始一次追踪（先清掉上一次的依赖）；用 using 包住要追踪的读取。</summary>
    public Scope Track()
    {
        notified = false;
        return BeginTracking();
    }

    /// <inheritdoc />
    protected internal override void OnSourceChanged()
    {
        if (notified) return;
        notified = true;
        onChanged();
    }
}
