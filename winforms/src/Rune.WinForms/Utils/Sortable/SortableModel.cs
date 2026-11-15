namespace Rune.WinForms;

/// <summary>拖动排序时的按键（平台的按键换成它交给 <see cref="SortableModel{T}.Press"/>）。</summary>
public enum SortableKey
{
    /// <summary>空格或 Enter：拿起 / 放下。</summary>
    Toggle,

    /// <summary>Esc：取消。</summary>
    Escape,

    /// <summary>↑。</summary>
    Up,

    /// <summary>↓。</summary>
    Down,

    /// <summary>←。</summary>
    Left,

    /// <summary>→。</summary>
    Right,

    /// <summary>Home：移到最前。</summary>
    Home,

    /// <summary>End：移到最后。</summary>
    End,
}

/// <summary>拖动中的落点（vue-tsx-pro 的 SortableTarget）。</summary>
/// <param name="Group">落在哪一组（不分组时是空字符串）。</param>
/// <param name="Index">在组里的位置，从 0 开始。</param>
/// <param name="Reason">不能放进这一组的原因；可以放时为 null。</param>
public sealed record SortableTarget(string Group, int Index, string? Reason);

/// <summary>分组（多容器，看板的列）：项分在几个组里，组内排序，也能拖到别的组（vue-tsx-pro 的 SortableGroups）。</summary>
/// <typeparam name="T">项的类型。</typeparam>
public sealed class SortableGroups<T>
{
    /// <summary>各组的 key，按排列顺序（键盘 ← / → 按这个顺序换组）。</summary>
    public required Func<IReadOnlyList<string>> Keys { get; init; }

    /// <summary>项在哪一组。</summary>
    public required Func<T, string> Of { get; init; }

    /// <summary>组的名称：读屏播报用它；不写时是 key。</summary>
    public Func<string, string>? Label { get; init; }

    /// <summary>能不能放进这一组：返回不能的原因，null 是可以。</summary>
    public Func<T, string, string?>? CanMove { get; init; }

    /// <summary>放到了别的组（第几位）：使用方改数据，项才换组。</summary>
    public required Action<T, string, int> Moved { get; init; }

    /// <summary>放到了不能放的组：项已回到原处，可以在这里提示原因。</summary>
    public Action<T, string, string>? Rejected { get; init; }
}

/// <summary>
/// 拖动排序的状态（vue-tsx-pro useSortable 里与平台无关的部分）：拿起、组内移动、换组、放下、取消，每一步的读屏播报；
/// 平台控件把按键、指针换成这里的调用，按 <see cref="Active"/>、<see cref="Target"/> 画，把 <see cref="Announcement"/> 交给读屏。
/// <list type="bullet">
/// <item>键盘：空格 / Enter 拿起，方向键（竖排 ↑↓，横排 ←→，从右到左时对调）在组内移动一位，Home / End 移到两端；分组时另一个方向的方向键换组
/// （落点在目标组里同样用方向键调整），空格 / Enter 放下，Esc 取消。焦点离开时组内的移动生效、就地放下，指着别的组时取消。</item>
/// <item>指针：<see cref="Grab"/> 后按指针的位置调 <see cref="DragOver"/>（自己组里立即重排，别的组只标出落点），松开调 <see cref="Drop"/>，取消调 <see cref="Cancel"/>。</item>
/// <item>组内顺序立即写回（<see cref="Reordered"/>）；换组时只报告 <see cref="SortableGroups{T}.Moved"/>，数据由使用方改。</item>
/// </list>
/// </summary>
/// <typeparam name="T">项的类型。</typeparam>
/// <param name="items">当前的各项（分组时是所有组的项）。</param>
/// <param name="getKey">项的 key，同一列表里唯一。</param>
/// <param name="lang">语言包（播报的文案）。</param>
public sealed class SortableModel<T>(Func<IReadOnlyList<T>> items, Func<T, string> getKey, Func<Lang> lang)
{
    // 拿起时在组里的位置：取消时回到这里
    int grabbedFrom = -1;

    /// <summary>项的名称：读屏播报与把手的名称用它；不写时是 key。</summary>
    public Func<T, string>? GetLabel { get; init; }

    /// <summary>分组；不写时是一列。</summary>
    public SortableGroups<T>? Groups { get; init; }

    /// <summary>组内顺序变了：写回新的列表（第二个参数是被移动的项）。分组时可以不给，组内就不能排序。</summary>
    public Action<IReadOnlyList<T>, T>? Reordered { get; init; }

    /// <summary>正在拖动或被键盘拿起的项的 key。</summary>
    public string? Active { get; private set; }

    /// <summary>拖动中的落点：分组时组件按它高亮目标组、画插入线。</summary>
    public SortableTarget? Target { get; private set; }

    /// <summary>读屏播报的文字（每一步换一次）。</summary>
    public string Announcement { get; private set; } = "";

