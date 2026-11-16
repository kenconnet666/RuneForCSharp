using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace Rune.Avalonia;

/// <summary>
/// 人员与组织选择器（vue-tsx-pro 的 PeoplePicker，表单字段）：触发框里是已选的标签，点开对话框里的 <see cref="PeoplePickerPanel"/> 挑选，按"确定"才写回，"取消"放弃这次的改动。
/// <code>
/// &lt;rune:Field Label="抄送"&gt;&lt;rune:PeoplePicker Source="{Binding Org}" Entries="{Binding Cc}" Types="user,department" /&gt;&lt;/rune:Field&gt;
/// &lt;rune:PeoplePicker Source="{Binding Org}" Entries="{Binding Owner}" Max="1" Placeholder="选择经办人" /&gt;   只选一个：值仍是列表，最多一项
/// </code>
/// 外壳同 <see cref="Select"/>（<see cref="SelectBase"/>）：多选时已选的标签排在前面（可移除），一个时直接显示名字；尾部是人员图标。
/// 键盘：触发框上 Enter / 空格 / ↓ 打开，Backspace 移除最后一个；对话框里的键盘见 PeoplePickerPanel。打开时焦点在搜索框里（对话框的第一个能聚焦的元素）。
/// "最近"同面板：<see cref="Recent"/> 由使用方存。读屏：弹出对话框的组合框。
/// </summary>
public class PeoplePicker : SelectBase
{
    /// <summary>数据来源。</summary>
    public static readonly StyledProperty<PeopleSource?> SourceProperty = PeoplePickerPanel.SourceProperty.AddOwner<PeoplePicker>();

    /// <summary>选中的（TS 的 _value，双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<PeopleEntry>> EntriesProperty =
        AvaloniaProperty.Register<PeoplePicker, IReadOnlyList<PeopleEntry>>(nameof(Entries), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>能选哪几种，默认只有人员。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> TypesProperty = PeoplePickerPanel.TypesProperty.AddOwner<PeoplePicker>();

    /// <summary>最多选几个；1 时只选一个（触发框里直接是名字）。</summary>
    public static readonly StyledProperty<int?> MaxProperty = PeoplePickerPanel.MaxProperty.AddOwner<PeoplePicker>();

    /// <summary>最近选过的（双向绑定，由使用方存）；null 时对话框里没有"最近"页。</summary>
    public static readonly StyledProperty<IReadOnlyList<PeopleEntry>?> RecentProperty =
        AvaloniaProperty.Register<PeoplePicker, IReadOnlyList<PeopleEntry>?>(nameof(Recent), defaultBindingMode: BindingMode.TwoWay);

    Dialog? dialog;

    /// <summary>数据来源。</summary>
    public PeopleSource? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>选中的。</summary>
    public IReadOnlyList<PeopleEntry> Entries { get => GetValue(EntriesProperty); set => SetValue(EntriesProperty, value); }

    /// <summary>能选哪几种。</summary>
    public IReadOnlyList<string> Types { get => GetValue(TypesProperty); set => SetValue(TypesProperty, value); }

    /// <summary>最多选几个。</summary>
    public int? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>最近选过的。</summary>
    public IReadOnlyList<PeopleEntry>? Recent { get => GetValue(RecentProperty); set => SetValue(RecentProperty, value); }

    /// <summary>开着的对话框（测试用）。</summary>
    internal Dialog? OpenDialog => dialog;

    /// <summary>对话框里的面板（测试用）。</summary>
    internal PeoplePickerPanel? Panel => dialog?.Content as PeoplePickerPanel;

    /// <summary>不只选一个时显示成标签。</summary>
    protected override bool Multiple => Max != 1;

    /// <inheritdoc />
    protected override IReadOnlyList<string> Shown => [.. Entries.Select(entry => entry.Key)];

    /// <inheritdoc />
    protected override string LabelOf(string value) => Entries.FirstOrDefault(entry => entry.Key == value)?.Name ?? value;

    /// <inheritdoc />
    protected override void Remove(string value) => SetCurrentValue(EntriesProperty, Entries.Where(entry => entry.Key != value).ToList());


    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EntriesProperty || change.Property == MaxProperty) Sync();
    }

    /// <summary>尾部的箭头换成人员图标。</summary>
    protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Icon>(SelectParts.Chevron) is { } icon) icon.Data = Lucide.UsersRound;
    }

    /// <summary>没有选择时显示的文字（也是对话框的标题）：选择人员。</summary>
    protected override string DefaultPlaceholder => Provider.GetLang(this).PeoplePlaceholder;

    /// <summary>打开对话框：里面改的是草稿，按"确定"才写回；打开时焦点在搜索框里。</summary>
    protected override async void Show(bool last = false)
    {
        if (Locked || dialog is not null) return;
        var panel = new PeoplePickerPanel { Source = Source, Types = Types, Max = Max, Recent = Recent, Entries = [.. Entries] };
        panel.PropertyChanged += (_, e) =>
        {
            if (e.Property == PeoplePickerPanel.RecentProperty) SetCurrentValue(RecentProperty, panel.Recent);
        };
        dialog = new Dialog
        {
            Size = DialogSize.Lg,
            Title = Placeholder ?? DefaultPlaceholder,
            Content = panel,
            Confirm = () =>
            {
                SetCurrentValue(EntriesProperty, panel.Entries);
                return Task.FromResult(true);
            },
        };
        SetOpen(true, OpenChangeReason.Trigger);
        try
        {
            var reason = await dialog.ShowAsync(this);
            dialog = null;
            SetOpen(false, reason);
        }
        finally
        {
            dialog = null;
        }
    }

    /// <summary>开关的是对话框，不是基类的弹出层：只记下状态（读屏的"已展开"）。</summary>
    protected override void Toggle()
    {
    }
}
