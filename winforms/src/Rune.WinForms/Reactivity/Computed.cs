namespace Rune.WinForms;

/// <summary>
/// 派生值：第一次读时计算，之后依赖不变就直接返回缓存；依赖变了只标脏并通知下游，下次读时才重新计算。
/// 组件把“按档位、作用域、主题算出的外观”写成派生值，绘制时读它：依赖没变时绘制不重复解析样式。
/// <code>
/// var look = new Computed&lt;Look&gt;(() => Resolve(variant.Value, Theme.Accent.Value));
/// </code>
/// 不再使用时释放（控件的 Dispose 里），退订它的依赖。
/// </summary>
/// <typeparam name="T">值的类型。</typeparam>
public sealed class Computed<T> : ReactiveSource, IDisposable
{
    readonly Node node;

    /// <summary>用计算函数创建；计算函数里读到的信号与派生值就是它的依赖。</summary>
    /// <param name="compute">计算函数，不要有副作用。</param>
    public Computed(Func<T> compute) => node = new Node(this, compute);

    /// <summary>当前值：读时登记依赖；脏了才重新计算。</summary>
    public T Value
    {
        get
        {
            Track();
            return node.Get();
        }
    }

    /// <summary>计算过几次（测试与性能观察用）。</summary>
    public int Computations => node.Computations;

    /// <summary>退订依赖。</summary>
    public void Dispose() => node.Dispose();

    void OnDirty() => NotifySubscribers();

    /// <summary>派生值自己也是追踪者：计算期间读到的就是依赖。</summary>
    sealed class Node(Computed<T> owner, Func<T> compute) : Tracker
    {
        bool dirty = true;
        T value = default!;

        public int Computations { get; private set; }

        public T Get()
        {
            if (!dirty) return value;
            using (BeginTracking()) value = compute();
            dirty = false;
            Computations++;
            return value;
        }

        protected internal override void OnSourceChanged()
        {
            // 已经脏了的不重复通知：下游在下次读之前不需要再知道一次
            if (dirty) return;
            dirty = true;
            owner.OnDirty();
        }
    }
}