    /// <summary>状态（拿起的项、落点、播报）变了。</summary>
    public event Action? Changed;

    /// <summary>操作说明：把手的读屏说明用它。</summary>
    public string Instructions => Groups is null ? lang().SortableInstructions : lang().SortableGroupInstructions;

    /// <summary>把手的读屏名称：“移动 项名”。</summary>
    /// <param name="key">项的 key。</param>
    public string HandleName(string key) => lang().SortableHandle(LabelOf(key));

    /// <summary>组里各项的 key（不分组时是全部）。</summary>
    /// <param name="group">组。</param>
    public IReadOnlyList<string> KeysIn(string group) => [.. items().Where(item => Groups is null || Groups.Of(item) == group).Select(getKey)];

    /// <summary>项所在的组（不分组时是空字符串）。</summary>
    /// <param name="key">项的 key。</param>
    public string GroupOf(string key) => Groups is not null && ItemOf(key) is { } item ? Groups.Of(item) : "";

    /// <summary>项在组里的位置。</summary>
    /// <param name="key">项的 key。</param>
    public int IndexOf(string key) => KeysIn(GroupOf(key)).ToList().IndexOf(key);

    /// <summary>拿起。</summary>
    /// <param name="key">项的 key。</param>
    public void Grab(string key)
    {
        Active = key;
        grabbedFrom = IndexOf(key);
        Target = new SortableTarget(GroupOf(key), grabbedFrom, null);
        Announce(Groups is null
            ? lang().SortableGrabbed(LabelOf(key), Position(key), Total(key))
            : lang().SortableGroupGrabbed(LabelOf(key), GroupLabel(GroupOf(key)), Position(key)));
    }

    /// <summary>放下：落在自己的组里就地放下；落在别的组里报告 Moved，不能放的弹回。</summary>
    /// <param name="key">项的 key。</param>
    public void Drop(string key)
    {
        var to = Target;
        var own = GroupOf(key);
        var item = ItemOf(key);
        Clear();
        if (Groups is null || to is null || to.Group == own || item is null)
        {
            Announce(Groups is null
                ? lang().SortableDropped(LabelOf(key), Position(key), Total(key))
                : lang().SortableGroupDropped(LabelOf(key), GroupLabel(own), Position(key)));
            return;
        }
        if (to.Reason is { } reason)
        {
            Announce(lang().SortableRejected(LabelOf(key), reason));
            Groups.Rejected?.Invoke(item, to.Group, reason);
            return;
        }
        Announce(lang().SortableGroupDropped(LabelOf(key), GroupLabel(to.Group), to.Index + 1));
        Groups.Moved(item, to.Group, to.Index);
    }

    /// <summary>取消：回到拿起时的位置。</summary>
    /// <param name="key">项的 key。</param>
    public void Cancel(string key)
    {
        Clear();
        MoveTo(key, grabbedFrom);
        Announce(Groups is null
            ? lang().SortableCancelled(LabelOf(key), grabbedFrom + 1)
            : lang().SortableGroupCancelled(LabelOf(key), GroupLabel(GroupOf(key)), grabbedFrom + 1));
    }

    /// <summary>焦点离开把手：组内的移动已经生效，就地放下；还指着别的组时不算数，取消。</summary>
    /// <param name="key">项的 key。</param>
    public void Blur(string key)
    {
        if (Active != key) return;
        if (Target is { } target && target.Group != GroupOf(key)) Cancel(key);
        else Drop(key);
    }

    /// <summary>在组里把项移到第 index 位（数据立即写回）；位置没变时返回 false。</summary>
    /// <param name="key">项的 key。</param>
    /// <param name="index">新的位置。</param>
    public bool MoveTo(string key, int index)
    {
        if (Reordered is null) return false;
        var group = GroupOf(key);
        var from = IndexOf(key);
        var siblings = KeysIn(group).Where(other => other != key).ToList();
        var to = Math.Clamp(index, 0, siblings.Count);
        if (from < 0 || from == to) return false;
        var list = items().ToList();
        var at = list.FindIndex(candidate => getKey(candidate) == key);
        var item = list[at];
        list.RemoveAt(at);
        // 插到组里第 to 项的前面；放在组尾时接在组的最后一项后面
        if (to < siblings.Count) list.Insert(list.FindIndex(candidate => getKey(candidate) == siblings[to]), item);
        else list.Insert(siblings.Count == 0 ? list.Count : list.FindIndex(candidate => getKey(candidate) == siblings[^1]) + 1, item);
        Reordered(list, item);
        return true;
    }

