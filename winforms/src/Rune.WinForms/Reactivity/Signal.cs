namespace Rune.WinForms;

/// <summary>
/// 可变的值：读的时候登记依赖，写入不同的值时只通知依赖它的追踪者（写入相同的值什么也不做）。
/// <code>
/// var count = new Signal&lt;int&gt;(0);
/// count.Value++;          // 读到 count 的绘制 effect 让对应控件失效重绘
/// </code>
/// </summary>
/// <typeparam name="T">值的类型。</typeparam>
/// <param name="initial">初始值。</param>
/// <param name="comparer">判断“值没变”的比较器；不传用默认的相等比较。</param>
public sealed class Signal<T>(T initial, IEqualityComparer<T>? comparer = null) : ReactiveSource
{
    readonly IEqualityComparer<T> comparer = comparer ?? EqualityComparer<T>.Default;
    T value = initial;

    /// <summary>当前值：读时登记依赖，写入不同的值时通知。</summary>
    public T Value
    {
        get
        {
            Track();
            return value;
        }
        set
        {
            if (comparer.Equals(this.value, value)) return;
            this.value = value;
            NotifySubscribers();
        }
    }

    /// <summary>读当前值但不登记依赖（事件处理、日志里用）。</summary>
    public T Peek() => value;
}