    /// <summary>指针拖到了 group 的第 index 位：自己的组里立即重排，别的组只标出落点（离开自己的组时组内挪过的回到原位）。</summary>
    /// <param name="key">项的 key。</param>
    /// <param name="group">指针下的组；不在任何组上时为 null。</param>
    /// <param name="index">在那一组里的位置。</param>
    public void DragOver(string key, string? group, int index)
    {
        if (group is null)
        {
            Target = null;
            Changed?.Invoke();
            return;
        }
        var own = GroupOf(key);
        if (group != own)
        {
            if (IndexOf(key) != grabbedFrom) MoveTo(key, grabbedFrom);
            Target = new SortableTarget(group, index, ReasonFor(key, group));
            Changed?.Invoke();
            return;
        }
        MoveTo(key, index);
        Target = new SortableTarget(own, IndexOf(key), null);
        Changed?.Invoke();
    }

    /// <summary>按键：拿起 / 放下、取消、组内移动、换组；处理了返回 true。</summary>
    /// <param name="key">项的 key。</param>
    /// <param name="pressed">按键。</param>
    /// <param name="horizontal">横排。</param>
    /// <param name="rightToLeft">从右到左排版：左右方向键对调。</param>
    public bool Press(string key, SortableKey pressed, bool horizontal, bool rightToLeft = false)
    {
        if (pressed == SortableKey.Toggle)
        {
            if (Active == key) Drop(key);
            else Grab(key);
            return true;
        }
        if (Active != key) return false;
        if (pressed == SortableKey.Escape)
        {
            Cancel(key);
            return true;
        }
        var (forward, backward) = rightToLeft ? (SortableKey.Left, SortableKey.Right) : (SortableKey.Right, SortableKey.Left);
        int? along = horizontal ? (pressed == forward ? 1 : pressed == backward ? -1 : null) : pressed switch { SortableKey.Down => 1, SortableKey.Up => -1, _ => null };
        // 分组时与组内方向垂直的方向键换组
        int? across = Groups is null ? null : horizontal ? pressed switch { SortableKey.Down => 1, SortableKey.Up => -1, _ => null } : (pressed == forward ? 1 : pressed == backward ? -1 : null);
        var own = GroupOf(key);
        var current = Target ?? new SortableTarget(own, IndexOf(key), null);
        if (across is { } groupStep)
        {
            var order = Groups!.Keys();
            var next = order[Math.Clamp(order.ToList().IndexOf(current.Group) + groupStep, 0, order.Count - 1)];
            if (next != current.Group) Retarget(key, next, next == own ? IndexOf(key) : current.Index);
            return true;
        }
        int? step = pressed switch { SortableKey.Home => int.MinValue, SortableKey.End => int.MaxValue, _ => along };
        if (step is not { } move) return false;
        if (current.Group != own)
        {
            var last = KeysIn(current.Group).Count;
            Retarget(key, current.Group, move == int.MinValue ? 0 : move == int.MaxValue ? last : current.Index + move);
            return true;
        }
        var from = IndexOf(key);
        var count = KeysIn(own).Count;
        var to = move == int.MinValue ? 0 : move == int.MaxValue ? count - 1 : from + move;
        if (to < 0 || to >= count || to == from || !MoveTo(key, to)) return true;
        Target = new SortableTarget(own, IndexOf(key), null);
        Announce(Groups is null
            ? lang().SortableMoved(LabelOf(key), Position(key), Total(key))
            : lang().SortableOver(LabelOf(key), GroupLabel(own), Position(key), null));
        return true;
    }

    /// <summary>指针拖动时的位置：越过了几个别的项的中线，就排到第几位。</summary>
    /// <param name="centers">组里别的项的中线（按顺序，不含被拖的项）。</param>
    /// <param name="pointer">指针在排列方向上的位置。</param>
    public static int IndexAt(IEnumerable<double> centers, double pointer) => centers.Count(center => center < pointer);

    /// <summary>落点换到 group 的第 index 位并播报。</summary>
    void Retarget(string key, string group, int index)
    {
        var size = KeysIn(group).Count + (group == GroupOf(key) ? 0 : 1);
        var clamped = Math.Clamp(index, 0, size - 1);
        Target = new SortableTarget(group, clamped, ReasonFor(key, group));
        Announce(lang().SortableOver(LabelOf(key), GroupLabel(group), clamped + 1, Target.Reason));
    }

    T? ItemOf(string key) => items().FirstOrDefault(candidate => getKey(candidate) == key);

    string LabelOf(string key) => ItemOf(key) is { } item && GetLabel is { } label ? label(item) : key;

    string GroupLabel(string group) => Groups?.Label?.Invoke(group) ?? group;

    string? ReasonFor(string key, string group) =>
        Groups is null || ItemOf(key) is not { } item || group == Groups.Of(item) ? null : Groups.CanMove?.Invoke(item, group);

    int Position(string key) => IndexOf(key) + 1;

    int Total(string key) => KeysIn(GroupOf(key)).Count;

    void Clear()
    {
        Active = null;
        Target = null;
    }

    void Announce(string text)
    {
        Announcement = text;
        Changed?.Invoke();
    }
}
